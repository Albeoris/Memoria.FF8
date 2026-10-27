using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278MorphTests
{
    [Fact]
    public void Morph_keeps_separate_product_rounding_and_leaves_destination_padding_unknown()
    {
        byte[] first = new byte[24];
        byte[] second = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(first.AsSpan(4), 2);
        short[] a = [-3, 1, 5, -100, 200, -300];
        short[] b = [2, -4, 8, 300, -200, 100];
        for (int index = 0; index < 2; index++)
        {
            for (int component = 0; component < 3; component++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(first.AsSpan(8 + index * 8 + component * 2), a[index * 3 + component]);
                BinaryPrimitives.WriteInt16LittleEndian(second.AsSpan(index * 8 + component * 2), b[index * 3 + component]);
            }
        }
        NativeBattleEffectMemory source = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0x2E84, 16) };
        NativeBattleStaticMeshResource mesh = new(0, 0x10C0D08, first.ToImmutableArray());
        NativeBattleEffectMemory result = NativeBattleCarbuncle278Morph.Morph683CA0(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(source)), mesh, second.ToImmutableArray(), 2048);
        byte[] current = result.Arena!.Read(0x2E84, 6);
        Assert.Equal((short)-1, BinaryPrimitives.ReadInt16LittleEndian(current));
        Assert.Equal((short)-2, BinaryPrimitives.ReadInt16LittleEndian(current.AsSpan(2)));
        Assert.Equal((short)6, BinaryPrimitives.ReadInt16LittleEndian(current.AsSpan(4)));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2E8A, 2));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2E92, 2));
        Assert.Throws<NotSupportedException>(() => source.Arena!.Read(0x2E84, 6));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(source))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCarbuncle278Morph.Morph683CA0(restored, mesh, second.ToImmutableArray(), 2048)));
    }
}
