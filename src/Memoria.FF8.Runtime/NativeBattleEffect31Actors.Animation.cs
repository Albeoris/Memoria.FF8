using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect31Actors
{
    private static NativeBattleEffect31Frame AdvanceFirst(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 2))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= 0)
                {
                    frame = InitializeProgramme(frame, address, resources);
                    frame = UpdatePoint(frame, address + 0x1C, Caster(frame), inputs);
                    frame = IncrementPhase(frame, address);
                }
                return frame;
            case 1:
                (frame, NativeBattlePackedMatrix32 world) = CasterMatrix(frame, address, 0x872020, 0x1C, 32, inputs);
                frame = Observe(frame, address, 0x872020, 0x8720FD, world, inputs);
                (frame, int remaining) = AdvanceProgramme(frame, address, world, resources, inputs);
                if (remaining == 0)
                {
                    (frame, uint child) = AppendHierarchy(frame, 5, 0x872170, 0x80, address, inputs);
                    frame = MarkComplete(frame, address);
                    frame = WriteBytes(frame, child + 0x58, PackedArena(world), 32);
                    frame = IncrementPhase(frame, address);
                }
                return frame;
            default:
                return frame;
        }
    }

    private static NativeBattleEffect31Frame AdvanceSecond(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 3))
        {
            case 0:
                return unchecked((short)task.ReadWord(0x24)) >= 16 ? IncrementPhase(frame, address) : frame;
            case 1:
                frame = InitializeProgramme(frame, address, resources);
                frame = UpdatePoint(frame, address + 0x1C, Caster(frame), inputs);
                frame = Observe(frame, address, 0x872990, 0x8729D3, Packed(ReadBytes(frame, address + 0x30, 32)), inputs);
                return IncrementPhase(frame, address);
            case 2:
                (frame, NativeBattlePackedMatrix32 world) = CasterMatrix(frame, address, 0x8729F0, 0x1C, 32, inputs);
                (frame, int remaining) = AdvanceProgramme(frame, address, world, resources, inputs);
                return remaining == 0 ? IncrementPhase(MarkComplete(frame, address), address) : frame;
            default:
                return frame;
        }
    }

    private static NativeBattleEffect31Frame AdvancePersistent(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = NativePhase(task, 3);
        if (phase == 0)
        {
            return unchecked((short)task.ReadWord(0x24)) >= 16 ? IncrementPhase(frame, address) : frame;
        }
        if (phase == 3)
        {
            return frame;
        }
        NativeBattlePackedMatrix32 world;
        if (phase == 1)
        {
            frame = InitializeProgramme(frame, address, resources);
            frame = UpdatePoint(frame, address + 0x1C, Caster(frame), inputs);
            world = Packed(ReadBytes(frame, address + 0x30, 32));
            (frame, world) = Bone(frame, world, inputs);
            frame = WriteBytes(frame, address + 0x30, PackedArena(world), 32);
            (frame, world) = Rotate(frame, world, 'X', 1024);
            frame = WriteBytes(frame, address + 0x30, PackedArena(world), 32);
            frame = AddPoint(frame, address + 0x1C, RotateShort(world, new(32, -32, -224)));
            world = Translation(world, Point(ReadBytes(frame, address + 0x1C, 8)));
            frame = WriteBytes(frame, address + 0x30, PackedArena(world), 32);
        }
        else
        {
            world = Packed(ReadBytes(frame, address + 0x30, 32));
        }
        frame = Observe(frame, address, phase == 1 ? 0x873240u : 0x873350u, phase == 1 ? 0x873326u : 0x87337Eu, world, inputs);
        (frame, int remaining) = AdvanceProgramme(frame, address, world, resources, inputs);
        if (phase == 1)
        {
            return IncrementPhase(frame, address);
        }
        return remaining == 0 ? IncrementPhase(MarkComplete(frame, address), address) : frame;
    }

    private static NativeBattleEffect31Frame InitializeProgramme(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int length = checked((int)task.ReadDword(0x78));
        NativeBattleGfAnimationSource source = resources.Programme(task.ReadDword(0x74));
        NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x94, length);
        workspace = workspace.Initialize7016B0(source, length);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x94, workspace);
        return frame;
    }

    private static (NativeBattleEffect31Frame Frame, int Remaining) AdvanceProgramme(NativeBattleEffect31Frame frame, uint address, NativeBattlePackedMatrix32 world, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int length = checked((int)task.ReadDword(0x78));
        NativeBattleGfAnimationSource source = resources.Programme(task.ReadDword(0x74));
        NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x94, length);
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world.ReadMatrix());
        NativeBattlePackedMatrix32 context = NativeBattlePackedMatrix32.FromMatrix(camera, Padding(world));
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x94, current);
            frame = DrawNode(frame, source, node, context, resources, inputs);
            return NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x94, length);
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x94, advanced.Workspace);
        return (frame, advanced.Remaining);
    }

    private static (NativeBattleEffect31Frame Frame, NativeBattlePackedMatrix32 Matrix) CasterMatrix(NativeBattleEffect31Frame frame, uint address, uint stateBody, int pointOffset, short offsetZ, NativeBattleEffect31Inputs inputs)
    {
        frame = UpdatePoint(frame, checked(address + (uint)pointOffset), Caster(frame), inputs);
        NativeBattleEffect31StackReadStep stacked = inputs.ReadActorStack?.Invoke(frame, address, stateBody) ?? throw new NotSupportedException("Effect31 actor requires its actual retained caller matrix bytes.");
        frame = stacked.Frame;
        (frame, NativeBattlePackedMatrix32 matrix) = Bone(frame, stacked.Output, inputs);
        (frame, matrix) = Rotate(frame, matrix, 'X', 1024);
        uint rotateSite = stateBody switch
        {
            0x872020 => 0x872075,
            0x8721D0 => 0x87221F,
            0x8725B0 => 0x8725FF,
            0x8729F0 => 0x872A3F,
            0x872B80 => 0x872BFB,
            _ => throw new InvalidDataException("Effect31 actor stack state body is unsupported.")
        };
        frame = Observe(frame, address, stateBody, rotateSite, matrix, inputs);
        frame = AddPoint(frame, checked(address + (uint)pointOffset), RotateShort(matrix, new(32, -32, offsetZ)));
        return (frame, Translation(matrix, Point(ReadBytes(frame, checked(address + (uint)pointOffset), 8))));
    }

    private static NativeBattleEffect31Frame UpdatePoint(NativeBattleEffect31Frame frame, uint address, int actor, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleEffect31PointStep point = inputs.Point502170(frame, ModelAddress(actor), 0xF1, 0, ReadBytes(frame, address, 8));
        return WriteBytes(point.Frame, address, point.Output8, 8);
    }

    private static (NativeBattleEffect31Frame Frame, NativeBattlePackedMatrix32 Matrix) Bone(NativeBattleEffect31Frame frame, NativeBattlePackedMatrix32 output, NativeBattleEffect31Inputs inputs)
    {
        int actor = Caster(frame);
        NativeBattleBoneMatrixOutputStep queried = NativeBattleBoneMatrixOutput8DE8F0.Query(frame.Memory, RequireStaging(frame), inputs.ReadModel(frame, actor), inputs.ReadPoseSource(frame, actor), 0xF1, 0, output);
        return (frame with { Memory = queried.Memory, Staging = queried.Staging }, queried.Output);
    }

    private static (NativeBattleEffect31Frame Frame, NativeBattlePackedMatrix32 Matrix) Rotate(NativeBattleEffect31Frame frame, NativeBattlePackedMatrix32 matrix, char axis, short angle)
    {
        NativeBattleStatefulMatrixRotationStep rotated = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, RequireStaging(frame), matrix, axis, angle);
        return (frame with { Memory = rotated.Memory, Staging = rotated.Staging }, rotated.Matrix);
    }

    private static NativeBattleVector RotateShort(NativeBattlePackedMatrix32 matrix, NativeBattleVector point) => NativeBattleGfMatrices.RotateShort56C4F0(matrix.ReadMatrix(), point);
    private static NativeBattlePackedMatrix32 Translation(NativeBattlePackedMatrix32 matrix, NativeBattleVector point) => Packed(Dword(Dword(Dword(PackedArena(matrix), 20, unchecked((uint)(int)point.X)), 24, unchecked((uint)(int)point.Y)), 28, unchecked((uint)(int)point.Z)));
    private static ushort? Padding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadUInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18)) : null;
    private static NativeBattleEffect31Frame Observe(NativeBattleEffect31Frame frame, uint task, uint stateBody, uint instruction, NativeBattlePackedMatrix32 matrix, NativeBattleEffect31Inputs inputs) => inputs.ObserveActorStack?.Invoke(frame, task, stateBody, instruction, matrix) ?? throw new NotSupportedException("Effect31 actor requires its current caller stack write observer.");
}
