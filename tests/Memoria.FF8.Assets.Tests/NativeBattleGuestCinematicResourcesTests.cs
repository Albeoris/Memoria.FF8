using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGuestCinematicResourcesTests
{
    [Theory]
    [InlineData(228)]
    [InlineData(261)]
    [InlineData(262)]
    [InlineData(263)]
    public void GuestProfilesReadTheirOriginalInitializersDirectoriesAndDispatchSlots(int effect)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory original = new(executable);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([checked((ushort)effect)])[effect - 1];
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        Assert.Equal(profile.InitialFile, binding.InitializerAssets!.Value[0].Name);
        NativeBattleCinematicResources resources = assets.ReadNativeCinematicResources(effect);
        resources.Validate();
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(binding.InitializerAssets.Value[0].Data.AsSpan().Slice(0x24)));
        Assert.Null(resources.Vibration);
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicResourcesReader.Read(executable, binding.InitializerAssets.Value[0].Data.SetItem(0x24, 4), profile));
        Assert.Same(resources, assets.ReadNativeCinematicResources(effect));
        NativeBattleScriptedInitializationDirectories directories = assets.ReadNativeCinematicInitializationDirectories(effect);
        Assert.Equal(original.Read(profile.InitializationAddress, 0x1C), directories.InitializationDirectory.Data.ToArray());
        Assert.Equal(original.Read(profile.OwnerDirectoryAddress, 0x14), directories.OwnerDirectory.Data.ToArray());
        foreach ((uint address, int length) in new[] { (profile.BoneTable, profile.BoneCount * 4), (profile.PrimitiveTable, 60 * 4), (profile.PrimitiveSizes, 20), (profile.SpriteTable, 11 * 4), (profile.PlacementTable, 5 * 4), (profile.ParticleInstructionTable, 24 * 4), (profile.DrawTable, 73 * 4), (profile.InstructionTable, 0x147 * 4) })
        {
            Assert.Equal(original.Read(address, length), resources.ReadStatic(address, length).ToArray());
        }
        foreach (int opcode in new[] { 0, 2, 3, 0x30, 0x37, 0x47, 0x4B, 0x81, 0x82, 0x9B, 0xA1, 0xA4, 0xAC, 0xB2, 0xFB })
        {
            uint actual = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(profile.InstructionTable + (uint)opcode * 4, 4));
            uint canonical = BinaryPrimitives.ReadUInt32LittleEndian(original.Read(0x1874F10u + (uint)opcode * 4, 4));
            Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(effect, "vm_table", opcode, actual));
        }
        foreach (int opcode in new[] { 0x3F, 0x41, 0x89, 0x93 })
        {
            uint actual = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(profile.InstructionTable + (uint)opcode * 4, 4));
            uint selected = NativeBattleCinematicDispatch.RequireSlot(effect, "vm_table", opcode, actual);
            bool noOp = NativeBattleCinematicDispatch.IsNoOp(effect, "vm_table", opcode, selected);
            Assert.Equal(effect != 261 || opcode == 0x93, noOp);
            if (noOp)
            {
                Assert.Equal((byte)0xC3, original.Read(actual, 1)[0]);
            }
        }
        if (effect is 261 or 262 or 263)
        {
            uint directory = effect switch { 261 => 0x187C20C, 262 => 0x187A2A4, 263 => 0x1877DAC, _ => throw new InvalidOperationException() };
            int count = BinaryPrimitives.ReadInt32LittleEndian(original.Read(directory, 4));
            int length = BinaryPrimitives.ReadInt32LittleEndian(original.Read(directory + 4u + 4u * (uint)count, 4));
            Assert.Equal(original.Read(directory, length), resources.AdditionalStaticData[directory].ToArray());
            Assert.Equal(original.Read(directory, length), resources.ReadStatic(directory, length).ToArray());
            uint handler = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(profile.InstructionTable + 0xAB * 4, 4));
            Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(effect, "vm_table", 0xAB, handler));
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(directory + (uint)length, 1).ToArray());
        }
        Assert.Equal(effect == 261 ? 6 : 3, resources.Files.Length);
    }
}
