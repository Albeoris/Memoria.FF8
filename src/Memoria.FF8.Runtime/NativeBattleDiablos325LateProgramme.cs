using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325LateProgramme
{
    public static NativeBattleDiablos325ProgrammeDraw Advance654BF8(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        int counter = unchecked((short)task.ReadWord(0xC));
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleDiablos325LateResources tables = frame.Effect.Binding.Diablos325Resources?.LateResources ?? throw new NotSupportedException("Native Diablos late programmes require their original selector tables.");
        int workspaceOffset;
        int workspaceLength;
        uint offset;
        short file;
        NativeBattleVector position;
        ImmutableArray<byte> kinds;
        ImmutableArray<byte> textures;
        int bias;
        byte phase = 0;
        int fade = 0;
        bool loop;
        bool initialize;
        if (counter is >= 268 and <= 327)
        {
            workspaceOffset = 0x7AC;
            workspaceLength = 0x1E8;
            offset = 0x51D40;
            file = 734;
            position = new(90, -9092, unchecked((short)(state.FarZ51DC - 160)));
            kinds = tables.Read(0xFE0FB4, 1).ToArray().ToImmutableArray();
            textures = kinds;
            bias = -64;
            loop = true;
            initialize = counter == 268;
        }
        else if (counter is >= 406 and <= 455)
        {
            workspaceOffset = 0x344;
            workspaceLength = 0x468;
            offset = 0x3B4E4;
            file = 735;
            position = new(unchecked((short)state.CenterX51D8), 0, unchecked((short)state.CenterZ51D4));
            kinds = tables.Read(0xFE0F84, 16).ToArray().ToImmutableArray();
            textures = tables.Read(0xFE0F94, 16).ToArray().ToImmutableArray();
            bias = 0;
            phase = unchecked((byte)((counter - 406) * 4));
            loop = false;
            initialize = counter == 406;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(task));
        }
        return Advance(frame, task, projection, colors, workspaceOffset, workspaceLength, offset, file, position, bias, fade, phase, kinds, textures, initialize, loop);
    }

    public static NativeBattleDiablos325ProgrammeDraw Advance654CE5(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        int counter = unchecked((short)task.ReadWord(0xC));
        if (counter is < 298 or > 327)
        {
            throw new ArgumentOutOfRangeException(nameof(task));
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleDiablos325LateResources tables = frame.Effect.Binding.Diablos325Resources?.LateResources ?? throw new NotSupportedException("Native Diablos late programmes require their original selector tables.");
        return Advance(frame, task, projection, colors, 0x344, 0x3C8, 0x4EF8C, 734, new(74, -10442, unchecked((short)(state.FarZ51DC - 242))), 0, 0, 0, tables.Read(0xFE0FA4, 8).ToArray().ToImmutableArray(), tables.Read(0xFE0FAC, 8).ToArray().ToImmutableArray(), counter == 298, false);
    }

    private static NativeBattleDiablos325ProgrammeDraw Advance(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, int workspaceOffset, int workspaceLength, uint offset, short fileId, NativeBattleVector position, int bias, int fade, byte phase, ImmutableArray<byte> primitiveKinds, ImmutableArray<byte> textureKinds, bool initialize, bool loop)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleGfAnimationSource source = ReadSource(frame, fileId, offset);
        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(workspaceOffset - 12, workspaceLength));
        if (initialize)
        {
            workspace = workspace.Initialize7016B0(source, workspaceLength);
        }
        NativeBattleEffectMemory memory = frame.Memory;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, unchecked((int)state.Pause5180), (current, node) =>
        {
            NativeBattleDiablos325NodeDraw drawn = NativeBattleDiablos325Programme.Emit656B70(memory, source, node, projection, colors, position, bias, fade, primitiveKinds, textureKinds, texturePhase: phase);
            memory = drawn.Memory;
            attempts.AddRange(drawn.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
            allocated = checked(allocated + drawn.Packets.AllocationBytes);
            return current;
        });
        workspace = advanced.Workspace;
        if (loop && advanced.Remaining == 0)
        {
            workspace = workspace.Initialize7016B0(source, workspaceLength);
        }
        byte[] payload = task.Payload.ToArray();
        workspace.Data.AsSpan().CopyTo(payload.AsSpan(workspaceOffset - 12, workspaceLength));
        return new(memory, task with { Payload = payload.ToImmutableArray() }, new(attempts.ToImmutable(), allocated));
    }

    internal static NativeBattleGfAnimationSource ReadSource(NativeBattleDiablos325Frame frame, short fileId, uint offset)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleEffectFileAsset file = frame.Effect.Binding.Diablos325Resources?.Files.SingleOrDefault(item => item.FileId == fileId) ?? throw new NotSupportedException("Native Diablos programme requires its original streamed resource owner.");
        uint address = checked(state.ModelBuffer5208 + offset);
        uint end = checked(state.ModelBuffer5208 + 0x3505C + (uint)file.Data.Length);
        if (address >= end)
        {
            throw new InvalidDataException("Native Diablos programme leaves its current original streamed file.");
        }
        int length = checked((int)(end - address));
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos programme requires its current mutable resource bytes.");
        return new(address, arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length).ToImmutableArray());
    }
}
