using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class FieldTextBoundaryTests
{
    [Fact]
    public void NativeTextTerminatorCanFollowAnAudioBlockBoundary()
    {
        byte[] source = MakeSection();
        PcFieldScripts scripts = PcFieldScripts.Read(source);
        Assert.Equal(new byte[] { 0x21, 0x41, 0x4B, 0x41, 0x4F }, scripts.Texts[0].ToArray());
        source[^1] = 0;
        Assert.Throws<InvalidDataException>(() => PcFieldScripts.Read(source));
    }

    [Fact]
    public void OptionalInstalledBasementRetainsTheOriginalFinalTextBytes()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original field text checks.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        PcFieldFile field = PcFieldFile.ReadCompressed(archive.ReadEntry(Assert.Single(archive.Find("mds7pb_2"))));
        ReadOnlySpan<byte> source = field.GetSection(PcFieldSectionKind.ScriptAndText).Span;
        PcFieldScripts scripts = PcFieldScripts.Read(source);
        int start = scripts.TextOffset + BinaryPrimitives.ReadUInt16LittleEndian(source[(scripts.TextOffset + 2 + 49 * 2)..]);
        int terminator = source[start..].IndexOf((byte)0xFF);
        Assert.True(start < scripts.AkaoOffsets.Min());
        Assert.True(start + terminator > scripts.AkaoOffsets.Min());
        Assert.Equal(source.Slice(start, terminator).ToArray(), scripts.Texts[49].ToArray());
    }

    private static byte[] MakeSection()
    {
        const int code = 108;
        const int text = code + 1;
        const int body = text + 4;
        byte[] source = new byte[body + 6];
        BinaryPrimitives.WriteUInt16LittleEndian(source, 0x0502);
        source[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(4), text);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8), 512);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(6), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(40), body + 1);
        for (int index = 0; index < 32; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(44 + index * 2), code);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(text), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(text + 2), 4);
        new byte[] { 0x21, 0x41, 0x4B, 0x41, 0x4F, 0xFF }.CopyTo(source, body);
        return source;
    }
}
