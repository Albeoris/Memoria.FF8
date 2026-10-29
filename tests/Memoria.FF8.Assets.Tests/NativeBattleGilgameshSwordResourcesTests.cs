using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshSwordResourcesTests
{
    [Fact]
    public void Original_four_swords_initialize_and_draw_while_paused_with_signed_selector3_and_retained_matrix_padding()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        ImmutableArray<NativeBattleEffectOwnedModelResource> models = game.ReadNativeGilgameshModelResources().StaticModels;
        NativeBattleModulePoolTask task = Task(0);
        NativeBattleGilgameshSwordStep step = Run(Retained(), Scratch(), task, models, 0x201);
        Assert.Same(task, step.UpdatedTask);
        Assert.Equal(4, step.Packets.Length);
        for (int index = 0; index < 4; index++)
        {
            NativeBattleCactuarRawModelState model = step.ModelMemory.Read(Address(index)).State;
            Assert.Equal((byte)3, model.Model[0x6C]);
            Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(model.Control.AsSpan().Slice(2)));
            Assert.Equal((byte)1, model.Model[0x72]);
            Assert.Equal(index == 0 ? (byte)6 : (byte)2, model.Model[0]);
            Assert.Equal((ushort)0x8080, BinaryPrimitives.ReadUInt16LittleEndian(model.Model.AsSpan().Slice(0x52)));
            Assert.Equal(new byte[12], model.Model.AsSpan().Slice(0x54, 12).ToArray());
            Assert.Equal((byte)0xA5, model.Control[0]);
        }
        NativeBattleGilgameshSwordStep repeated = Run(step.ModelMemory, step.Arena, task, models, 0x201);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(step.ModelMemory), NativeBattleSnapshotJson.Serialize(repeated.ModelMemory));
        Assert.Equal(NativeBattleSnapshotJson.Serialize(step.Packets), NativeBattleSnapshotJson.Serialize(repeated.Packets));
        for (int index = 0; index < 76; index++)
        {
            Assert.Throws<NotSupportedException>(() => step.Arena.Read(0x31800 + index * 8 + 6, 2));
        }
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 32).ToArray(), step.Arena.Read(0x31800 + 76 * 8, 32));
    }

    [Fact]
    public void Original_sword_flag8_timing_follows_draws_and_pause_and_never_changes_sibling_flags()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        ImmutableArray<NativeBattleEffectOwnedModelResource> models = game.ReadNativeGilgameshModelResources().StaticModels;
        NativeBattleGilgameshSwordStep initial = Run(Retained(), Scratch(), Task(0), models, 0x201);
        NativeBattleGilgameshSwordStep pausedTen = Run(initial.ModelMemory, initial.Arena, Task(10), models, 0x201);
        NativeBattleGilgameshSwordStep ten = Run(initial.ModelMemory, initial.Arena, Task(10), models, 0);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(pausedTen.Packets), NativeBattleSnapshotJson.Serialize(ten.Packets));
        Assert.Equal((byte)6, pausedTen.ModelMemory.Read(Address(0)).State.Model[0]);
        Assert.Equal((byte)14, ten.ModelMemory.Read(Address(0)).State.Model[0]);
        NativeBattleGilgameshSwordStep pausedFortyFive = Run(ten.ModelMemory, ten.Arena, Task(45), models, 0x201);
        NativeBattleGilgameshSwordStep fortyFive = Run(ten.ModelMemory, ten.Arena, Task(45), models, 0);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(pausedFortyFive.Packets), NativeBattleSnapshotJson.Serialize(fortyFive.Packets));
        Assert.Equal((byte)14, pausedFortyFive.ModelMemory.Read(Address(0)).State.Model[0]);
        Assert.Equal((byte)6, fortyFive.ModelMemory.Read(Address(0)).State.Model[0]);
        for (int index = 1; index < 4; index++)
        {
            Assert.Equal((byte)2, fortyFive.ModelMemory.Read(Address(index)).State.Model[0]);
        }
    }

    [Theory]
    [InlineData(104, 105, 2)]
    [InlineData(32767, 32768, 0)]
    public void Original_sword_counter_wrap_and_json_next_keep_exact_current_pose_and_packet_sequence(ushort counter, ushort expected, int result)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        ImmutableArray<NativeBattleEffectOwnedModelResource> models = game.ReadNativeGilgameshModelResources().StaticModels;
        NativeBattleGilgameshSwordStep initial = Run(Retained(), Scratch(), Task(0), models, 0x201);
        NativeBattleGilgameshSwordStep current = Run(initial.ModelMemory, initial.Arena, Task(counter), models, 0);
        Assert.Equal(expected, current.UpdatedTask.ReadWord(12));
        Assert.Equal(result, current.Result);
        NativeBattleGilgameshSwordStep restored = NativeBattleSnapshotJson.Deserialize<NativeBattleGilgameshSwordStep>(NativeBattleSnapshotJson.Serialize(current))!;
        Assert.Equal(NativeBattleSnapshotJson.Serialize(Run(current.ModelMemory, current.Arena, current.UpdatedTask, models, 0)), NativeBattleSnapshotJson.Serialize(Run(restored.ModelMemory, restored.Arena, restored.UpdatedTask, models, 0)));
    }

    [Fact]
    public void Original_all_admitted_sources_reuse_selected_pose_without_raw_draw_or_missing_pose_fallback()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        ImmutableArray<NativeBattleEffectOwnedModelResource> models = game.ReadNativeGilgameshModelResources().StaticModels;
        NativeBattleGilgameshSwordStep initial = Run(Retained(), Scratch(), Task(0), models, 0x201);
        NativeBattleGilgameshModelMemory memory = initial.ModelMemory;
        for (int frame = 0; frame < 12; frame++)
        {
            NativeBattleGilgameshSwordStep drawn = Run(memory, initial.Arena, Task((ushort)(frame + 1)), models, 0x201);
            Assert.Equal(4, drawn.Packets.Length);
            for (int index = 0; index < 4; index++)
            {
                NativeBattleGilgameshModelOwner owner = memory.Read(Address(index));
                memory = memory.Write(owner.Address, NativeBattleCactuarRawModel.Advance5A8FD0(owner.State, models[index], new(owner.ControlAddress, 4)).State);
            }
        }
        NativeBattleGilgameshModelOwner first = initial.ModelMemory.Read(Address(0));
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshSwords.Draw5951E0(first.State with { DecodedPose = null }, models[0], first.ControlAddress, Scratch(), View(), Projection(), Registers()));
        Assert.Throws<NotSupportedException>(() => Run(Retained(), Scratch(), Task(0).WriteWord(14, 65535), models, 0x201));
    }

    private static NativeBattleGilgameshSwordStep Run(NativeBattleGilgameshModelMemory memory, NativeBattleEffectArena arena, NativeBattleModulePoolTask task, ImmutableArray<NativeBattleEffectOwnedModelResource> models, uint flags) => NativeBattleGilgameshSwords.Step594FD0(memory, arena, task, models, View(), Projection(), Registers(), flags);
    private static NativeBattleModulePoolTask Task(ushort counter) => new NativeBattleModulePoolTask(1, 0, null, 0x594FD0, new byte[24].ToImmutableArray()).WriteWord(12, counter).WriteWord(14, 3);
    private static uint Address(int index) => 0x21FF4F0u + (uint)index * 0x9C;
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 288, 160, 108);
    private static NativeBattleScriptedGraphicsMemory Registers() => NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0();

    private static NativeBattleEffectArena Scratch()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x31800, Enumerable.Repeat((byte)0xA5, 76 * 8 + 32).ToArray());
        for (int index = 0; index < 76; index++)
        {
            arena = arena.MarkUnknown(0x31800 + index * 8 + 6, 2);
        }
        return arena;
    }

    private static NativeBattleGilgameshModelMemory Retained()
    {
        NativeBattleGilgameshModelMemory memory = NativeBattleGilgameshModelMemory.FromColdProcess();
        for (int index = 0; index < 4; index++)
        {
            NativeBattleCactuarRawModelState original = memory.Read(Address(index)).State;
            memory = memory.Write(Address(index), original with { Model = Enumerable.Repeat((byte)0x80, 0x90).ToImmutableArray(), Control = Enumerable.Repeat((byte)0xA5, 0x34).ToImmutableArray() });
        }
        return memory;
    }
}
