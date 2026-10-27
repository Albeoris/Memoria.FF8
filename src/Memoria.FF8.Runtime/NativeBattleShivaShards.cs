using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleShivaShards
{
    public const int ShardArenaOffset = 0x2E000;
    public const int ShardCount = 170;
    public const int ShardStride = 32;

    public static NativeBattleShivaEarlyTaskFrame Initialize5C1E80(NativeBattleShivaEarlyTaskFrame frame, NativeBattleStaticMeshResource mesh)
    {
        frame.Pool.Validate();
        if (frame.Pool.DescriptorAddress != 0x22BC198 || frame.Pool.BankAddress != 0x22BC1A8 || frame.Pool.Stride != 0x24 || frame.Pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native Shiva shard spawning requires its original child task bank.");
        }
        NativeBattleEffectTaskPool pool = frame.Pool;
        (pool, int slot) = pool.Append(0x5C2260);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0x0C, 0).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, 0).WriteWord(0x1C, 0x1400).WriteWord(0x20, 0x2200);
        frame = frame with { Pool = pool.WriteSlot(slot, task) };
        return Initialize5C1EE0(frame, mesh);
    }

    public static NativeBattleShivaEarlyTaskFrame Initialize5C1EE0(NativeBattleShivaEarlyTaskFrame frame, NativeBattleStaticMeshResource mesh)
    {
        if (mesh.Address != 0xD2F1C4 || mesh.Bytes.IsDefault || mesh.Bytes.Length != 0x12C8 || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()) != 0x340)
        {
            throw new InvalidDataException("Native Shiva shards require the complete original103-vertexD2F1C4 mesh.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva shards require their current physical arena.");
        using NativeBattleEffectArenaWriter writer = new(arena);
        NativeCrtRandomState random = frame.Random;
        int cursor = 0x340;
        int shard = 0;
        for (int group = 0; group < 8; group++)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            cursor += 4;
            if (count != (group == 2 ? 102u : group == 6 ? 68u : 0u))
            {
                throw new InvalidDataException("Native Shiva shard source differs from its102 FT3 and68 GT3 groups.");
            }
            for (int index = 0; index < count; index++)
            {
                int x = 0;
                int y = 0;
                int z = 0;
                for (int corner = 0; corner < 3; corner++)
                {
                    int vertex = 8 + 4 * BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2, 2));
                    if (vertex > 0x340 - 6)
                    {
                        throw new InvalidDataException("Native Shiva shard centroid leaves its actual source vertex bank.");
                    }
                    x += SourceWord(vertex);
                    y += SourceWord(vertex + 2);
                    z += SourceWord(vertex + 4);
                }
                x /= 3;
                y /= 3;
                z /= 3;
                int square = unchecked(x * x + y * y + z * z);
                if (square < 0)
                {
                    throw new NotSupportedException("Native Shiva shard radius exceeds the admitted signed32 square-root input.");
                }
                int distance = (int)Math.Sqrt(square);
                int factor = distance * 6 / 2000;
                random = random.Draw(out ushort sample);
                int firstAngle = (sample & 7) << 4;
                int secondAngle = ((sample >> 3) & 7) << 3;
                if (group == 6)
                {
                    if ((sample & 1) != 0)
                    {
                        firstAngle = -firstAngle;
                    }
                    else
                    {
                        secondAngle = -secondAngle;
                    }
                }
                int address = ShardArenaOffset + shard * ShardStride;
                PutWord(address, x);
                PutWord(address + 2, y);
                PutWord(address + 4, z);
                PutWord(address + 6, factor);
                PutWord(address + 0x0A, 0);
                PutWord(address + 0x0E, 0);
                PutWord(address + 0x12, -20 - sample % 50 * (6 - factor));
                PutWord(address + 0x18, 0);
                PutWord(address + 0x1A, firstAngle);
                PutWord(address + 0x1C, 0);
                PutWord(address + 0x1E, secondAngle);
                cursor += group == 2 ? 20 : 28;
                shard++;
            }
        }
        if (cursor != mesh.Bytes.Length || shard != ShardCount)
        {
            throw new InvalidDataException("Native Shiva shard traversal does not end at its original geometry boundary.");
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() }, Random = random };

        short SourceWord(int address) => BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(address, 2));

        void PutWord(int address, int value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
            writer.Write(address, bytes);
        }
    }
}
