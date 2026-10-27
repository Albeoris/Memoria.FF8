using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSearchProgramDraw(NativeBattleModulePoolTask Task, int Remaining, NativeBattleGfGeometryBatch Packets);

/// <summary>Search's retained701970 program and758EA0's proved direct-position FT4 submissions.</summary>
public static class NativeBattleSearchProgramDrawing
{
    public static NativeBattleModulePoolTask InitializeWorkspace7016B0(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source)
    {
        RequireTask(task, source);
        if (unchecked((int)task.ReadDword(0x78)) != 0xE0)
        {
            throw new NotSupportedException("Native Search program requires its actualE0-byte workspace clear operand.");
        }
        NativeBattleGfAnimationWorkspace workspace = Workspace(task).Initialize7016B0(source, 0xE0);
        return Store(task, workspace);
    }

    public static NativeBattleSearchProgramDraw Advance758D70(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        RequireTask(task, source);
        NativeBattleActionMatrix context = ComposeContext758D70(task, view);
        short depthBias = unchecked((short)task.ReadWord(0x86));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleGfAnimationAdvance advanced = Workspace(task).Advance701970(source, 0, (current, node) =>
        {
            NativeBattleGfGeometryBatch batch = Node758EA0(source, node, context, depthBias, projection, colors);
            attempts.AddRange(batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
            allocated += batch.AllocationBytes;
            return current;
        });
        return new(Store(task, advanced.Workspace), advanced.Remaining, new(attempts.ToImmutable(), allocated));
    }

    public static NativeBattleActionMatrix ComposeContext758D70(NativeBattleModulePoolTask task, NativeBattleActionMatrix view)
    {
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770();
        short y = unchecked((short)task.ReadWord(0x62));
        short x = unchecked((short)task.ReadWord(0x60));
        short z = unchecked((short)task.ReadWord(0x64));
        if (y != 0)
        {
            local = NativeBattleCuragaMatrices.RotateY8DD8A0(local, y);
        }
        if (x != 0)
        {
            local = NativeBattleCuragaMatrices.RotateX8DD7E0(local, x);
        }
        if (z != 0)
        {
            local = NativeBattleCuragaMatrices.RotateZ8DD960(local, z);
        }
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(0x50)), unchecked((int)task.ReadDword(0x54)), unchecked((int)task.ReadDword(0x58))) with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) };
        return NativeBattlePoseMatrices.Compose56C2F0(view, local);
    }

    public static NativeBattleGfGeometryBatch Node758EA0(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, short depthBias, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (node.Data.IsDefault || node.Data.Length != 44)
        {
            throw new InvalidDataException("Native Search program requires the actual decoded44-byte node.");
        }
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new([], 0);
        }
        if ((node.Header & 0x200) == 0 || (node.Header & ~0x2FFu) != 0 || node.Word40 != 0 || node.Word42 != 0)
        {
            throw new NotSupportedException("Native Search program requires its proved direct-position single-shape node owners.");
        }
        int index = unchecked((short)node.GeometryIndex);
        if (index < 0 || index >= U32(0))
        {
            throw new InvalidDataException("Native Search node selects an absent original geometry.");
        }
        int geometry = checked((int)U32(8 + index * 4));
        int count = checked((int)U32(geometry + 4));
        int directory = checked((int)U32(geometry));
        if (count < 1 || count > source.Data.Length / 8 || U32(geometry + 8) != 1 || directory != 12 + count * 8)
        {
            throw new InvalidDataException("Native Search geometry requires its original12-byte single-shape header.");
        }
        int end = index + 1 < U32(0) ? checked((int)U32(12 + index * 4)) : checked((int)U32(4));
        ImmutableArray<byte> mesh = Read(geometry, checked(end - geometry)).ToArray().ToImmutableArray();
        NativeBattleActionMatrix matrix = ComposeNodeMatrix(node, context);
        bool cue = node.Scalar36 != 0;
        NativeBattleTextureWindow window = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext operands = new(cue ? 0x20F0u : 0x2030u, cue ? node.Color32 : null, cue ? node.Scalar36 : null, depthBias, 0, 0, window, window, 17, 2);
        return NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, matrix, projection, operands, colors, offset =>
        {
            if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
            {
                throw new InvalidDataException("Native Search selected vertex leaves its actual12-byte shape bank.");
            }
            ReadOnlySpan<byte> vertex = Read(geometry + 12 + offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..]));
        });

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
            {
                throw new InvalidDataException("Native Search program access leaves its complete actual source.");
            }
            return source.Data.AsSpan().Slice(offset, length);
        }
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Read(offset, 4));
    }

    internal static NativeBattleActionMatrix ComposeNodeMatrix(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context)
    {
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler701310(node.Vector16), node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
        return matrix with { X = unchecked(context.X + node.Vector8.X), Y = unchecked(context.Y + node.Vector8.Y), Z = unchecked(context.Z + node.Vector8.Z) };
    }

    private static void RequireTask(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source)
    {
        if (task.PayloadLength < 0x17C - 12 || task.ReadDword(0x74) != source.Address)
        {
            throw new InvalidDataException("Native Search program requires its complete task and exact retained source pointer.");
        }
    }

    private static NativeBattleGfAnimationWorkspace Workspace(NativeBattleModulePoolTask task) => new(task.ReadRawPayloadSpan().Slice(0x94 - 12, 0xE8).ToArray().ToImmutableArray());

    private static NativeBattleModulePoolTask Store(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        return task.WriteRetainedBytes(12 + (0x94 - 12), workspace.Data.AsSpan());
    }
}
