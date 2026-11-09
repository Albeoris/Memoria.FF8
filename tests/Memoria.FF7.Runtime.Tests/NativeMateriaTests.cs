using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMateriaTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed materia: original resources or external standalone fixture are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream source = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(source);
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        int count = session.Progress.Materia.Length;
        FieldHostRequest request = new(long.MaxValue - 100, 3, 5, 0, new FieldMateriaCommand(0x12345635));
        Check(session.Host.Execute(request).Value == -1, "Native materia success must return the empty sentinel.");
        Check(session.Progress.Materia.Length == count + 1 && session.Progress.Materia[^1] == new Ff7MateriaState(53, 0x123456), "Encoded materia ID/AP or first-free inventory insertion changed.");
        session.Host.Execute(request);
        Check(session.Progress.Materia.Length == count + 1, "Repeated host request duplicated materia.");
        Ff7SessionSave captured = JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!;
        using Ff7Session cold = Ff7Session.Restore(installation, captured);
        cold.Host.Execute(request);
        Check(cold.Progress.Materia.Length == count + 1, "Cold request continuation duplicated materia.");
        cold.Host.Execute(request with { Id = request.Id + 1, Command = new FieldMateriaCommand(74) });
        cold.Host.Execute(request with { Id = request.Id + 2, Command = new FieldMateriaCommand(44) });
        Check((cold.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 75) & 3) == 3, "Original summon and Enemy Skill acquisition flags were omitted.");
        Ff7SessionSave full = captured with { Progress = captured.Progress with { Materia = Enumerable.Repeat(new Ff7MateriaState(53, 0), 200).ToArray() } };
        using Ff7Session capacity = Ff7Session.Restore(installation, full);
        Check(capacity.Host.Execute(request with { Id = request.Id + 3 }).Value == 0x12345635 && capacity.Progress.Materia.Length == 200, "Full inventory must preserve the offered encoded materia and native failure return.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
