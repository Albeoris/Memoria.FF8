using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed autoplay: original resources or external standalone fixture are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        Ff7SessionSave? captured = null;
        Ff7AutoPlayController controller = new(session, _ => default, checkpoint: snapshot => captured = snapshot);
        long originalTick = session.Host.Process.Tick;
        controller.Start();
        controller.Pause();
        controller.Advance(3);
        Check(session.Host.Process.Tick == originalTick && controller.Steps == 0, "Paused autoplay advanced native simulation.");
        controller.Resume();
        controller.Advance(3);
        Check(session.Host.Process.Tick > originalTick && controller.Steps is > 0 and <= 3, "Resumed autoplay did not obey its scheduler budget.");
        controller.SaveAndStop();
        Check(!controller.Running && captured is not null, "Stopping autoplay omitted its standalone state.");
        using MemoryStream serialized = new();
        CombinedSessionCodec.Write(serialized, envelope with { Ff7 = captured });
        serialized.Position = 0;
        using Ff7Session cold = Ff7Session.Restore(installation, CombinedSessionCodec.Read(serialized).Ff7!);
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Autoplay standalone cold restoration changed native state.");
        string temporary = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ffsave");
        try
        {
            CombinedSessionCodec.Save(temporary, envelope);
            byte[] validBytes = File.ReadAllBytes(temporary);
            try
            {
                Memoria.AutoPlay.AutoPlayCheckpoint.Save(temporary, () => envelope with { ActiveGame = SessionGame.Ff7, Ff7 = null }, CombinedSessionCodec.Save);
                throw new Exception("Corrupt replacement checkpoint was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            Check(File.ReadAllBytes(temporary).SequenceEqual(validBytes), "Codec failure changed the valid standalone checkpoint bytes.");
        }
        finally
        {
            File.Delete(temporary);
        }
        Ff7AutoPlayController failed = new(session, _ => throw new NotSupportedException("Explicit native policy boundary."), checkpoint: snapshot => captured = snapshot);
        failed.Start();
        failed.Advance();
        Check(!failed.Running && failed.Error!.Contains("Explicit native policy boundary.") && captured!.Vm.Tick == session.Machine.Tick, "Autoplay masked the native failure or failed to checkpoint its exact initialized boundary.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
