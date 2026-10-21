using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTurnBasedIdleContinuationTests
{
    [Fact]
    public void Standalone_pass_continues_after_an_empty_enemy_turn_and_restores_its_pending_request()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_IDLE_TURN_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone battle checkpoint is required.");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source!));
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        BattleSession battle = world.Battle!;
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == 3);
        BattleCombatant waitingPlayer = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor != caster);
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive);
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: BattleAbility.GuardianForce, ActionId: 1));
        for (int callback = 0; callback < 1000 && battle.GuardianForceCasts.Count == 0; callback++)
        {
            battle.Tick();
        }
        Assert.Single(battle.GuardianForceCasts);
        for (int pass = 0; pass < 2; pass++)
        {
            int completed = battle.Events.Count(item => item.Kind == "ActionComplete");
            Assert.True(battle.AdvanceTurn() > 0);
            for (int callback = 0; callback < 400; callback++)
            {
                battle.Tick();
            }
            Assert.True(battle.Events.Count(item => item.Kind == "ActionComplete") > completed);
            Assert.Null(battle.Capture().TurnAdvance);
        }
        Assert.True(battle.AdvanceTurn() > 0);
        Assert.NotNull(battle.Capture().TurnAdvance);
        Assert.Equal(249, battle.Frame);
        Assert.Equal((ushort)164, Assert.Single(battle.GuardianForceCasts).RemainingTicks);
        int queued = battle.Events.Count(item => item.Kind == "Queued");
        string? pendingPath = Save("pending-idle-turn");
        Assert.NotNull(pendingPath);
        GameSession restoredSession = new(GameSession.Load(pendingPath!));
        using GameWorld restoredWorld = new(restoredSession, TestResources.GameDirectory);
        restoredWorld.EnsureField();
        BattleSession restored = restoredWorld.Battle!;
        Assert.Equal(Digest(battle.Capture()), Digest(restored.Capture()));

        for (int callback = 0; callback < 10 && battle.GuardianForceCasts is [{ RemainingTicks: > 0 }]; callback++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.True(battle.GuardianForceCasts.Count == 0 || battle.GuardianForceCasts[0].RemainingTicks == 0);
        Assert.Equal(270, battle.Frame);
        Assert.Null(battle.Capture().TurnAdvance);
        Assert.True(waitingPlayer.IsReady);
        Assert.Equal(BattleSession.AtbMaximum, waitingPlayer.Atb);
        Assert.Equal(queued, battle.Events.Count(item => item.Kind == "Queued"));
        Assert.Equal(Digest(battle.Capture()), Digest(restored.Capture()));
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source!)));
        Save("charge-ready");

        string? Save(string name)
        {
            string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_IDLE_TURN_OUTPUT") ?? TestResources.ArtifactDirectory;
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, name + ".json");
            world.CaptureBattleState();
            session.SetEventState(world.Events.Capture());
            session.Save(path);
            return path;
        }
    }

    private static byte[] Digest(BattleSnapshot snapshot)
    {
        using SHA256 hash = SHA256.Create();
        using CryptoStream stream = new(Stream.Null, hash, CryptoStreamMode.Write);
        JsonSerializer.Serialize(stream, snapshot);
        stream.FlushFinalBlock();
        return hash.Hash!;
    }
}
