using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleSpeedTests
{
    public static void Run()
    {
        (byte Speed, int Rate, ushort PartyIncrement)[] vectors = [(0, 273, 546), (128, 91, 182), (255, 54, 108)];
        foreach ((byte speed, int rate, ushort increment) in vectors)
        {
            Check(Ff7NativeAtb.BaseRate(speed) == rate && Ff7NativeAtb.Increment(0, 6, 0, 0, 56, speed) == increment, "FFVII native speed rate or ATB increment changed.");
            Check(Ff7NativeBattleStatusClock.Advance(default, 0, speed).Timers.Remainder == rate * 2, "FFVII status time must use twice the native rate for an ordinary actor.");
            Check(Ff7NativeBattleEscape.AdvanceClock(new(1, 1), true, speed, 0).Remainder == rate, "FFVII escape time must use the native rate.");
        }

        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed FFVII battle speed continuation: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Check(session.Progress.NativeBattleSpeed == 128, "New Game must use the original native speed setting.");
        foreach ((byte speed, int rate, ushort increment) in vectors)
        {
            session.SetNativeBattleSpeed(speed);
            Ff7BattleSnapshot entry = Create(session.Progress).Capture();
            Check(entry.NativeBattleSpeed == speed, "Battle initialization must latch the configured speed.");
            Ff7BattleSnapshot before = entry with
            {
                Tick = 1,
                Actors = entry.Actors.Select(actor => actor with { Gauge = 0 }).ToArray(),
                Escape = new(1, 1)
            };
            Ff7BattleSession battle = Create(session.Progress, before);
            Ff7BattleSession cold = Create(session.Progress with { NativeBattleSpeed = (byte)(255 - speed) }, JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(before)));
            battle.Advance(escapeHeld: true);
            cold.Advance(escapeHeld: true);
            Ff7BattleSnapshot after = battle.Capture();
            Ff7BattleActorState player = after.Actors.Single(actor => actor.Actor.Slot == 0);
            Check(after.NativeBattleSpeed == speed && player.Gauge == increment && player.Timers.Remainder == rate * 2 && after.Escape.Remainder == rate, "FFVII encounter must apply one native speed to ATB, status and escape.");
            Check(JsonSerializer.Serialize(after) == JsonSerializer.Serialize(cold.Capture()), "Cold continuation must retain the captured speed independently of current configuration.");
            Ff7BattleSession legacy = Create(session.Progress, before with { NativeBattleSpeed = null });
            Check(legacy.Capture().NativeBattleSpeed == speed && JsonSerializer.Serialize(legacy.Capture().Random) == JsonSerializer.Serialize(before.Random), "Legacy battle must recover the saved configuration without replaying RNG.");
            Check(Create(session.Progress with { NativeBattleSpeed = null }, before with { NativeBattleSpeed = null }).Capture().NativeBattleSpeed == 128, "An older battle without a native configuration must use 128.");
        }

        Ff7BattleSession Create(Ff7Progress progress, Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, modifiers, 1, snapshot: snapshot);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
