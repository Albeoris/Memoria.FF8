using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco100ModelTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);

    [Fact]
    public void Original_model_has_separate_addresses_and_current_body_pose_texture_contracts()
    {
        using GameAssets game = Installed();
        NativeBattleChoco97ModelResources resources = game.ReadNativeChoco100ModelResources();
        NativeBattleChoco97ModelResources prior = game.ReadNativeChoco97ModelResources();
        Assert.Same(resources, game.ReadNativeChoco100ModelResources());
        Assert.Equal(0x1656DE4u, resources.Model.Address);
        Assert.Equal(0x1529F18u, resources.TextureProgram.Address);
        Assert.Equal(resources.Model.Address + 0x4EDCu, resources.TextureTable.Address);
        Assert.Equal(prior.Model.Bytes.ToArray(), resources.Model.Bytes.ToArray());
        Assert.Equal(NativeBattleStorageSnapshots.Capture(prior.ModelTim).Data.ToArray(), NativeBattleStorageSnapshots.Capture(resources.ModelTim).Data.ToArray());
        Assert.Equal(NativeBattleStorageSnapshots.Capture(prior.TextureProgram).Data.ToArray(), NativeBattleStorageSnapshots.Capture(resources.TextureProgram).Data.ToArray());
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleEffectOwnedModelStep initialized = Initialize(resources, 0x200000);
        NativeBattleEffectOwnedModelStep second = Initialize(resources, 0x200140);
        Assert.NotEqual(initialized.Task.ReadDword(0x94), second.Task.ReadDword(0x94));
        Assert.Equal(initialized.State.ResourceAddress, second.State.ResourceAddress);
        for (short pose = 0; pose <= 5; pose++)
        {
            NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(initialized.Task.WriteDword(0x114, 6144).WriteDword(0x118, 6144).WriteDword(0x11C, 6144), initialized.State, resources.Model, pose);
            NativeBattleEffectOwnedModelStep restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectOwnedModelStep>(NativeBattleSnapshotJson.Serialize(selected))!;
            NativeBattleEffectOwnedModelDraw draw = Draw(selected, Scratch(), resources, rules);
            Assert.Equal(0xCD0, draw.ProjectedBytesWritten);
            Assert.NotEmpty(draw.Packets.Attempts);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(draw), NativeBattleSnapshotJson.Serialize(Draw(restored, Scratch(), resources, rules)));
            Assert.Equal(NativeBattleSnapshotJson.Serialize(NativeBattleEffectOwnedModel.AdvanceOnce8DD220(selected.Task, selected.State, resources.Model)), NativeBattleSnapshotJson.Serialize(NativeBattleEffectOwnedModel.AdvanceOnce8DD220(restored.Task, restored.State, resources.Model)));
        }
    }

    [Fact]
    public void Eleven_phases_keep_once_counter_and_request_order_with_real_model_cold_next()
    {
        using GameAssets game = Installed();
        NativeBattleChoco97ModelResources resources = game.ReadNativeChoco100ModelResources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleEffectOwnedModelStep initialized = Initialize(resources, 0x200000);
        Saved current = new(initialized.Task, initialized.State, Scratch());
        HashSet<byte> phases = [];
        HashSet<byte> poses = [];
        List<NativeBattleChoco97ModelOperation> operations = [];
        int copies = 0;
        for (int callback = 0; current.Task.ReadByte(0x29) != 7 && callback < 2048; callback++)
        {
            phases.Add(current.Task.ReadByte(0x29));
            StepResult next = Step(current, false, false);
            poses.Add(next.Next.Task.ReadByte(0x9C));
            operations.AddRange(next.Operations);
            copies += next.Advance.TextureCopies.Length;
            Assert.False(next.Advance.ReadyToComplete);
            current = next.Next;
        }
        Assert.Equal((byte)7, current.Task.ReadByte(0x29));
        Assert.Equal(Enumerable.Range(0, 7).Select(value => (byte)value), phases.Order());
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, poses.Order());
        Assert.True(copies > 0);
        NativeBattleChoco97ModelAppendAuxiliary auxiliary = Assert.Single(operations.OfType<NativeBattleChoco97ModelAppendAuxiliary>());
        Assert.Equal((0x715AB0u, 0x70, 0x255ECB8u), (auxiliary.Callback, auxiliary.TaskBytes, auxiliary.PoolAddress));
        Assert.Equal(0x15297F8u, Assert.Single(operations.OfType<NativeBattleChoco97ModelSound>()).DescriptorAddress);
        current = current with { Task = current.Task.WriteWord(0x136, 14).WriteByte(0xA2, current.Task.ReadByte(0xA3)) };
        StepResult held = Step(current, false, false);
        Assert.Equal((byte)7, held.Next.Task.ReadByte(0x29));
        Assert.Equal((ushort)15, held.Next.Task.ReadWord(0x136));
        StepResult threshold = Step(held.Next, false, false);
        Assert.Equal((byte)8, threshold.Next.Task.ReadByte(0x29));
        Assert.Equal((ushort)16, threshold.Next.Task.ReadWord(0x136));
        Assert.Equal((byte)4, threshold.Next.Task.ReadByte(0x9C));
        StepResult requestHeld = Step(threshold.Next, false, false);
        Assert.Equal((byte)8, requestHeld.Next.Task.ReadByte(0x29));
        Assert.Equal((ushort)17, requestHeld.Next.Task.ReadWord(0x136));
        StepResult request = Step(requestHeld.Next, true, false);
        Assert.Equal((byte)9, request.Next.Task.ReadByte(0x29));
        Assert.Equal((ushort)18, request.Next.Task.ReadWord(0x136));
        StepResult terminalHeld = Step(request.Next, true, false);
        Assert.Equal((byte)9, terminalHeld.Next.Task.ReadByte(0x29));
        Assert.Equal((ushort)19, terminalHeld.Next.Task.ReadWord(0x136));
        StepResult terminal = Step(terminalHeld.Next, true, true);
        Assert.Equal((byte)10, terminal.Next.Task.ReadByte(0x29));
        Assert.Equal((ushort)20, terminal.Next.Task.ReadWord(0x136));
        Assert.Null(terminal.Advance.Drawing);
        Assert.False(terminal.Advance.ReadyToComplete);
        Assert.True(Step(terminal.Next with { Task = terminal.Next.Task.WriteByte(0x28, 0) }, true, true).Advance.ReadyToComplete);
        Assert.Throws<NotSupportedException>(() => Step(terminal.Next with { Task = terminal.Next.Task.WriteByte(0x29, 11) }, true, true));
        StepResult wrapped = Step(current with { Task = current.Task.WriteWord(0x136, 0x7FFF) }, false, false);
        Assert.Equal((byte)7, wrapped.Next.Task.ReadByte(0x29));
        Assert.Equal((ushort)0x8000, wrapped.Next.Task.ReadWord(0x136));

        StepResult Step(Saved saved, bool request5, bool finish)
        {
            Saved cold = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(saved))!;
            StepResult first = Advance(saved);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(first), NativeBattleSnapshotJson.Serialize(Advance(cold)));
            return first;

            StepResult Advance(Saved source)
            {
                List<NativeBattleChoco97ModelOperation> emitted = [];
                NativeBattleChoco97ModelInputs inputs = new([10, 0, 20, 0, 30, 0, 40, 0], mode => mode == 1 || mode == 5 && finish, mode => mode != 5 || request5, Execute, model => Draw(model, source.Scratch, resources, rules));
                NativeBattleChoco97ModelAdvance next = NativeBattleChoco100Model.Advance715540(source.Task, source.State, resources, inputs);
                return new(next, new(next.Task, next.State, next.Drawing?.ProjectedScratch ?? source.Scratch), emitted.ToImmutableArray());

                NativeBattleEffectOwnedModelStep Execute(NativeBattleEffectOwnedModelStep model, NativeBattleChoco97ModelOperation operation)
                {
                    emitted.Add(operation);
                    return operation is NativeBattleChoco97ModelAppendAuxiliary ? model with { Task = model.Task.WriteByte(0x28, 1) } : model;
                }
            }
        }
    }

    private static NativeBattleEffectOwnedModelStep Initialize(NativeBattleChoco97ModelResources resources, uint address) => NativeBattleChoco100Model.Initialize(new(1, 0, null, 0x715540, new byte[0x140 - 12].ToImmutableArray()), address, resources);
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
