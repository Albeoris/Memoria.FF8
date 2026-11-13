using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7LadderTimingTests
{
    private static readonly TimeSpan OneCallback = TimeSpan.FromTicks(333334);
    private static readonly FieldProgram Program = new([
        0,
        0x95, 0x05, 1, 0x5F,
        0xC2, 0, 0, 0, 0, 0, 0, 16, 0, 1, 0, 0, 3, 64, 1,
        0x95, 0x05, 2, 0x5F,
        0x12, 1
    ], [new ushort[32]]);

    [Fact]
    public void Held_sequence_matches_at_fast_forward_after_a_cold_ladder_restore()
    {
        LadderFixture normal = new();
        normal.Advance(OneCallback * 2, 0);
        normal.Advance(OneCallback, 0x4000);
        LadderFixture accelerated = new(accelerated: true, saved: normal.Capture());
        Assert.True(accelerated.Host.RequiresRealTimeFieldInput);
        Assert.Equal(1, accelerated.Tempo);

        ushort[] sequence = [0, 0x1000, 0x4000, 0x5000, 0x4000, 0x4000, 0x4000, 0x4000, 0];
        int?[] steps = [1, 0, 1, 1, 2, 3, 4, 4, null];
        bool?[] completions = [false, false, false, false, false, false, false, true, null];
        for (int index = 0; index < sequence.Length; index++)
        {
            normal.Advance(OneCallback, sequence[index]);
            accelerated.Advance(OneCallback, sequence[index]);
            Assert.Equal(steps[index], accelerated.Host.GetActor(0).Ladder?.Step);
            Assert.Equal(normal.Host.GetActor(0).AnimationFrame16, accelerated.Host.GetActor(0).AnimationFrame16);
            Assert.Equal(normal.Capture(), accelerated.Capture());
            Assert.Equal(completions[index], accelerated.Host.GetActor(0).Ladder?.Completed);
        }

        Assert.Equal(1, accelerated.Host.GetActor(0).Triangle);
        Assert.Equal(1, accelerated.Machine.Variables.ReadByte(FieldBank.MapByte, 1));
        Assert.Equal(1, accelerated.Machine.Variables.ReadByte(FieldBank.MapByte, 2));
        Assert.Equal(24, accelerated.Machine.Capture().Entities[0].ProgramCounter);
        Assert.Empty(accelerated.Machine.Capture().PendingRequests!);
        Assert.Equal(3, accelerated.Tempo);
    }

    [Fact]
    public void Entering_discards_accelerated_catch_up_and_leaving_finishes_at_real_time()
    {
        LadderFixture fixture = new(accelerated: true);
        fixture.Advance(TimeSpan.FromMilliseconds(100), 0x4000);
        Assert.Equal(2, fixture.Machine.Tick);
        Assert.Equal(0, fixture.Host.GetActor(0).Ladder!.Step);
        Assert.Equal(1, fixture.Tempo);
        fixture.Advance(TimeSpan.FromMilliseconds(200), 0x4000);
        Assert.Equal(8, fixture.Machine.Tick);
        Assert.Null(fixture.Host.GetActor(0).Ladder);
        Assert.Equal(24, fixture.Machine.Capture().Entities[0].ProgramCounter);
        Assert.Equal(3, fixture.Tempo);
        fixture.Advance(TimeSpan.FromMilliseconds(100), 0);
        Assert.Equal(17, fixture.Machine.Tick);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Npc_and_locked_ladders_accelerate_without_held_input(bool player, bool locked)
    {
        LadderFixture fixture = new(accelerated: true, player: player, locked: locked);
        fixture.Host.ReadInputHeld = () => throw new InvalidOperationException("Automatic ladder polled input.");
        fixture.Advance(TimeSpan.FromMilliseconds(100), 0);
        Assert.Equal(9, fixture.Machine.Tick);
        Assert.Equal(1, fixture.Host.GetActor(0).Triangle);
        Assert.Null(fixture.Host.GetActor(0).Ladder);
        Assert.Equal(3, fixture.Tempo);
    }

    private sealed class LadderFixture
    {
        private readonly GameplayClock clock = new();
        private double remainder;

        public LadderFixture(bool accelerated = false, bool player = true, bool locked = false, string? saved = null)
        {
            PcWalkmeshTriangle triangle = new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), -1, -1, -1);
            Ff7FieldResources field = new("ladder", null!, null!, null!, [], [triangle, triangle with { Index = 1 }], null!, null!, null!);
            LadderSnapshot? restored = saved is null ? null : JsonSerializer.Deserialize<LadderSnapshot>(saved);
            Ff7FieldProcess process = restored?.Process ?? new() { PlayerLocked = locked, Actors = [new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = player }] };
            Host = new(field, (_, _) => 3, process) { Movement = new([triangle], new short[512], new byte[129]) };
            FieldVmSnapshot vm;
            if (restored is null)
            {
                FieldMachine machine = new(Program);
                machine.Initialize();
                vm = machine.Capture();
                vm.Entities[0] = vm.Entities[0] with { ModelBound = true };
            }
            else
            {
                vm = restored.Vm;
            }
            Machine = FieldMachine.Restore(Program, vm, Host);
            Host.ReadInputHeld = () => Machine.Input.Held;
            if (accelerated)
            {
                clock.ToggleSpeed();
            }
        }

        public Ff7FieldHost Host { get; }
        public FieldMachine Machine { get; }
        public double Tempo => clock.EffectiveSpeed(SceneKind.Field, Host.RequiresRealTimeFieldInput);

        public void Advance(TimeSpan elapsed, ushort keys)
        {
            Ff7FieldTiming.Advance(clock, elapsed, SceneKind.Field, () => Host.RequiresRealTimeFieldInput, AdvanceCallback, ref remainder);

            bool AdvanceCallback()
            {
                Machine.SetInput(keys, 0);
                Machine.AdvanceTick();
                Host.AdvanceActors();
                Host.RetireCompletedRequests(Machine.Capture().PendingRequests!.Select(request => request.Id).ToHashSet());
                return true;
            }
        }

        public string Capture() => JsonSerializer.Serialize(new LadderSnapshot(Machine.Capture(), Host.Capture()));
    }

    private sealed record LadderSnapshot(FieldVmSnapshot Vm, Ff7FieldProcess Process);
}
