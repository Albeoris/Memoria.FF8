using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWater222ResourcesTests
{
    [Fact]
    public void Original_two_file_profile_resolves_all_five_exact_pixel_and_CLUT_sources()
    {
        using GameAssets game = Installed();
        NativeBattleWater222Resources resources = game.ReadNativeWater222Resources();
        resources.Validate();
        Assert.Equal(new[] { "mag221_b.00", "mag221_b.01" }, resources.Files.Select(file => file.Identity.ToLowerInvariant()));
        Assert.Equal(new[] { 0x1799C, 0x26258 }, resources.Files.Select(file => file.Data.Length));
        Assert.Equal(new byte[] { 0x80, 0x81, 0x81, 0x81, 0x81, 0xFF, 0xFF, 0xFF }, resources.Profile.Tables.Data.Skip(80).Take(8));
        Assert.Equal(new byte[] { 1, 1, 1, 1, 1, 0xFF, 0xFF, 0xFF }, resources.Profile.Tables.Data.Skip(88).Take(8));
        Assert.All(resources.Profile.Tables.Data.Skip(96), value => Assert.Equal((byte)0, value));
        Assert.Equal("FF8_EN:186C84C", resources.Profile.InitializationDirectory.Identity);
        Assert.Equal(0x1C, resources.Profile.InitializationDirectory.Data.Length);
        Assert.Equal(new uint[] { 0x20, 0x48, 0x70, 0x78, 0x80, 0, 0x90 }, Enumerable.Range(0, 7).Select(index => resources.Profile.InitializationOffset(index * 4)));
        Assert.Equal("FF8_EN:186C944", resources.Profile.OwnerDirectory.Identity);
        Assert.Equal(new uint[] { 0x2797690, 0x803FF000, 0x2797068, 0x27975D8, 0x2797628 }, Enumerable.Range(0, 5).Select(resources.Profile.OwnerAddress));
        for (int offset = 0; offset <= 0x18; offset += 4)
        {
            Assert.Equal(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(resources.Profile.InitializationDirectory.Data.AsSpan().Slice(offset)), resources.Profile.InitializationOffset(offset));
        }

        int[] offsets = [0x799C, 0x858, 0x10858, 0x11858, 0x21858];
        int[] lengths = [0x10000, 0x10000, 0x1000, 0x10000, 0x4000];
        for (int index = 0; index < 5; index++)
        {
            NativeBattleWater222UploadPlan pixels = NativeBattleWater222TexturePair.Resolve(resources.Profile, resources.Files, index, false);
            NativeBattleWater222UploadPlan clut = NativeBattleWater222TexturePair.Resolve(resources.Profile, resources.Files, index, true);
            Assert.Equal(index == 0 ? 0 : 1, pixels.SourceIndex);
            Assert.Equal(offsets[index], pixels.Offset);
            Assert.Equal(lengths[index], 2 * pixels.Rectangle.Width * pixels.Rectangle.Height);
            Assert.Equal(1, clut.SourceIndex);
            Assert.Equal(0x25858 + index * 0x200, clut.Offset);
            Assert.Equal(0x200, 2 * clut.Rectangle.Width * clut.Rectangle.Height);
            NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
            NativeBattleWater222TextureStep step = NativeBattleWater222TexturePair.Transfer27(resources.Profile, resources.Files, index, 0, 0, 0x58, 0, plan => textures = NativeBattleWater222TexturePair.Enqueue505DF0(textures, resources.Files, plan));
            Assert.Equal((byte)2, step.Count);
            NativeBattleEffectTextureDrain drain = textures.Drain505D20();
            Assert.Equal(resources.Files[pixels.SourceIndex].Data.Skip(pixels.Offset).Take(lengths[index]), drain.Uploads[0].Bytes);
            Assert.Equal(resources.Files[1].Data.Skip(clut.Offset).Take(0x200), drain.Uploads[1].Bytes);
        }
        Assert.Equal(0x26258, NativeBattleWater222TexturePair.Resolve(resources.Profile, resources.Files, 4, true).Offset + 0x200);
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_WATER222_EXPORT") is { } output)
        {
            Directory.CreateDirectory(output);
            List<object> manifest = [];
            foreach (NativeBattleEffectHeapAllocation file in resources.Files)
            {
                string name = Path.GetFileName(file.Identity);
                WriteNew(output, name, file.Data);
                manifest.Add(new { Kind = "magic", File = name, NativeName = file.Identity, Length = file.Data.Length, Sha256 = Convert.ToHexString(SHA256.HashData(file.Data.AsSpan())) });
            }
            using BattleAssets battle = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
            foreach (ushort id in new ushort[] { 363, 364, 365 })
            {
                NativeBattleArchiveFile file = battle.ReadNativeBattleFile(id);
                string name = $"data-{id}.bin";
                WriteNew(output, name, file.Bytes);
                manifest.Add(new { Kind = "battle", FileId = id, File = name, NativeName = file.Name, Length = file.Bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(file.Bytes.AsSpan())) });
            }
            using FileStream stream = new(Path.Combine(output, "manifest.json"), FileMode.CreateNew);
            JsonSerializer.Serialize(stream, manifest, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [Fact]
    public void Current_file1_script_slot_alias_is_consumed_after_JSON_and_partial_pair_retains_order()
    {
        using GameAssets game = Installed();
        NativeBattleWater222Resources original = game.ReadNativeWater222Resources();
        byte replacement = unchecked((byte)(original.Files[1].Data[0x858] + 1));
        NativeBattleWater222Resources changed = original with { Files = original.Files.SetItem(1, original.Files[1] with { Data = original.Files[1].Data.SetItem(0x858, replacement) }) };
        NativeBattleWater222Resources restored = JsonSerializer.Deserialize<NativeBattleWater222Resources>(JsonSerializer.Serialize(changed))!;
        restored.Validate();
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleWater222TextureStep first = NativeBattleWater222TexturePair.Transfer27(restored.Profile, restored.Files, 1, 0, 11, 0x1234, 18, plan => textures = NativeBattleWater222TexturePair.Enqueue505DF0(textures, restored.Files, plan));
        Assert.Equal((byte)12, first.Count);
        Assert.Equal(0x1234u, first.InstructionPointer);
        Assert.True(first.Yield);
        Assert.Single(first.Uploads);
        Assert.Equal(replacement, textures.Drain505D20().Uploads[0].Bytes[0]);
        Assert.NotEqual(replacement, original.Files[1].Data[0x858]);
        NativeBattleWater222TextureStep cold = JsonSerializer.Deserialize<NativeBattleWater222TextureStep>(JsonSerializer.Serialize(first))!;
        Assert.Equal(first, cold with { Uploads = first.Uploads });
        Assert.Equal((ushort?)18, cold.Wait);
    }

    private static void WriteNew(string output, string name, ImmutableArray<byte> data)
    {
        using FileStream stream = new(Path.Combine(output, name), FileMode.CreateNew);
        stream.Write(data.AsSpan());
    }

    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources are optional.");
        return new(directory);
    }
}
