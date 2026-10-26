using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect187Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187ActorOverlayTests
{
    [Fact]
    public void PausedWarmupPublishesOnlyTheNativeByteFlagAndKeepsItsClock()
    {
        NativeBattleEffect187Frame frame = Frame(29);
        List<NativeBattleEffect187Operation> operations = [];
        NativeBattleEffect187Step step = AdvanceActorOverlay648AF0(frame, ChildBank, Inputs(operations, 0x201));
        NativeBattleEffect187WriteModelBytes write = Assert.IsType<NativeBattleEffect187WriteModelBytes>(Assert.Single(operations));
        Assert.Equal(0, write.Offset);
        Assert.Equal(1, write.Length);
        Assert.Equal((byte)10, write.Data.Read(0, 1)[0]);
        Assert.Equal((ushort)29, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)10, Read(step.Frame, ChildBank).ReadWord(0x1C));
    }

    [Fact]
    public void PlaneOffsetUsesTheTargetWorldTimesTheActualPlaneRotation()
    {
        NativeBattleEffect187Frame frame = Frame(30);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x18, 1024));
        List<NativeBattleEffect187Operation> operations = [];
        NativeBattleEffect187Step step = AdvanceActorOverlay648AF0(frame, ChildBank, Inputs(operations, 0));
        NativeBattleEffect187DrawBody464 body = Assert.Single(operations.OfType<NativeBattleEffect187DrawBody464>());
        NativeBattleActionMatrix offset = body.OffsetWorld.ReadMatrix();
        Assert.Equal(0, offset.X);
        Assert.Equal(10, offset.Y);
        Assert.Equal(0, offset.Z);
        Assert.Equal((ushort)22, Read(step.Frame, ChildBank).ReadWord(0x1C));
        Assert.Equal((ushort)4, Read(step.Frame, ChildBank).ReadWord(0x1E));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(80, 0)]
    [InlineData(81, 2)]
    public void FinalCallbacksAdvanceWithoutDrawingAndRetainMinimumVelocity(int age, int result)
    {
        NativeBattleEffect187Frame frame = Frame(age);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x1E, 1));
        List<NativeBattleEffect187Operation> operations = [];
        NativeBattleEffect187Step step = AdvanceActorOverlay648AF0(frame, ChildBank, Inputs(operations, 0));
        Assert.Empty(operations);
        Assert.Equal(result, step.Result);
        Assert.Equal((ushort)11, Read(step.Frame, ChildBank).ReadWord(0x1C));
        Assert.Equal((ushort)2, Read(step.Frame, ChildBank).ReadWord(0x1E));
    }

    private static NativeBattleEffect187Inputs Inputs(List<NativeBattleEffect187Operation> operations, uint flags) => new(Colors: null!, ReadPackets: null!, ReadFlags: _ => flags, ReadStep508500: null!, ReadView: null!, ReadProjection: null!, ReadCameraRoll: null!, ReadCameraPose: null!, ReadEyePadding: null!, ReadTargetPadding: null!, ReadModel: (_, actor) => new(actor, 2), ReadWorld: (_, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), null), ReadTargetCenter5020A0: null!, ReadBone502170: null!, ReadVibrationPlaying4A2900: null!, Execute: (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });

    private static NativeBattleEffect187Frame Frame(int age)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
        (children, int slot) = children.Append(0x648AF0);
        children = children with { Slots = children.Slots.SetItem(slot, children.Slots[slot].WriteWord(12, checked((ushort)age)).WriteWord(14, 3).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, 0).WriteWord(0x18, 0).WriteWord(0x1C, 10).WriteWord(0x1E, 4)) };
        NativeBattleOriginalEffectState effect = new(Effect, 186, new(Effect, Entry, Initializer), new(0, 0, 8, 0, 0, 0), RootDescriptor) { Pools = [root, children] };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, NativeCrtRandomState.FromColdProcess(), NativeBattleEffect187State.FromColdProcess().DetachBank(0).DetachBank(1)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
}
