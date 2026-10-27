using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAngelWing84ProgramResources(ImmutableArray<NativeBattleGfAnimationSource> Sources);
public sealed record NativeBattleAngelWing84ProgramStep(NativeBattleAngelWing84Frame Frame, int Result, NativeBattleLogicalOrdering Ordering);
public sealed record NativeBattleAngelWing84ProgramInputs(NativeBattleAngelWing84Inputs Controller, NativeBattleAngelWing84ProgramResources Resources, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, NativeBattleLogicalOrdering Ordering, Func<NativeBattleAngelWing84Frame, bool, NativeBattleAngelWing84Frame> SetStageFlags);

/// <summary>78ECD0/791260/796290's three scene programs and original78EEE0 private mesh callback.</summary>
public static class NativeBattleAngelWing84Programs
{
    public static NativeBattleAngelWing84ProgramStep Advance(NativeBattleAngelWing84Frame frame, uint address, NativeBattleAngelWing84ProgramInputs inputs)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        bool opening = task.Callback == 0x78ECD0;
        bool stage = task.Callback == 0x791260;
        if (!opening && !stage && task.Callback != 0x796290)
        {
            throw new InvalidDataException("Native Angel Wing program requires one of its three original callbacks.");
        }
        NativeBattleGfAnimationSource source = inputs.Resources.Sources.Single(candidate => candidate.Address == task.ReadDword(116));
        int size = checked((int)task.ReadDword(120));
        if (size != (opening ? 1184 : stage ? 1928 : 344))
        {
            throw new InvalidDataException("Native Angel Wing program workspace contradicts its original callback.");
        }
        NativeBattleLogicalOrdering ordering = inputs.Ordering;
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                if (!opening || unchecked((short)task.ReadWord(36)) >= 24)
                {
                    if (stage)
                    {
                        Store();
                        frame = inputs.SetStageFlags(frame, false);
                        Read();
                    }
                    NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (148 - 12), size);
                    task = StoreWorkspace(task, workspace.Initialize7016B0(source, size));
                    NativeBattleModelTransform actor = inputs.Controller.ReadModel(task.ReadByte(44)).Transform ?? throw new NotSupportedException("Native Angel Wing program requires its actual caster position and WORD34.");
                    task = task.WriteWord(28, unchecked((ushort)actor.Position28.X)).WriteWord(30, unchecked((ushort)actor.Position28.Y)).WriteWord(32, unchecked((ushort)actor.Position28.Z)).WriteWord(34, unchecked((ushort)actor.Vertical34)).WriteDword(80, 4096).WriteDword(84, 4096).WriteDword(88, 4096);
                    if (opening)
                    {
                        task = task.WriteWord(30, unchecked((ushort)(task.ReadWord(30) - 1024))).WriteWord(32, unchecked((ushort)(task.ReadWord(32) + 512))).WriteWord(136, unchecked((ushort)-512));
                    }
                    else if (stage)
                    {
                        task = task.WriteWord(96, unchecked((ushort)-128));
                    }
                    else
                    {
                        task = task.WriteWord(30, 0);
                    }
                    AdvanceWorkspace();
                    Next();
                }
                break;
            case 1:
                bool reached = (opening || stage) && NativeBattleAngelWing84Scene.Read(frame).Word(64) >= (stage ? 4 : 3);
                if (reached)
                {
                    if (stage)
                    {
                        Store();
                        frame = inputs.SetStageFlags(frame, true);
                        Read();
                    }
                    Complete();
                }
                else if (AdvanceWorkspace() == 0 && !stage)
                {
                    Complete();
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native Angel Wing program phase exceeds its three callbacks.");
        }
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        Store();
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        bool completed = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (completed)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, completed ? 2 : 0, ordering);

        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void Complete()
        {
            task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1));
            Next();
        }
        void Store() => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        void Read() => task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        int AdvanceWorkspace()
        {
            NativeBattleActionMatrix context = NativeBattleSearchProgramDrawing.ComposeContext758D70(task, inputs.Controller.Camera);
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (148 - 12), size);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
            {
                NativeBattleGfGeometryBatch batch = DrawNode(source, node, context, unchecked((short)task.ReadWord(136)), task.ReadWord(128), inputs.Projection, inputs.Colors);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, frame.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native78EEE0 requires its current private draw cursor."), ordering, batch);
                frame = frame with { Memory = written.Memory, State = frame.State with { PrimaryDrawCursor = written.Cursor } };
                ordering = written.Ordering;
                return current;
            });
            task = StoreWorkspace(task, advanced.Workspace);
            return advanced.Remaining;
        }
    }

    public static NativeBattleGfGeometryBatch DrawNode(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, short depthBias, ushort mode, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new([], 0);
        }
        int index = unchecked((short)node.GeometryIndex);
        int count = Int(0);
        if (index < 0 || index >= count)
        {
            throw new InvalidDataException("Native78EEE0 selected an absent original geometry.");
        }
        int geometry = Int(8 + index * 4);
        int vertices = Int(geometry + 4);
        int shapes = Int(geometry + 8);
        int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Scalar38 == 4096 ? node.Word42 : throw new NotSupportedException("Native78EEE0 interpolated geometry requires its actual25C9EFC morph allocation.");
        if (vertices < 1 || shapes < 1 || shape < 0 || shape >= shapes)
        {
            throw new InvalidDataException("Native78EEE0 selected an absent original shape.");
        }
        int end = index + 1 < count ? Int(12 + index * 4) : Int(4);
        ImmutableArray<byte> mesh = Read(geometry, checked(end - geometry)).ToArray().ToImmutableArray();
        NativeBattleActionMatrix matrix = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        matrix = NativeBattleGfMatrices.Multiply56C090(context, matrix);
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)) : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(context, node.Vector8);
        matrix = matrix with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        bool fade = node.Scalar36 != 0;
        if (fade)
        {
            flags |= 0xC0;
        }
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext operands = new(flags, fade ? node.Color32 : null, fade ? node.Scalar36 : null, mode == 2 && index == 1 ? depthBias - 28672 : depthBias, 0, 0, full, full, 17, 2);
        return NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, matrix, projection, operands, colors, offset =>
        {
            if (offset < 0 || (offset & 7) != 0 || offset >= vertices * 8)
            {
                throw new InvalidDataException("Native78EEE0 selected vertex leaves its actual shape.");
            }
            ReadOnlySpan<byte> vertex = Read(checked(geometry + 12 + shape * vertices * 8 + offset), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..]));
        });

        ReadOnlySpan<byte> Read(int offset, int length) => source.Data.AsSpan().Slice(offset, length);
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
    }

    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        return workspace.CopyTo(task, 148, retainUnknown: true);
    }
}
