using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// Converts between a fixed-size record section's raw bytes and a strongly-typed array,
/// without requiring callers to write unsafe code.
/// </summary>
internal static class KernelStructArray
{
    public static T[] Read<T>(Byte[] content, Int32 offset, Int32 length) where T : unmanaged
    {
        Int32 elementSize = Unsafe.SizeOf<T>();
        if (length % elementSize != 0)
            throw new InvalidDataException($"Section size {length} is not a multiple of {typeof(T).Name} size {elementSize}.");

        ReadOnlySpan<T> span = MemoryMarshal.Cast<Byte, T>(content.AsSpan(offset, length));
        return span.ToArray();
    }

    public static Byte[] Write<T>(T[] items) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(items);
        return MemoryMarshal.AsBytes<T>(items).ToArray();
    }
}
