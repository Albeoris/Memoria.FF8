using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97ProgramResources(NativeBattleGfAnimationSource ProgramSource);
public sealed record NativeBattleChoco97ProgramDraw(NativeBattleModulePoolTask Task, int Remaining, NativeBattleGfGeometryBatch Packets, ImmutableArray<byte> MorphScratch);
public sealed record NativeBattleChoco97NodeDraw(NativeBattleGfGeometryBatch Packets, ImmutableArray<byte> MorphScratch);
internal sealed record NativeBattleChocoProgramProfile(int TaskBytes, int WorkspaceBytes, int ScratchBytes, ImmutableArray<int> ClearOperands)
{
    internal static NativeBattleChocoProgramProfile Choco97 { get; } = new(0x540, 0x4AC, 0x3C0, [0x4AC, 0x44C, 0x170, 0x290]);
    internal static NativeBattleChocoProgramProfile Choco98 { get; } = new(0x864, 0x7D0, 0x168, [0x4AC, 0x44C, 0x170, 0x494, 0x7D0]);
    internal static NativeBattleChocoProgramProfile Choco99 { get; } = new(0x548, 0x4B4, 0x760, [0x4AC, 0x44C, 0x170, 0xB0, 0x2C, 0x194, 0x30, 0x1E8]);
}

public static partial class NativeBattleChoco97ProgramDrawing
{
    public static NativeBattleModulePoolTask InitializeWorkspace72A6C0(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8)
    {
        return InitializeProfiled(task, source, actorRaw8, NativeBattleChocoProgramProfile.Choco97);
    }

    internal static NativeBattleModulePoolTask InitializeProfiled(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8, NativeBattleChocoProgramProfile profile)
    {
        if (actorRaw8.IsDefault || actorRaw8.Length != 8)
        {
            throw new InvalidDataException("Native Choco program requires its actual workspace size and raw actor origin.");
        }
        task = InitializeWorkspaceProfiled(task, source, profile);
        return task.WriteDword(0x1C, BinaryPrimitives.ReadUInt32LittleEndian(actorRaw8.AsSpan())).WriteDword(0x20, BinaryPrimitives.ReadUInt32LittleEndian(actorRaw8.AsSpan().Slice(4))).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
    }

    internal static NativeBattleModulePoolTask InitializeWorkspaceProfiled(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, NativeBattleChocoProgramProfile profile)
    {
        RequireTask(task, source, profile);
        int clearBytes = unchecked((int)task.ReadDword(0x78));
        if (!profile.ClearOperands.Contains(clearBytes))
        {
            throw new InvalidDataException("Native Choco program requires its actual workspace clear operand.");
        }
        return Store(task, Workspace(task, profile).Initialize7016B0(source, clearBytes), profile);
    }

    public static NativeBattleChoco97ProgramDraw Advance72A720(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozen)
    {
        return AdvanceProfiled(task, source, morphScratch, view, projection, colors, frozen, NativeBattleChocoProgramProfile.Choco97);
    }

    internal static NativeBattleChoco97ProgramDraw AdvanceProfiled(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozen, NativeBattleChocoProgramProfile profile)
    {
        RequireTask(task, source, profile);
        RequireScratch(morphScratch, profile);
        return AdvanceWithExplicitContext(task, source, morphScratch, NativeBattleSearchProgramDrawing.ComposeContext758D70(task, view), projection, colors, frozen, profile);
    }

