using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class ChocoboCaptureAiTests
{
    public static void Run()
    {
        TestCachedSelectors();
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original Chocobo death callbacks: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 2] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        foreach (int id in Enumerable.Range(56, 4))
        {
            SceneFormation formation = formations.Single(value => value.FormationId == id);
            Ff7BattleSession initial = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1);
            Ff7BattleSnapshot start = initial.Capture();
            Check(start.AiMasks == Ff7BattleAiCallbacks.ProjectMasks(start.Actors.Select(actor => actor.Actor).ToArray(), 0), $"Formation {id} did not refresh native masks after original initialization.");
            start = start with { Actors = start.Actors.Select(actor => actor.Actor.Slot < 3 ? actor with { Actor = actor.Actor with { HitPoints = 10000, MaxHitPoints = 10000 } } : actor).ToArray() };
            Ff7BattleSession Restore(Ff7BattleSnapshot snapshot) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
            Ff7BattleSession legacy = Restore(start with { AiMasks = null });
            Check(legacy.Capture().AiMasks == start.AiMasks && JsonSerializer.Serialize(legacy.Capture().Random) == JsonSerializer.Serialize(start.Random), $"Formation {id} absent cache restoration changed saved masks or RNG.");
            int pendingSlot = start.Actors.First(actor => actor.Actor.Slot >= 4).Actor.Slot;
            Ff7BattleSnapshot pending = start with { Actors = start.Actors.Select(actor => actor.Actor.Slot == pendingSlot ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | 1 } } : actor).ToArray() };
            Ff7BattleSession pendingCold = Restore(pending);
            Check(pendingCold.Capture().AiMasks == start.AiMasks, "Cold restore must preserve explicitly recorded pre-callback masks even when current actor death facts have changed.");
            Ff7BattleSession warm = Restore(start);
            Ff7BattleSession cold = Restore(start);
            for (int tick = 0; tick < 6000 && warm.LastHit is null; tick++)
            {
                try
                {
                    warm.Advance();
                }
                catch (NotSupportedException error)
                {
                    throw new NotSupportedException($"Original formation {id} turn failed at tick {warm.Tick}, command {warm.Action?.Command}: {error.Message}", error);
                }
                cold.Advance();
            }
            Check(warm.LastHit is { Source: >= 4 }, $"Formation {id} did not execute an original enemy turn.");
            Equal(warm, cold, $"Formation {id} original turn");
            warm = Restore(start);
            cold = Restore(start);
            Ff7BattleActorState? bird = start.Actors.SingleOrDefault(actor => actor.Identity == 339);
            int[] enemies = start.Actors.Where(actor => actor.Actor.Slot >= 4 && actor.Identity != 339).Select(actor => actor.Actor.Slot).ToArray();
            for (int index = 0; index < enemies.Length; index++)
            {
                int slot = enemies[index];
                Ff7BattleSnapshot before = warm.Capture();
                before = before with
                {
                    Actors = before.Actors.Select(actor => actor.Actor.Slot == slot ? actor with { Actor = actor.Actor with { HitPoints = 1 } } : actor).ToArray(),
                    Queue = Ff7BattleCommandQueue.Enqueue(before.Queue, new(2, 0, (byte)Ff7BattleCommand.Magic, 0x1E, (ushort)(1 << slot)))
                };
                warm = Restore(before);
                cold = Restore(before);
                bool observed = false;
                for (int tick = 0; tick < 2000; tick++)
                {
                    warm.Advance(waitForCommand: true);
                    cold.Advance(waitForCommand: true);
                    if (warm.Actors.Single(actor => actor.Actor.Slot == slot).Actor.HitPoints == 0)
                    {
                        observed = true;
                        break;
                    }
                }
                Check(observed, $"Formation {id} original death callback did not execute for slot {slot}.");
                Equal(warm, cold, $"Formation {id} death callback {index}");
                Ff7BattleSnapshot after = warm.Capture();
                Check((after.AiMasks!.Value.KnockedOut & 1 << slot) != 0, "Normal phase refresh must publish the newly dead actor after its callback.");
                bool caught = after.AiSaveBank1![0x54] == 1;
                bool finalEnemy = index == enemies.Length - 1;
                Check(caught == (bird is not null && finalEnemy), $"Formation {id} captured before the last hostile death or fabricated a bird: index={index}, caught={caught}.");
                if (bird is not null)
                {
                    Ff7BattleActorState currentBird = after.Actors.Single(actor => actor.Identity == 339);
                    Check(((currentBird.Actor.Conditions & 8) == 0) == finalEnemy, $"Formation {id} changed bird targetability at the wrong death boundary.");
                    if (!finalEnemy)
                    {
                        Check(warm.Message != "Caught a Chocobo.", "First hostile death must leave the catch message unpublished.");
                    }
                }
                cold = Restore(after);
                for (int tick = 0; tick < 8; tick++)
                {
                    warm.Advance(waitForCommand: true);
                    cold.Advance(waitForCommand: true);
                }
                Equal(warm, cold, $"Formation {id} after-death cold continuation");
            }
        }
        Console.WriteLine("Native cached ally selectors/count/comparison and original formations56..59 initialization/turn/first-last-death/JSON continuations passed.");
    }

    private static void TestCachedSelectors()
    {
        Ff7BattleAiActor[] actors = [new(0, 0, 8, 10, 10, 100, 100), new(4, 0, 0x1018, 10, 10, 130, 130), new(5, 1, 8, 0, 0, 0, 120), new(6, 0, 8, 0, 0, 120, 120)];
        Ff7BattleAiContext context = new(5, actors, 0x71, 0);
        SceneBattleData data = new(56, [], [], [], null);
        Ff7BattleRng random = new(new byte[3988], new(new byte[8]));
        byte[] script = [0x11, 0, 0, 0x02, 0x80, 0x20, 0x83, 0x90, 0x11, 8, 0, 0x02, 0x80, 0x20, 0x83, 0x60, 2, 0x40, 0x90, 0x73];
        Ff7BattleAiResult beforeRefresh = Ff7BattleAi.Run(script, context, [], data, random);
        Check(beforeRefresh.LocalMemory[0] == 3 && beforeRefresh.LocalMemory[1] == 0, "A death callback must count the dying actor in its cached alive-allies mask.");
        Ff7BattleAiResult afterRefresh = Ff7BattleAi.Run(script, context with { KnockedOutMask = 0x20 }, [], data, random);
        Check(afterRefresh.LocalMemory[0] == 2 && afterRefresh.LocalMemory[1] == 1, "A refreshed cache must exclude the previous death before the next callback.");
    }

    private static void Equal(Ff7BattleSession warm, Ff7BattleSession cold, string boundary)
    {
        Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), $"{boundary} changed after JSON restore.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
