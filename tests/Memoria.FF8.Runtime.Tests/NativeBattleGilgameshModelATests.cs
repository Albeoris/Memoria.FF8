using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshModelATests
{
    [Fact]
    public void Controller_flag_events_occur_while_paused_and_retain_every_other_model_bit()
    {
        NativeBattleGilgameshModelMemory memory = NativeBattleGilgameshModelMemory.FromColdProcess();
        NativeBattleCactuarRawModelState source = memory.Read(0x21FF760).State;
        byte[] bytes = source.Model.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0xAB47);
        memory = memory.Write(0x21FF760, source with { Model = bytes.ToImmutableArray() });
        NativeBattleModulePoolTask ten = Task(10);
        NativeBattleGilgameshModelAStep raised = Run(memory, ten, 0x201);
        Assert.Same(ten, raised.UpdatedTask);
        Assert.Equal((ushort)0xAB4F, BinaryPrimitives.ReadUInt16LittleEndian(raised.ModelMemory.Read(0x21FF760).State.Model.AsSpan()));
        NativeBattleModulePoolTask fortyFive = Task(45);
        NativeBattleGilgameshModelAStep cleared = Run(raised.ModelMemory, fortyFive, 0x201);
        Assert.Same(fortyFive, cleared.UpdatedTask);
        Assert.Equal((ushort)0xAB43, BinaryPrimitives.ReadUInt16LittleEndian(cleared.ModelMemory.Read(0x21FF760).State.Model.AsSpan()));
        Assert.Empty(cleared.Packets.Attempts);
    }

    [Theory]
    [InlineData(59, 60, 2)]
    [InlineData(60, 61, 2)]
    [InlineData(32767, 32768, 0)]
    [InlineData(65535, 0, 0)]
    public void Controller_signed_post_increment_completion_wraps_WORD_and_json_next_is_identical(ushort counter, ushort expected, int result)
    {
        NativeBattleGilgameshModelAStep first = Run(NativeBattleGilgameshModelMemory.FromColdProcess(), Task(counter));
        Assert.Equal(expected, first.UpdatedTask.ReadWord(12));
        Assert.Equal(result, first.Result);
        if (expected != 0)
        {
            NativeBattleGilgameshModelAStep restored = JsonSerializer.Deserialize<NativeBattleGilgameshModelAStep>(JsonSerializer.Serialize(first))!;
            Assert.Equal(JsonSerializer.Serialize(Run(first.ModelMemory, first.UpdatedTask)), JsonSerializer.Serialize(Run(restored.ModelMemory, restored.UpdatedTask)));
        }
    }

    [Fact]
    public void World_skinning_uses_saturated_signed_IR_then_writes_fourth_WORD_zero_and_preserves_arena_tail()
    {
        NativeBattleRawMesh mesh = new([new(new(100, -100, 20), 0)], []);
        NativeBattleActionMatrix bone = Identity() with { X = 40000, Y = -40000, Z = 50 };
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x100, Enumerable.Repeat((byte)0xA5, 16).ToArray()).MarkUnknown(0x100, 8);
        NativeBattleEffectArena written = NativeBattleGilgameshModelA.SkinWorld5960A0(mesh, [bone], arena, 0x100);
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(written.Read(0x100, 2)));
        Assert.Equal(short.MinValue, BinaryPrimitives.ReadInt16LittleEndian(written.Read(0x102, 2)));
        Assert.Equal((short)70, BinaryPrimitives.ReadInt16LittleEndian(written.Read(0x104, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(written.Read(0x106, 2)));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 8).ToArray(), written.Read(0x108, 8));
        Assert.Throws<NotSupportedException>(() => arena.Read(0x100, 8));
    }

    [Fact]
    public void Saturated_world_short_stage_is_observable_before_view_projection()
    {
        NativeBattleRawMesh mesh = new([new(new(0, 0, 0), 0)], []);
        NativeBattleActionMatrix bone = Identity() with { X = 40000 };
        NativeBattleActionMatrix view = Identity() with { X = -40000, Z = 3000 };
        NativeBattleEffectArena arena = NativeBattleGilgameshModelA.SkinWorld5960A0(mesh, [bone], NativeBattleEffectArena.Clear(), 0x100);
        byte[] world = arena.Read(0x100, 6);
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(world), BinaryPrimitives.ReadInt16LittleEndian(world.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(world.AsSpan(4)));
        NativeBattleCameraProjection projection = new(null, 288, 160, 108);
        NativeBattleModuleProjectedVertex actual = NativeBattleModuleProjection.Project45FE10(view, point, projection);
        NativeBattleModuleProjectedVertex collapsed = NativeBattleModuleProjection.Project45FE10(NativeBattleGfMatrices.Compose56C2F0(view, bone), mesh.Vertices[0].Position, projection);
        Assert.NotEqual(collapsed.Screen, actual.Screen);
        Assert.Equal(NativeBattleModuleProjection.Project45FE10(view, new(short.MaxValue, 0, 0), projection), actual);
    }

    [Fact]
    public void World_skinning_requires_actual_current_bones_without_a_pose_fallback()
    {
        NativeBattleRawMesh mesh = new([new(new(0, 0, 0), 1)], []);
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshModelA.SkinWorld5960A0(mesh, [Identity()], NativeBattleEffectArena.Clear(), 0x100));
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshModelA.SkinWorld5960A0(mesh, default, NativeBattleEffectArena.Clear(), 0x100));
    }

    private static NativeBattleGilgameshModelAStep Run(NativeBattleGilgameshModelMemory memory, NativeBattleModulePoolTask task, uint flags = 0) => NativeBattleGilgameshModelA.Step595EE0(memory, NativeBattleEffectArena.Clear(), task, null!, null!, null!, null, flags);
    private static NativeBattleModulePoolTask Task(ushort counter) => new NativeBattleModulePoolTask(1, 0, null, 0x595EE0, Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray()).WriteWord(12, counter);
    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
}
