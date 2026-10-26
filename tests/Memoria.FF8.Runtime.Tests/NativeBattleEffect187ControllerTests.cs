using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect187Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187ControllerTests
{
    [Fact]
    public void ScrollBirthPreservesUnwrittenPayloadAndConsumesExactlyOneCrtDraw()
    {
        NativeBattleEffect187Frame frame = Frame();
        NativeBattleEffectTaskPool child = frame.Effect.Pools[1];
        child = child with { Slots = child.Slots.Select(task => task.WriteWord(0x14, 0xABCD)).ToImmutableArray() };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, child) } };
        NativeCrtRandomState expected = frame.Random.Draw(out ushort phase);
        frame = InitializeScrollBurst6483B0(frame);
        Assert.Equal(expected, frame.Random);
        NativeBattleModulePoolTask warp = Assert.Single(frame.Effect.Pools[1].Slots, task => task.Callback == 0x648490);
        Assert.Equal((ushort)(phase & 0xFFF), warp.ReadWord(0x18));
        NativeBattleModulePoolTask scroll = Assert.Single(frame.Effect.Pools[1].Slots, task => task.Callback == 0x6485C0);
        Assert.Equal((ushort)0, scroll.ReadWord(0x14));
        NativeBattleModulePoolTask[] delayed = frame.Effect.Pools[1].Slots.Where(task => task.Callback == 0x6486F0).ToArray();
        Assert.Equal(2, delayed.Length);
        Assert.All(delayed, task => Assert.Equal((ushort)0xABCD, task.ReadWord(0x14)));
    }

    [Fact]
    public void PayloadWritesKeepTheNextLinkCreatedByAChildAppend()
    {
        NativeBattleEffect187Frame frame = Frame();
        (frame, uint first) = Spawn647900(frame, 0x647F80);
        NativeBattleModulePoolTask prior = Read(frame, first);
        (frame, _) = Spawn647900(frame, 0x649D00);
        int? next = Read(frame, first).Next;
        Assert.NotNull(next);
        frame = Write(frame, first, prior.WriteWord(12, 7));
        Assert.Equal(next, Read(frame, first).Next);
        Assert.Equal((ushort)7, Read(frame, first).ReadWord(12));
    }

    [Fact]
    public void PausedTintStillPublishesCurrentColorWithoutAdvancingItsClock()
    {
        NativeBattleEffect187Frame frame = InitializeBodyBurst64B040(Frame());
        int slot = Enumerable.Range(0, frame.Effect.Pools[1].Slots.Length).Single(index => frame.Effect.Pools[1].Slots[index].Callback == 0x64B0A0);
        uint address = checked(ChildBank + (uint)(slot * 36));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 4));
        NativeBattleEffect187Inputs inputs = new(Colors: null!, ReadPackets: null!, ReadFlags: _ => 0x201, ReadStep508500: null!, ReadView: null!, ReadProjection: null!, ReadCameraRoll: null!, ReadCameraPose: null!, ReadEyePadding: null!, ReadTargetPadding: null!, ReadModel: null!, ReadWorld: null!, ReadTargetCenter5020A0: null!, ReadBone502170: null!, ReadVibrationPlaying4A2900: null!, Execute: null!);
        NativeBattleEffect187Step step = AdvanceTint64B0A0(frame, address, inputs);
        Assert.Equal(0x242424u, step.Frame.State.ReadDword(0x24FD900));
        Assert.Equal((ushort)4, Read(step.Frame, address).ReadWord(12));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void BoneParticleBirthClearsOnlyRecordHeadsAndKeepsRetainedBodies()
    {
        NativeBattleEffect187Frame frame = Frame();
        byte[] retained = Enumerable.Repeat((byte)0xA7, 60).ToArray();
        frame = WriteBytes(frame, 0x24FD510, NativeBattleEffectArena.Clear().Write(0, retained), retained.Length);
        frame = InitializeBoneParticles64B610(frame);
        byte[] current = ReadBytes(frame, 0x24FD510, 60).Read(0, 60);
        for (int offset = 0; offset < current.Length; offset++)
        {
            Assert.Equal(offset % 20 < 4 ? (byte)0 : (byte)0xA7, current[offset]);
        }
        NativeBattleModulePoolTask task = Assert.Single(frame.Effect.Pools[1].Slots, task => task.Callback == 0x64B640);
        Assert.Equal((ushort)0, task.ReadWord(12));
    }

    [Fact]
    public void TextureReleaseConsumesTheImmediateCompletionByteBeforeReturning()
    {
        NativeBattleEffect187Frame frame = Frame();
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress) };
        (frame, uint address) = Spawn647900(frame, 0x649CC0);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 1));
        NativeBattleEffect187Inputs inputs = new(Colors: null!, ReadPackets: null!, ReadFlags: null!, ReadStep508500: null!, ReadView: null!, ReadProjection: null!, ReadCameraRoll: null!, ReadCameraPose: null!, ReadEyePadding: null!, ReadTargetPadding: null!, ReadModel: null!, ReadWorld: null!, ReadTargetCenter5020A0: null!, ReadBone502170: null!, ReadVibrationPlaying4A2900: null!, Execute: (current, operation) =>
        {
            NativeBattleEffect187QueueTextureRelease508630 queued = Assert.IsType<NativeBattleEffect187QueueTextureRelease508630>(operation);
            Assert.Equal(CommonAddress + 0x14000, queued.SourceAddress);
            Assert.Equal(address + 14, queued.CompletionAddress);
            return WriteCompletionByte(current, queued.CompletionAddress, 1);
        });
        NativeBattleEffect187Step step = AdvanceTextureRelease649CC0(frame, address, inputs);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)2, Read(step.Frame, address).ReadWord(12));
        Assert.Equal((ushort)1, Read(step.Frame, address).ReadWord(14));
    }

    private static NativeBattleEffect187Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(Effect, 186, new(Effect, Entry, Initializer), new(0, 0, 8, 0, 0, 0), null);
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray();
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect with { Pools = pools }, NativeCrtRandomState.FromColdProcess(), NativeBattleEffect187State.FromColdProcess().DetachBank(0).DetachBank(1));
    }
}
