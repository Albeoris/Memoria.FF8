using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePendingTransitionTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PENDING_TRANSITION_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP pending transition: original resources or external standalone fixture are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7SessionSave source = envelope.Ff7!;
        FieldTransitionCommand transition = source.Process.Transition ?? new(ushort.MaxValue, 0, 0, 0, 0);
        Ff7SessionSave authored = source with { Process = source.Process with { Transition = transition with { Map = ushort.MaxValue } } };
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), authored);
        Ff7SessionSave before = session.Capture();
        try
        {
            session.AdvanceTick();
            throw new Exception("An out-of-range native transition was accepted.");
        }
        catch (InvalidDataException)
        {
        }
        Check(session.Field.Name == before.FieldName && session.FieldGeneration == before.FieldGeneration, "A failed field load published its target metadata.");
        Check(JsonSerializer.Serialize(session.Machine.Capture()) == JsonSerializer.Serialize(before.Vm), "A pending transition advanced the old native VM before retrying its target.");
        Check(JsonSerializer.Serialize(session.Progress) == JsonSerializer.Serialize(before.Progress), "A pending transition advanced clock, RNG or progress before loading its target.");
        Check(session.Host.Process.Tick == before.Process.Tick, "A pending transition advanced its old field process.");
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(before), "A static field-asset failure did not retain its coherent pending-transition snapshot.");
        string temporary = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ffsave");
        try
        {
            CombinedSessionCodec.Save(temporary, envelope);
            AutoPlayCheckpoint.Save(temporary, session.Capture, (destination, captured) =>
            {
                CombinedSessionCodec.Save(destination, envelope with { Ff7 = captured });
            });
            using (FileStream saved = File.OpenRead(temporary))
            {
                Ff7SessionSave captured = CombinedSessionCodec.Read(saved).Ff7!;
                Check(JsonSerializer.Serialize(captured) == JsonSerializer.Serialize(before), "The replacement checkpoint lost its coherent post-tick facts.");
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
