using System.IO.Compression;
using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record SceneEnemyReward(short EnemyId, ReadOnlyMemory<byte> Name, uint Experience, uint Gil, ushort AbilityPoints, IReadOnlyList<SceneItemSlot> Items, ReadOnlyMemory<byte> Raw);
public sealed record SceneItemSlot(short ItemId, byte Rate);
public sealed record SceneEnemyInstance(short EnemyId, Vector3 Position, short Row, ushort CoverFlags, byte Flags);
public sealed record SceneFormation(int FormationId, short LocationId, short NextBattleId, short EscapeCounter, ushort BattleFlags, byte Layout, byte InitialCamera, IReadOnlyList<ushort> ArenaSuccessors, IReadOnlyList<SceneEnemyInstance> Enemies, IReadOnlyList<SceneEnemyReward> EnemyRewards, ReadOnlyMemory<byte> RawScene);

public static class SceneArchive
{
    public static IReadOnlyList<SceneFormation> Read(ReadOnlySpan<byte> source, int maximumScenes = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumScenes);
        if (source.Length % 0x2000 != 0)
        {
            throw new InvalidDataException("FF7 scene archive requires complete 8192-byte blocks.");
        }
        List<SceneFormation> formations = new();
        int sceneIndex = 0;
        for (int block = 0; block < source.Length; block += 0x2000)
        {
            ReadOnlySpan<byte> data = source.Slice(block, 0x2000);
            BinaryCursor directory = new(data);
            int[] offsets = new int[16];
            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = directory.I32();
            }
            int previous = 15;
            bool ended = false;
            for (int i = 0; i < offsets.Length; i++)
            {
                if (offsets[i] == -1)
                {
                    ended = true;
                    continue;
                }
                if (ended || offsets[i] <= previous || offsets[i] >= 2048)
                {
                    throw new InvalidDataException("FF7 scene block has invalid ordered word offsets.");
                }
                previous = offsets[i];
                int start = offsets[i] * 4;
                int end = i == 15 || offsets[i + 1] == -1 ? 0x2000 : offsets[i + 1] * 4;
                if (end <= start || end > 0x2000)
                {
                    throw new InvalidDataException("FF7 scene compressed member exceeds its block.");
                }
                if (sceneIndex >= maximumScenes)
                {
                    throw new InvalidDataException("FF7 scene count exceeds its configured limit.");
                }
                using MemoryStream input = new(data.Slice(start, end - start).ToArray(), false);
                using GZipStream gzip = new(input, CompressionMode.Decompress);
                byte[] decoded = new byte[0x2001];
                int length = 0;
                while (length < decoded.Length)
                {
                    int read = gzip.Read(decoded.AsSpan(length));
                    if (read == 0)
                    {
                        break;
                    }
                    length += read;
                }
                if (length < 0x4C0 || length > 0x2000)
                {
                    throw new InvalidDataException("FF7 scene decompressed length is unsupported.");
                }
                AddScene(formations, decoded.AsSpan(0, length), sceneIndex++);
            }
        }
        return formations.AsReadOnly();
    }

    private static void AddScene(List<SceneFormation> destination, ReadOnlySpan<byte> source, int sceneIndex)
    {
        byte[] raw = source.ToArray();
        BinaryCursor cursor = new(source);
        short[] ids = [cursor.I16(), cursor.I16(), cursor.I16()];
        SceneEnemyReward[] rewards = new SceneEnemyReward[3];
        for (int i = 0; i < rewards.Length; i++)
        {
            int recordOffset = 0x298 + i * 184;
            ReadOnlySpan<byte> record = source.Slice(recordOffset, 184);
            BinaryCursor enemy = new(record);
            byte[] name = enemy.Take(32).ToArray();
            enemy.Seek(136);
            byte[] rates = enemy.Take(4).ToArray();
            SceneItemSlot[] items = new SceneItemSlot[4];
            for (int item = 0; item < items.Length; item++)
            {
                items[item] = new SceneItemSlot(enemy.I16(), rates[item]);
            }
            enemy.Seek(158);
            ushort ap = enemy.U16();
            enemy.Seek(168);
            uint exp = enemy.U32();
            uint gil = enemy.U32();
            rewards[i] = new SceneEnemyReward(ids[i], name, exp, gil, ap, Array.AsReadOnly(items), raw.AsMemory(recordOffset, 184));
        }
        for (int formation = 0; formation < 4; formation++)
        {
            cursor.Seek(8 + formation * 20);
            short location = cursor.I16();
            short next = cursor.I16();
            short escape = cursor.I16();
            cursor.Skip(2);
            ushort[] arena = [cursor.U16(), cursor.U16(), cursor.U16(), cursor.U16()];
            ushort flags = cursor.U16();
            byte layout = cursor.U8();
            byte camera = cursor.U8();
            cursor.Seek(0x118 + formation * 96);
            SceneEnemyInstance[] enemies = new SceneEnemyInstance[6];
            for (int slot = 0; slot < enemies.Length; slot++)
            {
                short id = cursor.I16();
                Vector3 position = cursor.Vector16();
                short row = cursor.I16();
                ushort cover = cursor.U16();
                cursor.Skip(3);
                byte enemyFlags = cursor.U8();
                if (id != -1 && !ids.Contains(id))
                {
                    throw new InvalidDataException($"FF7 formation {(sceneIndex << 2) + formation} references absent enemy {id}.");
                }
                enemies[slot] = new SceneEnemyInstance(id, position, row, cover, enemyFlags);
            }
            destination.Add(new SceneFormation((sceneIndex << 2) + formation, location, next, escape, flags, layout, camera, Array.AsReadOnly(arena), Array.AsReadOnly(enemies), Array.AsReadOnly(rewards), raw));
        }
    }
}
