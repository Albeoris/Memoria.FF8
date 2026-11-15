using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeChocoboSessionTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure original Chocobo resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHOCOBO_ENTRY_SAVE") ?? throw new InvalidOperationException("Configure an external standalone Chocobo departure.");
        byte[] source = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(source);
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7SessionSave saved = Read(source);
        using Ff7Session initial = Ff7Session.Restore(installation, saved);
        Check(initial.ChocoboRace is null, "The departure fixture already contains a running race.");
        initial.AdvanceTick(advanceClock: false);
        Check(initial.ChocoboRace is { Tick: 0, Phase: Ff7ChocoboPhase.Preparing }, "The native minigame request did not prepare the race.");
        Ff7AutoPlayController firstController = Controller(initial, saved.FieldGeneration);
        firstController.Start();
        for (int callback = 0; callback < 1024 && firstController.Running; callback++)
        {
            firstController.Advance(1);
        }
        Check(firstController.Running && firstController.Error is null && initial.ChocoboRace is { Tick: 1024 }, $"Normal autoplay did not advance the race: {firstController.Error}");
        Ff7SessionSave middle = RoundTrip(initial.Capture());
        Check(middle.ChocoboRace is not null, "The standalone snapshot omitted its running race.");
        Ff7SessionSave wrong = middle with { ChocoboRace = middle.ChocoboRace! with { Request = middle.ChocoboRace.Request with { Id = middle.ChocoboRace.Request.Id + 1 } } };
        Reject(wrong);
        using Ff7Session resumed = Ff7Session.Restore(installation, middle);
        Check(JsonSerializer.Serialize(middle.ChocoboRace) == JsonSerializer.Serialize(resumed.Capture().ChocoboRace), "Cold session restoration changed the race.");
        Ff7AutoPlayController secondController = Controller(resumed, saved.FieldGeneration);
        secondController.Start();
        for (int callback = 0; callback < 4096 && secondController.Running; callback++)
        {
            secondController.Advance(1);
        }
        Check(secondController.Completed && secondController.Error is null, $"Autoplay did not complete the native race: {secondController.Error}");
        Check(resumed.ChocoboRace is null && resumed.MapId == initial.MapId && resumed.FieldGeneration == saved.FieldGeneration + 1, "Race return did not reload the authored field exactly once.");
        Check(resumed.Machine.Variables.ReadByte(FieldBank.Persistent2Byte, 0x19) == 0, "Autoplay did not publish its genuine winning placement.");
        Check(resumed.Progress.BattleRng!.CrtSeed == 1102880014, "Race return did not publish the original completed CRT state.");
        Check(resumed.Progress.Gil == saved.Progress.Gil && JsonSerializer.Serialize(resumed.Progress.CompletedBattles) == JsonSerializer.Serialize(saved.Progress.CompletedBattles) && resumed.Progress.BattleEscapes == saved.Progress.BattleEscapes, "Race return changed unrelated rewards or battle outcomes.");
        Check(resumed.Host.Process.GlobalCommand != Ff7GlobalCommand.Minigame && resumed.Machine.Tick == 0, "Race return resumed the departure VM instead of initializing a new field.");
        Ff7SessionSave returned = RoundTrip(resumed.Capture());
        using Ff7Session coldReturn = Ff7Session.Restore(installation, returned);
        resumed.AdvanceTick(advanceClock: false);
        coldReturn.AdvanceTick(advanceClock: false);
        Check(JsonSerializer.Serialize(resumed.Capture()) == JsonSerializer.Serialize(coldReturn.Capture()), "Cold field return diverged on the next normal callback.");
        Check(SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(hash), "The original standalone departure was modified.");
        Console.WriteLine("PASS original Chocobo departure, autoplay victory, in-flight standalone restore, exact RNG/result return and cold field continuation");
    }

    private static Ff7AutoPlayController Controller(Ff7Session session, long generation)
    {
        return new(session, _ => throw new InvalidOperationException("The field policy was used inside the native race."), hasReachedGoal: () => session.FieldGeneration > generation, advanceTick: () => session.AdvanceTick(advanceClock: false));
    }

    private static Ff7SessionSave RoundTrip(Ff7SessionSave save)
    {
        using MemoryStream stream = new();
        CombinedSessionCodec.Write(stream, new CombinedSessionSave { ActiveGame = SessionGame.Ff7, Ff7 = save });
        return Read(stream.ToArray());
    }

    private static Ff7SessionSave Read(byte[] bytes)
    {
        using MemoryStream stream = new(bytes, writable: false);
        return CombinedSessionCodec.Read(stream).Ff7!;
    }

    private static void Reject(Ff7SessionSave save)
    {
        try
        {
            save.Validate();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("An unrelated request authenticated the Chocobo snapshot.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
