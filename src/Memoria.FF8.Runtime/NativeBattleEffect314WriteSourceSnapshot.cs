using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>One ordered store's detached source bytes and contained resource descriptors.</summary>
internal readonly record struct NativeBattleEffect314WriteSourceSnapshot(byte[] Data, bool[] Known, ImmutableDictionary<int, NativeBattleResourcePointer?> Pointers)
{
    internal static NativeBattleEffect314WriteSourceSnapshot Capture(NativeBattleEffectArena source, int offset, int length)
    {
        source.RequireRange(offset, length);
        (byte[] data, bool[] known) = source.ReadSnapshot(offset, length);
        ImmutableDictionary<int, NativeBattleResourcePointer?>.Builder? pointers = null;
        if (source.MayHaveResourcePointers())
        {
            foreach ((int field, NativeBattleResourcePointer? pointer) in source.EnumerateResourcePointers())
            {
                if (field >= offset && field - offset <= length - 4)
                {
                    (pointers ??= ImmutableDictionary.CreateBuilder<int, NativeBattleResourcePointer?>()).Add(field - offset, pointer);
                }
            }
        }
        ImmutableDictionary<int, NativeBattleResourcePointer?> selected = pointers?.ToImmutable() ?? ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty;
        foreach ((int field, NativeBattleResourcePointer? pointer) in selected)
        {
            NativeBattleEffectArena.ValidatePointer(pointer);
            NativeBattleEffectArena.ValidateResourcePointerBytes(pointer, data.AsSpan(field, 4), known.AsSpan(field, 4));
        }
        return new(data, known, selected);
    }

    internal NativeBattleEffectArena CopyTo(NativeBattleEffectArena destination, int offset)
    {
        destination.WriteWithKnownness(offset, Data, Known);
        foreach ((int field, NativeBattleResourcePointer? pointer) in Pointers)
        {
            destination.WriteResourcePointer(checked(offset + field), pointer);
        }
        return destination;
    }
}
