using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco99ModelTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);

    [Fact]
    public void Original_model_has_separate_addresses_and_current_body_pose_texture_contracts()
    {
        using GameAssets game = Installed();
        NativeBattleChoco97ModelResources resources = game.ReadNativeChoco99ModelResources();
        NativeBattleChoco97ModelResources prior = game.ReadNativeChoco97ModelResources();
        Assert.Same(resources, game.ReadNativeChoco99ModelResources());
        Assert.Equal(0x1665B08u, resources.Model.Address);
        Assert.Equal(0x152AED0u, resources.TextureProgram.Address);
        Assert.Equal(resources.Model.Address + 0x4EDCu, resources.TextureTable.Address);
        Assert.Equal(prior.Model.Bytes.ToArray(), resources.Model.Bytes.ToArray());
        Assert.Equal(prior.ModelTim.Data.ToArray(), resources.ModelTim.Data.ToArray());
        Assert.Equal(prior.TextureProgram.Data.ToArray(), resources.TextureProgram.Data.ToArray());
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleEffectOwnedModelStep initialized = Initialize(resources, 0x256D750);
        NativeBattleEffectOwnedModelStep second = Initialize(resources, 0x256D890);
        Assert.NotEqual(initialized.Task.ReadDword(0x94), second.Task.ReadDword(0x94));
        Assert.Equal(initialized.State.ResourceAddress, second.State.ResourceAddress);
        for (short pose = 0; pose <= 4; pose++)
        {
            NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(initialized.Task.WriteDword(0x114, 6144).WriteDword(0x118, 6144).WriteDword(0x11C, 6144), initialized.State, resources.Model, pose);
            NativeBattleEffectOwnedModelDraw draw = Draw(selected, Scratch(), resources, rules);
            Assert.Equal(0xCD0, draw.ProjectedBytesWritten);
            Assert.NotEmpty(draw.Packets.Attempts);
            NativeBattleEffectOwnedModelStep restored = JsonSerializer.Deserialize<NativeBattleEffectOwnedModelStep>(JsonSerializer.Serialize(selected))!;
            Assert.Equal(JsonSerializer.Serialize(draw), JsonSerializer.Serialize(Draw(restored, Scratch(), resources, rules)));
            Assert.Equal(JsonSerializer.Serialize(NativeBattleEffectOwnedModel.AdvanceOnce8DD220(selected.Task, selected.State, resources.Model)), JsonSerializer.Serialize(NativeBattleEffectOwnedModel.AdvanceOnce8DD220(restored.Task, restored.State, resources.Model)));
        }
    }

    [Fact]
    public void Nine_phases_never_select_pose5_and_terminal_branch_preserves_pose_cursor_without_looping()
    {
        using GameAssets game = Installed();
        NativeBattleChoco97ModelResources resources = game.ReadNativeChoco99ModelResources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleEffectOwnedModelStep initialized = Initialize(resources, 0x256D750);
        Saved current = new(initialized.Task, initialized.State, Scratch());
        HashSet<byte> phases = [];
        HashSet<byte> poses = [];
        List<NativeBattleChoco97ModelOperation> operations = [];
        int textureCopies = 0;
        for (int callback = 0; current.Task.ReadByte(0x29) != 7 && callback < 2048; callback++)
        {
            phases.Add(current.Task.ReadByte(0x29));
            StepResult next = Step(current, false);
            Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(current))!;
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(Step(restored, false)));
            poses.Add(next.Next.Task.ReadByte(0x9C));
            operations.AddRange(next.Operations);
            textureCopies += next.Advance.TextureCopies.Length;
            Assert.False(next.Advance.ReadyToComplete);
            current = next.Next;
        }
        Assert.Equal((byte)7, current.Task.ReadByte(0x29));
        Assert.Equal(Enumerable.Range(0, 7).Select(value => (byte)value).ToArray(), phases.Order().ToArray());
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, poses.Order().ToArray());
        Assert.True(textureCopies > 0);
        NativeBattleChoco97ModelAppendAuxiliary aux = Assert.Single(operations.OfType<NativeBattleChoco97ModelAppendAuxiliary>());
        Assert.Equal((0x71EE40u, 0xB0, 0x2565C20u), (aux.Callback, aux.TaskBytes, aux.PoolAddress));
        Assert.Equal(0x1529FA8u, Assert.Single(operations.OfType<NativeBattleChoco97ModelSound>()).DescriptorAddress);
        StepResult waiting = Step(current, false);
        Assert.Equal((byte)7, waiting.Next.Task.ReadByte(0x29));
        Assert.Equal(unchecked((ushort)(current.Task.ReadWord(0x136) + 1)), waiting.Next.Task.ReadWord(0x136));
        Assert.NotNull(waiting.Advance.Drawing);
        StepResult terminal = Step(current, true);
        Saved terminalRestored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(current))!;
        Assert.Equal(JsonSerializer.Serialize(terminal), JsonSerializer.Serialize(Step(terminalRestored, true)));
        Assert.Equal((byte)8, terminal.Next.Task.ReadByte(0x29));
        Assert.Null(terminal.Advance.Drawing);
        Assert.Empty(terminal.Advance.TextureCopies);
        Assert.False(terminal.Advance.ReadyToComplete);
        foreach (int offset in new[] { 0x9C, 0x9D, 0x9E, 0x9F, 0xA0, 0xA1, 0xA2, 0xA3, 0x12C, 0x12D, 0x130, 0x131, 0x132, 0x133, 0x136, 0x137, 0x138, 0x139 })
        {
            Assert.Equal(current.Task.ReadByte(offset), terminal.Next.Task.ReadByte(offset));
        }
        Assert.Equal(JsonSerializer.Serialize(current.State.DecodedPose), JsonSerializer.Serialize(terminal.Next.State.DecodedPose));
        Assert.False(Step(terminal.Next, true).Advance.ReadyToComplete);
        Assert.True(Step(terminal.Next with { Task = terminal.Next.Task.WriteByte(0x28, 0) }, true).Advance.ReadyToComplete);
        Assert.Throws<NotSupportedException>(() => Step(terminal.Next with { Task = terminal.Next.Task.WriteByte(0x29, 9) }, true));

        StepResult Step(Saved saved, bool finish)
        {
            List<NativeBattleChoco97ModelOperation> emitted = [];
            NativeBattleChoco97ModelInputs inputs = new([10, 0, 20, 0, 30, 0, 40, 0], mode => mode == 1 || mode == 5 && finish, _ => true, Execute, model => Draw(model, saved.Scratch, resources, rules));
            NativeBattleChoco97ModelAdvance next = NativeBattleChoco99Model.Advance71E8D0(saved.Task, saved.State, resources, inputs);
            return new(next, new(next.Task, next.State, next.Drawing?.ProjectedScratch ?? saved.Scratch), emitted.ToImmutableArray());

            NativeBattleEffectOwnedModelStep Execute(NativeBattleEffectOwnedModelStep model, NativeBattleChoco97ModelOperation operation)
            {
                emitted.Add(operation);
                return operation is NativeBattleChoco97ModelAppendAuxiliary ? model with { Task = model.Task.WriteByte(0x28, 1) } : model;
            }
        }
    }

    private static NativeBattleEffectOwnedModelStep Initialize(NativeBattleChoco97ModelResources resources, uint address) => NativeBattleChoco99Model.Initialize(new(1, 0, null, 0x71E8D0, new byte[0x140 - 12].ToImmutableArray()), address, resources);
    private static ImmutableArray<byte> Scratch() => Enumerable.Repeat((byte)0xA5, 0xCD0).ToImmutableArray();
    private static NativeBattleEffectOwnedModelDraw Draw(NativeBattleEffectOwnedModelStep model, ImmutableArray<byte> scratch, NativeBattleChoco97ModelResources resources, NativeBattleGraphicsRules rules) => NativeBattleEffectOwnedModelDrawing.Draw7558A0(model.Task, model.State, resources.Model, View, Projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8(), rules.SummonColors!, scratch);
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState State, ImmutableArray<byte> Scratch);
    private sealed record StepResult(NativeBattleChoco97ModelAdvance Advance, Saved Next, ImmutableArray<NativeBattleChoco97ModelOperation> Operations);
}
