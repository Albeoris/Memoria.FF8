using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldBattleCheckpointTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_TRANSPORT_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP world battle checkpoint: configure original resources and an active world exploration fixture.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The fixture contains no FFVII state.");
        if (source.World is not { MovementEnabled: true } || source.AutoPlay?.Active?.Kind != Ff7AutoPlayTargetKind.WorldMesh || !source.Progress.FullCombatEnabled)
        {
            Console.WriteLine("SKIP world battle checkpoint: the fixture requires ordinary exploration and native combat.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, source);
        Ff7SessionSave? captured = null;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded exploration unexpectedly requested a choice checkpoint."), state => captured = state);
        Ff7AutoplayDriver driver = new(session);
        for (int callback = 0; callback < 1024 && captured is null; callback++)
        {
            driver.Apply(policy.ChooseInput(driver));
            session.AdvanceTick(advanceClock: false);
        }
        Ff7SessionSave checkpoint = captured ?? throw new InvalidOperationException("The bounded native exploration did not reach its battle checkpoint.");
        Ff7WorldProcess world = checkpoint.World ?? throw new InvalidOperationException("The battle checkpoint lost its world owner.");
        Ff7WorldBattleRequest pending = world.BattleState?.Pending ?? throw new InvalidOperationException("The battle checkpoint lost its native pending occurrence.");
        Check(checkpoint.Battle is null && session.Battle?.RequestId == pending.Id, "The checkpoint must precede battle construction and retain the actual native occurrence.");
        Ff7SessionSave decoded = JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(checkpoint)) ?? throw new InvalidDataException("The pending world checkpoint did not deserialize.");
        using Ff7Session first = Ff7Session.Restore(installation, decoded);
        using Ff7Session second = Ff7Session.Restore(installation, checkpoint);
        using Ff7AutoPlayPolicy restoredPolicy = new(first, (_, _) => throw new InvalidOperationException("Pending native battle entry requested a choice checkpoint."));
        Check(restoredPolicy.ChooseInput(new(first)) == default, "A pending native battle must initialize before another exploration input.");
        first.AdvanceTick(advanceClock: false);
        second.AdvanceTick(advanceClock: false);
        Check(first.World?.Process.Tick == world.Tick && second.World?.Process.Tick == world.Tick, "Restoring a pending battle replayed world travel or VM callbacks.");
        Check(first.Battle?.RequestId == pending.Id && first.Battle?.Formation.FormationId == pending.Formation, "Restoring a pending battle changed its occurrence or formation.");
        Check(JsonSerializer.Serialize(first.Battle!.Capture()) == JsonSerializer.Serialize(second.Battle!.Capture()), "Independent pending-battle restores changed native initialization or RNG.");
        Check(JsonSerializer.Serialize(first.Progress) == JsonSerializer.Serialize(checkpoint.Progress), "Pending battle initialization changed progress, inventory or rewards.");
        Check(original.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The supplied world checkpoint changed during the bounded regression.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
