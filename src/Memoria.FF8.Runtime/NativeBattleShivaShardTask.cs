using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaShardGteOperation(NativeBattleActionMatrix? Matrix, uint? FarColor);
public sealed record NativeBattleShivaShardTaskInputs(NativeBattleActionMatrix ViewWorld, NativeBattleStaticMeshResource Mesh, NativeBattleCameraProjection Projection, short Zsf3, NativeBattleSummonColors? Colors, Action<NativeBattleShivaShardGteOperation>? ObserveGte = null, Action<ushort>? ObserveDepth = null, Action<int>? ObserveDepthCue = null);
public sealed record NativeBattleShivaShardTaskStep(NativeBattleShivaEarlyTaskFrame Frame, NativeBattleGfGeometryBatch Packets, int Result);

public static class NativeBattleShivaShardTask
{
    public static NativeBattleShivaShardTaskStep Advance5C2260(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleShivaShardTaskInputs inputs)
    {
        frame.Pool.Validate();
        if (frame.Pool.DescriptorAddress != 0x22BC198 || frame.Pool.BankAddress != 0x22BC1A8 || frame.Pool.Stride != 0x24 || frame.Pool.Slots.Length != 100 || (uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot] is not { Callback: 0x5C2260 } task || (task.Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Shiva shard worker requires its allocated5C2260 task.");
        }
        NativeBattleStaticMeshResource mesh = inputs.Mesh;
        if (mesh.Address != 0xD2F1C4 || mesh.Bytes.IsDefault || mesh.Bytes.Length != 0x12C8 || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()) != 0x340)
        {
            throw new InvalidDataException("Native Shiva shard worker requires its complete original source mesh.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva shard worker requires its retained170 fragment records.");
        using NativeBattleEffectArenaTransaction? transaction = arena.Transaction is null ? new(arena) : null;
        arena = transaction?.View ?? arena;
        int counter = unchecked((short)task.ReadWord(0x0C));
        bool animated = counter >= 18;
        int phase = animated ? counter - 18 : counter;
        bool paused = (graphicsFlags & 0x201) != 0;
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x20)), unchecked((short)task.ReadWord(0x1C))) with { X = unchecked((short)task.ReadWord(0x10)), Y = unchecked((short)task.ReadWord(0x12)), Z = unchecked((short)task.ReadWord(0x14)) };
        NativeBattleActionMatrix basis = NativeBattlePoseMatrices.Compose56C2F0(inputs.ViewWorld, local);
        Publish(new(animated ? null : basis, 0));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int cursor = 0x340;
        int shard = 0;
        for (int group = 0; group < 8; group++)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            cursor += 4;
            if (count != (group == 2 ? 102u : group == 6 ? 68u : 0u))
            {
                throw new InvalidDataException("Native Shiva shard worker requires exactly102 FT3 and68 GT3 faces.");
            }
            for (int index = 0; index < count; index++)
            {
                int length = group == 2 ? 20 : 28;
                int faceOffset = cursor;
                cursor += length;
                int record = 0x2E000 + shard++ * 32;
                int lifetime = animated ? Word(record + 0x0E) : 0;
                if (animated && lifetime < 0 || !animated && Word(record + 6) >= phase && phase < 6)
                {
                    continue;
                }
                NativeBattleVector[] vertices = new NativeBattleVector[3];
                for (int corner = 0; corner < 3; corner++)
                {
                    int source = 8 + 4 * BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(faceOffset + 4 + corner * 2, 2));
                    if (source > 0x340 - 6)
                    {
                        throw new InvalidDataException("Native Shiva shard projection leaves its original vertex bank.");
                    }
                    vertices[corner] = new(SourceWord(source), SourceWord(source + 2), SourceWord(source + 4));
                }
                NativeBattleActionMatrix matrix = basis;
                int cue = 0;
                if (animated && 2 * Word(record + 6) < phase)
                {
                    short cx = Word(record);
                    short cy = Word(record + 2);
                    short cz = Word(record + 4);
                    for (int corner = 0; corner < vertices.Length; corner++)
                    {
                        NativeBattleVector original = vertices[corner];
                        vertices[corner] = new(unchecked((short)(original.X - cx)), unchecked((short)(original.Y - cy)), unchecked((short)(original.Z - cz)));
                    }
                    int vertical = Word(record + 0x0A);
                    int scale = (32 - lifetime) << 7;
                    cue = lifetime == 0 ? -4096 : lifetime * 85;
                    if (!paused)
                    {
                        int velocity = Word(record + 0x12);
                        PutWord(record + 0x0A, vertical + velocity);
                        PutWord(record + 0x12, velocity - (velocity >> 3));
                        PutWord(record + 0x1C, Word(record + 0x1C) + Word(record + 0x1E));
                        PutWord(record + 0x18, Word(record + 0x18) + Word(record + 0x1A));
                        int updatedLife = unchecked((short)(lifetime + 1));
                        PutWord(record + 0x0E, updatedLife >= 48 ? -1 : updatedLife);
                    }
                    NativeBattleActionMatrix fragment = NativeBattleGfMatrices.Euler56CD50(new(Word(record + 0x18), 0, Word(record + 0x1C)));
                    fragment = NativeBattlePoseMatrices.Scale56BEF0(fragment, scale, scale, scale) with { X = cx, Y = cy + vertical, Z = cz };
                    matrix = NativeBattlePoseMatrices.Compose56C2F0(basis, fragment);
                }
                if (animated)
                {
                    Publish(new(matrix, null));
                }
                NativeBattleGfGeometryContext context = new(0x33, 0, cue, null, null, 0, 17, 2);
                NativeBattleGfGeometryAttempt attempt = NativeBattleShivaShardPackets.EmitTriangle5C2410(mesh.Bytes.AsSpan().Slice(faceOffset, length), corner => NativeBattleModuleProjection.Project45FE10(matrix, vertices[corner], inputs.Projection), group == 6, animated, context, inputs.Zsf3, inputs.Colors, allocated, inputs.ObserveDepth, inputs.ObserveDepthCue);
                attempt = NativeBattleShivaVisualIdentity.Capture(attempt, NativeBattlePilotVisualLifetimes.Identity(185, task, faceOffset), vertices, matrix, inputs.Projection);
                attempts.Add(attempt);
                if (attempt.Primitive is not null)
                {
                    allocated += group == 2 ? 32 : 40;
                }
            }
        }
        if (cursor != mesh.Bytes.Length || shard != 170)
        {
            throw new InvalidDataException("Native Shiva shard worker does not end at its actual face/record boundary.");
        }
        frame = frame with { Memory = frame.Memory with { Arena = transaction?.Freeze() ?? arena } };
        int result = 0;
        if (!paused)
        {
            task = frame.Pool.Slots[slot].WriteWord(0x0C, unchecked((ushort)(frame.Pool.Slots[slot].ReadWord(0x0C) + 1)));
            frame = frame with { Pool = frame.Pool.WriteSlot(slot, task) };
            result = unchecked((short)task.ReadWord(0x0C)) >= 18 && allocated == 0 ? 2 : 0;
        }
        return new(frame, new(attempts.ToImmutable(), allocated), result);

        short Word(int address) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(address, 2));
        short SourceWord(int address) => BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(address, 2));

        void Publish(NativeBattleShivaShardGteOperation operation)
        {
            inputs.ObserveGte?.Invoke(operation);
        }

        void PutWord(int address, int value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
            arena = arena.Write(address, bytes);
        }
    }
}
