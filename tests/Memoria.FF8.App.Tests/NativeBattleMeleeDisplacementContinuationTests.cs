using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMeleeDisplacementContinuationTests
{
    [Theory]
    [InlineData("enemy-0")]
    [InlineData("enemy-1")]
    public void Attack_then_limit_uses_script_displacement_and_preserves_continuation(string targetId)
    {
        GameState source = GameSession.Load(TestResources.FixturePath("melee-command-sequence"));
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(TestResources.GameDirectory);
        GameSession session = new(source);
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Bind(battle);
        BattleScenePresentation scene = new(assets, battle.Capture().Definition, true);
        string? outputRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_MELEE_OUTPUT");
        string? output = outputRoot is null ? null : Path.Combine(outputRoot, targetId);
        if (output is not null)
        {
            Directory.CreateDirectory(output);
        }
        string attacker = Assert.Single(source.Battle!.Definition.Combatants, actor => actor.IsPlayer && actor.ModelId == 3).Id;
        string limitActor = Assert.Single(source.Battle.Definition.Combatants, actor => actor.IsPlayer && actor.ModelId == 0).Id;
        Publish("before-attack");
        Assert.True(battle.CanChoosePlayerCommand(attacker));
        battle.QueueAction(attacker, targetId, BattleAbility.Attack, 0);
        RunAction(attacker, false);
        Publish("after-attack");
        for (int callback = 0; callback < 4096 && !battle.CanChoosePlayerCommand(limitActor); callback++)
        {
            battle.AdvanceTurn();
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand(limitActor));
        for (int selection = 0; selection < 256 && battle.GetNativeLimitLevel(limitActor) == 0; selection++)
        {
            battle.RefreshNativeLimit(limitActor);
        }
        Assert.True(battle.GetNativeLimitLevel(limitActor) > 0);
        Publish("before-limit");
        battle.QueueNativePlayerCommand(limitActor, targetId, 5, 0);
        RunAction(limitActor, true);
        Publish("completed");

        void Bind(BattleSession session)
        {
            session.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        }

        void RunAction(string actorId, bool limit)
        {
            int index = battle.NativeGraphics!.ActorIds.IndexOf(actorId);
            int target = battle.NativeGraphics.ActorIds.IndexOf(targetId);
            NativeBattleGraphicsModel initial = battle.NativeGraphics.Models[index];
            double initialDistance = Distance(initial);
            double minimumDistance = initialDistance;
            int events = battle.Events.Count;
            bool displaced = false;
            bool restored = false;
            bool hit = false;
            using StreamWriter? trace = output is null ? null : new(Path.Combine(output, limit ? "limit.jsonl" : "attack.jsonl"));
            for (int callback = 0; callback < 4096; callback++)
            {
                battle.Tick();
                NativeBattleGraphicsModel model = battle.NativeGraphics!.Models[index];
                NativeBattleSequenceState state = model.SequenceState!;
                minimumDistance = Math.Min(minimumDistance, Distance(model));
                trace?.WriteLine(JsonSerializer.Serialize(new { battle.Frame, Callback = battle.NativeGraphics.Callback, model.Sequence, state.Header36, state.Header38, state.Header40, model.GeometryWorldMatrix, model.Transform, Effect = battle.NativeGraphics.OriginalEffect?.Binding.Effect }));
                bool header = state.Header36 != 0 || state.Header38 != 0 || state.Header40 != 0;
                if (header)
                {
                    displaced = true;
                    if (!restored)
                    {
                        Publish(limit ? "limit-approach" : "attack-approach");
                        CompareNext();
                        restored = true;
                    }
                }
                if (!hit && battle.Events.Skip(events).Any(entry => entry.Kind == "Damage" && entry.ActorId == actorId))
                {
                    hit = true;
                    Publish(limit ? "limit-contact" : "attack-contact");
                    if (limit)
                    {
                        Assert.True(Distance(model) < initialDistance, "The limit actor must reach toward its physical target before the first damage contact.");
                    }
                    CompareNext();
                }
                if (battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.GetNativeRenzokuken(actorId) is null && battle.NativeGraphics.OriginalEffect is not { ReturnedDescriptor: > 0 })
                {
                    Assert.True(displaced, "The installed actor sequence must publish its displacement operands.");
                    Assert.True(hit);
                    Assert.True(restored);
                    Assert.True(minimumDistance < initialDistance, "Scripted movement must reach the retained world matrix.");
                    CompareNext();
                    return;
                }
            }
            Assert.Fail("The physical action did not complete within its callback budget.");

            double Distance(NativeBattleGraphicsModel model)
            {
                NativeBattleActionMatrix matrix = model.GeometryWorldMatrix!;
                NativeBattleVector destination = battle.NativeGraphics!.Models[target].Transform!.Position28;
                return Math.Pow(matrix.X - destination.X, 2) + Math.Pow(matrix.Z - destination.Z, 2);
            }
        }

        void CompareNext()
        {
            BattleSnapshot snapshot = battle.Capture();
            BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
            Bind(clone);
            battle.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(battle, 640, 448), new BattleScenePresentation(assets, snapshot.Definition, true).Render(clone, 640, 448));
        }

        void Publish(string name)
        {
            if (output is null)
            {
                return;
            }
            world.CaptureBattleState();
            session.Save(Path.Combine(output, name + ".json"), compact: true);
            byte[] pixels = scene.Render(battle, 640, 448);
            using var bitmap = Memoria.FF8.GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 640, 448);
            bitmap.Save(Path.Combine(output, name + ".png"));
        }
    }
}
