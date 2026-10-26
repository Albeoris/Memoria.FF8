using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldPlaneStateTests
{
    [Fact]
    public void FreshVerifiedRegistrationResetsBothHalvesAndClassicAndOldSnapshotsStayUnknown()
    {
        NativeFieldTextureAnimations fresh = NativeFieldTextureAnimations.Register(new('A', 64), [], NativeFieldRuntimeProfile.RemasteredEnImageHash);
        Assert.All(fresh.Planes!.Closed, value => Assert.False(value));
        NativeFieldTextureAnimations changed = fresh.WritePlane(64, 20, true).WritePlane(64, 140, true);
        Assert.True(changed.Planes!.IsClosed(64, 20));
        Assert.True(changed.Planes.IsClosed(64, 140));
        Assert.All(NativeFieldTextureAnimations.Register(new('A', 64), [], NativeFieldRuntimeProfile.RemasteredEnImageHash).Planes!.Closed, value => Assert.False(value));
        Assert.Null(NativeFieldTextureAnimations.Register(new('A', 64), [], NativeFieldRuntimeProfile.FF8EnExecutableHash).Planes);
        NativeFieldTextureAnimations old = JsonSerializer.Deserialize<NativeFieldTextureAnimations>("{\"OneHash\":\"" + new string('A', 64) + "\",\"Textures\":[]}")!;
        Assert.Null(old.Planes);
        Assert.Null(old.WritePlane(64, 20, true).Planes);
        Assert.Throws<InvalidDataException>(() => (fresh.Planes with { ExecutableHash = NativeFieldRuntimeProfile.FF8EnExecutableHash }).Validate());
    }

    [Fact]
    public void PhysicalPageHalvesPreserveAliasesLastWriterAndIndependentJsonRestore()
    {
        NativeFieldTexturePlanes planes = NativeFieldTexturePlanes.FromReset(NativeFieldRuntimeProfile.RemasteredEnImageHash);
        planes = planes.Write(65, 12, true);
        Assert.True(planes.IsClosed(100, 100));
        Assert.False(planes.IsClosed(65, 128));
        Assert.False(planes.IsClosed(128, 12));
        planes = planes.Write(100, 100, false).Write(65, 128, true);
        Assert.False(planes.IsClosed(65, 12));
        Assert.True(planes.IsClosed(100, 255));
        Assert.False(planes.IsClosed(65, 512));
        planes = planes.Write(65, 512, true);
        Assert.True(planes.IsClosed(65, 512));
        NativeFieldTexturePlanes restored = JsonSerializer.Deserialize<NativeFieldTexturePlanes>(JsonSerializer.Serialize(planes))!;
        Assert.Equal(JsonSerializer.Serialize(planes.Write(65, 128, false)), JsonSerializer.Serialize(restored.Write(65, 128, false)));
        Assert.True(restored.IsClosed(65, 128));
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
        NativeFieldRegistrationStep registered = NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration, actors, NativeBattleRetainedMemory.FromColdProcess());
        uint instance = registered.State.Instances[0].Address;
        uint bank = BinaryPrimitives.ReadUInt32LittleEndian(registered.Memory.FileStaging!.Read(registered.State.Resources[0].Address + 20, 4));
        short x = BinaryPrimitives.ReadInt16LittleEndian(registered.Memory.FileStaging.Read(bank + 6, 2));
        short y = BinaryPrimitives.ReadInt16LittleEndian(registered.Memory.FileStaging.Read(bank + 8, 2));
        Assert.False(registered.Memory.FieldTextureAnimations!.Planes!.IsClosed(x, y));
        NativeBattleFieldResourceReloadResult closed = NativeFieldRegistrationMachine.SetEyes532340(registered.State, registered.Memory, 3, 0x200000, null, null);
        Assert.True(closed.Memory.FieldTextureAnimations!.Planes!.IsClosed(x, y));
        NativeBattleFieldResourceReloadResult openedAlias = NativeFieldRegistrationMachine.SetEyes532340(registered.State, closed.Memory, 4, 0x100000, null, null);
        Assert.False(openedAlias.Memory.FieldTextureAnimations!.Planes!.IsClosed(x, y));
        NativeBattleFieldResourceReloadResult aliasClosed = NativeFieldRegistrationMachine.SetEyes532340(registered.State, openedAlias.Memory, 4, 0x200000, null, null);
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
            live = step.Memory;
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
        NativeFieldTextureAnimations missing = live.FieldTextureAnimations! with { Planes = null };
        Assert.Throws<NotSupportedException>(() => assets.BindNativeRegisteredTextures(field, registered.State, live.EffectMemory!.Textures!, missing));
    }
}
