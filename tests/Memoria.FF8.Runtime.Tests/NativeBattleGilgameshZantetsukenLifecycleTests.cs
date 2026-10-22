namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshZantetsukenLifecycleTests
{
    [Fact]
    public void Rotation_initializes_only_reached_bytes_and_updates_while_paused()
    {
        NativeBattleGilgameshFrame frame = ZantetsukenFixture.Create(0x58E3E0, 0);
        frame = frame with { Memory = frame.Memory with { Gilgamesh = frame.Memory.Gilgamesh! with { ZantetsukenActors = null } } };
        NativeBattleGilgameshZantetsukenStep step = NativeBattleGilgameshZantetsukenLifecycle.AdvanceRotation(frame, ZantetsukenFixture.Address, ZantetsukenFixture.Inputs(default) with { GraphicsFlags = 1 });
        NativeBattleGilgameshZantetsukenActors actors = step.Frame.Memory.Gilgamesh!.ZantetsukenActors!;
        Assert.Equal((short)0, actors.ReadWord(3, 0));
        Assert.Equal((short)0, actors.ReadWord(3, 2));
        Assert.Equal((short)0, actors.ReadWord(3, 4));
        Assert.Equal(-300, actors.ReadMatrix(3).ReadMatrix().Y);
        Assert.False(actors.ReadMatrix(3).Known[18]);
        Assert.Throws<NotSupportedException>(() => actors.ReadWord(3, 6));
        Assert.Throws<NotSupportedException>(() => actors.ReadWord(2, 0));
        Assert.Equal((ushort)0, step.Frame.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Rotation_requires_unwritten_euler_z_in_imported_nonzero_age()
    {
        NativeBattleGilgameshFrame frame = ZantetsukenFixture.Create(0x58E3E0, 1);
        frame = frame with { Memory = frame.Memory with { Gilgamesh = frame.Memory.Gilgamesh! with { ZantetsukenActors = null } } };
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshZantetsukenLifecycle.AdvanceRotation(frame, ZantetsukenFixture.Address, ZantetsukenFixture.Inputs(default)));
    }

    [Theory]
    [InlineData(39, 0u, 40, 0, 4)]
    [InlineData(40, 0u, 41, 2, 1)]
    [InlineData(39, 1u, 39, 0, 4)]
    public void Actor_draw_fade_and_contact_follow_distinct_old_and_new_age_gates(int age, uint flags, int next, int result, int operationCount)
    {
        NativeBattleGilgameshFrame frame = ZantetsukenFixture.Create(0x58E570, (ushort)age);
        List<NativeBattleGilgameshOperation> operations = [];
        NativeBattleGilgameshZantetsukenInputs inputs = ZantetsukenFixture.Inputs(default) with { GraphicsFlags = flags, Execute = (current, operation) => { operations.Add(operation); return current; } };
        NativeBattleGilgameshZantetsukenStep step = NativeBattleGilgameshZantetsukenLifecycle.AdvanceActor(frame, ZantetsukenFixture.Address, inputs);
        Assert.Equal((ushort)next, step.Frame.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal(result, step.Result);
        Assert.Equal(operationCount, operations.Count);
        if (age < 40)
        {
            Assert.IsType<NativeBattleGilgameshZantetsukenActorFlags>(operations[0]);
            Assert.IsType<NativeBattleGilgameshZantetsukenBeginDrawActor>(operations[1]);
            Assert.Equal(4096 - NativeBattleCameraMath.Sine((age - 24) * 64), Assert.IsType<NativeBattleGilgameshZantetsukenFadeActor>(operations[2]).Cue);
            Assert.IsType<NativeBattleGilgameshZantetsukenDrawActor>(operations[3]);
        }
        else
        {
            Assert.Equal(2, Assert.IsType<NativeBattleGilgameshZantetsukenContact>(operations[0]).Ordinal);
        }
    }

    [Theory]
    [InlineData(64, 0u, 65, 0, 2)]
    [InlineData(65, 0u, 66, 2, 1)]
    [InlineData(64, 0x200u, 64, 0, 2)]
    public void Stage_visibility_and_draw_precede_pause_but_restore_only_after_sixty_five(int age, uint flags, int next, int result, int operationCount)
    {
        NativeBattleGilgameshFrame frame = ZantetsukenFixture.Create(0x58F940, (ushort)age);
        frame = ZantetsukenFixture.Write(frame, ZantetsukenFixture.Address, frame.Effect.Pools[1].Slots[0].WriteWord(14, 0));
        List<NativeBattleGilgameshOperation> operations = [];
        NativeBattleGilgameshZantetsukenInputs inputs = ZantetsukenFixture.Inputs(default) with { GraphicsFlags = flags, Execute = (current, operation) => { operations.Add(operation); return current; } };
        NativeBattleGilgameshZantetsukenStep step = NativeBattleGilgameshZantetsukenLifecycle.AdvanceStage(frame, ZantetsukenFixture.Address, inputs);
        Assert.Equal((ushort)next, step.Frame.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal(result, step.Result);
        Assert.Equal(operationCount, operations.Count);
        Assert.Equal(age >= 65, Assert.IsType<NativeBattleGilgameshZantetsukenStageVisibility>(operations[0]).Visible);
        if (age < 65)
        {
            Assert.IsType<NativeBattleGilgameshZantetsukenDrawStage>(operations[1]);
        }
        if (flags == 0)
        {
            Assert.Equal((ushort)0, step.Frame.Effect.Pools[1].Slots[0].ReadWord(0x20));
            Assert.Equal(unchecked((ushort)-262), step.Frame.Effect.Pools[1].Slots[0].ReadWord(0x22));
        }
    }

    [Theory]
    [InlineData(0u, 1)]
    [InlineData(1u, 2)]
    public void Actor_delay_short_circuits_draw_and_unknown_actor_or_age(uint flags, int expectedDelay)
    {
        NativeBattleGilgameshFrame frame = ZantetsukenFixture.Create(0x58E570, 0);
        frame = ZantetsukenFixture.Write(frame, ZantetsukenFixture.Address, frame.Effect.Pools[1].Slots[0].WriteWord(0x16, 2).MarkPayloadUnknown(12, 4));
        NativeBattleGilgameshZantetsukenInputs inputs = ZantetsukenFixture.Inputs(default) with { GraphicsFlags = flags, Execute = (_, _) => throw new InvalidOperationException() };
        NativeBattleGilgameshZantetsukenStep step = NativeBattleGilgameshZantetsukenLifecycle.AdvanceActor(frame, ZantetsukenFixture.Address, inputs);
        Assert.Equal((ushort)expectedDelay, step.Frame.Effect.Pools[1].Slots[0].ReadWord(0x16));
    }
}
