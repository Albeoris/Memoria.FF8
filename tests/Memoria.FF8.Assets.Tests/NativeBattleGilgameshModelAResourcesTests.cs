using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshModelAResourcesTests
{
    [Fact]
    public void Original_modelA_initializes_while_paused_with_argument32_and_retains_partial_identity_padding()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectOwnedModelResource resource = game.ReadNativeGilgameshModelResources().StreamModel746;
        NativeBattleGilgameshModelMemory memory = Retained();
        NativeBattleModulePoolTask task = Task(0);
        NativeBattleGilgameshModelAStep result = Run(memory, NativeBattleEffectArena.Clear(), task, resource, 0x201);
        Assert.Same(task, result.UpdatedTask);
        NativeBattleCactuarRawModelState model = result.ModelMemory.Read(0x21FF760).State;
        Assert.Equal((ushort)32, BinaryPrimitives.ReadUInt16LittleEndian(model.Control.AsSpan().Slice(2)));
        Assert.Equal((byte)6, model.Model[0]);
        Assert.Equal((byte)1, model.Model[0x72]);
        Assert.Equal((ushort)0x8080, BinaryPrimitives.ReadUInt16LittleEndian(model.Model.AsSpan().Slice(0x52)));
        Assert.Equal(new byte[12], model.Model.AsSpan().Slice(0x54, 12).ToArray());
        Assert.Equal((byte)0xA5, model.Control[0]);
        NativeBattleCactuarRawModelState advanced = NativeBattleCactuarRawModel.Advance5A8FD0(model, resource, new(0x21FF4B8, 4)).State;
        Assert.Equal((byte)2, advanced.Model[0x72]);
        NativeBattleGilgameshModelAStep repeated = Run(result.ModelMemory.Write(0x21FF760, advanced), result.Arena, task, resource, 0x201);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(result.ModelMemory), NativeBattleSnapshotJson.Serialize(repeated.ModelMemory));
    }

    [Fact]
    public void Original505_world_vertices_and983_radial_labels_preserve_zero_fourth_WORD_and_both_unused_tails()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectOwnedModelResource resource = game.ReadNativeGilgameshModelResources().StreamModel746;
        NativeBattleGilgameshModelAStep initialized = Run(Retained(), NativeBattleEffectArena.Clear(), Task(0), resource, 0x201);
        NativeBattleEffectArena arena = initialized.Arena.Write(0x31800, Enumerable.Repeat((byte)0xA5, 505 * 8 + 32).ToArray());
        NativeBattleModulePoolTask task = Task(25);
        NativeBattleGilgameshModelAStep labels = NativeBattleGilgameshModelA.Step595EE0(initialized.ModelMemory, arena, task, resource, null!, null!, null, 0x201);
        Assert.Same(task, labels.UpdatedTask);
        Assert.Empty(labels.Packets.Attempts);
        Assert.Equal(0x22000 + 1966, labels.LabelEndOffset);
        Assert.Equal(505, resource.Geometry[0].Vertices.Length);
        Assert.Equal(982, resource.Geometry[0].Polygons.Count(polygon => !polygon.Quad));
        Assert.Single(resource.Geometry[0].Polygons.Where(polygon => polygon.Quad));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 32).ToArray(), labels.Arena.Read(0x31800 + 505 * 8, 32));
        for (int index = 0; index < 505; index++)
        {
            Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(labels.Arena.Read(0x31800 + index * 8 + 6, 2)));
        }
        for (int offset = 1966; offset < 2048; offset += 2)
        {
            Assert.Equal((ushort)0x4000, BinaryPrimitives.ReadUInt16LittleEndian(labels.Arena.Read(0x22000 + offset, 2)));
        }
        NativeBattleGilgameshModelAStep repeated = Run(labels.ModelMemory, labels.Arena, task, resource, 0x201);
        Assert.Equal(labels.Arena.Read(0x22000, 2048), repeated.Arena.Read(0x22000, 2048));
    }

    [Fact]
    public void Original_radial_reveal_reaches_all_twenty_phases_with_same_json_next_and_keeps_current_pose()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectOwnedModelResource resource = game.ReadNativeGilgameshModelResources().StreamModel746;
        NativeBattleGilgameshModelAStep initial = Run(Retained(), NativeBattleEffectArena.Clear(), Task(0), resource, 0x201);
        NativeBattleGilgameshModelAStep current = Run(initial.ModelMemory, initial.Arena, Task(25), resource, 0x201);
        int accepted = 0;
        for (ushort counter = 26; counter <= 44; counter++)
        {
            NativeBattleModulePoolTask task = Task(counter);
            NativeBattleGilgameshModelAStep? restored = counter == 30 ? NativeBattleSnapshotJson.Deserialize<NativeBattleGilgameshModelAStep>(NativeBattleSnapshotJson.Serialize(current)) : null;
            NativeBattleGilgameshModelAStep step = Run(current.ModelMemory, current.Arena, task, resource, 0x201);
            Assert.Same(task, step.UpdatedTask);
            Assert.Equal(0x22000 + 1966, step.LabelEndOffset);
            Assert.InRange(step.Packets.Attempts.Length, 0, 983);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(initial.ModelMemory.Read(0x21FF760).State.DecodedPose), NativeBattleSnapshotJson.Serialize(step.ModelMemory.Read(0x21FF760).State.DecodedPose));
            accepted += step.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            if (restored is not null)
            {
                Assert.Equal(NativeBattleSnapshotJson.Serialize(step), NativeBattleSnapshotJson.Serialize(Run(restored.ModelMemory, restored.Arena, task, resource, 0x201)));
            }
            current = step;
        }
        Assert.True(accepted > 0);
        NativeBattleGilgameshModelAStep visible = Run(current.ModelMemory, current.Arena, Task(45), resource, 0x201);
        Assert.Equal(0, visible.ModelMemory.Read(0x21FF760).State.Model[0] & 12);
    }

    [Fact]
    public void Original_model_mask_skips_world_labels_and_reveal_without_reading_unknown_scratch_but_pose_must_exist()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectOwnedModelResource resource = game.ReadNativeGilgameshModelResources().StreamModel746;
        NativeBattleGilgameshModelAStep initial = Run(Retained(), NativeBattleEffectArena.Clear(), Task(0), resource, 0x201);
        NativeBattleCactuarRawModelState model = initial.ModelMemory.Read(0x21FF760).State;
        byte[] bytes = model.Model.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x7C), 0);
        model = model with { Model = bytes.ToImmutableArray() };
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x31800, 505 * 8).MarkUnknown(0x22000, 2048);
        NativeBattleGilgameshModelADraw draw = NativeBattleGilgameshModelA.Draw596010(model, resource, arena, null!, null!, null, 1);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(arena), NativeBattleSnapshotJson.Serialize(draw.Arena));
        Assert.Equal(0x22000, draw.LabelEndOffset);
        Assert.Empty(draw.Packets.Attempts);
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshModelA.Draw596010(model with { DecodedPose = null }, resource, arena, null!, null!, null, 1));
    }

    private static NativeBattleGilgameshModelAStep Run(NativeBattleGilgameshModelMemory memory, NativeBattleEffectArena arena, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource, uint flags) => NativeBattleGilgameshModelA.Step595EE0(memory, arena, task, resource, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096), new(null, 288, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), flags);
    private static NativeBattleModulePoolTask Task(ushort counter) => new NativeBattleModulePoolTask(1, 0, null, 0x595EE0, new byte[24].ToImmutableArray()).WriteWord(12, counter);

    private static NativeBattleGilgameshModelMemory Retained()
    {
        NativeBattleGilgameshModelMemory memory = NativeBattleGilgameshModelMemory.FromColdProcess();
        NativeBattleCactuarRawModelState original = memory.Read(0x21FF760).State;
        return memory.Write(0x21FF760, original with { Model = Enumerable.Repeat((byte)0x80, 0x90).ToImmutableArray(), Control = Enumerable.Repeat((byte)0xA5, 0x34).ToImmutableArray() });
    }
}
