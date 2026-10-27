using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278ProgrammeNode(NativeBattleEffectMemory Memory, NativeBattleGfGeometryBatch Packets, bool Active, uint? FarColor);
public sealed record NativeBattleCarbuncle278ProgrammeDraw(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets, int Remaining);

/// <summary>The creature's caller-owned programme, private mesh packets and shared projection scratch.</summary>
public static class NativeBattleCarbuncle278Programme
{
    public static NativeBattleCarbuncle278ProgrammeDraw Advance681949(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, NativeBattleEffectOwnedModelResource creature, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        int counter = unchecked((short)task.ReadWord(0xC));
        bool second = source.Address == 0x10C5C8C;
        if (task.PayloadLength != 0x5E0 - 12 || (second ? counter is < 145 or >= 177 : source.Address != 0x10C2608 || counter is < 110 or > 145))
        {
            throw new NotSupportedException("Native Carbuncle programme requires its current source and actual creature workspace window.");
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Carbuncle programme requires its current view matrix.");
        NativeBattleActionMatrix context;
        if (second)
        {
            context = NativeBattleGfMatrices.Compose56C2F0(view, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096));
        }
        else
        {
            NativeBattleVector point = NativeBattleCarbuncle278Anchors.ReadVertex682270(task, memory.CarbuncleExecutableMemory ?? throw new NotSupportedException("Native Carbuncle programme requires its current creature skeleton."), creature, 0, 176);
            context = PrepareContext681949(point, view);
        }
        return AdvanceWorkspace(memory, task, source, context, projection, colors, 0xCC, second ? 0x3D8 : 0x514, counter == (second ? 145 : 110), second ? -512 : -256);
    }

    public static NativeBattleCarbuncle278ProgrammeDraw AdvanceRecipient6822F0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (source.Address != 0x10C1CD8 || task.PayloadLength != 0x10C - 12 || unchecked((short)task.ReadWord(0xC)) is < 0 or >= 21)
        {
            throw new NotSupportedException("Native Carbuncle recipient programme requires its current source and actual workspace window.");
        }
        NativeBattleActionMatrix local = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], unchecked((short)task.ReadWord(0x5C)), unchecked((short)task.ReadWord(0x5E)), unchecked((short)task.ReadWord(0x60)), 4096);
        NativeBattleActionMatrix context = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Native Carbuncle recipient programme requires its current view matrix."), local);
        return AdvanceWorkspace(memory, task, source, context, projection, colors, 0x64, 0xA8, false, 0);
    }

    private static NativeBattleCarbuncle278ProgrammeDraw AdvanceWorkspace(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, NativeBattleActionMatrix context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, int offset, int length, bool initialize, int depthBias)
    {
        NativeBattleGfAnimationWorkspace workspace = new(task.ReadRawPayloadSpan().Slice(offset - 12, length).ToArray().ToImmutableArray());
        if (initialize)
        {
            workspace = workspace.Initialize7016B0(source, length);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(memory);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, unchecked((int)state.Pause8114), (current, node) =>
        {
            NativeBattleCarbuncle278ProgrammeNode drawn = Node681FD0(memory, source, node, context, projection, colors, depthBias);
            memory = drawn.Memory;
            if (drawn.Active)
            {
                NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Carbuncle programme requires its retained far-color controls.");
                NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
                registers = drawn.FarColor is uint color ? registers.PublishModelColor(color) : registers with { FarRed = null, FarGreen = null, FarBlue = null };
                memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers } };
            }
            attempts.AddRange(drawn.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
            allocated = checked(allocated + drawn.Packets.AllocationBytes);
            return current;
        });
        task = task.WriteRetainedBytes(12 + (offset - 12), advanced.Workspace.Data.AsSpan());
        return new(memory, task, new(attempts.ToImmutable(), allocated), advanced.Remaining);
    }

    public static NativeBattleActionMatrix PrepareContext681949(NativeBattleVector point, NativeBattleActionMatrix view)
    {
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(2560));
        short sine = unchecked((short)NativeBattleCameraMath.Sine(2560));
        NativeBattleActionMatrix local = new([4096, 0, 0, 0, cosine, sine, 0, unchecked((short)-sine), cosine], point.X, point.Y, point.Z, 4096);
        return NativeBattleGfMatrices.Compose56C2F0(view, local);
    }

    public static NativeBattleCarbuncle278ProgrammeNode Node681FD0(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, int depthBias = -256)
    {
        if (node.Data.IsDefault || node.Data.Length != 44)
        {
            throw new InvalidDataException("Native Carbuncle programme requires its complete decoded callback record.");
        }
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(memory, new([], 0), false, null);
        }
        int index = unchecked((short)node.GeometryIndex);
        if (index < 0 || index >= Int(0))
        {
            throw new InvalidDataException("Native Carbuncle programme selects an absent original geometry.");
        }
        int geometry = Int(checked(8 + index * 4));
        int count = Int(checked(geometry + 4));
        if (count < 0 || count > source.Data.Length / 8)
        {
            throw new InvalidDataException("Native Carbuncle programme vertex extent leaves its original bounded source.");
        }
        int selected = 0;
        ImmutableArray<NativeBattleVector> morph = [];
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = checked(geometry + 12 + shape * count * 8);
        }
        else
        {
            morph = NativeBattleGfMorphGeometry.MorphVertices701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38);
            using NativeBattleEffectArenaWriter arena = new(memory.Arena ?? throw new NotSupportedException("Native Carbuncle programme requires its actual shared morph scratch."));
            for (int vertex = 0; vertex < morph.Length; vertex++)
            {
                NativeBattleVector position = morph[vertex];
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, position.X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), position.Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), position.Z);
                arena.Write(checked(0x2E84 + 8 * vertex), xyz);
            }
            memory = memory with { Arena = arena.Freeze() };
        }
        bool cue = node.Scalar36 != 0;
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, default, Compose681FD0(node, context), projection, new(cue ? 0x20F0u : 0x2030u, cue ? node.Color32 : 0, node.Scalar36, null, null, depthBias, 17, 2), colors, offset =>
        {
            if (offset < 0 || (offset & 7) != 0 || offset / 8 >= count)
            {
                throw new InvalidDataException("Native Carbuncle programme selected vertex leaves its actual shape bank.");
            }
            if (!morph.IsEmpty)
            {
                return morph[offset / 8];
            }
            ReadOnlySpan<byte> vertex = Read(checked(selected + offset), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(4)));
        });
        return new(memory, batch, true, cue ? node.Color32 : null);

        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
            {
                throw new InvalidDataException("Native Carbuncle programme operand leaves its original bounded source.");
            }
            return source.Data.AsSpan().Slice(offset, length);
        }
    }

    public static NativeBattleActionMatrix Compose681FD0(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context)
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
}
