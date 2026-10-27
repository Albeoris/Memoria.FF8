using System.Collections.Immutable;
using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8ActInitializeTarget(uint TaskAddress, byte Actor) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8ActPlace(uint TaskAddress, short Selector) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8ActPreparation(NativeBattleDoom8Frame Frame, NativeBattleEffectArena Context92);
public sealed record NativeBattleDoom8ActInputs(Func<NativeBattleDoom8Frame, uint, NativeBattleGfAnimationSource> ReadSource, Func<NativeBattleDoom8Frame, uint, NativeBattleDoom8ActPreparation> PrepareDraw, Func<NativeBattleDoom8Frame, uint, NativeBattleEffectArena, NativeBattleGfAnimationNode, NativeBattleDoom8Frame> EmitNode, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> Execute, Func<NativeBattleDoom8Frame, NativeBattleDoom8Step> AdvanceStrips, Func<int, short> ReadPlacementSelector, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> SelectActors);

public static class NativeBattleDoom8Act
{
    public const uint PrimaryCallback = 0x8C58B0;
    public const uint MovingCallback = 0x8C8050;
    public const uint AuxiliaryCallback = 0x8C8790;

    public static NativeBattleDoom8Step Advance(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8ActInputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        Require(frame, address, task);
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        switch (task.Callback)
        {
            case PrimaryCallback:
                if (phase == 0)
                {
                    if (task.ReadWord(0x24) == 11)
                    {
                        frame = Initialize(frame, address, inputs);
                        task = Read(frame, address).WriteWord(0x62, frame.State.SharedWord(0x4A)).WriteDword(0x1C, frame.State.ReadDword(NativeBattleDoom8State.SharedAddress)).WriteDword(0x20, frame.State.ReadDword(NativeBattleDoom8State.SharedAddress + 4));
                        NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
                        identity = identity with { Known = identity.Known.Select((known, index) => index < 18 && known).ToImmutableArray() };
                        NativeBattleStatefulMatrixRotationStep rotated = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, frame.Staging ?? throw new NotSupportedException("Doom ACT initialization requires its physical rotation temporary memory."), identity, 'Y', Word(task, 0x62));
                        frame = frame with { Memory = rotated.Memory, Staging = rotated.Staging };
                        NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotated.Matrix.Bytes.AsSpan().Slice(index * 2))).ToImmutableArray(), 0, 0, 0, 4096);
                        NativeBattleVector shift = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(-17, -1076, -409));
                        task = task.WriteWord(0x1C, unchecked((ushort)(Word(task, 0x1C) + shift.X))).WriteWord(0x1E, unchecked((ushort)(Word(task, 0x1E) + shift.Y))).WriteWord(0x20, unchecked((ushort)(Word(task, 0x20) + shift.Z)));
                        task = Scale(task).WriteWord(0x60, 0xF00).WriteWord(0x88, 0xFFF0);
                        frame = AdvanceAnimation(Put(frame, address, task), address, inputs).Frame;
                        frame = Next(frame, address);
                    }
                }
                else if (phase == 1)
                {
                    NativeBattleDoom8Step advanced = AdvanceAnimation(frame, address, inputs);
                    frame = advanced.Frame;
                    if (unchecked((ushort)advanced.Result) == 19)
                    {
                        NativeBattleDoom8FrontAppend child = Append(frame, address, MovingCallback, 0x128, 0x8C, 1, inputs);
                        task = Read(child.Frame, address);
                        frame = Put(child.Frame, child.Address, Read(child.Frame, child.Address).WriteDword(0x1C, task.ReadDword(0x1C)).WriteDword(0x20, task.ReadDword(0x20)));
                    }
                    else if (unchecked((ushort)advanced.Result) == 0)
                    {
                        frame = CompletePhase(frame, address);
                    }
                }
                else if (phase != 2)
                {
                    throw PhaseError();
                }
                break;
            case MovingCallback:
                if (phase == 0)
                {
                    frame = Initialize(frame, address, inputs);
                    frame = Put(frame, address, Scale(Read(frame, address)));
                    frame = inputs.Execute(frame, new NativeBattleDoom8ActInitializeTarget(address, Read(frame, address).ReadByte(0x2D)));
                    frame = Place(frame, address, inputs);
                    frame = AdvanceAnimation(frame, address, inputs).Frame;
                    frame = Next(frame, address);
                }
                else if (phase == 1)
                {
                    short index = unchecked((short)(Read(frame, address).ReadWord(0x456) + 1));
                    short selector = inputs.ReadPlacementSelector(index);
                    frame = Put(frame, address, Read(frame, address).WriteWord(0x456, unchecked((ushort)index)).WriteWord(0x454, unchecked((ushort)selector)));
                    if (selector >= 127)
                    {
                        task = Read(frame, address);
                        frame = Put(frame, address, task.WriteDword(0x1C, task.ReadDword(0x44C)).WriteDword(0x20, task.ReadDword(0x450)).WriteWord(0x454, 17));
                        NativeBattleDoom8FrontAppend child = Append(frame, address, AuxiliaryCallback, 0x12C, 0x3B0, 2, inputs);
                        frame = Put(child.Frame, child.Address, Read(child.Frame, child.Address).WriteDword(0x1C, child.Frame.State.ReadDword(0x275F548)).WriteDword(0x20, child.Frame.State.ReadDword(0x275F54C)));
                        frame = Next(frame, address);
                    }
                    frame = Place(frame, address, inputs);
                    frame = AdvanceAnimation(frame, address, inputs).Frame;
                    frame = inputs.AdvanceStrips(frame).Frame;
                }
                else if (phase == 2)
                {
                    NativeBattleDoom8Step advanced = AdvanceAnimation(frame, address, inputs);
                    frame = advanced.Frame;
                    if (advanced.Result == 0)
                    {
                        frame = Next(frame, address);
                    }
                    frame = inputs.AdvanceStrips(frame).Frame;
                }
                else if (phase == 3)
                {
                    NativeBattleDoom8Step strips = inputs.AdvanceStrips(frame);
                    frame = strips.Frame;
                    if (strips.Result == 0)
                    {
                        frame = CompletePhase(frame, address);
                    }
                }
                else if (phase != 4)
                {
                    throw PhaseError();
                }
                break;
            case AuxiliaryCallback:
                if (phase == 0)
                {
                    frame = Initialize(frame, address, inputs);
                    frame = Put(frame, address, Scale(Read(frame, address)).WriteWord(0x88, 0xFE00));
                    frame = AdvanceAnimation(frame, address, inputs).Frame;
                    frame = Next(frame, address);
                }
                else if (phase == 1)
                {
                    NativeBattleDoom8Step advanced = AdvanceAnimation(frame, address, inputs);
                    frame = advanced.Frame;
                    if (advanced.Result == 0)
                    {
                        frame = CompletePhase(frame, address);
                    }
                }
                else if (phase != 2)
                {
                    throw PhaseError();
                }
                break;
        }
        return NativeBattleDoom8Controller.Finish(frame, address);
    }

    public static NativeBattleDoom8Step AdvanceAnimation(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8ActInputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        Require(frame, address, task);
        NativeBattleDoom8ActPreparation prepared = inputs.PrepareDraw(frame, address);
        frame = prepared.Frame;
        task = Read(frame, address);
        int size = WorkspaceSize(task);
        NativeBattleGfAnimationSource source = Source(frame, task.ReadDword(0x94), inputs);
        NativeBattleGfAnimationWorkspace workspace = Workspace(task, size);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, unchecked((int)frame.State.ReadDword(0x275ABDC)), (retained, node) =>
        {
            frame = Put(frame, address, Read(frame, address).WriteBytes(0x94, retained.Data.AsSpan()));
            frame = inputs.EmitNode(frame, address, prepared.Context92, node);
            return Workspace(Read(frame, address), size);
        });
        frame = Put(frame, address, advanced.Workspace.CopyTo(Read(frame, address), 0x94, retainUnknown: false));
        return new(frame, advanced.Remaining);
    }

    private static NativeBattleDoom8Frame Initialize(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8ActInputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int size = WorkspaceSize(task);
        NativeBattleGfAnimationWorkspace workspace = Workspace(task, size).Initialize7016B0(Source(frame, task.ReadDword(0x74), inputs), size);
        return Put(frame, address, workspace.CopyTo(task, 0x94, retainUnknown: false));
    }

    private static NativeBattleGfAnimationSource Source(NativeBattleDoom8Frame frame, uint pointer, NativeBattleDoom8ActInputs inputs)
    {
        NativeBattleGfAnimationSource source = inputs.ReadSource(frame, pointer);
        if (source.Address != pointer)
        {
            throw new InvalidDataException("Doom ACT source does not identify its retained native resource pointer.");
        }
        return source;
    }

    private static int WorkspaceSize(NativeBattleModulePoolTask task)
    {
        int size = unchecked((int)task.ReadDword(0x78));
        if (size < 8 || size > 1112 - 0x94)
        {
            throw new InvalidDataException("Doom ACT workspace exceeds its original physical task.");
        }
        return size;
    }

    private static NativeBattleGfAnimationWorkspace Workspace(NativeBattleModulePoolTask task, int size) => new(task.ReadBytes(0x94, size).ToImmutableArray());
    private static NativeBattleDoom8Frame Place(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8ActInputs inputs) => inputs.Execute(frame, new NativeBattleDoom8ActPlace(address, Word(Read(frame, address), 0x454)));
    private static NativeBattleModulePoolTask Scale(NativeBattleModulePoolTask task) => task.WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
    private static NativeBattleDoom8Frame Next(NativeBattleDoom8Frame frame, uint address) => Put(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static NativeBattleDoom8Frame CompletePhase(NativeBattleDoom8Frame frame, uint address) => Next(Put(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1))), address);
    private static NativeBattleModulePoolTask Read(NativeBattleDoom8Frame frame, uint address) => NativeBattleDoom8Controller.Read(frame, address);
    private static NativeBattleDoom8Frame Put(NativeBattleDoom8Frame frame, uint address, NativeBattleModulePoolTask task) => NativeBattleDoom8Controller.Write(frame, address, task);
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static InvalidDataException PhaseError() => new("Doom ACT signed phase exceeds its original callback table.");

    private static NativeBattleDoom8FrontAppend Append(NativeBattleDoom8Frame frame, uint parent, uint callback, int sourceOffset, short length, short selector, NativeBattleDoom8ActInputs inputs)
    {
        if (frame.State.ReadDword(NativeBattleDoom8Io.ControlPointerGlobal) != NativeBattleDoom8Io.ControlAddress)
        {
            throw new NotSupportedException("Doom ACT child requires its executed IO control pointer.");
        }
        uint source = frame.State.ReadDword(NativeBattleDoom8Io.ControlAddress + (uint)sourceOffset);
        return NativeBattleDoom8Fronts.Append8C5860(frame, parent, callback, source, length, selector, 0, inputs.SelectActors);
    }

    private static void Require(NativeBattleDoom8Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        frame.State.Validate();
        NativeBattleCureFamilyPool expected = NativeBattleDoom8State.Pools[4];
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == expected.Descriptor);
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry || task.Callback is not (PrimaryCallback or MovingCallback or AuxiliaryCallback) || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity || address < pool.BankAddress || address >= pool.BankAddress + pool.Stride * pool.Slots.Length || (address - pool.BankAddress) % pool.Stride != 0)
        {
            throw new InvalidDataException("Doom ACT requires its actual module, callback and 1112-byte task bank.");
        }
    }
}
