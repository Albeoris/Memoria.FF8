using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleWorkingStorageIsolationTests
{
    [Fact]
    public void Published_session_memory_keeps_its_physical_prefix_while_unpublished_task_banks_roll_back()
    {
        BattleSession session = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        NativeBattleEffectMemory memory = session.NativeEffectMemory!.BeginLoad(0);
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x3000000, 0x20DFAB8, 20, 1).BindArena(memory.Arena!);
        NativeBattleEffectTaskPool auxiliary = NativeBattleEffectTaskPool.Initialize(0x3100000, 0x3200000, 20, 1);
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleTaskOperation operation = new([main, auxiliary]);
            main.Slots[0].WriteDword(12, 17);
            auxiliary.Slots[0].WriteDword(12, 23);
            typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, memory);
            main.Slots[0].WriteDword(12, 99);
            auxiliary.Slots[0].WriteDword(12, 100);
            throw new InvalidOperationException("Unpublished suffix failed.");
        }));
        Assert.Equal(17u, session.NativeEffectMemory!.Arena!.ReadUInt32(12));
        Assert.Equal(17u, main.Slots[0].ReadDword(12));
        Assert.Equal(0u, auxiliary.Slots[0].ReadDword(12));
        Assert.Equal(17u, session.Capture().NativeEffectMemory!.Arena!.ReadUInt32(12));
    }

    [Fact]
    public void Captures_definitions_cold_templates_and_two_restores_have_independent_owned_banks()
    {
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        string coldJson = JsonSerializer.Serialize(cold);
        BattleDefinition definition = new()
        {
            NativeRetainedMemory = cold,
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        };
        BattleSession live = new(definition, BattleAtbMode.Normal);
        NativeBattleQuake38State fields = live.NativeEffectMemory!.Quake38!;
        fields.SetDword(0x278C8E0, 17);
        BattleSnapshot first = live.Capture();
        string firstJson = JsonSerializer.Serialize(first);
        fields.SetDword(0x278C8E0, 18);
        BattleSnapshot second = live.Capture();
        BattleSession a = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(firstJson)!);
        BattleSession b = BattleSession.Restore(first);
        a.NativeEffectMemory!.Quake38!.SetDword(0x278C8E0, 19);

        Assert.Equal(17u, b.NativeEffectMemory!.Quake38!.Dword(0x278C8E0));
        Assert.Equal(18u, second.NativeEffectMemory!.Quake38!.Dword(0x278C8E0));
        Assert.Equal(18u, fields.Dword(0x278C8E0));
        Assert.Equal(firstJson, JsonSerializer.Serialize(first));
        Assert.Equal(coldJson, JsonSerializer.Serialize(cold));
        Assert.Equal(0u, first.Definition.NativeRetainedMemory!.EffectMemory!.Quake38!.Dword(0x278C8E0));
        Assert.Throws<InvalidOperationException>(() => first.NativeEffectMemory!.Quake38!.SetByte(0x278C8E0, 0));
        Assert.Throws<InvalidOperationException>(() => cold.EffectMemory!.Quake38!.SetByte(0x278C8E0, 0));
    }

    [Fact]
    public void A_checkpoint_cannot_escape_through_battle_capture()
    {
        BattleSession live = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        NativeBattleQuake38State fields = live.NativeEffectMemory!.Quake38!;
        fields.BeginCheckpoint();
        fields.SetDword(0x278C8E0, 20);
        Assert.Throws<InvalidOperationException>(() => live.Capture());
        fields.RollbackCheckpoint();
        Assert.Equal(0u, live.Capture().NativeEffectMemory!.Quake38!.Dword(0x278C8E0));
    }
}
