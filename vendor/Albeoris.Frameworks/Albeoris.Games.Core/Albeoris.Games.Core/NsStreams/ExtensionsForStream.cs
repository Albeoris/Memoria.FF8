namespace Albeoris.Games.Core.NsStreams;

public static class ExtensionsForStream
{
    public static void WriteStruct<T>(this Stream stream, in T value) where T : unmanaged
    {
        unsafe
        {
            fixed (T* ptr = &value)
            {
                Span<Byte> buffer = new(ptr, sizeof(T));
                stream.Write(buffer);
            }
        }
    }

    public static void ReadStruct<T>(this Stream stream, out T value) where T : unmanaged
    {
        unsafe
        {
            fixed (T* ptr = &value)
            {
                Span<Byte> buffer = new(ptr, sizeof(T));
                stream.ReadExactly(buffer);
            }
        }
    }

    public static T ReadStruct<T>(this Stream stream) where T : unmanaged
    {
        unsafe
        {
            T value = new T();
            T* ptr = &value;
            Span<Byte> buffer = new(ptr, sizeof(T));
            stream.ReadExactly(buffer);
            return value;
        }
    }

    public static void SetPosition(this Stream stream, Int64 position)
    {
        if (stream.Position != position)
            stream.Position = position;
    }
}