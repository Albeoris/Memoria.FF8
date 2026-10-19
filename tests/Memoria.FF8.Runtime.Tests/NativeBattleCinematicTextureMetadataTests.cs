using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicTextureMetadataTests
{
    private const uint Static = 0x100000;
    private const uint Arena = 0x20DFAB8;
    private const uint Manager = 0x2797690;

    [Fact]
    public void Descriptor_retains_all_twenty_three_entries_raw_flags_and_unmasked_CLUT_sources()
    {
        NativeBattleCinematicResources resources = Resources();
        resources.Validate();
        Assert.Equal((byte)9, resources.Source(22, false));
        Assert.Equal((byte)9, resources.Source(22, true));
        Assert.Equal((byte)127, resources.Source(21, false));
        Assert.Equal((byte)255, resources.Source(21, true));
        Assert.Equal(0x1E4u, resources.InitializationOffset(0x1C));
        Assert.Throws<NotSupportedException>(() => resources.Rectangle(23, false));
        Assert.Throws<NotSupportedException>(() => resources.Rectangle(-1, false));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(Static - 1, 1).ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(Static + 0x300, 1).ToArray());
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleCinematicResources>(JsonSerializer.Serialize(resources))));
    }

    [Fact]
    public void Texture_helpers_use_current_streamed_pointer_nine_and_mask_only_pixel_directory_offsets()
    {
        NativeBattleCinematicResources resources = Resources();
        NativeBattleWaterExecutionFrame frame = Frame();
        NativeBattleCinematicTextureSelection pixel = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 22);
        NativeBattleCinematicTextureSelection clut = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, 22);
        Assert.Equal(Arena + 0x5060, pixel.SourceAddress);
        Assert.Equal(Arena + 0x5180, clut.SourceAddress);
        Assert.Equal((byte)0x89, pixel.SourceMapByte);
        Assert.Equal((uint?)0xFB, pixel.TexturePage);
        NativeBattleEffectTextureState textures = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, pixel);
        textures = NativeBattleCinematicTextureMetadata.Enqueue505DF0(textures, frame, clut);
        NativeBattleEffectTextureDrain drain = textures.Drain505D20(frame.Arena);
        Assert.Equal(new byte[] { 0x12, 0x34 }, drain.Uploads[0].Bytes);
        Assert.Equal(new byte[] { 0x56, 0x78 }, drain.Uploads[1].Bytes);
        frame = frame with { Arena = frame.Arena.Write(0x6000, frame.Arena.Read(0x5000, 0x200)).Write(0x6060, [0xAB, 0xCD]) };
        frame = WriteCore(frame, 0x2798A68 + 9 * 4, Arena + 0x6000);
        Assert.Equal(Arena + 0x6060, NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 22).SourceAddress);
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, 21));
    }

    [Fact]
    public void Metadata_getters_share_descriptor_count_and_require_current_authenticated_directory_pointers()
    {
        NativeBattleCinematicResources resources = Resources();
        NativeBattleWaterExecutionFrame frame = Frame();
        NativeBattleWaterExecutionFrame selected = NativeBattleWaterTextureMetadata.SelectTPageB665C0(frame, resources, 22);
        Assert.Equal(0xFBu, selected.ReadTemporaryDword(0xFC));
        Assert.Equal((ushort)0x402C, NativeBattleWaterTextureMetadata.ReadClutB66640(frame, resources, 22));
        frame = WriteCore(frame, Manager + 0x98, Static + 0x28);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, resources, 22));
    }

    [Fact]
    public void Shared_pair_budget_supports_streamed_source_nine_and_preserves_a_partially_submitted_pair()
    {
        NativeBattleCinematicResources resources = Resources();
        NativeBattleWaterExecutionFrame frame = Frame();
        ImmutableArray<NativeBattleEffectHeapAllocation> files = Enumerable.Range(0, 10).Select(index => new NativeBattleEffectHeapAllocation("current", index == 9 ? frame.Arena.Read(0x5000, 0x200).ToImmutableArray() : [])).ToImmutableArray();
        List<NativeBattleWater222UploadPlan> submitted = [];
        NativeBattleWater222TextureStep partial = NativeBattleWater222TexturePair.Transfer27(resources, files, 22, 0, 11, 100, 9, submitted.Add);
        Assert.Equal((byte)12, partial.Count);
        Assert.True(partial.Yield);
        Assert.Equal(100u, partial.InstructionPointer);
        Assert.Equal(9, Assert.Single(submitted).SourceIndex);
        NativeBattleWater222TextureStep retry = NativeBattleWater222TexturePair.Transfer27(resources, files, 22, 0, 0, partial.InstructionPointer, 9, submitted.Add);
        Assert.False(retry.Yield);
        Assert.Equal(104u, retry.InstructionPointer);
        Assert.Equal(submitted[0], submitted[1]);
        Assert.Equal(0x180, submitted[2].Offset);
    }

    internal static NativeBattleCinematicResources Resources(uint staticAddress = Static)
    {
        byte[] data = new byte[0x300];
        uint[] directory = [0x20, 0xD8, 0x190, 0x1A8, 0x1C0, 0, 0x1DC, 0x1E4];
        for (int index = 0; index < directory.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), directory[index]);
        }
        foreach (int offset in new[] { 0x20 + 22 * 8, 0xD8 + 22 * 8 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), 704);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 2), 256);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 4), 1);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 6), 1);
        }
        data[0x190 + 22] = 0x89;
        data[0x1A8 + 22] = 9;
        data[0x190 + 21] = 0xFF;
        data[0x1A8 + 21] = 0xFF;
        return new(staticAddress, new("static", data.ToImmutableArray()), new(23, staticAddress, staticAddress + 0x250, staticAddress + 0x20, staticAddress + 0xD8, staticAddress + 0x190, staticAddress + 0x1A8), new(new string('0', 64), [0], [0, 0, 0, 0]));
    }

    internal static NativeBattleWaterExecutionFrame Frame()
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = WriteCore(frame, 0x27973EC, Manager);
        frame = WriteCore(frame, 0x27973E8, Arena + 0x2000);
        frame = WriteCore(frame, Manager + 0x98, Static + 0x20);
        frame = WriteCore(frame, Manager + 0x9C, Static + 0xD8);
        frame = WriteCore(frame, Manager + 0xA4, Static + 0x190);
        frame = WriteCore(frame, Manager + 0xA8, Static + 0x1A8);
        frame = WriteCore(frame, 0x2798A68 + 9 * 4, Arena + 0x5000);
        byte[] file = new byte[0x200];
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x14), 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), 0x100);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x40 + 22 * 4), 0xA0000020);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x100 + 22 * 4), 0x80);
        file[0x60] = 0x12;
        file[0x61] = 0x34;
        file[0x180] = 0x56;
        file[0x181] = 0x78;
        return frame with { Arena = frame.Arena.Write(0x5000, file).Write(0x2092, [0x60]) };
    }

    internal static NativeBattleWaterExecutionFrame WriteCore(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }
}
