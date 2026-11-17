using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelTextureRoutingTests
{
    private static readonly BattlePartyModelOwner First = new BattlePartyActorModel(new("first"));
    private static readonly BattlePartyModelOwner Second = new BattlePartyActorModel(new("second"));

    [Fact]
    public void HostedStatusUploadsKeepActorPalettesAndScratchPayloadsIndependent()
    {
        var fixture = Fixture();
        BattleSession session = fixture.Session;
        NativeBattleModelCpuBank first = fixture.Cpu.Palettes.Write(0, Palette(0x1F));
        NativeBattleModelCpuBank second = fixture.Cpu.Palettes.Write(0, Palette(0x3E0));
        Set(session, nameof(BattlePresentationProcess.NativeModelCpuResources), fixture.Cpu with { PresentationPalettes = ImmutableDictionary<BattlePartyModelOwner, NativeBattleModelCpuBank>.Empty.Add(First, first).Add(Second, second) });
        NativeBattleEffectTextureState prior = session.Presentation!.Memory.Textures!;
        NativeBattlePcTextureWindowCache firstCache = prior.RequirePresentationTexture("first").Cache;

        Upload(session, First, 0x10);
        NativeBattleEffectHeapAllocation queuedFirst = session.Presentation!.Memory.Textures!.RequirePresentationTexture("first").Textures.Slots[0]!.Source!;
        Upload(session, Second, 0x10);

        NativeBattleEffectTextureState queued = session.Presentation!.Memory.Textures!;
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
        NativeBattlePrimitiveProcess process = session.Presentation!.Memory.PrimitiveProcess!;
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
        Motion(session, First, copy);
        NativeBattleEffectMemory first = Drain(session);
        AssertActor(first.Textures!, "first", 0x51, 0x11);
        NativeBattlePresentationTextureBank beforeSecond = first.Textures!.RequirePresentationTexture("second");
        Assert.Equal((ushort)0x22, beforeSecond.Textures.ReadVramWord(0, 0));
        Assert.True(beforeSecond.Cache.Rows[0].Regions[0].CurrentPixels);

        NativeBattleHostedTestFixture.SetProperty(session.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), first);
        Motion(session, Second, copy);
        NativeBattleEffectMemory pending = session.Presentation!.Memory;
        NativeBattleEffectMemory independentlyRestored = NativeBattleTestSnapshots.Independent(pending);
        NativeBattleEffectMemory second = Drain(session);
        NativeBattleEffectMemory restored = BattlePresentationProcess.DrainNativeTextureUploads(independentlyRestored, independentlyRestored.Textures!, null);
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
        NativeBattleEffectMemory memory = session.Presentation!.Memory;
        NativeBattleEffectTextureState global = memory.Textures! with { PresentationTextures = null };
        NativeBattleHostedTestFixture.SetProperty(session.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), memory with { Textures = global });
        Upload(session, OriginalOwner(index), 0x10);
        Motion(session, First, new(1, 0, 1, 1, 0, 0));
        NativeBattleEffectMemory queued = session.Presentation!.Memory;
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
            BattleModelRoster other = new(fixture.Roster.Owners.Select(owner => owner == First ? new BattlePartyActorModel(new("wrong")) : owner));
            Set(session, nameof(BattlePresentationProcess.NativeModelCpuResources), NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(other, Aliases(other)));
        }
        else
        {
            NativeBattleEffectMemory memory = session.Presentation!.Memory;
            NativeBattleEffectTextureState textures = memory.Textures!;
            NativeBattleHostedTestFixture.SetProperty(session.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), memory with { Textures = textures with { PresentationTextures = textures.PresentationTextures!.Remove("first") } });
        }
        NativeBattleEffectMemory before = session.Presentation!.Memory;
        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => Upload(session, First, 0x10));
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.Same(before, session.Presentation?.Memory);
        Assert.Equal(0, before.PrimitiveProcess!.ByteCursor);
        if (!wrongRoster)
        {
            TargetInvocationException motionFailure = Assert.Throws<TargetInvocationException>(() => Motion(session, First, new(1, 0, 1, 1, 0, 0)));
            Assert.IsType<InvalidDataException>(motionFailure.InnerException);
            Assert.Same(before, session.Presentation?.Memory);
        }
    }

    private static (BattleSession Session, NativeBattleModelCpuResources Cpu, BattleModelRoster Roster) Fixture()
    {
        BattleModelRoster roster = new([OriginalOwner(0), OriginalOwner(2), First, Second]);
        NativeBattleModelTextureBinding body = new(new byte?[16].ToImmutableArray());
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, Aliases(roster), _ => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { BodyTextureSlots = body });
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(0, 0).Prepare464BD0().Cache;
        NativeBattleEffectTextureState textures = Image(0x33, 0x73)
            .WithPresentationTexture("first", new(Image(0x11, 0x51), cache))
            .WithPresentationTexture("second", new(Image(0x22, 0x62), cache));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0) with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { TextureWindowCache = cache }, Textures = textures };
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(roster, Aliases(roster));
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        NativeBattleHostedTestFixture.BindProcess(session, graphics);
        NativeBattleHostedTestFixture.SetProperty(session.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), memory);
        Set(session, nameof(BattlePresentationProcess.NativeModelCpuResources), cpu);
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
        NativeBattleEffectMemory memory = session.Presentation!.Memory;
        return BattlePresentationProcess.DrainNativeTextureUploads(memory, memory.Textures!, null);
    }

    private static NativeBattleModelAliases Aliases(BattleModelRoster roster) => new(roster, [(NativeBattleModelSlot.Decode(0), OriginalOwner(0)), (NativeBattleModelSlot.Decode(2), OriginalOwner(2))]);

    private static void Upload(BattleSession session, BattleModelOwner owner, uint flags) => Invoke(session, "UploadNativeStatusPalette509E00", owner, flags);

    private static void Motion(BattleSession session, BattleModelOwner owner, NativeBattleTextureCopy copy) => Invoke(session, "EnqueueNativeTextureMotion", owner, session.Presentation!.Graphics.ActorModels.Get(owner), copy);

    private static void Invoke(BattleSession session, string name, params object[] args) => typeof(BattlePresentationProcess).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session.Presentation!, args);

    private static void Set(BattleSession session, string property, object value) => NativeBattleHostedTestFixture.SetProperty(session.Presentation!, property, value);
}
