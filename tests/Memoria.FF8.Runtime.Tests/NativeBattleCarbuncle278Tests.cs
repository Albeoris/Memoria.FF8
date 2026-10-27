using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278Tests
{
    [Fact]
    public void Entry_uses_exact_physical_position_scale_and_ordered_initializer_Tims()
    {
        var fixture = Fixture();
        NativeBattleCarbuncle278Frame result = NativeBattleCarbuncle278Entry.Initialize680C50(fixture.Frame, fixture.Packets, fixture.Models, 42);
        NativeBattleCarbuncle278State state = result.Memory.Carbuncle278!;
        Assert.Equal(0x26u, 0x1D974BAu - (0x1D972C0u + 3u * 0x9C));
        Assert.Equal(0, state.Centroid81C4);
        Assert.Equal(-2000, state.Base81CC);
        Assert.Equal(-3899, state.Lift81D0);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress, state.ModelBuffer81F8);
        Assert.Equal(0x1D972C0u, state.Caster8124);
        NativeCrtRandomState random = fixture.Frame.Random.Draw(out ushort value);
        Assert.Equal(random, result.Random);
        Assert.Equal((uint)value, state.Random811C);
        Assert.Equal(new int?[] { 0x100, 0x2A120 }, result.Memory.Textures!.Slots.Take(result.Memory.Textures.Count).Select(slot => slot!.ArenaOffset));
        NativeBattleEffectTaskPool pool = Assert.Single(result.Effect.Pools);
        Assert.Equal(NativeBattleCarbuncle278Entry.MainDescriptor, result.Effect.ReturnedDescriptor);
        Assert.Equal(NativeBattleCarbuncle278Entry.MainBank, pool.BankAddress);
        NativeBattleModulePoolTask task = Assert.Single(pool.Slots);
        Assert.Equal(NativeBattleCarbuncle278Entry.MainCallback, task.Callback);
        Assert.Equal(new byte[8], NativeBattleStorageSnapshots.Capture(task).Payload);
        Assert.Equal(0, fixture.Frame.Memory.Textures!.Count);
    }

    [Fact]
    public void Encounter_override_occurs_after_physical_reads_and_preserves_retained_module_fields()
    {
        var fixture = Fixture();
        NativeBattleCarbuncle278Frame source = fixture.Frame with { Memory = fixture.Frame.Memory with { Carbuncle278 = fixture.Frame.Memory.Carbuncle278! with { Pause8114 = 7, Done81D8 = 1 } } };
        NativeBattleCarbuncle278Frame result = NativeBattleCarbuncle278Entry.Initialize680C50(source, fixture.Packets, fixture.Models, 0x3F);
        Assert.Equal(0, result.Memory.Carbuncle278!.Lift81D0);
        Assert.Equal(0, result.Memory.Carbuncle278.Base81CC);
        Assert.Equal(7u, result.Memory.Carbuncle278.Pause8114);
        Assert.Equal((byte)1, result.Memory.Carbuncle278.Done81D8);
        ImmutableArray<NativeBattleGraphicsModel> unknown = fixture.Models.SetItem(3, fixture.Models[3] with { Transform = null });
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(source, fixture.Packets, unknown, 0x3F));
    }

    [Fact]
    public void Independent_entry_and_master_restore_reproduce_first_two_callbacks_and_reached_boundary()
    {
        var fixture = Fixture();
        NativeBattleCarbuncle278Frame live = NativeBattleCarbuncle278Entry.Initialize680C50(fixture.Frame, fixture.Packets, fixture.Models, 42);
        NativeBattleCarbuncle278Frame restored = RoundTrip(live);
        List<(uint, int)> stages = [];
        for (int index = 0; index < 2; index++)
        {
            live = NativeBattleCarbuncle278Controller.Advance680DF0(live, (color, flags) => stages.Add((color, flags)));
            restored = NativeBattleCarbuncle278Controller.Advance680DF0(restored, (_, _) => { });
            Assert.Equal(NativeBattleTestSnapshots.Json(live), NativeBattleTestSnapshots.Json(restored));
            uint offset = index == 0 ? 0x13ECCu : 0x3ECCu;
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + offset, live.Memory.Carbuncle278!.PacketCursor81BC);
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + offset + 0x10000, live.Memory.Carbuncle278.AlternateCursor81C0);
        }
        Assert.Equal(new[] { (0u, 0), (0u, 0) }, stages);
        string before = NativeBattleTestSnapshots.Json(live);
        NotSupportedException boundary = Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Controller.Advance680DF0(live, (_, _) => throw new InvalidOperationException()));
        Assert.Contains("6D6070", boundary.Message);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(live));
    }

    [Fact]
    public void Pause_keeps_counter_two_but_still_flips_cursors_and_publishes_flash()
    {
        var fixture = Fixture();
        NativeBattleCarbuncle278Frame frame = NativeBattleCarbuncle278Entry.Initialize680C50(fixture.Frame, fixture.Packets, fixture.Models, 42);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[0];
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(0xC, 2)) });
        frame = frame with { Effect = frame.Effect with { Pools = [pool] }, Memory = frame.Memory with { Carbuncle278 = frame.Memory.Carbuncle278! with { Pause8114 = 1, Flash81D4 = 123 } } };
        int calls = 0;
        NativeBattleCarbuncle278Frame result = NativeBattleCarbuncle278Controller.Advance680DF0(frame, (color, flags) =>
        {
            Assert.Equal(0u, color);
            Assert.Equal(0, flags);
            calls++;
        });
        Assert.Equal(1, calls);
        Assert.Equal((ushort)2, result.Effect.Pools[0].Slots[0].ReadWord(0xC));
        Assert.Equal(1u, result.Effect.Pools[0].Slots[0].ReadDword(0x10));
        Assert.Equal(0u, result.Memory.Carbuncle278!.Flash81D4);
    }

    [Fact]
    public void Module_validation_bounds_publications_without_normalizing_retained_flags()
    {
        NativeBattleCarbuncle278State cold = NativeBattleCarbuncle278State.FromColdProcess();
        cold.Validate();
        (cold with { Pause8114 = uint.MaxValue, Done81D8 = byte.MaxValue, Flash81D4 = uint.MaxValue, Lift81D0 = int.MinValue }).Validate();
        foreach (NativeBattleCarbuncle278State invalid in new[]
        {
            cold with { Random811C = 0x8000 },
            cold with { Action8120 = 1 },
            cold with { Caster8124 = 0x1D972C1 },
            cold with { ModelBuffer81F8 = NativeBattleEffectNodeMemory.ArenaAddress + 4 },
            cold with { PacketCursor81BC = NativeBattleEffectNodeMemory.ArenaAddress - 1 },
            cold with { AlternateCursor81C0 = NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length + 1 }
        })
        {
            Assert.Throws<InvalidDataException>(invalid.Validate);
        }
    }

    [Fact]
    public void Entry_rejects_missing_owners_and_keeps_native_zero_target_division()
    {
        var fixture = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(fixture.Frame with { Memory = fixture.Frame.Memory with { Carbuncle278 = null } }, fixture.Packets, fixture.Models, 42));
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(fixture.Frame with { Effect = fixture.Frame.Effect with { ResourcePointers = [] } }, fixture.Packets, fixture.Models, 42));
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Frame.Memory.Arena!)).MarkUnknown(0x100, 8) } }, fixture.Packets, fixture.Models, 42));
        NativeBattleActionHeaderSlot header = fixture.Packets.Headers[0];
        NativeBattleActionPacketsState empty = fixture.Packets with { Headers = fixture.Packets.Headers.SetItem(0, header with { Payload = header.Payload.SetItem(0x10, 0) }) };
        Assert.Throws<DivideByZeroException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(fixture.Frame, empty, fixture.Models, 42));
        Assert.Throws<JsonException>(() => NativeBattleTestSnapshots.RestoreJson<NativeBattleCarbuncle278State>("{}"));
    }

    private static (NativeBattleCarbuncle278Frame Frame, NativeBattleActionPacketsState Packets, ImmutableArray<NativeBattleGraphicsModel> Models) Fixture()
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
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x100, tim).Write(0x2A120, tim), Carbuncle278 = NativeBattleCarbuncle278State.FromColdProcess() };
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 3, 278, null));
        foreach (byte target in new byte[] { 0, 1 })
        {
            (packets, _) = packets.AppendContact(new(new(target, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(2);
        NativeBattleOriginalEffectState effect = new(278, 277, new(278, 0x680C50, 0x680C60), new(0, 0, 3, 0, 0, 0), null) { ResourcePointers = [new(0x2508110, 0x100)] };
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, index == 3 ? (ushort)2 : (ushort)0)
        {
            Transform = new(new(0, 0, 0), new(0, 0, checked((short)(index == 0 ? -101 : index == 1 ? 100 : 400))), 0, 0, -3)
        }).ToImmutableArray();
        return (new(memory, effect, NativeCrtRandomState.FromColdProcess()), packets, models);
    }

    private static T RoundTrip<T>(T value) => NativeBattleTestSnapshots.RestoreJson<T>(NativeBattleTestSnapshots.Json(value))!;
}
