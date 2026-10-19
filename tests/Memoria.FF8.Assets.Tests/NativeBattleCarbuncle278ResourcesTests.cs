using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCarbuncle278ResourcesTests
{
    [Fact]
    public void Installed_resources_supply_complete_original_mirror_model_and_signed_bounds()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        NativeBattleCarbuncle278Resources resources = game.ReadNativeCarbuncle278Resources();
        Assert.Same(resources, game.ReadNativeCarbuncle278Resources());
        resources.Validate();
        Assert.Equal(0x10C0D08u, resources.MirrorBoundsMesh.Address);
        Assert.Equal(115u, BinaryPrimitives.ReadUInt32LittleEndian(resources.MirrorBoundsMesh.Bytes.AsSpan().Slice(4)));
        Assert.Equal(new NativeBattleEffectLocalBounds(new(-1606, 0, -400), new(1606, 0, 3200)), NativeBattleStaticMeshBounds.Read6D6070(resources.MirrorBoundsMesh.Bytes.AsSpan()));
        Assert.Throws<InvalidDataException>(() => (resources with { MirrorBoundsMesh = resources.MirrorBoundsMesh with { Address = 0 } }).Validate());
        NativeBattleEffectOwnedModelResource model = Assert.IsType<NativeBattleEffectOwnedModelResource>(resources.CreatureModel);
        Assert.Equal(30, model.PoseSource.BoneCount);
        Assert.Equal(0x5F04, model.Bytes.Length);
        Assert.NotEmpty(model.Geometry);
        NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(0x2508188, 0x20E235C, 0x5E0, 1).Slots[0];
        task = NativeBattleCarbuncle278Creature.Initialize6EC060(task, 0x20E235C, model);
        NativeBattleCarbuncleExecutableMemory executable = NativeBattleCarbuncleExecutableMemory.FromOriginal(model);
        NativeBattleCarbuncle278CreaturePose selected = NativeBattleCarbuncle278Creature.Select6574D0(task, executable, model, 0);
        Assert.Equal((byte)0, selected.Task.ReadByte(0x8C));
        Assert.Equal((byte)4, selected.Task.ReadByte(0x8D));
        Assert.Equal((byte)1, selected.Task.ReadByte(0x92));
        Assert.True(selected.Task.ReadByte(0x93) > 0);
        Assert.Equal(30, selected.Executable.Read(NativeBattleCarbuncleExecutableMemory.Address, 1)[0]);
        NativeVibrationBank vibration = Assert.IsType<NativeVibrationBank>(resources.Vibration);
        Assert.Equal(64, vibration.Offsets.Length);
        Assert.Equal(0x100, vibration.Offsets[1]);
        Assert.Equal(1, vibration.Offsets.Count(offset => offset != 0));
        Assert.Equal((ushort)54, vibration.ReadWord(0x100));
        Assert.Equal((ushort)4, vibration.ReadWord(0x102));
        Assert.Equal(0x140, vibration.Bytes.Length);
        Assert.Equal(115 * 8, resources.MirrorAlternateVertices.Length);
        Assert.Equal(0x10BFB08u, resources.SecondaryMesh!.Address);
        Assert.Equal(0x1200, resources.SecondaryMesh.Bytes.Length);
    }
}
