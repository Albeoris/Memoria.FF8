using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAngelWing84SceneTests
{
    [Fact]
    public void Target_initialization_wraps_original_words_and_preserves_unknown_copied_padding()
    {
        NativeBattleAngelWing84SceneMemory scene = NativeBattleAngelWing84SceneMemory.Initialize(new(32767, -2, 20), new(-32768, 30, 20), null);
        Assert.Equal((ushort)32767, scene.Word(0));
        Assert.Equal((short)-2, scene.SignedWord(2));
        Assert.Equal((short)-32768, scene.SignedWord(8));
        Assert.Equal((ushort)3072, scene.Word(74));
        Assert.Throws<NotSupportedException>(() => scene.Settings.Read(4, 4));
        Assert.Throws<NotSupportedException>(() => scene.Settings.Read(12, 4));
        Assert.Null(scene.Camera);
        Assert.False(scene.CanRequestPhase(2));
        Assert.True(scene.CanRequestPhase(1));
        Assert.Throws<InvalidDataException>(() => scene.Word(84));
        scene.Validate();
    }

    [Fact]
    public void Requested_phase_commits_before_birth_and_visibility_restores_only_owned_bit()
    {
        var fixture = NativeBattleAngelWing84ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(2, 0x78DCE0, 72, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = appended.Hierarchy;
        hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteByte(41, 1));
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToArray();
        models[0] = models[0] with { Flags0 = 2 };
        models[3] = models[3] with { Flags0 = 0x42 };
        List<ushort> births = [];
        NativeBattleAngelWing84SceneInputs inputs = new(fixture.Inputs with { ReadModel = index => models[index] }, (current, operation) =>
        {
            switch (operation)
            {
                case NativeBattleAngelWing84PhaseBirth birth:
                    births.Add(birth.Phase);
                    break;
                case NativeBattleAngelWing84ModelFlags flags:
                    models[flags.Actor] = models[flags.Actor] with { Flags0 = flags.Flags };
                    break;
                default:
                    throw new InvalidOperationException("Unexpected scene operation in the selected handshake.");
            }
            return current;
        });
        NativeBattleAngelWing84Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools }, State = fixture.Frame.State with { Scene = NativeBattleAngelWing84SceneMemory.Initialize(new(0, 0, 0), new(1, 0, 0), null) } };
        frame = NativeBattleAngelWing84Scene.Advance(frame, appended.Address, inputs).Frame;
        Assert.Equal((ushort)1, frame.State.Scene!.Word(68));
        Assert.Equal((ushort)0, frame.State.Scene.Word(66));
        frame = NativeBattleAngelWing84Scene.Advance(frame, appended.Address, inputs).Frame;
        Assert.Equal((ushort)1, frame.State.Scene!.Word(66));
        Assert.Equal((ushort)0, frame.State.Scene.Word(64));
        Assert.Empty(births);
        frame = NativeBattleAngelWing84Scene.Advance(frame, appended.Address, inputs).Frame;
        Assert.Equal(new ushort[] { 1 }, births);
        Assert.Equal((ushort)0, frame.State.Scene!.Word(70));
        Assert.Equal((ushort)0x46, models[3].Flags0);
        Assert.Equal((ushort)0x42, frame.State.Scene.Word(44));
        Assert.Equal((ushort)2, models[0].Flags0);
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteByte(41, 11));
        NativeBattleAngelWing84SceneMemory scene = frame.State.Scene.WriteWord(64, 4).WriteWord(66, 4).WriteWord(68, 4);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools }, State = frame.State with { Scene = scene } };
        frame = JsonSerializer.Deserialize<NativeBattleAngelWing84Frame>(JsonSerializer.Serialize(frame))!;
        models[3] = models[3] with { Flags0 = 0x86 };
        frame = NativeBattleAngelWing84Scene.Advance(frame, appended.Address, inputs).Frame;
        Assert.Equal((ushort)0x82, models[3].Flags0);
        Assert.Equal((byte)12, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(appended.Address).ReadByte(41));
    }
}
