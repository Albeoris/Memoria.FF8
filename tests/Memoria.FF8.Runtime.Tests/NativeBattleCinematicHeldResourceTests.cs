using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicHeldResourceTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public void Current_clone_descriptors_refresh_while_held_polygon_keeps_its_publication_after_rebind_and_restore(bool remastered, bool logical, bool owned)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        ImmutableArray<byte> first = Enumerable.Range(0, 64).Select(value => (byte)value).ToImmutableArray();
        int origin = logical ? layout.BankLength + 4 : 4;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(origin, first, 0, first.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleModelCpuResourceRow row = NativeBattleModelCpuResourceRow.Unknown();
        foreach (int field in new[] { 12, 16, 20, 24 })
        {
            row = row.Set(field, new(0, origin + (field - 12) * 4) { Layout = layout, ResourceOnly = logical }) with { Lengths = row.Lengths.SetItem(field, 16) };
        }
        if (owned)
        {
            bank = NativeBattleModelCpuBank.Unknown(68).BindResource(4, first, 0, first.Length);
            cpu = cpu with { EnemyResources = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty.Add(7, bank) };
            foreach (int field in new[] { 12, 16, 20, 24 })
            {
                row = row.Set(field, new(3, 1024 + (field - 12) * 4) { Layout = layout, EnemyResourceId = 7, ResourceOffset = 4 + (field - 12) * 4 });
            }
        }
        uint clone = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x4010, Enumerable.Repeat((byte)0xA5, 12).ToArray()).Write(0x4020, [0x5A]);
        arena = NativeBattleCinematicActorResourceDescriptors.Refresh(arena, cpu, row, clone);
        NativeBattleResourcePointer held = arena.ReadResourcePointer(0x4034)!;
        Assert.Same(bank, held.CpuPublication);
        NativeBattleEffectArena before = NativeBattleStorageSnapshots.Capture(arena);
        ImmutableArray<byte> replacement = Enumerable.Repeat((byte)0xCC, 64).ToImmutableArray();
        NativeBattleModelCpuBank next = owned ? bank.BindResource(4, replacement, 0, replacement.Length) : bank.BindPartyResource(origin, replacement, 0, replacement.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, next) } };
        if (owned)
        {
            cpu = cpu with { EnemyResources = cpu.EnemyResources!.SetItem(7, next) };
        }
        row = row.Set(16, row.Require(20));
        arena = NativeBattleCinematicActorResourceDescriptors.Refresh(arena, cpu, row, clone);
        NativeBattleResourcePointer refreshed = arena.ReadResourcePointer(0x4034)!;
        Assert.Same(next, refreshed.CpuPublication);
        Assert.Equal(row.Require(20), refreshed.Cpu);
        Assert.Equal((byte)0x5A, arena.Read(0x4020, 1)[0]);
        Assert.Throws<NotSupportedException>(() => arena.Read(0x4010, 12));
        Assert.Equal(first.AsSpan(16, 16).ToArray(), NativeBattleModelResourceView.Open(cpu, NativeBattleFileStaging.Unknown(), held, 16).Read(0, 16));
        Assert.True(NativeBattleModelCpuBank.SamePublication(bank, before.ReadResourcePointer(0x4034)!.CpuPublication));
        NativeBattleEffectArena saved = NativeBattleEffectArena.CreateUnknown(68).WriteResourcePointer(0, held);
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(saved));
        NativeBattleResourcePointer restoredHeld = restored.ReadResourcePointer(0)!;
        Assert.Equal(first.AsSpan(16, 16).ToArray(), NativeBattleModelResourceView.Open(cpu, NativeBattleFileStaging.Unknown(), restoredHeld, 16).Read(0, 16));
        if (logical || owned)
        {
            Assert.Throws<NotSupportedException>(() => restored.ReadUInt32(0));
        }
        else
        {
            Assert.Equal(held.Cpu!.PhysicalAddress, restored.ReadUInt32(0));
        }
    }

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(5, false, false)]
    [InlineData(6, false, false)]
    [InlineData(7, false, false)]
    [InlineData(2, true, false)]
    [InlineData(1, false, true)]
    [InlineData(2, true, true)]
    public void Logical_shard_spawn_physics_packets_retirement_and_reuse_follow_the_same_mode_algorithm(int selectedMode, bool quad, bool owned)
    {
        ushort mode = (ushort)selectedMode;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        ImmutableArray<byte> polygon = Enumerable.Range(0, 20).Select(value => (byte)value).ToImmutableArray();
        int origin = NativeBattleModelCpuBank.Length + 4;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindPartyResource(origin, polygon, 0, polygon.Length);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        NativeBattleResourcePointer pointer = new NativeBattleResourcePointer(new(0, origin) { ResourceOnly = true }, null, quad ? 20 : 16).Pin(cpu);
        if (owned)
        {
            bank = NativeBattleModelCpuBank.Unknown(polygon.Length).BindResource(0, polygon, 0, polygon.Length);
            cpu = cpu with { EnemyResources = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty.Add(7, bank) };
            pointer = new NativeBattleResourcePointer(new(3, 1024) { EnemyResourceId = 7 }, null, quad ? 20 : 16).Pin(cpu);
        }
        ImmutableArray<NativeBattleVector> vertices = quad ? [new(-5, -1, 100), new(5, -1, 100), new(5, -1, 110), new(-5, -1, 110)] : [new(-5, -1, 100), new(5, -1, 100), new(0, -1, 110)];
        NativeBattleControl18Face numeric = new(0x1939020, quad, vertices);
        NativeBattleControl18ResourceFace logical = new(pointer, quad, vertices);
        byte[] payload = Controls(mode);
        short threshold = mode == 7 ? (short)-2 : (short)0;
        NativeBattleControl18SpawnServices services = new(1, (random, _, _, _, _) => random) { Mode5Anchors = ImmutableArray.Create<byte>(0, 0, 100, 0) };
        NativeBattleControl18SpawnStep expected = mode == 5 ? NativeBattleCinematicBreakupMode5.Advance(new byte[136], new byte[2], 0, payload, [numeric], threshold, services.Mode5Anchors.AsSpan(), new(123), services) : NativeBattleControl18Spawn.Advance(new byte[136], new byte[2], 0, payload, [numeric], threshold, new(123), services);
        NativeBattleControl18SpawnStep actual = mode == 5 ? NativeBattleCinematicBreakupMode5.Advance(new byte[136], new byte[2], 0, payload, [logical], threshold, services.Mode5Anchors.AsSpan(), new(123), services, ImmutableDictionary<int, NativeBattleResourcePointer>.Empty) : NativeBattleControl18Spawn.Advance(new byte[136], new byte[2], 0, payload, [logical], threshold, new(123), services, ImmutableDictionary<int, NativeBattleResourcePointer>.Empty);
        Assert.Equal((ushort)1, actual.LiveCount);
        Assert.Equal(expected.Records.AsSpan()[4..].ToArray(), actual.Records.AsSpan()[4..].ToArray());
        Assert.Equal(expected.Random, actual.Random);
        Assert.Same(bank, actual.ResourcePointers[0].CpuPublication);
        Assert.True(NativeBattleControl18ResourceRecords.Active(actual.Records.AsSpan(), 0, actual.ResourcePointers));
        NativeBattleControl18SpawnStep second = NativeBattleControl18Spawn.Advance(actual.Records.AsSpan(), new byte[] { 1, 0 }, 1, Controls(2), [logical, logical], 0, actual.Random, services, actual.ResourcePointers);
        Assert.Equal((ushort)2, second.LiveCount);
        Assert.True(second.ResourcePointers.ContainsKey(68));
        NativeBattleEffectArena records = NativeBattleEffectArena.CreateUnknown(136).Write(0, actual.Records.AsSpan()).WriteResourcePointer(0, actual.ResourcePointers[0]);
        records = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(records));
        ImmutableDictionary<int, NativeBattleResourcePointer> owners = records.EnumerateResourcePointers().ToImmutableDictionary(entry => entry.Key, entry => entry.Value!);
        NativeBattleModelCpuResources replaced = cpu with { Cpu = NativeBattleModelCpuBanks.Unknown(), EnemyResources = owned ? ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty : cpu.EnemyResources };
        NativeBattleModuleProjectedVertex Project(NativeBattleVector point) => new(0x00100020, 1000, 0);
        NativeBattleGfGeometryBatch before = NativeBattleControl18Shards.Emit(expected.Records.AsSpan(), (_, length) => polygon.AsSpan(0, length).ToArray().ToImmutableArray(), new uint[] { 1, 2, 3 }, Project, 17);
        NativeBattleGfGeometryBatch after = NativeBattleControl18Shards.Emit(actual.Records.AsSpan(), owners, (source, length) => NativeBattleModelResourceView.Open(replaced, NativeBattleFileStaging.Unknown(), source, length).Read(0, length).ToImmutableArray(), new uint[] { 1, 2, 3 }, Project, 17);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        Assert.NotNull(Assert.Single(after.Attempts).Primitive);
        NativeBattleControl18PhysicsStep a = NativeBattleControl18Physics.Advance(actual.Records.AsSpan(), actual.PolygonStates.AsSpan(), actual.LiveCount, mode, owners);
        NativeBattleControl18PhysicsStep e = NativeBattleControl18Physics.Advance(expected.Records.AsSpan(), expected.PolygonStates.AsSpan(), expected.LiveCount, mode);
        Assert.Equal(e.Records.AsSpan()[4..].ToArray(), a.Records.AsSpan()[4..].ToArray());
        Assert.Equal(e.LiveCount, a.LiveCount);
        for (int tick = 0; tick < 3; tick++)
        {
            a = NativeBattleControl18Physics.Advance(a.Records.AsSpan(), a.PolygonStates.AsSpan(), a.LiveCount, mode, a.ResourcePointers);
        }
        Assert.Equal((ushort)0, a.LiveCount);
        Assert.Empty(a.ResourcePointers);
        Assert.False(NativeBattleControl18ResourceRecords.Active(a.Records.AsSpan(), 0, a.ResourcePointers));
        NativeBattleModelCpuBank next = owned ? bank.BindResource(0, Enumerable.Repeat((byte)0xCC, polygon.Length).ToImmutableArray(), 0, polygon.Length) : bank.BindPartyResource(origin, Enumerable.Repeat((byte)0xCC, polygon.Length).ToImmutableArray(), 0, polygon.Length);
        NativeBattleModelCpuResources nextCpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, next) } };
        if (owned)
        {
            nextCpu = nextCpu with { EnemyResources = nextCpu.EnemyResources!.SetItem(7, next) };
        }
        NativeBattleResourcePointer nextPointer = new NativeBattleResourcePointer(pointer.Cpu, null, pointer.Length).Pin(nextCpu);
        NativeBattleControl18SpawnStep reused = NativeBattleControl18Spawn.Advance(a.Records.AsSpan(), new byte[2], 0, Controls(2), [new NativeBattleControl18ResourceFace(nextPointer, quad, vertices)], 0, actual.Random, services, a.ResourcePointers);
        Assert.Equal((ushort)1, reused.LiveCount);
        Assert.Same(next, reused.ResourcePointers[0].CpuPublication);
        Assert.Same(bank, actual.ResourcePointers[0].CpuPublication);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x80000000u)]
    [InlineData(uint.MaxValue)]
    public void Dissolve_identity_keeps_original_uint_unchecked_mix(uint geometry)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        uint clone = NativeBattleEffectNodeMemory.ArenaAddress + 0x1000;
        NativeBattlePilotVisualLifetimes.Allocated(arena, clone, 0);
        NativeBattlePilotVisualIdentity source = new(265, 1, 1, 1, 1) { ResourceGeneration = long.MaxValue };
        NativeBattlePilotVisualIdentity allocated = NativeBattlePilotVisualLifetimes.Identity(source.Effect, arena, clone, 0);
        long expected = unchecked(((allocated.Generation * 397 ^ 0x1D99774u) * 397 ^ geometry) * 397 ^ long.MaxValue);
        Assert.Equal(expected, NativeBattleCinematic31Presentation.DissolveSource(source, arena, clone, 0x1D99774, geometry).ResourceGeneration);
    }

    [Fact]
    public void Invalid_record_receipt_slot_or_unpinned_CPU_owner_is_rejected()
    {
        NativeBattleResourcePointer pointer = new(new(0, 0) { ResourceOnly = true }, null, 16);
        Assert.Throws<InvalidDataException>(() => NativeBattleControl18ResourceRecords.Require(ImmutableDictionary<int, NativeBattleResourcePointer>.Empty.Add(0, pointer), 68));
        Assert.Throws<InvalidDataException>(() => NativeBattleControl18ResourceRecords.Require(ImmutableDictionary<int, NativeBattleResourcePointer>.Empty.Add(1, new(null, 0x1939020)), 68));
    }

    [Fact]
    public void Mode5_typed_anchor_initialization_preserves_polar_quantization_and_frame_registers()
    {
        NativeBattleControl18Face triangle = new(0x1939020, false, [new(8, 300, 0), new(8, 300, 0), new(8, 300, 0)]);
        NativeBattleControl18Face quad = new(0x1939040, true, [new(-1, 100, 0), new(0, 200, 0), new(0, 300, 0), new(0, 400, 0)]);
        NativeBattleCinematicBreakupMode5Step numeric = NativeBattleCinematicBreakupMode5.Initialize(WaterSchedulerFixture.Create(), new(0, 123, 0), [triangle, quad]);
        NativeBattleCinematicBreakupMode5Step typed = NativeBattleCinematicBreakupMode5.Initialize(WaterSchedulerFixture.Create(), new(0, 123, 0), [new NativeBattleControl18ResourceFace(new(null, triangle.Address), false, triangle.Vertices), new(new(null, quad.Address), true, quad.Vertices)]);
        Assert.Equal(numeric.Anchors, typed.Anchors);
        Assert.Equal(numeric.Frame.ReadTemporary(0xD0, 6), typed.Frame.ReadTemporary(0xD0, 6));
        Assert.Equal(numeric.Frame.ReadTemporaryDword(0xFC), typed.Frame.ReadTemporaryDword(0xFC));
    }

    private static byte[] Controls(ushort mode)
    {
        byte[] controls = new byte[0x4C];
        BinaryPrimitives.WriteUInt16LittleEndian(controls.AsSpan(8), mode);
        controls[0xC] = 2;
        return controls;
    }
}
