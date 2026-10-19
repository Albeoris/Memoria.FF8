using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAngelWing84BodyResourcesTests
{
    [Fact]
    public void Original_body_initializes_and_advances_its_owned_pose_without_actor_row_substitution()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleAngelWing84BodyResources resources = game.ReadNativeAngelWing84BodyResources();
        resources.Validate();
        Assert.Equal(22, resources.Model.PoseSource.BoneCount);
        Assert.Equal(14, resources.OriginBones.Length);
        Assert.Equal((ushort)4, resources.OriginBones[0]);
        Assert.Equal((ushort)21, resources.OriginBones[^1]);
        Assert.Equal(0x32808080u, resources.DefaultColor44);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25C9ED8, 0x25C9D80, 340, 1);
        (pool, int slot) = pool.Append(0x7961E0);
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(pool.Slots[slot], 0x25C9D80, resources.Model, 3, resources.DefaultColor44);
        NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(initialized.Task, initialized.State, resources.Model, 0);
        Assert.NotNull(selected.State.DecodedPose);
        NativeBattleEffectOwnedModelStep restored = JsonSerializer.Deserialize<NativeBattleEffectOwnedModelStep>(JsonSerializer.Serialize(selected))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffectOwnedModel.AdvanceOnce8DD220(selected.Task, selected.State, resources.Model)), JsonSerializer.Serialize(NativeBattleEffectOwnedModel.AdvanceOnce8DD220(restored.Task, restored.State, resources.Model)));
    }

    [Fact]
    public void Original_body_controller_keeps_origin_WORD34_and_completes_native_color_fade()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleAngelWing84BodyResources resources = game.ReadNativeAngelWing84BodyResources();
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x25C9ED8, 0x25C9D80, 340, 1)]);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(0, 0x7961E0, 340);
        NativeBattleOriginalEffectState effect = new(84, 83, new(84, 0x78D7B0, 0x78D790), null, null) { Pools = append.Hierarchy.Pools };
        NativeBattleAngelWing84Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(0, 0, null, null, null, null, null));
        frame = NativeBattleAngelWing84Body.Initialize(frame, append.Address, resources);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleAngelWing84Inputs controller = new(NativeBattleActionPacketsState.Fresh(), camera, index => new(index, 3) { Transform = new(new(0, 0, 0), new(10, 20, 30), 40, 0, 4096) }, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        List<NativeBattleAngelWing84BodyOperation> operations = [];
        NativeBattleAngelWing84BodyInputs inputs = new(resources, controller, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        frame = NativeBattleAngelWing84Body.Advance(frame, append.Address, inputs).Frame;
        hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(append.Address);
        Assert.Equal((ushort)10, task.ReadWord(76));
        Assert.Equal((ushort)20, task.ReadWord(78));
        Assert.Equal((ushort)30, task.ReadWord(80));
        Assert.Equal((ushort)40, task.ReadWord(82));
        Assert.Equal((ushort)0, frame.State.BodyShaderAngle);
        Assert.Collection(operations, operation => Assert.IsType<NativeBattleAngelWing84InitializeBodyShader>(operation), operation => Assert.IsType<NativeBattleAngelWing84BindCaster>(operation), operation => Assert.IsType<NativeBattleAngelWing84DrawBody>(operation));
        task = task.WriteByte(41, 4).WriteWord(314, 3840);
        hierarchy = hierarchy.Write(append.Address, task);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleAngelWing84Frame restored = JsonSerializer.Deserialize<NativeBattleAngelWing84Frame>(JsonSerializer.Serialize(frame))!;
        operations.Clear();
        NativeBattleAngelWing84LeafStep terminal = NativeBattleAngelWing84Body.Advance(frame, append.Address, inputs);
        Assert.Empty(operations);
        Assert.Equal(2, terminal.Result);
        task = new NativeBattleEffectHierarchy(terminal.Frame.Effect.Pools).Read(append.Address);
        Assert.Equal((ushort)4096, task.ReadWord(314));
        Assert.Equal(0x02000000u, task.ReadDword(88));
        Assert.Equal(0x32000000u, task.ReadDword(92));
        Assert.Equal((byte)0, task.ReadByte(55));
        Assert.Equal(JsonSerializer.Serialize(terminal), JsonSerializer.Serialize(NativeBattleAngelWing84Body.Advance(restored, append.Address, inputs)));
    }

}
