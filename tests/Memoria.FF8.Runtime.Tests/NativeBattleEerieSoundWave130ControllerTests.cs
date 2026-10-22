using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEerieSoundWave130Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEerieSoundWave130ControllerTests
{
    [Fact]
    public void Startup_sorts_actor_pointers_with_original_ordinals_and_scans_new_travel_same_pass()
    {
        Fixture fixture = new();
        NativeBattleEerieSoundWave130Frame next = fixture.Step(fixture.Frame);
        NativeBattleModulePoolTask main = next.Effect.Pools[1].Slots[0];
        Assert.Equal(new uint[] { Model(5), Model(3), Model(4) }, new[] { main.ReadDword(0x10), main.ReadDword(0x14), main.ReadDword(0x18) });
        Assert.Equal(new byte[] { 2, 0, 1 }, new[] { main.ReadByte(0x1C), main.ReadByte(0x1D), main.ReadByte(0x1E) });
        Assert.Equal(new byte[] { 0, 1 }, fixture.Calls.Select(call => call.Kind).ToArray());
        NativeBattleModulePoolTask travel = next.Effect.Pools[2].Slots[0];
        Assert.Equal(Model(5), travel.ReadDword(0x10));
        Assert.Equal((ushort)1, travel.ReadWord(12));
        Assert.Equal((byte)2, travel.ReadByte(15));
        Assert.Equal((byte)1, travel.ReadByte(14));
        Assert.Throws<NotSupportedException>(() => travel.ReadWord(0x1A));
        Assert.Equal(ArenaAddress + SecondPacketOffset, next.State.ReadDword(CursorGlobal));
    }

    [Fact]
    public void Style9_creates_impact_in_same_pass_and_impact_remaining8_contacts_selected_actor()
    {
        Fixture fixture = new();
        NativeBattleEerieSoundWave130Frame frame = fixture.Step(fixture.Frame);
        frame = Age(frame, 9);
        fixture.Clear();
        fixture.Remaining = kind => kind == 2 ? 8 : 50;
        NativeBattleEerieSoundWave130Frame next = fixture.Step(frame);
        Assert.Equal(new byte[] { 0, 1, 1, 2 }, fixture.Calls.Select(call => call.Kind).ToArray());
        NativeBattleModulePoolTask impact = next.Effect.Pools[3].Slots[0];
        Assert.Equal(Model(5), impact.ReadDword(0x14));
        Assert.Equal(unchecked((ushort)0xFEDC), impact.ReadWord(18));
        Assert.Equal(2, Assert.Single(fixture.Operations.OfType<NativeBattleEerieSoundWave130Contact>()).Index);
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(6, 1)]
    [InlineData(9, 0)]
    [InlineData(12, 1)]
    public void Native_rem12_DIV3_births_preserve_actor_group_and_style(int age, byte expectedStyle)
    {
        Fixture fixture = new();
        NativeBattleEerieSoundWave130Frame frame = Age(fixture.Step(fixture.Frame), (ushort)age);
        fixture.Clear();
        NativeBattleEerieSoundWave130Frame next = fixture.Step(frame);
        NativeBattleModulePoolTask born = next.Effect.Pools[2].Slots[1];
        Assert.Equal(expectedStyle, born.ReadByte(14));
        Assert.Equal(age < 12 ? Model(5) : Model(3), born.ReadDword(0x10));
        Assert.Equal(age < 12 ? (byte)2 : (byte)0, born.ReadByte(15));
    }

    [Fact]
    public void Travel_blends_actual_target_but_basis_uses_unblended_delta_and_keeps_GPF_GPL_feedback()
    {
        Fixture fixture = new();
        NativeBattleEerieSoundWave130Frame frame = fixture.Step(fixture.Frame);
        NativeBattleModulePoolTask travel = frame.Effect.Pools[2].Slots[0].WriteWord(12, 12);
        frame = NativeBattleEerieSoundWave130Controller.Write(frame, frame.Effect.Pools[2].BankAddress, travel);
        frame = Age(frame, 1);
        fixture.Clear();
        NativeBattleEerieSoundWave130Frame next = fixture.Step(frame);
        Assert.Equal((uint)NativeBattleCameraMath.Sine(512), next.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new int?[] { 524, 625, 724 }, next.Memory.ScriptedColorRegisters.Mac.ToArray());
        Assert.Equal(2, fixture.Calls.Count);
        Assert.Equal((byte)1, fixture.Calls[1].Kind);
        Assert.Equal(524, BitConverter.ToInt32(fixture.Calls[1].Context.Read(20, 4)));
        Assert.Equal(625, BitConverter.ToInt32(fixture.Calls[1].Context.Read(24, 4)));
        Assert.Equal(724, BitConverter.ToInt32(fixture.Calls[1].Context.Read(28, 4)));
        Assert.Equal(0, BitConverter.ToInt32(fixture.Calls[1].Context.Read(32, 4)));
    }

    [Fact]
    public void Root_waits_for_all_three_pools_and_JSON_next_keeps_operation_order()
    {
        Fixture fixture = new();
        NativeBattleEerieSoundWave130Frame frame = fixture.Step(fixture.Frame);
        fixture.Remaining = kind => kind == 0 ? 0 : 50;
        NativeBattleEerieSoundWave130Frame live = fixture.Step(frame);
        Assert.Equal(RootDescriptor, live.Effect.ReturnedDescriptor);
        fixture.Remaining = _ => 0;
        NativeBattleEerieSoundWave130Frame restored = JsonSerializer.Deserialize<NativeBattleEerieSoundWave130Frame>(JsonSerializer.Serialize(live))!;
        NativeBattleEerieSoundWave130Frame expected = fixture.Step(live);
        NativeBattleEerieSoundWave130Frame actual = fixture.Step(restored);
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Fact]
    public void Full_travel_pool_skips_NULL_append_without_losing_main_or_existing_children()
    {
        Fixture fixture = new();
        NativeBattleEerieSoundWave130Frame frame = Age(fixture.Step(fixture.Frame), 3);
        NativeBattleEffectTaskPool travel = NativeBattleEffectTaskPool.Initialize(TravelDescriptor, ArenaAddress + TravelOffset, 0x74, 12);
        for (int index = 0; index < 12; index++)
        {
            (travel, int slot) = travel.Append(TravelCallback);
            NativeBattleModulePoolTask task = travel.Slots[slot].WriteBytes(12, new byte[0x68]).WriteWord(12, 30).WriteDword(0x10, Model(5));
            travel = travel with { Slots = travel.Slots.SetItem(slot, task) };
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, travel) } };
        fixture.Clear();
        NativeBattleEerieSoundWave130Frame next = fixture.Step(frame);
        Assert.Equal(12, fixture.Calls.Count(call => call.Kind == 1));
        Assert.Equal((ushort)4, next.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal(12, next.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
    }

    private static NativeBattleEerieSoundWave130Frame Age(NativeBattleEerieSoundWave130Frame frame, ushort age) => NativeBattleEerieSoundWave130Controller.Write(frame, frame.Effect.Pools[1].BankAddress, frame.Effect.Pools[1].Slots[0].WriteWord(12, age));
    private static uint Model(int actor) => 0x1D972C0u + (uint)(actor * 156);

    private sealed class Fixture
    {
        public NativeBattleEerieSoundWave130Frame Frame { get; }
        public List<(byte Kind, NativeBattleEffectArena Context)> Calls { get; } = [];
        public List<NativeBattleEerieSoundWave130Operation> Operations { get; } = [];
        public Func<byte, int> Remaining { get; set; } = _ => 50;
        private NativeBattleEerieSoundWave130Resources Resources { get; } = new(Source(MainActAddress, 6000), Source(TravelActAddress, 3940), Source(ImpactActAddress, 1960), new("FF8_EN:1223810", new byte[160].ToImmutableArray()), 0x64197);
        private NativeBattleActionPacketsState Packets { get; }
        public Fixture()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(0, 8, 11, 0, 185, 130, null));
            foreach (int actor in new[] { 3, 4, 5 })
            {
                (packets, _) = packets.AppendContact(new(new((byte)actor, 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            }
            packets = packets.WritePlayerRepeatMode(0, 1);
            (packets, _) = packets.Publish(3);
            Packets = packets;
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
            (root, int slot) = root.Append(RootCallback);
            root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteBytes(12, new byte[8]).WriteWord(12, 1)) };
            NativeBattleOriginalEffectState effect = new(130, 129, new(130, Entry, Initializer), new(0, 0, 8, 0, 0, 0), null) { Pools = [root], ReturnedDescriptor = RootDescriptor };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
            Frame = new(memory, effect, new(123), NativeBattleEerieSoundWave130State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, Model(0)));
        }
        public void Clear()
        {
            Calls.Clear();
            Operations.Clear();
        }
        public NativeBattleEerieSoundWave130Frame Step(NativeBattleEerieSoundWave130Frame frame) => NativeBattleEerieSoundWave130Controller.Advance(frame, Resources, new(Packets, actor => new(actor, 1) { Rotation14 = 0, Transform = new(Initial20: new(0, 0, 0), Position28: new((short)(actor == 5 ? 700 : actor == 4 ? -300 : 100), 0, 0), Vertical34: 0, Angle36: 0, Scale38: 512) }, () => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (current, _, _, _) => new(current, new(100, 200, 300)), (current, _) => new(current, new(700, 800, 900, unchecked((short)0xFEDC))), (current, _, kind, context) =>
        {
            Calls.Add((kind, context));
            return new(current, Remaining(kind));
        }, (current, operation) =>
        {
            Operations.Add(operation);
            return current;
        }, NativeBattleGteColorInterpolation.CodeConstants45F270));
        private static NativeBattleGfAnimationSource Source(uint address, int length)
        {
            byte[] data = new byte[length];
            BitConverter.GetBytes(8).CopyTo(data, 4);
            BitConverter.GetBytes(56).CopyTo(data, 12);
            return new(address, data.ToImmutableArray());
        }
    }
}
