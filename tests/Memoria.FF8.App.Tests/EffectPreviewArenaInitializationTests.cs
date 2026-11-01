using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class EffectPreviewArenaInitializationTests
{
    [Fact]
    public void Installed_Shiva_ability_publishes_its_actual_GF_header_without_menu_or_stock_admission()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select original resources for GF ability metadata.");
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        BattleSession active = world.StartEffectPreview(new(1, 0) { AbilityId = 1, CommandId = 3 });
        BattleNativeAction selected = active.ResolveDiagnosticPreviewAbility(3, 1);
        Assert.Equal(2, selected.KernelSection);
        Assert.Equal(1, selected.KernelIndex);
        Assert.Equal(254, selected.Command);
        Assert.Equal(65, selected.ActionId);
        Assert.Equal(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(selected.RawRecord.AsSpan().Slice(4)), selected.MagicId);
        int dispatches = 0;
        active.DiagnosticEffectDispatched += _ => dispatches++;
        active.PublishDiagnosticAbilityPreview(3, 1);
        NativeBattleActionHeaderSlot header = active.NativeActionPackets.Headers[0];
        Assert.Equal(254, header.ReadByte(1));
        Assert.Equal(65, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal(selected.MagicId, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal(0x50B2A0u, NativeBattleActionGraphics.SelectService(header).Callback);
        Assert.Empty(active.QueuedActions);
        Assert.Equal(0, dispatches);
    }

    [Fact]
    public void Installed_common_arena_initializes_selected_visual_models_without_enemy_AI_or_ATB_growth()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select original resources for graphical arena initialization.");
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        BattleSession active = world.StartEffectPreview(new(1, 0, 2));
        Assert.True(active.NativeEntryReady);
        Assert.Empty(active.ObserveDiagnosticDrainBlockers());
        BattleCombatant ally = Assert.Single(active.Combatants, actor => actor.IsPlayer);
        BattleCombatant enemy = Assert.Single(active.Combatants, actor => !actor.IsPlayer);
        Assert.Equal(0, ally.ModelId);
        Assert.Equal(1, enemy.ModelId);
        Assert.Equal(0, ally.NativeIndex);
        Assert.Equal(3, enemy.NativeIndex);
        Assert.Null(enemy.NativeAi);
        Assert.Equal(ally.NativeAtb!.Maximum, ally.NativeAtb.Current);
        Assert.Equal(0, enemy.NativeAtb!.Current);
        int allyHp = ally.CurrentHp;
        int enemyHp = enemy.CurrentHp;
        for (int callback = 0; callback < 4; callback++)
        {
            active.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1d / 30));
        }
        Assert.Equal(ally.NativeAtb.Maximum, ally.NativeAtb.Current);
        Assert.Equal(0, enemy.NativeAtb.Current);
        Assert.Equal(allyHp, ally.CurrentHp);
        Assert.Equal(enemyHp, enemy.CurrentHp);
        Assert.Empty(active.QueuedActions);
    }
}
