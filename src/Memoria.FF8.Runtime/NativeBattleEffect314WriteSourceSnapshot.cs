using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>One ordered store's detached source bytes and contained resource descriptors.</summary>
internal readonly record struct NativeBattleEffect314WriteSourceSnapshot(byte[] Data, bool[] Known, ImmutableDictionary<int, NativeBattleResourcePointer?> Pointers)
{
    internal static NativeBattleEffect314WriteSourceSnapshot Capture(NativeBattleEffectArena source, int offset, int length)
    {
        source.RequireRange(offset, length);
        (byte[] data, bool[] known) = source.ReadSnapshot(offset, length);
        return CaptureOwned(data, known, source.MayHaveResourcePointers() ? source.EnumerateResourcePointers() : null, offset);
    }

    internal static NativeBattleEffect314WriteSourceSnapshot Capture(ReadOnlySpan<byte> data, ReadOnlySpan<bool> known)
    {
        if (data.Length > NativeBattleEffectArena.Length || data.Length != known.Length)
        {
            throw new InvalidDataException("Native detached write requires bounded bytes and matching knownness.");
        }
        return new(data.ToArray(), known.ToArray(), ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty);
    }

    internal NativeBattleEffect314WriteSourceSnapshot Slice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > Data.Length - length)
        {
            throw new InvalidDataException("Native effect memory access exceeds20DFAB8..21DFAB8.");
        }
        return offset == 0 && length == Data.Length ? this : CaptureOwned(Data.AsSpan(offset, length).ToArray(), Known.AsSpan(offset, length).ToArray(), Pointers, offset);
    }

    private static NativeBattleEffect314WriteSourceSnapshot CaptureOwned(byte[] data, bool[] known, IEnumerable<KeyValuePair<int, NativeBattleResourcePointer?>>? sourcePointers, int offset)
    {
        ImmutableDictionary<int, NativeBattleResourcePointer?>.Builder? pointers = null;
        if (sourcePointers is not null)
        {
            foreach ((int field, NativeBattleResourcePointer? pointer) in sourcePointers)
            {
                if (field >= offset && field - offset <= data.Length - 4)
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
