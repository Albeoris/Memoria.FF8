using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelTextureRoutingTests
{
    [Fact]
    public void HostedStatusUploadsKeepActorPalettesAndScratchPayloadsIndependent()
    {
        var fixture = Fixture();
        BattleSession session = fixture.Session;
        NativeBattleModelCpuBank first = fixture.Cpu.Palettes.Write(0, Palette(0x1F));
        NativeBattleModelCpuBank second = fixture.Cpu.Palettes.Write(0, Palette(0x3E0));
        Set(session, nameof(BattleSession.NativeModelCpuResources), fixture.Cpu with { PresentationPalettes = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty.Add(7, first).Add(24, second) });
        NativeBattleEffectTextureState prior = session.NativeEffectMemory!.Textures!;
        NativeBattlePcTextureWindowCache firstCache = prior.RequirePresentationTexture("first").Cache;

        Upload(session, 7, 0x10);
        NativeBattleEffectHeapAllocation queuedFirst = session.NativeEffectMemory!.Textures!.RequirePresentationTexture("first").Textures.Slots[0]!.Source!;
        Upload(session, 24, 0x10);

        NativeBattleEffectTextureState queued = session.NativeEffectMemory!.Textures!;
        NativeBattlePresentationTextureBank actor1 = queued.RequirePresentationTexture("first");
        NativeBattlePresentationTextureBank actor2 = queued.RequirePresentationTexture("second");
        Assert.Equal(0, queued.Count);
        Assert.Same(firstCache, actor1.Cache);
        Assert.Same(queuedFirst, actor1.Textures.Slots[0]!.Source);
        Assert.Equal(new NativeBattleEffectTextureRectangle(0, 224, 256, 2), actor1.Textures.Slots[0]!.Rectangle);
        Assert.Equal(actor1.Textures.Slots[0]!.Rectangle, actor2.Textures.Slots[0]!.Rectangle);
        byte[] firstPayload = NativeBattleStatusPalette.Transform509E00(first.Read(0, 1024), 0x10).ToArray();
        byte[] secondPayload = NativeBattleStatusPalette.Transform509E00(second.Read(0, 1024), 0x10).ToArray();
        Assert.NotEqual(firstPayload[2], secondPayload[2]);
        NativeBattlePrimitiveProcess process = session.NativeEffectMemory!.PrimitiveProcess!;
        Assert.Equal(2048, process.ByteCursor);
        Assert.Equal(firstPayload, process.ReadCommonPacket(0, 1024));
        Assert.Equal(secondPayload, process.ReadCommonPacket(1024, 1024));
        Assert.Equal(firstPayload, queuedFirst.Data.ToArray());

        NativeBattleEffectMemory drained = Drain(session);
        NativeBattleEffectTextureState textures = drained.Textures!;
        Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(firstPayload.AsSpan(2)), textures.RequirePresentationTexture("first").Textures.ReadVramWord(1, 224));
        Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(secondPayload.AsSpan(2)), textures.RequirePresentationTexture("second").Textures.ReadVramWord(1, 224));
        Assert.Equal((ushort)0, textures.ReadVramWord(1, 224));
        Assert.False(textures.RequirePresentationTexture("first").Cache.Rows[0].Regions[0].CurrentPixels);
        Assert.False(textures.RequirePresentationTexture("second").Cache.Rows[0].Regions[0].CurrentPixels);
        Assert.True(drained.PrimitiveProcess!.TextureWindowCache!.Rows[0].Regions[0].CurrentPixels);
    }

    [Fact]
    public void HostedMotionReadsEachActorImageAndInvalidatesOnlyTheAffectedCache()
    {
        var fixture = Fixture();
        BattleSession session = fixture.Session;
        NativeBattleTextureCopy copy = new(1, 0, 1, 1, 0, 0);
        Motion(session, 7, copy);
        NativeBattleEffectMemory first = Drain(session);
        AssertActor(first.Textures!, "first", 0x51, 0x11);
        NativeBattlePresentationTextureBank beforeSecond = first.Textures!.RequirePresentationTexture("second");
        Assert.Equal((ushort)0x22, beforeSecond.Textures.ReadVramWord(0, 0));
        Assert.True(beforeSecond.Cache.Rows[0].Regions[0].CurrentPixels);

        Set(session, nameof(BattleSession.NativeEffectMemory), first);
        Motion(session, 24, copy);
        NativeBattleEffectMemory pending = session.NativeEffectMemory!;
        string json = NativeBattleTestSnapshots.Json(pending);
        NativeBattleEffectMemory independentlyRestored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(json);
        NativeBattleEffectMemory second = Drain(session);
        NativeBattleEffectMemory restored = BattleSession.DrainNativeTextureUploads(independentlyRestored, independentlyRestored.Textures!, null);
        AssertActor(second.Textures!, "first", 0x51, 0x11);
        AssertActor(second.Textures!, "second", 0x62, 0x22);
        Assert.Equal((ushort)0x33, second.Textures!.ReadVramWord(0, 0));
        Assert.Equal(NativeBattleTestSnapshots.Json(second), NativeBattleTestSnapshots.Json(restored));
    }

    [Theory]
    [InlineData((byte)0, 224)]
    [InlineData((byte)2, 228)]
    public void MissingPresentationTextureMapRetainsNativePaletteAndMotionDestinations(byte index, int expectedY)
    {
        var fixture = Fixture();
        BattleSession session = fixture.Session;
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;
        NativeBattleEffectTextureState global = memory.Textures! with { PresentationTextures = null };
        Set(session, nameof(BattleSession.NativeEffectMemory), memory with { Textures = global });
        Upload(session, index, 0x10);
        Motion(session, 7, new(1, 0, 1, 1, 0, 0));
        NativeBattleEffectMemory queued = session.NativeEffectMemory!;
        NativeBattlePrimitiveProcess process = queued.PrimitiveProcess!;
        NativeBattleEffectTextureState textures = queued.Textures!;
        Assert.Equal(1024, process.ByteCursor);
        Assert.Equal((short)expectedY, textures.Slots[0]!.Rectangle!.Y);
        Assert.Equal(3, textures.Slots[1]!.Kind);
        Assert.Null(textures.PresentationTextures);
        Assert.Equal(NativeBattleStatusPalette.Transform509E00(fixture.Cpu.Palettes.Read(index * 1024, 1024), 0x10).ToArray(), process.ReadCommonPacket(0, 1024));
        Assert.Equal((ushort)0x73, Drain(session).Textures!.ReadVramWord(0, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingActorBankOrWrongPaletteRosterFailsBeforeScratchPublication(bool wrongRoster)
    {
        var fixture = Fixture();
        BattleSession session = fixture.Session;
        if (wrongRoster)
        {
            NativeBattleGraphicsRoster other = new(fixture.Roster.Rows.SetItem(7, new("wrong", true)));
            Set(session, nameof(BattleSession.NativeModelCpuResources), NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(other));
        }
        else
        {
            NativeBattleEffectMemory memory = session.NativeEffectMemory!;
            NativeBattleEffectTextureState textures = memory.Textures!;
            Set(session, nameof(BattleSession.NativeEffectMemory), memory with { Textures = textures with { PresentationTextures = textures.PresentationTextures!.Remove("first") } });
        }
        NativeBattleEffectMemory before = session.NativeEffectMemory!;
        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => Upload(session, 7, 0x10));
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.Same(before, session.NativeEffectMemory);
        Assert.Equal(0, before.PrimitiveProcess!.ByteCursor);
        if (!wrongRoster)
        {
            TargetInvocationException motionFailure = Assert.Throws<TargetInvocationException>(() => Motion(session, 7, new(1, 0, 1, 1, 0, 0)));
            Assert.IsType<InvalidDataException>(motionFailure.InnerException);
            Assert.Same(before, session.NativeEffectMemory);
        }
    }

    private static (BattleSession Session, NativeBattleModelCpuResources Cpu, NativeBattleGraphicsRoster Roster) Fixture()
    {
        NativeBattleGraphicsActor?[] actors = new NativeBattleGraphicsActor?[25];
        actors[7] = new("first", true);
        actors[24] = new("second", true);
        NativeBattleGraphicsRoster roster = new(actors.ToImmutableArray());
        NativeBattleModelTextureBinding body = new(new byte?[16].ToImmutableArray());
        ImmutableArray<NativeBattleGraphicsModel> models = [.. NativeBattleGraphicsModels.ClearPresentationBank(roster)];
        models = models.SetItem(7, models[7] with { BodyTextureSlots = body }).SetItem(24, models[24] with { BodyTextureSlots = body });
        NativeBattleGraphicsState graphics = new(models, roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(0, 0).Prepare464BD0().Cache;
        NativeBattleEffectTextureState textures = Image(0x33, 0x73)
            .WithPresentationTexture("first", new(Image(0x11, 0x51), cache))
            .WithPresentationTexture("second", new(Image(0x22, 0x62), cache));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0) with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { TextureWindowCache = cache }, Textures = textures };
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(roster);
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        Set(session, nameof(BattleSession.NativeGraphics), graphics);
        Set(session, nameof(BattleSession.NativeEffectMemory), memory);
        Set(session, nameof(BattleSession.NativeModelCpuResources), cpu);
        return (session, cpu, roster);
    }

    private static NativeBattleEffectTextureState Image(byte destination, byte source)
    {
        return NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 2, 1, [destination, 0, source, 0]);
    }

    private static byte[] Palette(ushort value)
    {
        return Enumerable.Range(0, 512).SelectMany(_ => new[] { (byte)value, (byte)(value >> 8) }).ToArray();
    }

    private static void AssertActor(NativeBattleEffectTextureState textures, string actor, ushort expected, ushort old)
    {
        NativeBattlePresentationTextureBank bank = textures.RequirePresentationTexture(actor);
        Assert.Equal(0, bank.Textures.Count);
        Assert.Equal(expected, bank.Textures.ReadVramWord(0, 0));
        Assert.Equal(old, bank.Cache.Rows[0].Regions[0].RetainedPixels!.ReadWord(0, 0));
    }

    private static NativeBattleEffectMemory Drain(BattleSession session)
    {
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;
        return BattleSession.DrainNativeTextureUploads(memory, memory.Textures!, null);
    }

    private static void Upload(BattleSession session, byte row, uint flags) => Invoke(session, "UploadNativeStatusPalette509E00", row, flags);

    private static void Motion(BattleSession session, int row, NativeBattleTextureCopy copy) => Invoke(session, "EnqueueNativeTextureMotion", session.NativeGraphics!.Models[row], copy);

    private static void Invoke(BattleSession session, string name, params object[] args) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, args);

    private static void Set(BattleSession session, string property, object value) => typeof(BattleSession).GetProperty(property)!.SetValue(session, value);
}
