using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class InstalledOpeningTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed opening: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Session? cold = null;
        try
        {
            for (int tick = 0; tick < 4200; tick++)
            {
                Step(original, tick);
                if (cold is not null)
                {
                    Step(cold, tick);
                    string expected = JsonSerializer.Serialize(original.Capture());
                    string actual = JsonSerializer.Serialize(cold.Capture());
                    if (expected != actual)
                    {
                        int difference = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).First(index => expected[index] != actual[index]);
                        throw new Exception($"Cold opening mismatch tick {tick}, JSON offset {difference}: {expected.Substring(Math.Max(0, difference - 40), Math.Min(120, expected.Length - Math.Max(0, difference - 40)))} versus {actual.Substring(Math.Max(0, difference - 40), Math.Min(120, actual.Length - Math.Max(0, difference - 40)))}");
                    }
                }
                if (tick is 3740 or 4000)
                {
                    cold?.Dispose();
                    Ff7SessionSave save = JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(original.Capture()))!;
                    cold = Ff7Session.Restore(installation, save);
                }
            }
            Equal("md1stin", original.Field.Name);
            Equal(false, original.Host.Process.PlayerLocked);
            Equal(0, original.Progress.CompletedBattles.Length);
            Equal(0, original.Host.Process.Actors.Single(actor => actor.PlayerControlled).PartyCharacter);
            Equal(true, original.Host.Process.Movie is { Completed: true });
            int previousSlot = original.Host.GetActor(5).Slot;
            int count = original.Host.Process.Actors.Length;
            original.Host.Execute(new FieldHostRequest(long.MaxValue, 5, 2, 0, new FieldActorBind(4)));
            Equal(count + 1, original.Host.Process.Actors.Length);
            Equal(false, original.Host.Process.Actors.Single(actor => actor.Slot == previousSlot).EntityBound);
            Equal(count, original.Host.GetActor(5).Slot);
            original.AdvanceTick();
            original.Capture().Validate();
        }
        finally
        {
            cold?.Dispose();
        }
    }

    private static void Step(Ff7Session session, int tick)
    {
        if (session.Host.Process.Movie is { Playing: true } movie)
        {
            session.Host.PublishMovieFrame(Math.Min(movie.Frame + (tick % 2), 1799), movie.Frame >= 1799);
        }
        if (tick % 30 == 0)
        {
            foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed))
            {
                session.Host.CompleteDialogue(dialogue.RequestId, dialogue.FirstChoice ?? 0);
            }
        }
        session.AdvanceTick();
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
