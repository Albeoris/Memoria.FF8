using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWindBlast29ResourcesTests
{
    [Fact]
    public void Original_Wind_Blast_resources_preserve_source_extent_relocation_and_all_draw_groups()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Wind Blast.");
        using GameAssets game = new(directory!);
        NativeBattleWindBlast29Resources resources = game.ReadNativeWindBlast29Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeWindBlast29Resources());
        Assert.Equal(276, resources.Animation.Data.Length);
        Assert.Equal(0x61B65u, resources.Sound);
        Assert.Equal(14192, resources.Executable.Data.Length);
        Assert.Equal(0x17D5F4Cu, resources.Executable.ResourceAddress + (uint)resources.Executable.Data.Length);
        Assert.Equal((ushort)29, resources.GenericNodes().Effect);
        Assert.Same(resources.Executable, resources.GenericNodes().Executable);
        NativeBattleCuragaExecutableState cold = new(0, null) { ResourceAddress = resources.Executable.ResourceAddress };
        NativeBattleCuragaExecutableState initialized = cold.Initialize87EA20(resources.Executable);
        Assert.Equal(1u, initialized.InitializationFlag);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan()));
        Assert.Equal(14192, initialized.Data!.Length);
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                int offset = 4 + table * 64 + index * 4;
                uint original = BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(offset));
                uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(initialized.Read(resources.Executable.ResourceAddress + (uint)offset, 4));
                Assert.Equal(original == 0 ? 0u : resources.Executable.ResourceAddress + original, pointer);
            }
        }
        int[][] expectedGroups = [[0, 0, 0, 1, 0, 0, 0, 0], [0, 0, 0, 0, 18, 0, 0, 0], [0, 0, 0, 0, 18, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 64]];
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int index = 1; index <= 4; index++)
        {
            int mesh = BinaryPrimitives.ReadInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(4 + 128 + index * 4));
            int cursor = mesh + BinaryPrimitives.ReadInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(mesh));
            for (int group = 0; group < 8; group++)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(cursor));
                Assert.Equal(expectedGroups[index - 1][group], count);
                cursor += 4 + count * strides[group];
            }
        }
        NativeBattleWindBlast29Resources restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWindBlast29Resources>(SnapshotJson(resources))!);
        restored.Validate();
        Assert.Equal(SnapshotJson(resources), SnapshotJson(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { Executable = resources.Executable with { ResourceAddress = 0x17D5F4C } }).Validate());
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([29])[28];
        Assert.Equal(0x8794A0u, binding.Entry);
        Assert.Equal("mag028.tim", binding.InitializerAsset!.Name);
        Assert.Equal(166432, binding.InitializerAsset.Data.Length);
        Assert.NotNull(binding.WindBlast29Resources);
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
