using Memoria.FF8.Assets;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Replacement CAM draw phases are presentation state; saved native CAM receipts retain their original source.</summary>
internal sealed class MoviePresentationCamera
{
    private readonly byte[] drawValues;

    public MoviePresentationCamera(byte[] bytes, NativeMovieCameraFlags nativeFlags, MovieInformation nativeInformation, MovieInformation mediaInformation)
    {
        Bytes = bytes;
        Flags = NativeMovieCameraReader.ReadPresentation(bytes);
        drawValues = new byte[Flags.Flags.Length];
        byte value = 1;
        for (int record = 0; record < Flags.Flags.Length; record++)
        {
            int nativeRecord = MovieFrameTimeline.FrameAt(record / mediaInformation.FramesPerSecond, nativeInformation);
            if (nativeRecord >= nativeFlags.Flags.Length || Flags.Flags[record] != nativeFlags.Flags[nativeRecord])
            {
                throw new NotSupportedException($"Replacement movie CAM flags change the original script contract at media record {record}, native record {nativeRecord}.");
            }
            drawValues[record] = value;
            value = Flags.Apply(record, value);
        }
    }

    public byte[] Bytes { get; }
    public NativeMovieCameraFlags Flags { get; }
    public int DrawRecord(int mediaFrame) => Math.Clamp(mediaFrame, 1, Flags.Flags.Length - 1);
    public byte DrawValue(int record) => drawValues[record];
}
