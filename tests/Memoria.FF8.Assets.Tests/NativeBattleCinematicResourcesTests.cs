using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCinematicResourcesTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(201)]
    [InlineData(202)]
    [InlineData(203)]
    [InlineData(204)]
    [InlineData(205)]
    [InlineData(206)]
    public void Each_cinematic_profile_reads_its_original_directories_and_complete_dispatch_tables(int effect)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory original = new(executable);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([checked((ushort)effect)])[effect - 1];
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        NativeBattleEffectInitializerAsset file = binding.InitializerAssets!.Value[0];
        Assert.Equal(profile.InitialFile, file.Name);
        NativeBattleCinematicResources resources = NativeBattleCinematicResourcesReader.Read(executable, file.Data, profile);
        resources.Validate();
        Assert.Equal(original.Read(profile.BoneTable, profile.BoneCount * 4), resources.ReadStatic(profile.BoneTable, profile.BoneCount * 4).ToArray());
        Assert.Equal(original.Read(profile.PrimitiveTable, 60 * 4), resources.ReadStatic(profile.PrimitiveTable, 60 * 4).ToArray());
        Assert.Equal(original.Read(profile.PrimitiveSizes, 20), resources.ReadStatic(profile.PrimitiveSizes, 20).ToArray());
        Assert.Equal(original.Read(profile.DrawTable, 73 * 4), resources.ReadStatic(profile.DrawTable, 73 * 4).ToArray());
        Assert.Equal(original.Read(profile.InstructionTable, 0x147 * 4), resources.ReadStatic(profile.InstructionTable, 0x147 * 4).ToArray());
        Assert.Equal(original.Read(profile.InitializationAddress, 0x20), resources.ReadStatic(profile.InitializationAddress, 0x20).ToArray());
        NativeBattleScriptedInitializationDirectories directories = assets.ReadNativeCinematicInitializationDirectories(effect);
        Assert.Equal(original.Read(profile.InitializationAddress, 0x1C), directories.InitializationDirectory.Data.ToArray());
        Assert.Equal(original.Read(profile.OwnerDirectoryAddress, 0x14), directories.OwnerDirectory.Data.ToArray());
        NativeBattleCinematicResources complete = assets.ReadNativeCinematicResources(effect);
        Assert.Same(complete, assets.ReadNativeCinematicResources(effect));
        Assert.Equal(resources.StaticData.Data, complete.StaticData.Data);
        Assert.All(Enumerable.Range(profile.SourceBase, profile.LastSource - profile.SourceBase + 1), id => Assert.Single(complete.Files, file => file.FileId == id));
        foreach (int opcode in new[] { 0, 2, 3, 0x30, 0x37, 0x39, 0x3F, 0x47, 0x4B, 0x81, 0x82, 0x90, 0x9B, 0xA1, 0xA4, 0xB2, 0xFB })
        {
            uint selected = BinaryPrimitives.ReadUInt32LittleEndian(complete.ReadStatic(profile.InstructionTable + (uint)(4 * opcode), 4));
            uint expected = BinaryPrimitives.ReadUInt32LittleEndian(original.Read(0x1874F10u + (uint)(4 * opcode), 4));
            if (opcode == 0x90 && effect is not (202 or 205))
            {
                Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, "vm_table", opcode, selected));
                continue;
            }
            if (opcode == 0x90 && effect is 202 or 205)
            {
                expected = selected;
            }
            Assert.Equal(expected, NativeBattleCinematicDispatch.RequireSlot(effect, "vm_table", opcode, selected));
        }
        foreach (int draw in new[] { 1, 2, 3, 6 })
        {
            uint selected = BinaryPrimitives.ReadUInt32LittleEndian(complete.ReadStatic(profile.DrawTable + (uint)(4 * draw), 4));
            uint expected = BinaryPrimitives.ReadUInt32LittleEndian(original.Read(0x1874D6Cu + (uint)(4 * draw), 4));
            if (draw == 3 && effect == 205)
            {
                Assert.Equal(0xAF5810u, NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", draw, selected));
            }
            else
            {
                Assert.Equal(expected, NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", draw, selected));
            }
        }
    }

    [Theory]
    [InlineData(202, 41, 0xB1B5C0u)]
    [InlineData(202, 43, 0xB1B9E0u)]
    [InlineData(205, 3, 0xAF5810u)]
    [InlineData(205, 38, 0xAF59D0u)]
    [InlineData(206, 56, 0xAE52C0u)]
    [InlineData(206, 57, 0xAEAAE0u)]
    public void Selected_draw_variants_match_their_actual_original_table_index(int effect, int index, uint expected)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        NativeBattleCinematicResources resources = assets.ReadNativeCinematicResources(effect);
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(profile.DrawTable + (uint)(4 * index), 4));
        Assert.Equal(expected, actual);
        Assert.Equal(actual, NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", index, actual));
        if (effect == 202 && index == 43)
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", 42, actual));
        }
    }

    [Fact]
    public void Original_Ifrit_static_owner_supplies_all_twenty_three_texture_rows_and_the_full_IO_descriptor()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory original = new(executable);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([201])[200];
        NativeBattleEffectInitializerAsset file = binding.InitializerAssets!.Value[0];
        Assert.Equal("mag200_b.00", file.Name);
        NativeBattleCinematicResources resources = NativeBattleCinematicResourcesReader.ReadIfrit(executable, file.Data);
        resources.Validate();
        Assert.Equal(0x1874894u, resources.StaticAddress);
        Assert.Equal(0xB98, resources.StaticData.Data.Length);
        Assert.Equal(23, resources.TextureProfile.Count);
        Assert.Equal(0x1E4u, resources.InitializationOffset(0x1C));
        Assert.Equal(new byte[] { 1, 3 }, resources.ReadStatic(0x1874A70, 2).ToArray());
        for (int index = 0; index < 23; index++)
        {
            foreach (bool clut in new[] { false, true })
            {
                uint rectangleAddress = checked((clut ? 0x187496Cu : 0x18748B4u) + (uint)index * 8);
                byte[] rectangle = original.Read(rectangleAddress, 8);
                Assert.Equal(new NativeBattleEffectTextureRectangle(BinaryPrimitives.ReadInt16LittleEndian(rectangle), BinaryPrimitives.ReadInt16LittleEndian(rectangle.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(rectangle.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(rectangle.AsSpan(6))), resources.Rectangle(index, clut));
                byte source = original.Read(checked((clut ? 0x1874A3Cu : 0x1874A24u) + (uint)index), 1)[0];
                Assert.Equal(clut ? source : (byte)(source & 0x7F), resources.Source(index, clut));
            }
        }
        Assert.Contains((byte)3, Enumerable.Range(0, 23).Select(index => resources.Source(index, false)));
        Assert.Contains((byte)6, Enumerable.Range(0, 23).Select(index => resources.Source(index, false)));
        Assert.Contains((byte)9, Enumerable.Range(0, 23).Select(index => resources.Source(index, false)));
        Assert.Equal((byte)127, resources.Source(0, false));
        Assert.Equal((byte)255, resources.Source(0, true));
        int vibrationStart = BinaryPrimitives.ReadInt32LittleEndian(file.Data.AsSpan().Slice(0x24));
        Assert.Equal(0x2540, vibrationStart);
        Assert.Equal(64, resources.Vibration.Offsets.Length);
        Assert.Equal(0, resources.Vibration.Offsets[0]);
        Assert.Equal(0x100, resources.Vibration.Offsets[1]);
        Assert.All(resources.Vibration.Offsets.Skip(2), offset => Assert.Equal(0, offset));
        Assert.Equal(0x2DE, resources.Vibration.Bytes.Length);
        Assert.Equal(file.Data.AsSpan().Slice(vibrationStart, resources.Vibration.Bytes.Length).ToArray(), resources.Vibration.Bytes.ToArray());
    }

    [Fact]
    public void Reader_rejects_an_unauthenticated_executable_before_reading_resource_metadata()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicResourcesReader.ReadIfrit(new byte[64], default));
    }
}
