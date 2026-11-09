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
        var entry = Ff7FieldResources.Load(installation, "md1stin").Triggers.Gateways.Single(gateway => gateway.DestinationField == 117);
        Equal(58, entry.Direction);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Session? cold = null;
        try
        {
            for (int tick = 0; tick < 4500; tick++)
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
                if (tick is 3740 or 4000 or 4262 or 4264)
                {
                    cold?.Dispose();
                    Ff7SessionSave save = JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(original.Capture()))!;
                    cold = Ff7Session.Restore(installation, save);
                }
            }
            Equal("md1stin", original.Field.Name);
            Equal(false, original.Host.Process.PlayerLocked);
            Equal(false, original.CanSave);
            Equal((byte)2, (byte)(original.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 31) & 2));
            Equal(1, original.Progress.CompletedBattles.Length);
            Equal((ushort)300, original.Progress.CompletedBattles[0].Formation);
            Equal(7, original.Progress.Characters[0].Level);
            Equal(0, original.Host.Process.BattleResult);
            Equal(0, original.Host.Process.Actors.Single(actor => actor.PlayerControlled).PartyCharacter);
            Equal(true, original.Host.Process.Movie is { Completed: true });
            Equal(2, original.Host.Process.Actors.Count(actor => actor.Entity == 5));
            Equal(1, original.Host.Process.Actors.Count(actor => actor.Entity == 5 && actor.EntityBound));
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
        if (tick >= 4110 && !session.Host.Process.PlayerLocked)
        {
            session.MovePlayer(0, -1, true);
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
