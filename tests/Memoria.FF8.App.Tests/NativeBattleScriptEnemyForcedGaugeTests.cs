using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleScriptEnemyForcedGaugeTests
{
    [Theory]
    [InlineData(0, 0x4000u)]
    [InlineData(0x20, 0u)]
    [InlineData(0x20, 0x4000u)]
    public void Original_script_enemy_forced_readiness_uses_deferred_callback_without_UI_command_or_target_RNG(ushort saved, uint status)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original enemy, formation and clock resources are required.");
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCRIPT_ENEMY_GAUGE_FIXTURE");
        Assert.SkipUnless(fixture is not null && File.Exists(fixture), "Set MEMORIA_FF8_SCRIPT_ENEMY_GAUGE_FIXTURE to a standalone story snapshot at the supported encounter.");
        GameSession session = new(GameSession.Load(fixture!));
        Assert.Equal(768, session.State.FieldId);
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        if (world.Battle is null)
        {
            Assert.Equal(136, session.State.PendingBattleEncounter?.Formation);
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 1));
        }
        BattleSession battle = world.Battle ?? throw new InvalidDataException("The selected story fixture did not enter its native battle.");
        Assert.Equal(136, battle.EncounterId);
        for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot initialized = battle.Capture();
        string enemyId = initialized.Definition.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == 88).Id;
        BattleCombatantSnapshot enemy = initialized.Combatants.Single(actor => actor.Id == enemyId);
        NativeBattleAtbState gauge = enemy.NativeAtb!;
        NativeBattleParticipantSnapshot participant = enemy.NativeParticipant!;
        NativeBattleStatusSnapshot statuses = enemy.NativeStatus!;
        BattleSnapshot source = initialized with
        {
            Combatants = initialized.Combatants.Select(actor => actor.Id == enemy.Id ? actor with
            {
                Atb = BattleSession.AtbMaximum,
                NativeAtb = gauge with { Current = gauge.Maximum },
                NativeParticipant = participant with { NativeCombatantFlags = (participant.NativeCombatantFlags!.Value & ~0x20Cu) | 0x10 },
                NativeStatus = statuses with { Saved = saved, Battle = status }
            } : actor).ToArray()
        };
        battle = Restore(source);
        BattleCombatant live = battle.Combatants.Single(actor => actor.Id == enemyId);
        BattleSnapshot before = battle.Capture();
        Invoke(battle, "PublishNativeFullGaugeRegistration", live);
        BattleSnapshot registered = battle.Capture();
        Assert.Equal(before.NativeGlobalRandom, registered.NativeGlobalRandom);
        Assert.Equal(before.NativeCrtRandom, registered.NativeCrtRandom);
        Assert.Equal(JsonSerializer.Serialize(before.NativeGraphics!.UiServices!.Commands), JsonSerializer.Serialize(registered.NativeGraphics!.UiServices!.Commands));
        Assert.Equal(JsonSerializer.Serialize(before.NativeCommandBanks), JsonSerializer.Serialize(registered.NativeCommandBanks));
        uint expectedFlags = before.Combatants.Single(actor => actor.Id == enemyId).NativeParticipant!.NativeCombatantFlags!.Value | 0x204;
        Assert.Equal(expectedFlags, live.NativeCombatantFlags);
        Invoke(battle, "PublishNativeRegisteredCallbacks4847B0");
        BattleSnapshot queued = battle.Capture();
        BattleAttackCommand command = Assert.Single(queued.NativeCommandBanks!.Banks[2].LogicalCommands.OfType<BattleAttackCommand>());
        Assert.Equal(new BattleNativeCallback(checked((byte)live.NativeIndex), 1), command.NativeCallback);
        Assert.Equal(expectedFlags & ~0x200u, live.NativeCombatantFlags);
        BattleSession restored = Restore(queued);
        Invoke(battle, "PublishNativeRegisteredCallbacks4847B0");
        Invoke(restored, "PublishNativeRegisteredCallbacks4847B0");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Single(battle.NativeCommandBanks.Banks[2].LogicalCommands.OfType<BattleAttackCommand>());
        byte turn = live.AiTurnCount;
        Invoke(battle, "RunNativeAi", live, 1, null);
        Invoke(restored, "RunNativeAi", restored.Combatants.Single(actor => actor.Id == enemyId), 1, null);
        BattleSnapshot accepted = battle.Capture();
        Assert.Equal(unchecked((byte)(turn + 1)), live.AiTurnCount);
        Assert.Equal(JsonSerializer.Serialize(accepted), JsonSerializer.Serialize(restored.Capture()));
        Assert.Contains(accepted.QueuedActions, action => action.ActorId == live.Id && action.NativeQueuedCommand == 8 && action.ActionId == ((saved & 0x20) != 0 ? 204 : 205));
    }

    private static BattleSession Restore(BattleSnapshot snapshot)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
    }

    private static void Invoke(BattleSession battle, string name, params object?[] arguments)
    {
        try
        {
            typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }
}


