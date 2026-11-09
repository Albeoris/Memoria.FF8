using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAudioTransitionTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUDIO_TRANSITION_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed audio transition: original resources or external standalone fixture are unset.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        Ff7FieldHost previous = session.Host;
        NotSupportedException marker = new("Explicit initializer audio output boundary.");
        Action<FieldAudioCommand> callback = _ => throw marker;
        session.Host.AudioRequested = callback;
        Ff7AutoplayDriver driver = new(session);
        Ff7OpeningAutoPlayPolicy policy = new(session);
        bool observed = false;
        try
        {
            for (int tick = 0; tick < 8; tick++)
            {
                driver.Apply(policy.ChooseInput(driver));
                session.AdvanceTick();
            }
        }
        catch (FieldExecutionException error) when (ReferenceEquals(error.InnerException, marker))
        {
            observed = true;
        }
        Check(observed, "The destination initializer did not propagate its original audio callback failure.");
        Check(!ReferenceEquals(previous, session.Host), "The fixture did not reach a new native field host.");
        Check(ReferenceEquals(callback, session.Host.AudioRequested), "The new field host lost its presentation audio binding.");
        Check(File.ReadAllBytes(path).SequenceEqual(original), "The initializer regression rewrote its supplied standalone checkpoint.");
        using Ff7Session cold = Ff7Session.Restore(installation, envelope.Ff7!);
        Check(cold.Field.Name == envelope.Ff7!.FieldName && cold.Host.Process.Tick == envelope.Ff7.Process.Tick, "The supplied pre-transition standalone did not cold-restore after the initializer failure.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
