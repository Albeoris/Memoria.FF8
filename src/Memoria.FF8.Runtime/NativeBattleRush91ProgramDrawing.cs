using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRush91ProgramDraw(NativeBattleModulePoolTask Task, int Remaining, NativeBattleGfGeometryBatch Packets, ImmutableArray<byte> MorphScratch);
public sealed record NativeBattleRush91NodeDraw(NativeBattleGfGeometryBatch Packets, ImmutableArray<byte> MorphScratch);

/// <summary>761BB0's retained program and761CE0's private mesh submissions with caller-owned morph scratch.</summary>
public static class NativeBattleRush91ProgramDrawing
{
    public static NativeBattleModulePoolTask InitializeWorkspace761B60(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source)
    {
        RequireTask(task, source);
        if (task.ReadDword(0x78) != 0x70)
        {
            throw new NotSupportedException("Native Rush program requires its actual70-byte workspace clear operand.");
        }
        return Store(task, Workspace(task).Initialize7016B0(source, 0x70));
    }

    public static NativeBattleRush91ProgramDraw Advance761BB0(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        RequireTask(task, source);
        RequireScratch(morphScratch);
        NativeBattleActionMatrix context = ComposeContext761BB0(task, view);
        short depthBias = unchecked((short)task.ReadWord(0x88));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleGfAnimationAdvance advanced = Workspace(task).Advance701970(source, 0, (current, node) =>
        {
            NativeBattleRush91NodeDraw draw = Node761CE0(source, node, morphScratch, context, depthBias, projection, colors);
            morphScratch = draw.MorphScratch;
            attempts.AddRange(draw.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
            allocated = checked(allocated + draw.Packets.AllocationBytes);
            return current;
        });
        return new(Store(task, advanced.Workspace), advanced.Remaining, new(attempts.ToImmutable(), allocated), morphScratch);
    }

    public static NativeBattleActionMatrix ComposeContext761BB0(NativeBattleModulePoolTask task, NativeBattleActionMatrix view)
    {
        return NativeBattleSearchProgramDrawing.ComposeContext758D70(task, view);
    }

    public static NativeBattleRush91NodeDraw Node761CE0(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix context, short depthBias, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        RequireScratch(morphScratch);
        if (node.Data.IsDefault || node.Data.Length != 44)
        {
            throw new InvalidDataException("Native Rush program requires its actual decoded44-byte node.");
        }
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(new([], 0), morphScratch);
        }
        int index = unchecked((short)node.GeometryIndex);
        if (index < 0 || index >= U32(0))
        {
            throw new InvalidDataException("Native Rush node selects an absent original geometry.");
        }
        int geometry = checked((int)U32(8 + index * 4));
        int count = checked((int)U32(geometry + 4));
        int shapes = checked((int)U32(geometry + 8));
        int first = node.Word40;
        int second = node.Word42;
        if (count < 1 || count > 180 || shapes < 1 || first < 0 || first >= shapes || second < 0 || second >= shapes || U32(geometry) != 12 + count * shapes * 8)
        {
            throw new InvalidDataException("Native Rush node requires its complete twelve-byte geometry and in-range shape owners.");
        }
        ImmutableArray<byte> selected;
        int ratio = node.Scalar38;
        if (first == second || ratio is 0 or 4096)
        {
            int shape = first == second || ratio == 0 ? first : second;
            selected = Read(checked(geometry + 12 + count * shape * 8), checked(count * 8)).ToArray().ToImmutableArray();
        }
        else
        {
            morphScratch = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, first, second, ratio, morphScratch);
            selected = morphScratch;
        }
        int end = index + 1 < U32(0) ? checked((int)U32(12 + index * 4)) : checked((int)U32(4));
        ImmutableArray<byte> mesh = Read(geometry, checked(end - geometry)).ToArray().ToImmutableArray();
        bool cue = node.Scalar36 != 0;
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext operands = new(cue ? 0x20F0u : 0x2030u, cue ? node.Color32 : null, cue ? node.Scalar36 : null, depthBias, 0, 0, full, full, 17, 2);
        NativeBattleGfGeometryBatch packets = NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, ComposeNodeMatrix(node, context), projection, operands, colors, offset =>
        {
            if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
            {
                throw new InvalidDataException("Native Rush selected vertex leaves its actual shape bank.");
            }
            ReadOnlySpan<byte> vertex = selected.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..]));
        });
        return new(packets, morphScratch);

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
            {
                throw new InvalidDataException("Native Rush program access leaves its complete actual source.");
            }
            return source.Data.AsSpan().Slice(offset, length);
        }
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Read(offset, 4));
    }

    internal static NativeBattleActionMatrix ComposeNodeMatrix(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context)
    {
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(context, node.Vector8);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(context, NativeBattleGfMatrices.Euler701310(node.Vector16));
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
    }

    private static void RequireTask(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source)
    {
        if (task.Payload.IsDefault || task.Payload.Length != 0x108 - 12 || task.ReadDword(0x74) != source.Address)
        {
            throw new InvalidDataException("Native Rush program requires its complete108-byte task and exact retained source pointer.");
        }
    }

    private static void RequireScratch(ImmutableArray<byte> scratch)
    {
        if (scratch.IsDefault || scratch.Length != 0x5A0)
        {
            throw new InvalidDataException("Native Rush morph requires its actual retained5A0-byte scratch owner.");
        }
    }

    private static NativeBattleGfAnimationWorkspace Workspace(NativeBattleModulePoolTask task) => new(task.Payload.AsSpan().Slice(0x94 - 12, 0x70).ToArray().ToImmutableArray());

    private static NativeBattleModulePoolTask Store(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        byte[] bytes = task.Payload.ToArray();
        workspace.Data.AsSpan().CopyTo(bytes.AsSpan(0x94 - 12, 0x70));
        return task with { Payload = bytes.ToImmutableArray() };
    }
}
