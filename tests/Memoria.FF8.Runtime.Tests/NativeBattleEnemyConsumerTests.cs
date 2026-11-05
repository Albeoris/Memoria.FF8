using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnemyConsumerTests
{
    [Theory]
    [InlineData(290, false, false)]
    [InlineData(290, false, true)]
    [InlineData(290, true, false)]
    [InlineData(290, true, true)]
    [InlineData(308, false, false)]
    [InlineData(308, false, true)]
    [InlineData(308, true, false)]
    [InlineData(308, true, true)]
    public void Current_bone_padding_writes_selected_enemy_owner_and_keeps_pinned_history_after_restore(int family, bool remastered, bool owned)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        byte[] bytes = new byte[64];
        bytes[0] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(50), 0x1234);
        NativeBattleModelCpuReference reference = new(3, 1024) { Layout = layout, EnemyResourceId = owned ? 7 : null, ResourceOffset = owned ? 4 : 0 };
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        if (owned)
        {
            cpu = cpu with { EnemyResources = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty.Add(7, NativeBattleModelCpuBank.Unknown(68).BindResource(4, bytes.ToImmutableArray(), 0, bytes.Length)) };
        }
        cpu = cpu with { Rows = cpu.Rows.SetItem(0, cpu.Rows[0].Set(12, reference) with { Lengths = ImmutableDictionary<int, int>.Empty.Add(12, 64) }) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write((owned ? reference.NativePlacementAddress : reference.SharedAddress), bytes);
        NativeBattleResourcePointer held = new NativeBattleResourcePointer(reference, null, 64).Pin(cpu);
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        Set(nameof(BattleSession.NativeModelCpuResources), cpu);
        NativeBattleGraphicsState graphics = new([new(0, 16) { ActiveResourceRow100 = 0 }], ["enemy"], 0, 0);
        NativeBattleOriginalEffectState effect = new((ushort)family, family - 1, new((ushort)family, 1, 1), null, 0);
        object frame = family == 290
            ? (object)new NativeBattleEffect290Frame(NativeBattleEffectMemory.FromColdProcess(), effect, new(42), NativeBattleEffect290State.FromColdProcess()) { Staging = staging }
            : new NativeBattleEffect308Frame(NativeBattleEffectMemory.FromColdProcess(), effect, new(42), NativeBattleEffect308State.FromColdProcess()) { Staging = staging };
        frame = typeof(BattleSession).GetMethod($"WriteNativeEffect{family}BonePadding", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [graphics, frame, 0, 0, (ushort)0x5678])!;
        staging = (NativeBattleFileStaging)frame.GetType().GetProperty("Staging")!.GetValue(frame)!;
        NativeBattleResourcePointer current = new(reference, null, 64);
        Assert.Equal((ushort)0x5678, BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleModelResourceView.Open(session.NativeModelCpuResources!, staging, current, 64).Read(50, 2)));
        if (owned)
        {
            Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleModelResourceView.Open(session.NativeModelCpuResources!, staging, held, 64).Read(50, 2)));
            Assert.Equal(bytes, staging.Read((owned ? reference.NativePlacementAddress : reference.SharedAddress), bytes.Length));
        }
        Saved restored = NativeBattleTestSnapshots.RestoreJson<Saved>(NativeBattleTestSnapshots.Json(new Saved(session.NativeModelCpuResources!, staging, held)));
        Assert.Equal((ushort)0x5678, BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleModelResourceView.Open(restored.Cpu, restored.Staging, current, 64).Read(50, 2)));
        if (owned)
        {
            Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleModelResourceView.Open(restored.Cpu, restored.Staging, restored.Held, 64).Read(50, 2)));
        }
        void Set(string name, object value)
        {
            typeof(BattleSession).GetProperty(name)!.SetValue(session, value);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pointer_bit_observers_keep_authentic_enemy_placement_without_projecting_resource_memory(bool remastered)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelCpuReference reference = new(3, 1024) { Layout = layout, EnemyResourceId = 7, ResourceOffset = 16 };
        NativeBattleResourcePointer owned = new(reference, null, 112);
        Assert.Null(reference.TryPhysicalAddress());
        Assert.Throws<NotSupportedException>(() => reference.SharedAddress);
        NativeBattleStackBytes expected31 = NativeBattleEffect31Stack.ObserveTarget(NativeBattleEffect31Stack.Begin(), 0x2000000, 0x1D972C0, reference.NativePlacementAddress);
        NativeBattleStackBytes actual31 = NativeBattleEffect31Stack.ObserveTarget(NativeBattleEffect31Stack.Begin(), 0x2000000, 0x1D972C0, owned);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected31), NativeBattleTestSnapshots.Json(actual31));
        NativeBattleStackBytes expected79 = NativeBattleEffect79Stack.ObserveTargetBounds(NativeBattleEffect79Stack.Begin(0), new(null, reference.NativePlacementAddress, 112), 2);
        NativeBattleStackBytes actual79 = NativeBattleEffect79Stack.ObserveTargetBounds(NativeBattleEffect79Stack.Begin(0), owned, 2);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected79), NativeBattleTestSnapshots.Json(actual79));
        NativeBattleResourcePointer restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleResourcePointer>(NativeBattleTestSnapshots.Json(owned));
        Assert.Equal(NativeBattleTestSnapshots.Json(actual79), NativeBattleTestSnapshots.Json(NativeBattleEffect79Stack.ObserveTargetBounds(NativeBattleEffect79Stack.Begin(0), restored, 2)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Cleanup_writes_current_enemy_geometry_and_retains_pinned_source(bool remastered, bool owned)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        byte[] bytes = NativeBattleEffect301BodyTests.Geometry(false);
        bytes[59] |= 0x80;
        NativeBattleModelCpuReference reference = new(3, 1024) { Layout = layout, EnemyResourceId = owned ? 7 : null, ResourceOffset = owned ? 4 : 0 };
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        if (owned)
        {
            cpu = cpu with { EnemyResources = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty.Add(7, NativeBattleModelCpuBank.Unknown(bytes.Length + 4).BindResource(4, bytes.ToImmutableArray(), 0, bytes.Length)) };
        }
        cpu = cpu with { Rows = cpu.Rows.SetItem(0, cpu.Rows[0].Set(16, reference) with { Lengths = ImmutableDictionary<int, int>.Empty.Add(16, bytes.Length) }) };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(reference.NativePlacementAddress, bytes);
        NativeBattleResourcePointer held = new NativeBattleResourcePointer(reference, null, bytes.Length).Pin(cpu);
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!.SetValue(session, cpu);
        NativeBattleGraphicsState graphics = new([new(0, 16) { ActiveResourceRow100 = 0 }], ["enemy"], 0, 0);
        NativeBattleEffect64Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), new(64, 63, new(64, 1, 1), null, 0), new(42), NativeBattleEffect64State.FromColdProcess()) { Staging = staging };
        frame = (NativeBattleEffect64Frame)typeof(BattleSession).GetMethod("ClearNativeEffect64Geometry", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [graphics, frame, 0x1D972C0u])!;
        Assert.Equal(0, NativeBattleModelResourceView.Open(session.NativeModelCpuResources!, frame.Staging!, new(reference, null, bytes.Length), bytes.Length).Read(59, 1)[0] & 0x80);
        if (owned)
        {
            Assert.Equal(0x80, NativeBattleModelResourceView.Open(session.NativeModelCpuResources!, frame.Staging!, held, bytes.Length).Read(59, 1)[0] & 0x80);
            Assert.Equal(bytes, frame.Staging!.Read(reference.NativePlacementAddress, bytes.Length));
        }
        Saved restored = NativeBattleTestSnapshots.RestoreJson<Saved>(NativeBattleTestSnapshots.Json(new Saved(session.NativeModelCpuResources!, frame.Staging!, held)));
        Assert.Equal(0, NativeBattleModelResourceView.Open(restored.Cpu, restored.Staging, new(reference, null, bytes.Length), bytes.Length).Read(59, 1)[0] & 0x80);
    }

    private sealed record Saved(NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging, NativeBattleResourcePointer Held);
}
