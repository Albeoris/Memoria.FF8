using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Memoria.FF8.Assets;

namespace Memoria.FF8;

/// <summary>Uses the same Windows ACM Microsoft ADPCM→PCM16 contract as original46E420.</summary>
internal static class WindowsAcmDecoder
{
    public static byte[] Decode(GlobalAudioWaveFormat format, byte[] compressed)
    {
        if (format.Tag == 1 && format.BitsPerSample == 16)
        {
            return compressed;
        }
        if (format.Tag != 2 && !(format.Tag == 1 && format.BitsPerSample == 8) || format.Channels is not (1 or 2))
        {
            throw new NotSupportedException("Field sound output requires original PCM8/16 or Microsoft ADPCM mono/stereo.");
        }
        int sourceFormatSize = format.Tag == 1 ? 16 : 18 + format.ExtraData.Count;
        byte[] sourceFormat = new byte[Math.Max(18, sourceFormatSize)];
        PcmAudioDecoder.CreateWave(format, []).AsSpan(20, sourceFormatSize).CopyTo(sourceFormat);
        byte[] destinationFormat = new byte[18];
        BinaryPrimitives.WriteUInt16LittleEndian(destinationFormat, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(destinationFormat.AsSpan(2), format.Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(destinationFormat.AsSpan(4), format.SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(destinationFormat.AsSpan(8), checked(format.SampleRate * format.Channels * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(destinationFormat.AsSpan(12), checked((ushort)(format.Channels * 2)));
        BinaryPrimitives.WriteUInt16LittleEndian(destinationFormat.AsSpan(14), 16);
        GCHandle sourceWave = GCHandle.Alloc(sourceFormat, GCHandleType.Pinned);
        GCHandle destinationWave = GCHandle.Alloc(destinationFormat, GCHandleType.Pinned);
        nint stream = 0;
        try
        {
            Check(acmStreamOpen(out stream, 0, sourceWave.AddrOfPinnedObject(), destinationWave.AddrOfPinnedObject(), 0, 0, 0, 4));
            Check(acmStreamSize(stream, (uint)compressed.Length, out uint destinationLength, 0));
            byte[] output = new byte[checked((int)destinationLength)];
            GCHandle source = GCHandle.Alloc(compressed, GCHandleType.Pinned);
            GCHandle destination = GCHandle.Alloc(output, GCHandleType.Pinned);
            try
            {
                var header = new StreamHeader { Size = (uint)Marshal.SizeOf<StreamHeader>(), Source = source.AddrOfPinnedObject(), SourceLength = (uint)compressed.Length, Destination = destination.AddrOfPinnedObject(), DestinationLength = destinationLength, Reserved = new uint[15] };
                Check(acmStreamPrepareHeader(stream, ref header, 0));
                try
                {
                    Check(acmStreamConvert(stream, ref header, 4));
                    if (header.SourceUsed != compressed.Length)
                    {
                        throw new InvalidDataException("Windows ACM did not consume the complete original sound sample.");
                    }
                    return output.AsSpan(0, checked((int)header.DestinationUsed)).ToArray();
                }
                finally
                {
                    Check(acmStreamUnprepareHeader(stream, ref header, 0));
                }
            }
            finally
            {
                destination.Free();
                source.Free();
            }
        }
        finally
        {
            if (stream != 0)
            {
                acmStreamClose(stream, 0);
            }
            destinationWave.Free();
            sourceWave.Free();
        }
    }

    private static void Check(uint result)
    {
        if (result != 0)
        {
            throw new InvalidDataException($"Windows ACM conversion failed with MMRESULT {result}.");
        }
    }

    // Windows x64 msacm.h packs this header to one byte and reserves15 DWORDs.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct StreamHeader
    {
        public uint Size;
        public uint Status;
        public nuint User;
        public nint Source;
        public uint SourceLength;
        public uint SourceUsed;
        public nuint SourceUser;
        public nint Destination;
        public uint DestinationLength;
        public uint DestinationUsed;
        public nuint DestinationUser;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)] public uint[] Reserved;
    }

    [DllImport("msacm32.dll")] private static extern uint acmStreamOpen(out nint stream, nint driver, nint sourceFormat, nint destinationFormat, nint filter, nuint callback, nuint instance, uint flags);
    [DllImport("msacm32.dll")] private static extern uint acmStreamSize(nint stream, uint sourceLength, out uint destinationLength, uint flags);
    [DllImport("msacm32.dll")] private static extern uint acmStreamPrepareHeader(nint stream, ref StreamHeader header, uint flags);
    [DllImport("msacm32.dll")] private static extern uint acmStreamConvert(nint stream, ref StreamHeader header, uint flags);
    [DllImport("msacm32.dll")] private static extern uint acmStreamUnprepareHeader(nint stream, ref StreamHeader header, uint flags);
    [DllImport("msacm32.dll")] private static extern uint acmStreamClose(nint stream, uint flags);
}
