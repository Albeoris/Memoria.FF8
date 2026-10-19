using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCameraChannelServices<T>(Func<T, uint, int, byte[]> ReadAddress, Func<T, uint, ReadOnlyMemory<byte>, T> WriteAddress);

public static class NativeBattleCameraChannels
{
    private static readonly uint[] ChannelOffsets = [0, 0x14, 0x28, 0x3C];
    private static readonly uint[] OutputOffsets = [0x5E, 0xDE, 0xD8, 0xDA];

    public static T InitializeFour<T>(T frame, uint camera, uint source, NativeBattleCameraChannelServices<T> services)
    {
        frame = Word(frame, unchecked(camera + 0xE6), 0, services);
        for (int index = 0; index < 4; index++)
        {
            frame = Dword(frame, unchecked(camera + ChannelOffsets[index]), U32(frame, unchecked(source + (uint)(4 * index)), services), services);
        }
        for (int index = 0; index < 4; index++)
        {
            frame = Initialize(frame, unchecked(camera + ChannelOffsets[index]), unchecked(camera + OutputOffsets[index]), services);
        }
        return frame;
    }

    public static T TickFour<T>(T frame, uint camera, NativeBattleCameraChannelServices<T> services)
    {
        for (int index = 0; index < 4; index++)
        {
            frame = Tick(frame, camera, unchecked(camera + ChannelOffsets[index]), unchecked(camera + OutputOffsets[index]), services).Frame;
        }
        return Word(frame, unchecked(camera + 0xE6), unchecked((ushort)(U16(frame, unchecked(camera + 0xE6), services) + 1)), services);
    }

    public static T Initialize<T>(T frame, uint channel, uint output, NativeBattleCameraChannelServices<T> services)
    {
        uint stream = U32(frame, U32(frame, channel, services), services);
        short tag = I16(frame, stream, services);
        frame = Word(frame, unchecked(channel + 8), unchecked((ushort)tag), services);
        uint cursor = unchecked(stream + 2);
        if (tag == 2)
        {
            frame = Word(frame, unchecked(channel + 0x10), U16(frame, cursor, services), services);
            cursor = unchecked(cursor + 2);
        }
        else if (tag == 1)
        {
            ushort start = U16(frame, cursor, services);
            if (start == 0x7FFE)
            {
                start = U16(frame, output, services);
            }
            frame = Word(frame, unchecked(channel + 0xA), start, services);
            cursor = unchecked(cursor + 2);
            frame = Word(frame, unchecked(channel + 0xC), U16(frame, cursor, services), services);
            cursor = unchecked(cursor + 2);
        }
        return Dword(frame, unchecked(channel + 4), cursor, services);
    }

    public static (T Frame, bool Complete) Tick<T>(T frame, uint camera, uint channel, uint output, NativeBattleCameraChannelServices<T> services)
    {
        if (U32(frame, U32(frame, channel, services), services) == 0)
        {
            return (frame, true);
        }
        var stepped = Step(frame, camera, channel, output, services);
        frame = stepped.Frame;
        if (!stepped.Complete)
        {
            return (frame, false);
        }
        uint outer = unchecked(U32(frame, channel, services) + 4);
        frame = Dword(frame, channel, outer, services);
        return U32(frame, outer, services) == 0 ? (frame, true) : (Initialize(frame, channel, output, services), false);
    }

    public static (T Frame, bool Complete) Step<T>(T frame, uint camera, uint channel, uint output, NativeBattleCameraChannelServices<T> services)
    {
        short tag = I16(frame, unchecked(channel + 8), services);
        if (tag == 2)
        {
            return (frame, I16(frame, unchecked(camera + 0xE6), services) >= I16(frame, unchecked(channel + 0x10), services));
        }
        if (tag is not (0 or 1))
        {
            return (frame, false);
        }
        uint cursor = U32(frame, unchecked(channel + 4), services);
        short sample = I16(frame, cursor, services);
        if (sample == 0x7FFF)
        {
            return (frame, true);
        }
        if (tag == 0)
        {
            frame = Word(frame, unchecked(channel + 0xE), unchecked((ushort)sample), services);
            frame = Dword(frame, unchecked(channel + 4), unchecked(cursor + 2), services);
            return (Word(frame, output, unchecked((ushort)sample), services), false);
        }
        int start = I16(frame, unchecked(channel + 0xA), services);
        int end = I16(frame, unchecked(channel + 0xC), services);
        frame = Dword(frame, unchecked(channel + 4), unchecked(cursor + 2), services);
        int product = unchecked((end - start) * sample);
        int value = unchecked(start + ((product < 0 ? unchecked(product + 4095) : product) >> 12));
        frame = Word(frame, unchecked(channel + 0xE), unchecked((ushort)value), services);
        return (Word(frame, output, unchecked((ushort)value), services), false);
    }

    private static ushort U16<T>(T frame, uint address, NativeBattleCameraChannelServices<T> services) => BinaryPrimitives.ReadUInt16LittleEndian(Read(frame, address, 2, services));
    private static short I16<T>(T frame, uint address, NativeBattleCameraChannelServices<T> services) => unchecked((short)U16(frame, address, services));
    private static uint U32<T>(T frame, uint address, NativeBattleCameraChannelServices<T> services) => BinaryPrimitives.ReadUInt32LittleEndian(Read(frame, address, 4, services));
    private static T Word<T>(T frame, uint address, ushort value, NativeBattleCameraChannelServices<T> services) => services.WriteAddress(frame, address, BitConverter.GetBytes(value));
    private static T Dword<T>(T frame, uint address, uint value, NativeBattleCameraChannelServices<T> services) => services.WriteAddress(frame, address, BitConverter.GetBytes(value));
    private static byte[] Read<T>(T frame, uint address, int length, NativeBattleCameraChannelServices<T> services)
    {
        byte[] bytes = services.ReadAddress(frame, address, length);
        return bytes is not null && bytes.Length == length ? bytes : throw new InvalidDataException("Native camera channel read lost its exact addressed extent.");
    }
}
