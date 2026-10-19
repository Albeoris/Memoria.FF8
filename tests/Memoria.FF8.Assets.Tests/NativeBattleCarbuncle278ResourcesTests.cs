using System.Buffers.Binary;
using System.Text.Json;
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
        NativeBattleModuleResource sprite = Assert.IsType<NativeBattleModuleResource>(resources.SparkleSprite);
        Assert.Equal(0x10BD044u, sprite.Address);
        Assert.Equal(300, sprite.Bytes.Length);
        Assert.Equal((byte)7, sprite.Kind);
        for (int phase = 0; phase < 11; phase++)
        {
            Assert.Equal(20, sprite.ProjectedFrame571C80(phase).Quads.Length);
        }
        Assert.Throws<InvalidDataException>(() => sprite.ProjectedFrame571C80(11));
        NativeBattleVector cameraAnchor = NativeBattleCarbuncle278Anchors.ReadVertex682270(selected.Task.WriteWord(0x60, 4096).WriteWord(0x68, 4096).WriteWord(0x70, 4096).WriteDword(0x7C, 5000), selected.Executable, model, 0, 176);
        Assert.NotEqual(new NativeBattleVector(0, 0, 0), cameraAnchor);
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
        NativeBattleModulePoolTask bodyTask = selected.Task.WriteWord(0x60, 4096).WriteWord(0x68, 4096).WriteWord(0x70, 4096).WriteDword(0x7C, 5000);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleEffectMemory bodyMemory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0x2E84, 0x1000), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), CarbuncleExecutableMemory = selected.Executable, Carbuncle278 = NativeBattleCarbuncle278State.FromColdProcess() with { ModelBuffer81F8 = 0x20DFAB8, PacketCursor81BC = 0x20E3984 } };
        NativeBattleCarbuncle278ModelDraw body = NativeBattleCarbuncle278Model.Draw6A79B0(bodyMemory, bodyTask, model, new(identity, 288, 160, 108), true);
        Assert.Equal(492 * 8, body.Memory.Arena!.Read(0x2E84, 492 * 8).Length);
        Assert.Throws<NotSupportedException>(() => body.Memory.Arena.Read(0x2E84 + 492 * 8, 0xA0));
        Assert.Equal(0x10BBAD0u, body.Memory.PrimitiveProcess!.ModelRendererRegisters!.SourceCursor);
        Assert.Equal(JsonSerializer.Serialize(selected.Executable), JsonSerializer.Serialize(body.Memory.CarbuncleExecutableMemory));
        Assert.Throws<NotSupportedException>(() => bodyMemory.Arena!.Read(0x2E84, 8));
    }
}
