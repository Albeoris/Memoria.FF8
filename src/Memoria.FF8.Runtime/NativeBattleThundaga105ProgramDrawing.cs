using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleThundaga105NodeDraw(NativeBattleEffectMemory Memory, NativeBattleGfGeometryBatch Packets);
public sealed record NativeBattleThundaga105ProgramDraw(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task, int Remaining, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleThundaga105ProgramDrawing
{
    private const int WorkspaceOffset = 0x1C;
    private const int WorkspaceLength = 0x868;
    private const int MorphArenaOffset = 0x2254;

    public static NativeBattleModulePoolTask InitializeWorkspace7016B0(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source)
    {
        RequireTask(task);
        return Store(task, Workspace(task).Initialize7016B0(source, WorkspaceLength));
    }

    public static NativeBattleThundaga105ProgramDraw Advance6D43B0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> selector, ImmutableArray<byte> anchor, NativeBattleActionMatrix view, NativeBattleActionMatrix normalizedBillboard, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        RequireTask(task);
        ValidateContext(selector, anchor);
        int ownerScale = unchecked((int)task.ReadDword(0x18));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleGfAnimationAdvance advance = Workspace(task).Advance701970(source, 0, (current, node) =>
        {
            NativeBattleThundaga105NodeDraw draw = Node6D4910(memory, source, node, selector, unchecked((ushort)current.Frame), ownerScale, anchor, view, normalizedBillboard, projection, colors);
            memory = draw.Memory;
            attempts.AddRange(draw.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
            allocated = checked(allocated + draw.Packets.AllocationBytes);
            return current;
        });
        return new(memory, Store(task, advance.Workspace), advance.Remaining, new(attempts.ToImmutable(), allocated));
    }

    public static NativeBattleThundaga105NodeDraw Node6D4910(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, ImmutableArray<byte> selector, ushort frame, int ownerScale, ImmutableArray<byte> anchor, NativeBattleActionMatrix view, NativeBattleActionMatrix normalizedBillboard, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        ValidateContext(selector, anchor);
        if (node.Data.IsDefault || node.Data.Length != 44)
        {
            throw new InvalidDataException("Native Thundaga program requires its complete decoded node.");
        }
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(memory, new([], 0));
        }
        int index = unchecked((short)node.GeometryIndex);
        int geometryCount = I32(0);
        if (index < 0 || index >= geometryCount || node.NodeIndex >= selector.Length)
        {
            throw new InvalidDataException("Native Thundaga program selects an absent geometry or selector.");
        }
        int geometry = I32(8 + index * 4);
        int vertices = I32(geometry + 4);
        int shapes = I32(geometry + 8);
        int first = node.Word40;
        int second = node.Word42;
        if (vertices < 1 || vertices > source.Data.Length / 8 || shapes < 1 || first < 0 || second < 0 || first >= shapes || second >= shapes)
        {
            throw new InvalidDataException("Native Thundaga geometry requires its actual shaped vertex bank.");
        }
        int bytes = checked(vertices * 8);
        ImmutableArray<byte> selected;
        int ratio = node.Scalar38;
        if (first == second || ratio is 0 or 4096)
        {
            int shape = first == second || ratio == 0 ? first : second;
            selected = NativeBattleTelemetry.Immutable(1263, NativeBattleTelemetry.Materialize(1259, Read(checked(geometry + 12 + shape * bytes), bytes)));
        }
        else
        {
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Thundaga morph requires its retained arena owner.");
            if (bytes > 0x7D0)
            {
                throw new InvalidDataException("Native Thundaga morph leaves its actual shared scratch extent.");
            }
            ImmutableArray<byte> scratch = NativeBattleTelemetry.Immutable(1264, arena.Read(MorphArenaOffset, bytes));
            selected = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, first, second, ratio, scratch);
            memory = memory with { Arena = arena.Write(MorphArenaOffset, selected.AsSpan()) };
        }
        NativeBattleActionMatrix matrix = ComposeNodeMatrix(node, ownerScale, anchor, view, normalizedBillboard);
        uint flags = node.Scalar36 != 0 ? 0x20F0u : 0x2030u;
        if (selector[node.NodeIndex] == 0)
        {
            return new(memory, NativeBattleModulePackets.EmitBatch572200(source.Data, geometry, selected, matrix, projection, new(flags, node.Color32, node.Scalar36, null, null, null, 17, 2), colors));
        }
        matrix = matrix with { Z = unchecked(matrix.Z - 256) };
        int cursor = checked(geometry + I32(geometry));
        SkipEmptyGroups();
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        Dictionary<ushort, NativeBattleModuleProjectedVertex> projected = [];
        ushort v = (ushort)((Math.Min((int)frame, 4) & 3) << 5);
        for (int list = 0; list < 2; list++)
        {
            bool gouraud = list != 0;
            if (gouraud)
            {
                SkipEmptyGroups();
            }
            int count = I32(cursor);
            cursor = checked(cursor + 4);
            if (count < 0 || count > source.Data.Length / (gouraud ? 36 : 24))
            {
                throw new InvalidDataException("Native Thundaga quad list exceeds its current source.");
            }
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                int length = gouraud ? 36 : 24;
                byte[] face = NativeBattleTelemetry.Materialize(1260, Read(cursor, length));
                NativeBattleEffectMeshWindow window = new(0, v, new(0, 0, 256, 256), new(0, 128, 0, 128), true, gouraud ? (byte)0 : (byte)128);
                uint cueFlag = node.Scalar36 == 0 ? 0u : gouraud ? 0x80u : 0x40u;
                NativeBattleGfGeometryBatch batch = NativeBattleEffectMeshPackets.EmitTextured(face, corner => Project(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2))), true, gouraud, new(cueFlag, node.Color32, node.Scalar36, null, null, 0, false, null, 17, 2, window), colors, allocated, NativeBattleEffectMeshClip.OutsideScreen);
                attempts.AddRange(batch.Attempts);
                allocated = checked(allocated + batch.AllocationBytes);
                cursor = checked(cursor + length);
            }
        }
        return new(memory, new(attempts.ToImmutable(), allocated));

        void SkipEmptyGroups()
        {
            for (int group = 0; group < 3; group++)
            {
                if (I32(cursor) != 0)
                {
                    throw new NotSupportedException("Native Thundaga custom quads require their original empty skipped primitive groups.");
                }
                cursor = checked(cursor + 4);
            }
        }
        NativeBattleModuleProjectedVertex Project(ushort indexOffset)
        {
            if (!projected.TryGetValue(indexOffset, out NativeBattleModuleProjectedVertex vertex))
            {
                int offset = checked(indexOffset * 4);
                if ((offset & 7) != 0 || offset > selected.Length - 8)
                {
                    throw new InvalidDataException("Native Thundaga quad index leaves its selected vertex bank.");
                }
                ReadOnlySpan<byte> point = selected.AsSpan(offset, 6);
                vertex = NativeBattleModuleProjection.Project45FE10(matrix, new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point[2..]), BinaryPrimitives.ReadInt16LittleEndian(point[4..])), projection);
                projected.Add(indexOffset, vertex);
            }
            return vertex;
        }
        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
            {
                throw new InvalidDataException("Native Thundaga geometry access leaves its actual source.");
            }
            return source.Data.AsSpan(offset, length);
        }
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
    }

    internal static NativeBattleActionMatrix ComposeNodeMatrix(NativeBattleGfAnimationNode node, int ownerScale, ImmutableArray<byte> anchor, NativeBattleActionMatrix view, NativeBattleActionMatrix normalizedBillboard)
    {
        NativeBattleActionMatrix basis = (node.Header & 0x1000) != 0 ? normalizedBillboard : view;
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(basis, NativeBattleGfMatrices.Euler56CE30(node.Vector16));
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, ownerScale, ownerScale, ownerScale);
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, unchecked(BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan()) + node.Vector8.X), unchecked(BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan().Slice(2)) + node.Vector8.Y), unchecked(BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan().Slice(4)) + node.Vector8.Z));
        return matrix with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
    }

    private static void RequireTask(NativeBattleModulePoolTask task)
    {
        if (task.Callback != 0x6D43B0 || task.Payload.IsDefault || task.Payload.Length != 0x884 - 12)
        {
            throw new InvalidDataException("Native Thundaga primary program requires its actual complete task.");
        }
    }

    private static void ValidateContext(ImmutableArray<byte> selector, ImmutableArray<byte> anchor)
    {
        if (selector.IsDefault || selector.Length != 32 || anchor.IsDefault || anchor.Length != 8)
        {
            throw new InvalidDataException("Native Thundaga program requires its actual selector bank and four-WORD anchor.");
        }
    }

    private static NativeBattleGfAnimationWorkspace Workspace(NativeBattleModulePoolTask task) => new(NativeBattleTelemetry.Immutable(1265, NativeBattleTelemetry.Materialize(1261, task.Payload.AsSpan(WorkspaceOffset - 12, WorkspaceLength))));

    private static NativeBattleModulePoolTask Store(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        byte[] payload = NativeBattleTelemetry.Materialize(1262, task.Payload);
        NativeBattleTelemetry.Copy(1267, workspace.Data.AsSpan(), payload.AsSpan(WorkspaceOffset - 12, WorkspaceLength));
        return task with { Payload = NativeBattleTelemetry.Immutable(1266, payload) };
    }
}
