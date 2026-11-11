using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class InstalledMessageScrollTests
{
    public static void Run()
    {
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? savePath = Environment.GetEnvironmentVariable("MEMORIA_FF7_MESSAGE_SCROLL_SAVE");
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(savePath))
        {
            Console.WriteLine("SKIP installed message scrolling: configure MEMORIA_FF7_GAME_DIR and MEMORIA_FF7_MESSAGE_SCROLL_SAVE.");
            return;
        }
        using Stream source = File.OpenRead(savePath);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(source);
        Ff7Installation installation = Ff7Installation.Discover(gameDirectory);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7 ?? throw new InvalidDataException("The configured message fixture has no FFVII continuation."));
        long? requestId = null;
        bool scrolled = false;
        for (int callback = 0; callback < 512; callback++)
        {
            Ff7Dialogue? active = requestId is long request
                ? session.Host.Process.Dialogues.SingleOrDefault(dialogue => dialogue.RequestId == request)
                : session.Host.Process.Dialogues.FirstOrDefault(dialogue => !dialogue.Completed);
            if (requestId is not null && (active is null || active.Completed))
            {
                if (!scrolled)
                {
                    throw new InvalidDataException("The configured message fixture completed without reaching row scrolling.");
                }
                Console.WriteLine($"Installed message scrolling completed after {callback} identical hot/cold callbacks.");
                return;
            }
            if (active is not null)
            {
                requestId = active.RequestId;
                scrolled |= active.Presentation?.NativeState == 8;
                if (active.AwaitingInput)
                {
                    session.Host.CompleteDialogue(active.RequestId, active.Selected);
                }
            }
            using Ff7Session cold = Ff7Session.Restore(installation, session.Capture());
            session.AdvanceTick();
            cold.AdvanceTick();
            if (JsonSerializer.Serialize(session.Capture()) != JsonSerializer.Serialize(cold.Capture()))
            {
                throw new InvalidDataException($"Message scrolling changed after cold restoration at callback {callback}.");
            }
        }
        throw new InvalidDataException("The configured scrolling message did not complete within 512 callbacks.");
    }
}
