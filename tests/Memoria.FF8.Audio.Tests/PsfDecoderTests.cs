using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class PsfDecoderTests
{
    [Fact]
    public void ContainerDecodesProgramAndTimingAndLibraryOrder()
    {
        byte[] program = Program();
        PsfDocument document = PsfDocument.Read(Container(program, "length=1:02.125\nfade=0:03\n_lib=base.psflib\n_lib2=extra.psflib\nvolume=0.5\n"));
        Assert.Equal(program, document.Program);
        Assert.Equal(TimeSpan.FromSeconds(62.125), document.Metadata.Length);
        Assert.Equal(TimeSpan.FromSeconds(3), document.Metadata.Fade);
        Assert.Equal(0.5, document.Metadata.Volume);
        Assert.Equal(new[] { "base.psflib", "extra.psflib" }, document.Libraries);
    }

    [Fact]
    public void CorruptCrcAndInvalidRamAreRejected()
    {
        byte[] file = Container(Program(), "");
        file[12] ^= 1;
        Assert.Throws<InvalidDataException>(() => PsfDocument.Read(file));
        byte[] program = Program();
        BinaryPrimitives.WriteUInt32LittleEndian(program.AsSpan(0x18), 0x801fffff);
        Assert.Throws<InvalidDataException>(() => PsfDocument.Read(Container(program, "")));
    }

    [Theory]
    [InlineData("1:60")]
    [InlineData("-1")]
    [InlineData("NaN")]
    [InlineData("1:2:3:4")]
    public void InvalidTimeIsRejected(string value)
    {
        Assert.Throws<InvalidDataException>(() => PsfDocument.ParseTime(value));
    }

    [Fact]
    public async Task CyclicLibrariesAndRootEscapeAreRejectedBeforeStartingHelper()
    {
        byte[] bios = BiosProfile();
        byte[] cycle = Container(Program(), "_lib=song.minipsf\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => PsfDecoder.OpenAsync("song.minipsf", cycle, (_, _) => cycle, bios, true, TestContext.Current.CancellationToken));
        byte[] escape = Container(Program(), "_lib=../outside.psflib\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => PsfDecoder.OpenAsync("song.minipsf", escape, (_, _) => throw new Exception("must not resolve escaped name"), bios, true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancellationPrecedesResourceReads()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PsfDecoder.OpenAsync("song", [], (_, _) => throw new Exception(), [], true, cancellation.Token));
    }

    // Optional externally configured resources never become public test assets.
    [Fact]
    public async Task ConfiguredNativeResourcesRenderDeterministicallyAndCancel()
    {
        string? resource = Environment.GetEnvironmentVariable("MEMORIA_TEST_PSF");
        string? biosPath = Environment.GetEnvironmentVariable("MEMORIA_TEST_PSF_BIOS");
        string? helper = Environment.GetEnvironmentVariable("MEMORIA_TEST_PSF_DECODER");
        if (resource is null || biosPath is null || helper is null)
        {
            Assert.Skip("Configure MEMORIA_TEST_PSF, MEMORIA_TEST_PSF_BIOS and MEMORIA_TEST_PSF_DECODER for native PSF checks.");
        }
        byte[] bios = File.ReadAllBytes(biosPath);
        byte[] psf = File.ReadAllBytes(resource);
        string directory = Path.GetDirectoryName(Path.GetFullPath(resource))!;
        byte[] Resolve(string requester, string dependency) => File.ReadAllBytes(Path.Combine(directory, Path.GetDirectoryName(requester) ?? "", dependency));
        async Task<byte[]> Render()
        {
            await using PsfPcmStream stream = await PsfDecoder.OpenAsync(Path.GetFileName(resource), psf, Resolve, bios, true, TestContext.Current.CancellationToken, decoderPath: helper);
            byte[] pcm = new byte[44100 * 4 * 3];
            await stream.ReadExactlyAsync(pcm, TestContext.Current.CancellationToken);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { _ = await stream.ReadAsync(new byte[16], cancellation.Token); });
            return pcm;
        }
        byte[] first = await Render();
        byte[] second = await Render();
        Assert.Contains(first, b => b != 0);
        Assert.Equal(first, second);
        int silentPrefix = 0;
        while (silentPrefix < first.Length && BinaryPrimitives.ReadUInt32LittleEndian(first.AsSpan(silentPrefix)) == 0)
        {
            silentPrefix += 4;
        }
        await using (PsfPcmStream trimmed = await PsfDecoder.OpenAsync(Path.GetFileName(resource), psf, Resolve, bios, true, TestContext.Current.CancellationToken, helper, suppressOpeningSilence: true))
        {
            byte[] pcm = new byte[Math.Min(4096, first.Length - silentPrefix)];
            await trimmed.ReadExactlyAsync(pcm, TestContext.Current.CancellationToken);
            Assert.Equal(first.AsSpan(silentPrefix, pcm.Length).ToArray(), pcm);
        }

        PsfDocument document = PsfDocument.Read(psf);
        string tags = string.Join('\n', document.Metadata.Tags.Where(t => !new[] { "length", "fade", "volume" }.Contains(t.Key, StringComparer.OrdinalIgnoreCase)).Select(t => t.Key + "=" + t.Value));
        byte[] shortPsf = Container(document.Program, tags + "\nlength=0.01\nfade=0.01\nvolume=1\n");
        await using (PsfPcmStream finite = await PsfDecoder.OpenAsync(Path.GetFileName(resource), shortPsf, Resolve, bios, false, TestContext.Current.CancellationToken, helper))
        {
            byte[] pcm = new byte[882 * 4];
            await finite.ReadExactlyAsync(pcm, TestContext.Current.CancellationToken);
            Assert.Equal(0, await finite.ReadAsync(new byte[4], TestContext.Current.CancellationToken));
            if (document.Metadata.Volume == 1)
            {
                for (int frame = 0; frame < 882; frame++)
                {
                    double gain = frame < 441 ? 1 : (882 - frame) / 441.0;
                    for (int channel = 0; channel < 2; channel++)
                    {
                        int offset = frame * 4 + channel * 2;
                        short original = BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan(offset));
                        Assert.Equal((short)(original * gain), BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(offset)));
                    }
                }
            }
        }
        await using (PsfPcmStream infinite = await PsfDecoder.OpenAsync(Path.GetFileName(resource), shortPsf, Resolve, bios, true, TestContext.Current.CancellationToken, helper))
        {
            byte[] pcm = new byte[4096 * 4];
            await infinite.ReadExactlyAsync(pcm, TestContext.Current.CancellationToken);
            Assert.Equal(pcm.Length, infinite.Position);
        }
        using var bootCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bootCancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
        byte[] blockedBios = new byte[524288];
        Encoding.ASCII.GetBytes("Highly Experimental ps1preboot=100 ps2preboot=0").CopyTo(blockedBios, 0x80);
        BinaryPrimitives.WriteUInt32LittleEndian(blockedBios, 0x0bf00000); // Synthetic MIPS jump to reset vector, followed by NOP.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PsfDecoder.OpenAsync("synthetic.psf", Container(Program(), ""), (_, _) => throw new Exception(), blockedBios, true, bootCancellation.Token, helper));
    }

    private static byte[] BiosProfile()
    {
        byte[] bios = new byte[524288];
        Encoding.ASCII.GetBytes("Highly Experimental ps1preboot=0 ps2preboot=0").CopyTo(bios, 0x80);
        return bios;
    }

    private static byte[] Program()
    {
        byte[] program = new byte[2056];
        "PS-X EXE"u8.CopyTo(program);
        BinaryPrimitives.WriteUInt32LittleEndian(program.AsSpan(0x18), 0x80010000);
        BinaryPrimitives.WriteUInt32LittleEndian(program.AsSpan(0x10), 0x80010000);
        BinaryPrimitives.WriteUInt32LittleEndian(program.AsSpan(0x30), 0x801fff00);
        return program;
    }

    private static byte[] Container(byte[] program, string tags)
    {
        using var payload = new MemoryStream();
        using (var zlib = new ZLibStream(payload, CompressionLevel.SmallestSize, true))
        {
            zlib.Write(program);
        }
        byte[] compressed = payload.ToArray();
        uint crc = uint.MaxValue;
        foreach (byte value in compressed)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
            }
        }
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write("PSF\u0001"u8);
        writer.Write(0u);
        writer.Write((uint)compressed.Length);
        writer.Write(~crc);
        writer.Write(compressed);
        writer.Write(Encoding.UTF8.GetBytes("[TAG]" + tags));
        return output.ToArray();
    }
}
