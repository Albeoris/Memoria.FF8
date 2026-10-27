using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMoogle96ProgramDraw(NativeBattleModulePoolTask Task, int Remaining, NativeBattleGfGeometryBatch Packets);

/// <summary>734450's retained program and734580's COMMON mesh submissions over the caller's mutable source.</summary>
public static class NativeBattleMoogle96ProgramDrawing
{
    private const int WorkspaceBytes = 0x488;
    private static readonly int[] FaceLengths = [12, 12, 20, 24, 20, 24, 28, 36];

    public static NativeBattleModulePoolTask InitializeWorkspace734400(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> dogActorRaw8)
    {
        RequireTask(task, source);
        if (task.ReadDword(0x78) != WorkspaceBytes || dogActorRaw8.IsDefault || dogActorRaw8.Length != 8)
        {
            throw new InvalidDataException("Native Moogle program requires its exact488-byte workspace and raw eight-byte Dog origin.");
        }
        task = Store(task, Workspace(task).Initialize7016B0(source, WorkspaceBytes));
        return task.WriteDword(0x1C, BinaryPrimitives.ReadUInt32LittleEndian(dogActorRaw8.AsSpan())).WriteDword(0x20, BinaryPrimitives.ReadUInt32LittleEndian(dogActorRaw8.AsSpan().Slice(4))).WriteWord(0x1E, 0).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
    }

    public static NativeBattleMoogle96ProgramDraw Advance734450(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, int frozen)
    {
        RequireTask(task, source);
        NativeBattleActionMatrix context = NativeBattleSearchProgramDrawing.ComposeContext758D70(task, view);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleGfAnimationAdvance advanced = Workspace(task).Advance701970(source, frozen, (current, node) =>
        {
            NativeBattleGfGeometryBatch batch = Node734580(source, node, context, projection, colors);
            attempts.AddRange(batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
            allocated = checked(allocated + batch.AllocationBytes);
            return current;
        });
        return new(Store(task, advanced.Workspace), advanced.Remaining, new(attempts.ToImmutable(), allocated));
    }

    public static NativeBattleGfGeometryBatch Node734580(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (node.Data.IsDefault || node.Data.Length != 44)
        {
            throw new InvalidDataException("Native Moogle requires its complete decoded44-byte node.");
        }
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new([], 0);
        }
        if (node.Word40 != 0 || node.Word42 != 0)
        {
            throw new NotSupportedException("Native Moogle's original program requires its single shape; partial morph has no supplied scratch owner.");
        }
        int index = unchecked((short)node.GeometryIndex);
        int count = ReadInt(source.Data, 0);
        if (index < 0 || index >= count)
        {
            throw new InvalidDataException("Native Moogle node selects an absent geometry.");
        }
        int geometry = ReadInt(source.Data, checked(8 + index * 4));
        int vertices = ReadInt(source.Data, checked(geometry + 4));
        if (vertices < 1 || vertices > 160 || ReadInt(source.Data, checked(geometry + 8)) != 1 || ReadInt(source.Data, geometry) != 12 + vertices * 8)
        {
            throw new NotSupportedException("Native Moogle requires its complete single-shape geometry and bounded vertex owner.");
        }
        ImmutableArray<byte> selected = Read(source.Data, checked(geometry + 12), checked(vertices * 8)).ToArray().ToImmutableArray();
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if ((node.Header & 0x2000) != 0)
        {
            flags |= 0xC;
        }
        bool cue = node.Scalar36 != 0;
        if (cue)
        {
            flags |= 0xC0;
        }
        NativeBattleModuleMeshContext operands = new(flags, cue ? node.Color32 : null, node.Scalar36, null, null, null, 17, 2);
        return NativeBattleModulePackets.EmitBatch572200(source.Data, geometry, selected, ComposeNodeMatrix(node, context), projection, operands, colors);
    }

    public static NativeBattleActionMatrix ComposeNodeMatrix(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context)
    {
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(context, node.Vector8);
        NativeBattleActionMatrix local = (node.Header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(context, local);
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix diagonal = new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096);
                matrix = NativeBattleGfMatrices.Multiply56C090(matrix, diagonal);
            }
            else
            {
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
            }
        }
        return matrix with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
    }

    public static NativeBattleGfAnimationSource MutateUv7340A0(NativeBattleGfAnimationSource source)
    {
        if (ReadInt(source.Data, 0) < 21)
        {
            throw new InvalidDataException("Native Moogle UV motion requires all five original geometry selectors.");
        }
        byte[] bytes = source.Data.ToArray();
        foreach (int geometryIndex in new[] { 13, 14, 15, 19, 20 })
        {
            int geometry = ReadInt(source.Data, checked(8 + geometryIndex * 4));
            int cursor = checked(geometry + ReadInt(source.Data, geometry));
            for (int group = 0; group < FaceLengths.Length; group++)
            {
                int count = ReadInt(source.Data, cursor);
                cursor = checked(cursor + 4);
                if (count < 0)
                {
                    throw new InvalidDataException("Native Moogle UV motion requires a nonnegative original face count.");
                }
                int length = checked(count * FaceLengths[group]);
                _ = Read(source.Data, cursor, length);
                if (group is 2 or 3 or 6 or 7)
                {
                    int[] corners = (group & 1) == 0 ? [11, 13, 17] : [13, 17, 21, 23];
                    for (int face = 0; face < count; face++)
                    {
                        int start = checked(cursor + face * FaceLengths[group]);
                        bool wraps = corners.Any(corner => bytes[start + corner] + 2 > 255);
                        foreach (int corner in corners)
                        {
                            bytes[start + corner] = unchecked((byte)(bytes[start + corner] + 2 - (wraps ? 128 : 0)));
                        }
                    }
                }
                cursor = checked(cursor + length);
            }
        }
        return source with { Data = bytes.ToImmutableArray() };
    }

    private static void RequireTask(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source)
    {
        if (task.PayloadLength != 0x51C - 12 || task.ReadDword(0x74) != source.Address)
        {
            throw new InvalidDataException("Native Moogle program requires its complete51C-byte task and retained source pointer.");
        }
    }

    private static NativeBattleGfAnimationWorkspace Workspace(NativeBattleModulePoolTask task) => NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x94 - 12), WorkspaceBytes);

    private static NativeBattleModulePoolTask Store(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        return workspace.CopyTo(task, 12 + (0x94 - 12), retainUnknown: true);
    }

    private static int ReadInt(ImmutableArray<byte> source, int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(source, offset, 4));

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> source, int offset, int length)
    {
        if (source.IsDefault || offset < 0 || length < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Native Moogle geometry read leaves its retained source owner.");
        }
        return source.AsSpan().Slice(offset, length);
    }
}
