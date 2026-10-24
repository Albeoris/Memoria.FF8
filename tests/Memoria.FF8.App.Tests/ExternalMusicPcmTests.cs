namespace Memoria.FF8.App.Tests;

public sealed class ExternalMusicPcmTests
{
    [Fact]
    public void OffsetIsMeasuredInWholeInterleavedFrames()
    {
        PcmAudio pcm = new(new byte[44100 * 4 * 2], 44100, 2);
        Assert.Equal(44100 * 4, ExternalMusicPcm.OffsetBytes(pcm, 1));
        Assert.Equal(0, ExternalMusicPcm.OffsetBytes(pcm, 0));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void OffsetMustAddressAFrameInTheDecodedTrack(double seconds)
    {
        Assert.Throws<InvalidDataException>(() => ExternalMusicPcm.OffsetBytes(new PcmAudio(new byte[44100 * 4 * 2], 44100, 2), seconds));
    }

    [Fact]
    public void VorbisLoopTagsUseSourceSamplesAndAcceptThousandsSeparators()
    {
        byte[] ogg = Ogg(22050, "LOOPSTART=22,050", "LOOPEND=44,100");
        ExternalMusicLoop loop = ExternalMusicLoop.Read(ogg, new PcmAudio(new byte[44100 * 4 * 3], 44100, 2));
        Assert.Equal(44100 * 4, loop.StartByte);
        Assert.Equal(44100 * 4 * 2, loop.EndByte);
    }

    [Fact]
    public void UntaggedVorbisLoopsTheWholeTrack()
    {
        Assert.Equal(new ExternalMusicLoop(0, 400), ExternalMusicLoop.Read(Ogg(44100), new PcmAudio(new byte[400], 44100, 2)));
    }

    [Fact]
    public void LoopLengthIsRelativeToLoopStart()
    {
        Assert.Equal(new ExternalMusicLoop(40, 120), ExternalMusicLoop.Read(Ogg(44100, "LOOPSTART=10", "LOOPLENGTH=20"), new PcmAudio(new byte[400], 44100, 2)));
    }

    [Fact]
    public void OutOfRangeLoopTagsFailBeforeDeviceCreation()
    {
        Assert.Throws<InvalidDataException>(() => ExternalMusicLoop.Read(Ogg(44100, "LOOPSTART=100"), new PcmAudio(new byte[400], 44100, 2)));
    }

    private static byte[] Ogg(int sourceRate, params string[] tags)
    {
        byte[] identity = new byte[30];
        "\x01vorbis"u8.CopyTo(identity);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(identity.AsSpan(12, 4), sourceRate);
        using MemoryStream comments = new();
        using (BinaryWriter writer = new(comments, System.Text.Encoding.UTF8, true))
        {
            writer.Write("\x03vorbis"u8);
            writer.Write(0);
            writer.Write(tags.Length);
            foreach (string tag in tags)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(tag);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }
        using MemoryStream result = new();
        foreach (byte[] packet in new[] { identity, comments.ToArray() })
        {
            byte[] header = new byte[28];
            "OggS"u8.CopyTo(header);
            header[26] = 1;
            header[27] = checked((byte)packet.Length);
            result.Write(header);
            result.Write(packet);
        }
        return result.ToArray();
    }
}