    internal static NativeBattleChoco97ProgramDraw AdvanceWithExplicitContext(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozen, NativeBattleChocoProgramProfile profile)
    {
        RequireTask(task, source, profile);
        RequireScratch(morphScratch, profile);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleGfAnimationAdvance advanced = Workspace(task, profile).Advance701970(source, unchecked((int)frozen), (current, node) =>
        {
            NativeBattleChoco97NodeDraw draw = NodeProfiled(source, node, morphScratch, context, projection, colors, profile);
            morphScratch = draw.MorphScratch;
            attempts.AddRange(draw.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
            allocated = checked(allocated + draw.Packets.AllocationBytes);
            return current;
        });
        return new(Store(task, advanced.Workspace, profile), advanced.Remaining, new(attempts.ToImmutable(), allocated), morphScratch);
    }

    public static NativeBattleChoco97NodeDraw Node72A840(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        return NodeProfiled(source, node, morphScratch, context, projection, colors, NativeBattleChocoProgramProfile.Choco97);
    }

    internal static NativeBattleChoco97NodeDraw NodeProfiled(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleChocoProgramProfile profile)
    {
        if (node.Data.IsDefault || node.Data.Length != 44)
        {
            throw new InvalidDataException("Native Choco program requires its complete decoded node.");
        }
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(new([], 0), morphScratch);
        }
        int index = unchecked((short)node.GeometryIndex);
        if (index < 0 || index >= ReadInt(source.Data, 0))
        {
            throw new InvalidDataException("Native Choco node selects an absent geometry.");
        }
        int geometry = ReadInt(source.Data, checked(8 + index * 4));
        int vertices = ReadInt(source.Data, checked(geometry + 4));
        int shapes = ReadInt(source.Data, checked(geometry + 8));
        int first = node.Word40;
        int second = node.Word42;
        if (vertices < 1 || vertices > source.Data.Length / 8 || shapes < 1 || first < 0 || first >= shapes || second < 0 || second >= shapes)
        {
            throw new InvalidDataException("Native Choco geometry requires its actual shaped vertex bank.");
        }
        int bytes = checked(vertices * 8);
        ImmutableArray<byte> selected;
        if (first == second || node.Scalar38 is 0 or 4096)
        {
            int shape = first == second || node.Scalar38 == 0 ? first : second;
            selected = Read(source.Data, checked(geometry + 12 + shape * bytes), bytes).ToArray().ToImmutableArray();
        }
        else
        {
            RequireScratch(morphScratch, profile);
            if (bytes > morphScratch.Length)
            {
                throw new InvalidDataException("Native Choco morph leaves its explicitly retained scratch owner.");
            }
            morphScratch = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, first, second, node.Scalar38, morphScratch);
            selected = morphScratch.AsSpan(0, bytes).ToArray().ToImmutableArray();
        }
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        bool cue = node.Scalar36 != 0;
        if (cue)
        {
            flags |= 0xC0;
        }
        NativeBattleModuleMeshContext operands = new(flags, cue ? node.Color32 : null, cue ? node.Scalar36 : null, null, null, null, 17, 2);
        return new(NativeBattleModulePackets.EmitBatch572200(source.Data, geometry, selected, ComposeNodeMatrix(node, context), projection, operands, colors), morphScratch);
    }

    public static NativeBattleActionMatrix ComposeNodeMatrix(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context)
    {
        NativeBattleActionMatrix matrix = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if ((node.Header & 0x200) == 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(context, node.Vector8);
            matrix = NativeBattleGfMatrices.Multiply56C090(context, matrix);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)) : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
    }

    private static void RequireTask(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, NativeBattleChocoProgramProfile profile)
    {
        if (task.Payload.IsDefault || task.Payload.Length != profile.TaskBytes - 12 || task.ReadDword(0x74) != source.Address)
        {
            throw new InvalidDataException("Native Choco program requires its complete task and retained source pointer.");
        }
    }

    private static void RequireScratch(ImmutableArray<byte> scratch, NativeBattleChocoProgramProfile profile)
    {
        if (scratch.IsDefault || scratch.Length != profile.ScratchBytes)
        {
            throw new NotSupportedException("Native Choco program requires its explicitly retained morph scratch owner.");
        }
    }

    private static NativeBattleGfAnimationWorkspace Workspace(NativeBattleModulePoolTask task, NativeBattleChocoProgramProfile profile) => new(task.Payload.AsSpan().Slice(0x94 - 12, profile.WorkspaceBytes).ToArray().ToImmutableArray());

    private static NativeBattleModulePoolTask Store(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace, NativeBattleChocoProgramProfile profile)
    {
        if (workspace.Data.Length != profile.WorkspaceBytes)
        {
            throw new InvalidDataException("Native Choco workspace leaves its caller's physical capacity.");
        }
        byte[] bytes = task.Payload.ToArray();
        workspace.Data.AsSpan().CopyTo(bytes.AsSpan(0x94 - 12, profile.WorkspaceBytes));
        return task with { Payload = bytes.ToImmutableArray() };
    }

    private static int ReadInt(ImmutableArray<byte> source, int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(source, offset, 4));

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> source, int offset, int length)
    {
        if (source.IsDefault || offset < 0 || length < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Native Choco geometry access leaves its retained source.");
        }
        return source.AsSpan().Slice(offset, length);
    }
}
