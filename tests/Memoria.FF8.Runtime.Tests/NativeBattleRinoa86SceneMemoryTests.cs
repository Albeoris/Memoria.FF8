using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa86SceneMemoryTests
{
    [Fact]
    public void Original_scene_uses_the_loaded_enemy_box_center_and_copies_the_caster_fourth_WORD()
    {
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToArray();
        models[0] = models[0] with { Transform = new(default, new(10, 20, 30), -73, 0, 4096) };
        models[3] = models[3] with { Flags0 = 2, Transform = new(default, new(-101, -200, -100), 0, 0, 4096) };
        models[4] = models[4] with { Flags0 = 2, Transform = new(default, new(100, -400, 99), 0, 0, 4096) };
        NativeBattleRinoa86SceneMemory scene = NativeBattleRinoa86SceneMemory.Initialize(0, models);
        Assert.Equal((short)10, scene.SignedWord(0));
        Assert.Equal((short)20, scene.SignedWord(2));
        Assert.Equal((short)30, scene.SignedWord(4));
        Assert.Equal((short)-73, scene.SignedWord(6));
        Assert.Equal((short)0, scene.SignedWord(8));
        Assert.Equal((short)0, scene.SignedWord(10));
        Assert.Equal((short)0, scene.SignedWord(12));
        Assert.Equal((ushort)0, scene.Word(14));
        Assert.InRange(scene.Word(74), (ushort)0, (ushort)4095);
        NativeBattleRinoa86SceneMemory restored = JsonSerializer.Deserialize<NativeBattleRinoa86SceneMemory>(JsonSerializer.Serialize(scene))!;
        Assert.Equal(scene.Settings.Read(0, 84), restored.Settings.Read(0, 84));
        Assert.Throws<InvalidDataException>(() => scene.Word(84));
    }

    [Fact]
    public void No_loaded_enemy_retains_the_native_zero_center_and_phase_request_needs_both_handshake_words()
    {
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToArray();
        models[0] = models[0] with { Transform = new(default, new(10, 20, 30), 31, 0, 4096) };
        NativeBattleRinoa86SceneMemory scene = NativeBattleRinoa86SceneMemory.Initialize(0, models);
        Assert.Equal(new byte[8], scene.Settings.Read(8, 8));
        Assert.True(scene.CanRequestPhase(1));
        scene = scene.WriteWord(68, 1);
        Assert.False(scene.CanRequestPhase(2));
        scene = scene.WriteWord(64, 1);
        Assert.True(scene.CanRequestPhase(2));
        Assert.False(scene.WriteWord(68, 2).CanRequestPhase(2));
    }
}
