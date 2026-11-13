using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleHellHouseTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original Hell House resources: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using Ff7Session host = Ff7Session.Restore(installation, original.Capture() with { Progress = original.Progress with { Party = [0, 1, 2], FullCombatEnabled = true } });
        host.Host.StartEncounter(new FieldHostRequest(1L << 40, host.Host.Process.Actors[0].Entity, 7, 0, new FieldBattleCommand(380)), 0);
        Ff7SessionSave entry = host.Capture();
        Ff7BattleSnapshot snapshot = entry.Battle!;
        Ff7BattleQueuedCommand command = new(2, 4, 0x20, 3, 7, AttackId: 0x13E);
        using Ff7Session battleHost = Ff7Session.Restore(installation, entry with { Battle = snapshot with { Queue = Ff7BattleCommandQueue.Enqueue(snapshot.Queue, command) } });
        Ff7BattleSession battle = battleHost.Battle!;
        Ff7Session? cold = null;
        List<short> countdown = [];
        int fadeSound = 0;
        try
        {
            for (int step = 0; step < 2000; step++)
            {
                battle.Advance(waitForCommand: true);
                Ff7BattleSoundReceipt[] emitted = battle.DrainSoundRequests();
                if (cold is not null)
                {
                    cold.Battle!.Advance(waitForCommand: true);
                    Check(emitted.SequenceEqual(cold.Battle.DrainSoundRequests()) && JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Battle.Capture()), "Hell House scripted fade diverged after JSON restore.");
                }
                if (battle.Action?.Command.AttackId != 0x13E)
                {
                    if (countdown.Count != 0)
                    {
                        break;
                    }
                    continue;
                }
                Check(battle.Action.Animation == 5, "Hell House failed to select original transformation animation 5.");
                Ff7BattleSnapshot current = battle.Capture();
                Ff7BattleActorDeathState? fade = current.ActorMotion?.Slots.SingleOrDefault(callback => callback is { Target: 4, ScriptedFade: true });
                if (fade is not null && (countdown.Count == 0 || countdown[^1] != fade.Remaining))
                {
                    countdown.Add(fade.Remaining);
                    Ff7BattleActorModelModifiers modifiers = current.ActorModels.Single(model => model.Slot == 4).Modifiers!;
                    Check(modifiers.Visible && modifiers.Red == 0 && modifiers.ExtraEnabled && modifiers.Transparency == 2048 + (14 - fade.Remaining) * 128, "Hell House did not apply the authored fade transform.");
                    if (fade.Remaining == 8)
                    {
                        cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(battleHost.Capture()))!);
                    }
                }
                fadeSound += emitted.Count(cue => cue is { Sound: 22, Channel: 3 });
            }
        }
        finally
        {
            cold?.Dispose();
        }
        Check(countdown.SequenceEqual(Enumerable.Range(0, 14).Reverse().Select(value => (short)value)) && fadeSound == 1 && battle.Action is null && !battle.ActorModelOutput(4).Visible, $"Hell House did not retire its original transformation fade. Countdown={string.Join(',', countdown)}, sound={fadeSound}, action={battle.Action?.Command}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
