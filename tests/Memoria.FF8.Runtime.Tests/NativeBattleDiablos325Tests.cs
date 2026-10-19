using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325Tests
{
    [Fact]
    public void Entry_averages_both_signed_axes_and_clears_only_native_entry_fields()
    {
        var fixture = Fixture();
        NativeBattleDiablos325State retained = fixture.Frame.Memory.Diablos325! with { Pause5180 = 9, Control51E4 = 7, PreviousFlash51EC = 17, Mode51F0 = 3 };
        NativeBattleDiablos325Frame source = fixture.Frame with { Memory = fixture.Frame.Memory with { Diablos325 = retained } };
        NativeBattleDiablos325Frame result = NativeBattleDiablos325Entry.Initialize6541E0(source, fixture.Packets, fixture.Models);
        NativeBattleDiablos325State state = result.Memory.Diablos325!;
        Assert.Equal(-1, state.CenterX51D8);
        Assert.Equal(-2, state.CenterZ51D4);
        Assert.Equal(3998, state.FarZ51DC);
        Assert.Equal(1998, state.MidZ51E0);
        Assert.Equal(0u, state.Pause5180);
        Assert.Equal(0u, state.Control51E4);
        Assert.Equal(17u, state.PreviousFlash51EC);
        Assert.Equal((byte)3, state.Mode51F0);
        Assert.Equal(0x1D972C0u + 2u * 0x9C, state.Caster5188);
        Assert.Equal(0x1D99A78u, state.Action5184);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress, state.ModelBuffer5208);
        Assert.Single(result.Memory.Textures!.Slots.Take(result.Memory.Textures.Count));
        Assert.Equal(0x100, result.Memory.Textures.Slots[0]!.ArenaOffset);
        Assert.Equal(new byte[4], Assert.Single(result.Effect.Pools).Slots[0].Payload);
        Assert.Equal(0, source.Memory.Textures!.Count);
        Assert.Equal(9u, source.Memory.Diablos325!.Pause5180);
    }

    [Fact]
    public void First_two_master_callbacks_flip_byte_parity_then_stop_at_actual_timeline_creation()
    {
        var fixture = Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleDiablos325Frame restored = JsonSerializer.Deserialize<NativeBattleDiablos325Frame>(JsonSerializer.Serialize(frame))!;
        for (int index = 0; index < 2; index++)
        {
            frame = NativeBattleDiablos325Controller.Advance654350(frame);
            restored = NativeBattleDiablos325Controller.Advance654350(restored);
            Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(restored));
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + (index == 0 ? 0xF85Cu : 0x885Cu), frame.Memory.Diablos325!.PacketCursor51D0);
            Assert.Equal((ushort)(index + 1), frame.Effect.Pools[0].Slots[0].ReadWord(0xC));
        }
        string before = JsonSerializer.Serialize(frame);
        Assert.Contains("6543BA", Assert.Throws<NotSupportedException>(() => NativeBattleDiablos325Controller.Advance654350(frame)).Message);
        Assert.Equal(before, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void Paused_counter_two_still_flips_parity_and_zeroes_flash_before_native_transition_service()
    {
        var fixture = Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[0];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(0xC, 2)) };
        frame = frame with { Effect = frame.Effect with { Pools = [pool] }, Memory = frame.Memory with { Diablos325 = frame.Memory.Diablos325! with { Pause5180 = 1, Flash51E8 = 99, PreviousFlash51EC = 4096 } } };
        int calls = 0;
        NativeBattleDiablos325Frame result = NativeBattleDiablos325Controller.Advance654350(frame, (current, flash) =>
        {
            calls++;
            Assert.Equal(0u, flash);
            Assert.Equal(4096u, current.Memory.Diablos325!.PreviousFlash51EC);
            Assert.Equal(0u, current.Memory.Diablos325.Flash51E8);
            return current;
        });
        Assert.Equal(1, calls);
        Assert.Equal((ushort)2, result.Effect.Pools[0].Slots[0].ReadWord(0xC));
        Assert.Equal(0u, result.Memory.Diablos325!.PreviousFlash51EC);
        Assert.Equal((byte)1, result.Effect.Pools[0].Slots[0].ReadByte(0xE));
    }

    [Fact]
    public void Missing_warm_owner_physical_target_or_zero_native_divisor_remains_explicit()
    {
        var fixture = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame with { Memory = fixture.Frame.Memory with { Diablos325 = null } }, fixture.Packets, fixture.Models));
        Assert.Throws<NotSupportedException>(() => NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models.SetItem(1, fixture.Models[1] with { Transform = null })));
        NativeBattleActionPacketsState empty = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (empty, _) = empty.Prepare(new(0, 240, 0, 0, 3, 325, null));
        (empty, _) = empty.Publish(0);
        Assert.Throws<DivideByZeroException>(() => NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, empty, fixture.Models));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleDiablos325State>("{}"));
    }

    internal static (NativeBattleDiablos325Frame Frame, NativeBattleActionPacketsState Packets, ImmutableArray<NativeBattleGraphicsModel> Models) Fixture()
    {
        byte[] tim = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(tim, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(62), 1);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x100, tim), Diablos325 = NativeBattleDiablos325State.FromColdProcess() };
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 3, 325, null));
        foreach (byte target in new byte[] { 0, 1 })
        {
            (packets, _) = packets.AppendContact(new(new(target, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(2);
        NativeBattleOriginalEffectState effect = new(325, 324, new(325, 0x6541E0, 0x6541F0), new(2, 0, 3, 0, 0, 0), null) { ResourcePointers = [new(0x250517C, 0x100)] };
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)
        {
            Transform = new(new(0, 0, 0), new(checked((short)(index == 0 ? -103 : 100)), 444, checked((short)(index == 0 ? -105 : 100))), 0, 0, 4096)
        }).ToImmutableArray();
        return (new(memory, effect), packets, models);
    }
}
