using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect314TemporaryReads
{
    internal static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int offset, int length)
    {
        source.RequireRange(offset, length);
        byte[] data = new byte[length];
        Span<bool> known = length <= 512 ? stackalloc bool[length] : new bool[length];
        source.ReadWithKnownness(offset, data, known);
        ImmutableDictionary<int, NativeBattleResourcePointer?>.Builder? pointers = null;
        long version = length == 0 ? 0 : 2;
        if (source.MayHaveResourcePointers())
        {
            foreach ((int field, NativeBattleResourcePointer? pointer) in source.EnumerateResourcePointers())
            {
                if (field >= offset && field - offset <= length - 4)
                {
                    (pointers ??= ImmutableDictionary.CreateBuilder<int, NativeBattleResourcePointer?>()).Add(field - offset, pointer);
                    version++;
                }
            }
        }
        return NativeBattleEffectArena.CreateOwnedRead(data, known, pointers?.ToImmutable(), version);
    }

    internal static NativeBattleEffectArena Known(byte[] ownedData)
    {
        Span<bool> known = ownedData.Length <= 512 ? stackalloc bool[ownedData.Length] : new bool[ownedData.Length];
        known.Fill(true);
        return NativeBattleEffectArena.CreateOwnedRead(ownedData, known, null, ownedData.Length == 0 ? 0 : 1);
    }
}
