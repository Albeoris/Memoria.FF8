using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTrainGraveyardTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed Train Graveyard: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.NewGame(installation);
        session.Machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 209);
        Check(session.Host.RequestGateway(new(144, 1685, -3480, 68, 112)), "The fixture must enter at the original sewer ladder.");
        session.AdvanceTick();
        Ff7AutoplayDriver driver = new(session);
        Ff7MidgarAutoPlayPolicy policy = new(session);
        List<ushort> visited = [session.MapId];
        for (int tick = 0; tick < 8000 && session.MapId != 160; tick++)
        {
            Ff7Dialogue? dialogue = session.Host.Process.Dialogues.FirstOrDefault(dialogue => !dialogue.Completed && dialogue.AwaitingInput && session.Host.CanConfirmDialogue(dialogue.RequestId));
            if (dialogue is not null)
            {
                session.Host.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
            }
            Check(policy.TryChooseInput(driver, out Ff7AutoplayInput input), "Every field to the Sector 7 stairs must have a physical route.");
            driver.Apply(input);
            session.AdvanceTick();
            if (visited[^1] == session.MapId)
            {
                continue;
            }
            visited.Add(session.MapId);
            using Ff7Session restored = Ff7Session.Restore(installation, session.Capture());
            Check(JsonSerializer.Serialize(restored.Capture()) == JsonSerializer.Serialize(session.Capture()), "A native field transition must cold-restore exactly.");
        }

        Check(visited.SequenceEqual(new ushort[] { 144, 145, 146, 156, 157, 156, 158, 159, 160 }), "The physical route must reach the top of the Sector 7 pillar.");
        Check(session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 221, "The original directors must publish the story updates.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
