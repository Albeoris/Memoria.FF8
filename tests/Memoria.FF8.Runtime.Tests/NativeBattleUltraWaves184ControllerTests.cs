using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleUltraWaves184Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltraWaves184ControllerTests
{
    [Fact]
    public void Main_binds_actual_caster_before_ring_and_root_selects_current_parity()
    {
        Fixture fixture = new(MainCallback, 1);
        NativeBattleUltraWaves184Frame result = NativeBattleUltraWaves184Controller.Advance(fixture.Frame, fixture.Resources, fixture.Inputs());
        Assert.Collection(fixture.Operations, operation =>
        {
            NativeBattleUltraWaves184BindCaster bind = Assert.IsType<NativeBattleUltraWaves184BindCaster>(operation);
            Assert.Equal((byte)0, bind.Actor);
            Assert.Equal(SavedBindingAddress, bind.SaveAddress);
            Assert.Equal(CasterDatAddress, bind.ResourceAddress);
        }, operation => Assert.Equal(new NativeBattleUltraWaves184RingSequence(0, 1), operation));
        Assert.Equal(PacketAddress, result.State.ReadCursor());
        Assert.Equal((ushort)2, NativeBattleUltraWaves184Controller.Read(result, ChildBank).ReadWord(12));
        result = NativeBattleUltraWaves184Controller.Advance(result, fixture.Resources, fixture.Inputs());
        Assert.Equal(PacketAddress + PacketBankLength, result.State.ReadCursor());
    }

    [Fact]
    public void Age_six_anchors_each_emitter_at_distance2048_and_consumes_four_CRT_draws()
    {
        Fixture fixture = new(MainCallback, 6);
        NativeBattleUltraWaves184Frame result = NativeBattleUltraWaves184Controller.Advance(fixture.Frame, fixture.Resources, fixture.Inputs());
        Assert.Equal((ushort)0xBEEF, NativeBattleUltraWaves184Controller.Read(result, ChildBank).ReadWord(22));
        Assert.Equal(4, fixture.Anchors.Count);
        Assert.All(fixture.Anchors, anchor => Assert.Equal(((short)0xF1, (short)2048), anchor));
        Assert.Equal(4, fixture.Emitters.Count);
        Assert.Equal(new ushort[] { 1, 4, 16, 64 }, fixture.Emitters.Select(task => task.ReadWord(14)));
        Assert.All(fixture.Emitters, task =>
        {
            Assert.Equal(unchecked((ushort)-4106), task.ReadWord(16));
            Assert.Equal(unchecked((ushort)-20), task.ReadWord(18));
            Assert.Equal((ushort)4096, task.ReadWord(24));
            Assert.Equal((ushort)0, task.ReadWord(26));
            Assert.Equal((ushort)0, task.ReadWord(28));
        });
        NativeCrtRandomState random = fixture.Frame.Random;
        for (int index = 0; index < 4; index++)
        {
            random = random.Draw(out ushort spin);
            Assert.Equal((ushort)(spin & 4095), fixture.Emitters[index].ReadWord(32));
        }
        Assert.Equal(random, result.Random);
        Assert.Equal(new NativeBattleUltraWaves184Sound(0x1234, 0, 128), Assert.Single(fixture.Operations));
    }

    [Fact]
    public void Contact_runs_at_old_age41_and_retains_frame_returning_service_updates()
    {
        Fixture fixture = new(MainCallback, 41);
        NativeBattleUltraWaves184Frame result = NativeBattleUltraWaves184Controller.Advance(fixture.Frame, fixture.Resources, fixture.Inputs());
        Assert.IsType<NativeBattleUltraWaves184ContactAll>(Assert.Single(fixture.Operations));
        Assert.Equal(123u, result.State.ReadDword(0x22BD108));
        Assert.Equal((ushort)42, NativeBattleUltraWaves184Controller.Read(result, ChildBank).ReadWord(12));
    }

    [Fact]
    public void Paused_warp_still_captures_and_draws_before_skipping_visibility_and_age()
    {
        Fixture fixture = new(WarpCallback, 0);
        NativeBattleUltraWaves184Frame result = NativeBattleUltraWaves184Controller.Advance(fixture.Frame, fixture.Resources, fixture.Inputs(0x201));
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleUltraWaves184CaptureStage>(operation), operation => Assert.Equal(new NativeBattleUltraWaves184DrawGrid(0, 0x400000), operation));
        Assert.Equal((ushort)0, NativeBattleUltraWaves184Controller.Read(result, ChildBank).ReadWord(12));
        Assert.Equal(123u, result.State.ReadDword(0x22BD108));
    }

    [Fact]
    public void Final_warp_restores_stage_then_waits_for_real_completion_and_restored_raw_cursor()
    {
        Fixture fixture = new(WarpCallback, 35);
        NativeBattleUltraWaves184Frame waiting = NativeBattleUltraWaves184Controller.Advance(fixture.Frame, fixture.Resources, fixture.Inputs());
        Assert.Equal(new NativeBattleUltraWaves184StageVisibility(true), Assert.Single(fixture.Operations));
        NativeBattleModulePoolTask wait = Assert.Single(waiting.Effect.Pools[0].Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Equal(NativeBattleUltraWaves184Profile.WaitCallback, wait.Callback);
        Assert.Equal((ushort)1, wait.ReadWord(12));
        Assert.Equal((ushort)0, wait.ReadWord(14));
        Assert.Equal(RootDescriptor, waiting.Effect.ReturnedDescriptor);
        string json = JsonSerializer.Serialize(waiting);
        NativeBattleUltraWaves184Frame restored = JsonSerializer.Deserialize<NativeBattleUltraWaves184Frame>(json)!;
        fixture.CompleteTexture = true;
        NativeBattleUltraWaves184Frame complete = NativeBattleUltraWaves184Controller.Advance(restored, fixture.Resources, fixture.Inputs());
        NativeBattleUltraWaves184RestoreTexture operation = Assert.IsType<NativeBattleUltraWaves184RestoreTexture>(fixture.Operations[^1]);
        Assert.Equal(PacketAddress, operation.Destination);
        Assert.Equal(RootBank + 16 + 14, operation.CompletionAddress);
        Assert.Equal(0u, complete.Effect.ReturnedDescriptor);
        Assert.Equal(0x10u, complete.State.ReadCursor());
        complete.State.Validate();
    }

    private sealed class Fixture
    {
        public NativeBattleUltraWaves184Frame Frame { get; }
        public List<NativeBattleUltraWaves184Operation> Operations { get; } = [];
        public List<(short Selector, short Distance)> Anchors { get; } = [];
        public List<NativeBattleModulePoolTask> Emitters { get; } = [];
        public bool CompleteTexture { get; set; }
        public NativeBattleUltraWaves184Resources Resources { get; } = new(new(0, 0, []), new("unused", []), new("unused", []), 0x1234, Enumerable.Range(0, 35).Select(index => 0x400000u + (uint)index).ToImmutableArray(), [], Enumerable.Range(0, 8).Select(index => new NativeBattleModuleAnchor(index % 2 == 0 ? (short)-4096 : (short)4096, 0, 0, 0)).ToImmutableArray(), [], []);

        public Fixture(uint callback, ushort age)
        {
            NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
            (roots, int root) = roots.Append(RootCallback);
            roots = roots with { Slots = roots.Slots.SetItem(root, roots.Slots[root].WriteWord(12, 0)) };
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            (children, int child) = children.Append(callback);
            children = children with { Slots = children.Slots.SetItem(child, children.Slots[child].WriteWord(12, age).WriteWord(22, 0xBEEF)) };
            NativeBattleOriginalEffectState effect = new(184, 184, new(184, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [roots, children] };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleUltraWaves184State.FromColdProcess());
        }

        public NativeBattleUltraWaves184Inputs Inputs(uint flags = 0) => new(NativeBattleActionPacketsState.Fresh(), flags, _ => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), (frame, actor, selector, distance) =>
        {
            Assert.Equal(0, actor);
            Anchors.Add((selector, distance));
            return new(frame, new(-10, -20, -30, 50));
        }, frame => frame with { State = frame.State.WriteDword(0x22BD108, 123) }, (frame, address, _) =>
        {
            Emitters.Add(NativeBattleUltraWaves184Controller.Read(frame, address));
            return new(frame, 0);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            frame = frame with { State = frame.State.WriteDword(0x22BD108, 123) };
            if (operation is NativeBattleUltraWaves184RestoreTexture restore && CompleteTexture)
            {
                byte[] bytes = new byte[PacketPhysicalLength];
                bytes[PacketDrawingLength] = 0x10;
                frame = frame with { State = frame.State.CopyCommonTexture(bytes) };
                frame = NativeBattleUltraWaves184Controller.WriteCompletionByte(frame, restore.CompletionAddress, 1);
            }
            return frame;
        });
    }
}
