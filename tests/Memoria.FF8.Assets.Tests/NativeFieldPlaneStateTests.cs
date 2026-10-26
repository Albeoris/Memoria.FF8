using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldPlaneStateTests
{
    [Fact]
    public void SubmittedMaterialSnapshotPreservesLogicalScaleAcrossLaterHandleChanges()
    {
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Remastered, []));
        ResourceService resources = new(source);
        TextureKey key = new(new("field/model"), "texture/0");
        PixelImage first = new(768, 768);
        AssetHandle<TextureValue> handle = resources.GetTexture(new(key, source.Descriptor, 1, "synthetic", 128, 128, () => first));
        ResourceTextureList live = new([handle]);
        ResourceTextureList submitted = live.Snapshot();
        PixelImage next = new(512, 512);
        resources.GetTexture(new(key, source.Descriptor, 2, "synthetic", 128, 128, () => next));
        Assert.Same(first, submitted[0]);
        Assert.Same(next, live[0]);
        Assert.Equal(new System.Numerics.Vector2(6), ResourceTextureList.PhysicalScale(submitted, 0));
        Assert.Equal(new System.Numerics.Vector2(4), ResourceTextureList.PhysicalScale(live, 0));
    }

    [Fact]
    public void FreshVerifiedRegistrationAndOldSnapshotsHaveDistinctKnownness()
    {
        NativeFieldTextureAnimations fresh = NativeFieldTextureAnimations.Register(new('A', 64), [], NativeFieldRuntimeProfile.RemasteredEnImageHash);
        Assert.All(fresh.Planes!.Slots, slot => Assert.False(slot.Closed));
        Assert.All(fresh.Planes.Sources, source => Assert.Null(source));
        Assert.Null(NativeFieldTextureAnimations.Register(new('A', 64), [], NativeFieldRuntimeProfile.FF8EnExecutableHash).Planes);
        NativeFieldTextureAnimations old = JsonSerializer.Deserialize<NativeFieldTextureAnimations>("{\"OneHash\":\"" + new string('A', 64) + "\",\"Textures\":[]}")!;
        Assert.Null(old.Planes);
        Assert.Null(old.WritePlane(64, 20, true).Planes);
        Assert.DoesNotContain("\"Planes\"", JsonSerializer.Serialize(old));
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        Assert.True(cold.FieldTexturePlaneCacheFresh);
        NativeBattleRetainedMemory unknown = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(cold with { FieldTexturePlaneCacheFresh = null }))!;
        Assert.Null(unknown.FieldTexturePlaneCacheFresh);
        Assert.Throws<InvalidDataException>(() => (fresh.Planes with { ExecutableHash = NativeFieldRuntimeProfile.FF8EnExecutableHash }).Validate());
    }

    [Fact]
    public void PageSourcePairReplacementResetsBothHalvesAndRetainsOnlyExplicitPending()
    {
        NativeFieldTexturePageIdentity original = new(0x100, 0x200);
        NativeFieldTexturePageIdentity changedOtherHalf = new(0x100, 0x300);
        NativeFieldTexturePlanes fresh = NativeFieldTexturePlanes.FromReset(NativeFieldRuntimeProfile.RemasteredEnImageHash);
        NativeFieldTexturePlanes automaticBeforeFirstBind = fresh.Write(64, 12, true);
        Assert.False(automaticBeforeFirstBind.IsClosed(1, true));
        Assert.Equal(2, automaticBeforeFirstBind.Get(64, 12).Pending);
        Assert.True(automaticBeforeFirstBind.Bind(1, original).IsClosed(1, true));
        NativeFieldTexturePlanes loaded = fresh.Bind(1, original).Write(64, 12, true).Write(64, 140, true).Bind(1, original);
        Assert.True(loaded.IsClosed(1, true));
        Assert.True(loaded.IsClosed(1, false));
        Assert.Same(loaded, loaded.Bind(1, original));
        NativeFieldTexturePlanes automatic = loaded.Write(64, 12, true).Bind(1, changedOtherHalf);
        Assert.False(automatic.IsClosed(1, true));
        Assert.False(automatic.IsClosed(1, false));
        Assert.Equal(0, automatic.Get(64, 12).Pending);
        NativeFieldTexturePlanes retained = loaded.Write(64, 12, true, retain: true).Bind(1, changedOtherHalf);
        Assert.True(retained.IsClosed(1, true));
        Assert.False(retained.IsClosed(1, false));
        Assert.False(retained.Get(64, 12).Retain);
        Assert.Equal(0, retained.Get(64, 12).Pending);
        Assert.False(retained.Bind(1, new(0x400, 0x300)).IsClosed(1, true));
        NativeFieldTexturePlanes aliasLastWriter = loaded.Write(65, 12, true, retain: true).Write(100, 100, false).Bind(1, original);
        Assert.False(aliasLastWriter.IsClosed(1, true));
        Assert.True(aliasLastWriter.IsClosed(1, false));
        NativeFieldTexturePlanes independent = JsonSerializer.Deserialize<NativeFieldTexturePlanes>(JsonSerializer.Serialize(retained))!;
        Assert.Equal(JsonSerializer.Serialize(retained.Write(64, 140, true).Bind(1, changedOtherHalf)), JsonSerializer.Serialize(independent.Write(64, 140, true).Bind(1, changedOtherHalf)));
        Assert.False(independent.IsClosed(1, false));
    }

    [Fact]
    public void LiteralWriterBankAndDrawPageAreDistinctAndUploadIdentityUsesOriginalFamilyAndSequence()
    {
        NativeFieldTexturePlanes planes = NativeFieldTexturePlanes.FromReset(NativeFieldRuntimeProfile.RemasteredEnImageHash).Write(64, 256, true);
        planes = planes.Bind(1, new(0x100, 0x200));
        Assert.True(planes.IsClosed(1, false));
        Assert.False(planes.IsClosed(17, true));
        Assert.Equal((uint)(122 << 8 | 2), NativeFieldTexturePlanes.SourceIdentity("d025"u8, 2));
        Assert.Equal((uint)(1122 << 8 | 1), NativeFieldTexturePlanes.SourceIdentity("n025"u8, 1));
        Assert.Equal((uint)(2122 << 8 | 1), NativeFieldTexturePlanes.SourceIdentity("o025"u8, 1));
        Assert.Equal((uint)(3122 << 8 | 1), NativeFieldTexturePlanes.SourceIdentity("p025"u8, 1));
        Assert.Equal(1u, NativeFieldTexturePlanes.SourceIdentity("c025"u8, 1));
    }

    [Fact]
    public void OriginalRemasteredProducersPublishOnlyActualCopiesAndUseNativeFrameOneWithSharedAliases()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game) && File.Exists(Path.Combine(game!, "main.zzz")), "Select optional corresponding Remastered resources with MEMORIA_FF8_REMASTERED_GAME_DIR.");
        using GameAssets assets = new(game!);
        FieldAssets field = assets.LoadField(689);
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, field.NativeRegistration!.ExecutableHash);
        int count = field.Jsm[0] + field.Jsm[1] + field.Jsm[2] + field.Jsm[3];
        ushort group = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8 + 2 * index))).OrderBy(value => value >> 7).ElementAt(3);
        NativeFieldRetainedActor[] actors = [new(3, 3, group >> 7, (group >> 7) + (group & 127) + 1), new(4, 3, group >> 7, (group >> 7) + (group & 127) + 1)];
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration, actors, cold with { FieldTexturePlaneCacheFresh = null }));
        Assert.Throws<NotSupportedException>(() => NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration, actors, cold with { FieldTexturePlaneCacheFresh = false }));
        NativeFieldRegistrationStep registered = NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration, actors, cold);
        Assert.False(registered.Memory.FieldTexturePlaneCacheFresh);
        Assert.Throws<NotSupportedException>(() => NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration, actors, registered.Memory with { FieldTextureAnimations = null }));
        uint instance = registered.State.Instances[0].Address;
        uint bank = BinaryPrimitives.ReadUInt32LittleEndian(registered.Memory.FileStaging!.Read(registered.State.Resources[0].Address + 20, 4));
        short x = BinaryPrimitives.ReadInt16LittleEndian(registered.Memory.FileStaging.Read(bank + 6, 2));
        short y = BinaryPrimitives.ReadInt16LittleEndian(registered.Memory.FileStaging.Read(bank + 8, 2));
        NativeBattleRetainedMemory initial = Bind(registered.Memory);
        Assert.False(initial.FieldTextureAnimations!.Planes!.IsClosed(x, y));
        NativeBattleFieldResourceReloadResult closed = NativeFieldRegistrationMachine.SetEyes532340(registered.State, initial, 3, 0x200000, null, null);
        Assert.Equal(2, closed.Memory.FieldTextureAnimations!.Planes!.Get(x, y).Pending);
        closed = closed with { Memory = Bind(closed.Memory) };
        Assert.True(closed.Memory.FieldTextureAnimations!.Planes!.IsClosed(x, y));
        NativeFieldRegistrationStep reloaded = NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration, actors, closed.Memory);
        Assert.Same(closed.Memory.FieldTextureAnimations.Planes, reloaded.Memory.FieldTextureAnimations!.Planes);
        Assert.False(reloaded.Memory.FieldTexturePlaneCacheFresh);
        NativeBattleFieldResourceReloadResult openedAlias = NativeFieldRegistrationMachine.SetEyes532340(registered.State, closed.Memory, 4, 0x100000, null, null);
        openedAlias = openedAlias with { Memory = Bind(openedAlias.Memory) };
        Assert.False(openedAlias.Memory.FieldTextureAnimations!.Planes!.IsClosed(x, y));
        NativeBattleFieldResourceReloadResult aliasClosed = NativeFieldRegistrationMachine.SetEyes532340(registered.State, openedAlias.Memory, 4, 0x200000, null, null);
        aliasClosed = aliasClosed with { Memory = Bind(aliasClosed.Memory) };
        NativeBattleFieldResourceReloadResult started = NativeFieldRegistrationMachine.SetEyes532340(registered.State, aliasClosed.Memory, 3, 0x80000, 4, NativeCrtRandomState.FromColdProcess());
        Assert.True(started.Memory.FieldTextureAnimations!.Planes!.IsClosed(x, y));
        NativeBattleRetainedMemory live = started.Memory;
        NativeCrtRandomState? random = started.CrtRandomAfter;
        bool sawClosed = false;
        bool sawOtherFrame = false;
        bool sawCountdown = false;
        for (int callback = 0; callback < 40; callback++)
        {
            byte countdown = live.FileStaging!.Read(instance + 102, 1)[0];
            bool before = live.FieldTextureAnimations!.Planes!.IsClosed(x, y);
            NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(live))!;
            NativeBattleFieldResourceReloadResult step = NativeFieldRegistrationMachine.AdvanceEyes530810(registered.State, live, random);
            NativeBattleFieldResourceReloadResult independent = NativeFieldRegistrationMachine.AdvanceEyes530810(registered.State, restored, random);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(independent));
            live = Bind(step.Memory);
            random = step.CrtRandomAfter;
            if (countdown == 1)
            {
                byte frame = live.FileStaging!.Read(instance + 103, 1)[0];
                Assert.Equal(frame == 1, live.FieldTextureAnimations!.Planes!.IsClosed(x, y));
                sawClosed |= frame == 1;
                sawOtherFrame |= frame != 1;
            }
            else
            {
                Assert.Equal(before, live.FieldTextureAnimations!.Planes!.IsClosed(x, y));
                sawCountdown = true;
            }
        }
        Assert.True(sawClosed);
        Assert.True(sawOtherFrame);
        Assert.True(sawCountdown);
        NativeBattleRetainedMemory Bind(NativeBattleRetainedMemory memory)
        {
            NativeFieldTextureBinding binding = assets.BindNativeRegisteredTextureState(field, registered.State, memory.EffectMemory!.Textures!, memory.FieldTextureAnimations, 3);
            return memory with { FieldTextureAnimations = binding.Animations };
        }
        NativeFieldTextureAnimations missing = live.FieldTextureAnimations! with { Planes = null };
        Assert.Throws<NotSupportedException>(() => assets.BindNativeRegisteredTextures(field, registered.State, live.EffectMemory!.Textures!, missing));
    }
}
