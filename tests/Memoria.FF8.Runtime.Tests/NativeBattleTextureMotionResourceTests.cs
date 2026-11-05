using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureMotionResourceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Current_texture_table_reads_and_advances_only_its_declared_domain_after_promotion_and_JSON(bool remastered, bool logical)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        byte[] guest = Table(2);
        byte[] resource = Table(5);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindResource(4, guest.ToImmutableArray(), 0, guest.Length).BindPartyResource(layout.BankLength + 4, resource.ToImmutableArray(), 0, resource.Length);
        int origin = logical ? layout.BankLength + 4 : 4;
        NativeBattleModelCpuReference selected = new(0, origin) { Layout = layout, ResourceOnly = logical };
        NativeBattleModelCpuResourceRow row = cpu.Rows[0].Set(48, selected).Set(44, selected with { Offset = origin + guest.Length });
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) }, Rows = cpu.Rows.SetItem(0, row) };
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.FromColdProcess();
        cache = cache with { Rows = cache.Rows.SetItem(0, cache.Rows[0] with { Kind = 1 }) };
        Set(nameof(BattleSession.NativeModelCpuResources), cpu);
        Set(nameof(BattleSession.NativeModelTextureCache), cache);
        Set(nameof(BattleSession.NativeFileStaging), NativeBattleFileStaging.Unknown());
        NativeBattleGraphicsModel model = new(0, 3) { TextureResourceIndex = 0, TextureAllocationMask = 1 };
        ImmutableArray<byte> Read() => (ImmutableArray<byte>)typeof(BattleSession).GetMethod("ReadNativeTextureMotion", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [model])!;
        Assert.Equal(logical ? resource : guest, Read().ToArray());
        typeof(BattleSession).GetMethod("AdvanceNativeTextureMotion", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [model, (byte)0]);
        Assert.Equal(logical ? 6 : 3, Read()[9]);
        NativeBattleModelCpuResources changed = session.NativeModelCpuResources!;
        NativeBattleResourcePointer opposite = new(new(0, logical ? 4 : layout.BankLength + 4) { Layout = layout, ResourceOnly = !logical }, null, guest.Length);
        Assert.Equal(logical ? guest : resource, NativeBattleModelResourceView.Open(changed, session.NativeFileStaging!, opposite, guest.Length).Read(0, guest.Length));
        Set(nameof(BattleSession.NativeModelCpuResources), NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(changed)));
        Assert.Equal(logical ? 6 : 3, Read()[9]);
        Assert.Equal(guest, bank.ReadGuest(4, guest.Length));
        Assert.Equal(resource, bank.Read(layout.BankLength + 4, resource.Length));
        void Set(string property, object value) => typeof(BattleSession).GetProperty(property)!.SetValue(session, value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Enemy_motion_counter_updates_owned_source_and_preserves_guest_shadow_and_pin_after_JSON(bool remastered)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        byte[] bytes = Table(5);
        NativeBattleModelCpuReference target = new(3, 1024) { Layout = layout, EnemyResourceId = 7, ResourceOffset = 4 };
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout), EnemyResources = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty.Add(7, NativeBattleModelCpuBank.Unknown(16).BindResource(4, bytes.ToImmutableArray(), 0, bytes.Length)) };
        cpu = cpu with { Rows = cpu.Rows.SetItem(0, cpu.Rows[0].Set(48, target) with { Lengths = ImmutableDictionary<int, int>.Empty.Add(48, bytes.Length) }) };
        NativeBattleResourcePointer pin = new NativeBattleResourcePointer(target, null, bytes.Length).Pin(cpu);
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.FromColdProcess();
        cache = cache with { Rows = cache.Rows.SetItem(0, cache.Rows[0] with { Kind = 3 }) };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!.SetValue(session, cpu);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelTextureCache))!.SetValue(session, cache);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeFileStaging))!.SetValue(session, NativeBattleFileStaging.Unknown().Write(target.NativePlacementAddress, bytes));
        NativeBattleGraphicsModel model = new(0, 16) { TextureResourceIndex = 0, TextureAllocationMask = 1 };
        typeof(BattleSession).GetMethod("AdvanceNativeTextureMotion", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [model, (byte)0]);
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(session.NativeModelCpuResources!));
        Assert.Equal(6, NativeBattleModelResourceView.Open(restored, session.NativeFileStaging!, new(target, null, bytes.Length), bytes.Length).Read(9, 1)[0]);
        Assert.Equal(5, NativeBattleModelResourceView.Open(restored, session.NativeFileStaging!, pin, bytes.Length).Read(9, 1)[0]);
        Assert.Equal(bytes, session.NativeFileStaging!.Read(target.NativePlacementAddress, bytes.Length));
    }

    private static byte[] Table(byte counter)
    {
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 2);
        bytes[7] = 1;
        bytes[8] = 1;
        bytes[9] = counter;
        return bytes;
    }
}
