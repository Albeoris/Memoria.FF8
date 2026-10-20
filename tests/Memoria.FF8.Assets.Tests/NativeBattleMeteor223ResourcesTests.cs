using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMeteor223ResourcesTests
{
    [Fact]
    public void Original_Meteor_final_vibration_section_retains_only_actual_file_bytes_and_proved_handlers()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory native = new(executable);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(223);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([223])[222];
        Assert.Equal(0xA8F890u, binding.Entry);
        Assert.Equal(0xA8F860u, binding.Initializer);
        NativeBattleEffectInitializerAsset first = binding.InitializerAssets!.Value[0];
        Assert.Equal("mag222_b.00", first.Name);
        NativeBattleCinematicResources resources = assets.ReadNativeCinematicResources(223);
        resources.Validate();
        NativeVibrationBank vibration = resources.Vibration!;
        int start = BinaryPrimitives.ReadInt32LittleEndian(first.Data.AsSpan().Slice(0x24));
        Assert.Equal(first.Data.Length - start, vibration.Bytes.Length);
        Assert.Equal(first.Data.AsSpan().Slice(start).ToArray(), vibration.Bytes.ToArray());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(first.Data.AsSpan())), vibration.SourceHash);
        Assert.Equal(7, resources.TextureProfile.Count);
        Assert.Equal(native.Read(profile.InstructionTable, 0x147 * 4), resources.ReadStatic(profile.InstructionTable, 0x147 * 4).ToArray());
        foreach ((string table, int index, uint address) in new[] { ("vm_table", 0x35, 0xA996A0u), ("vm_table", 0xBB, 0xA997A0u), ("vm_table", 0xBD, 0xA99730u), ("draw_table", 25, 0xA90AA0u), ("draw_table", 28, 0xA91F20u) })
        {
            uint basis = table == "vm_table" ? profile.InstructionTable : profile.DrawTable;
            uint selected = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(basis + (uint)(4 * index), 4));
            Assert.Equal(address, selected);
            Assert.Equal(address, NativeBattleCinematicDispatch.RequireSlot(223, table, index, selected));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Physical_EOF_requires_only_reached_interpolation_bytes_and_never_supplies_padding(bool sentinel)
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        byte[] bytes = new byte[0x28 + 10];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x24), 0x28);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x28), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x2C), 2);
        bytes[^2] = 80;
        bytes[^1] = sentinel ? (byte)255 : (byte)0;
        if (!sentinel)
        {
            Assert.Throws<InvalidDataException>(() => NativeBattleCinematicResourcesReader.Read(executable, bytes.ToImmutableArray(), NativeBattleCinematicProfile.ForEffect(201)));
            return;
        }
        NativeVibrationBank bank = NativeBattleCinematicResourcesReader.Read(executable, bytes.ToImmutableArray(), NativeBattleCinematicProfile.ForEffect(201)).Vibration!;
        Assert.Equal(10, bank.Bytes.Length);
        NativeVibrationEnqueue queued = NativeVibrationProcessState.FromColdProcess().Enqueue(bank, 0, 128);
        Assert.Equal(2, queued.State.Slots[0].A.Bytes.Length);
        Assert.Equal(new byte[] { 80, 255 }, queued.State.Slots[0].A.Bytes.ToArray());
    }
}
