using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePartialTickTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PARTIAL_TICK_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP partial native tick: select original resources and a standalone partial-cycle fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(source);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(new MemoryStream(source));
        Ff7SessionSave save = envelope.Ff7 ?? throw new InvalidDataException("The partial-cycle fixture has no native session.");
        Check(save.TickContinuation is not null && save.Vm.CycleCursor is not null, "The fixture requires its explicitly migrated partial-cycle ownership.");
        FieldInputState input = save.TickContinuation!.Input;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session baseline = Ff7Session.Restore(installation, save);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(save))!);
        Check(baseline.HasPendingTick && !baseline.CanSave, "A partial VM cycle was exposed as a stable native save boundary.");
        Check(JsonSerializer.Serialize(cold.Capture()) == JsonSerializer.Serialize(baseline.Capture()), "Cold restore changed the pending cycle.");
        string clock = JsonSerializer.Serialize(baseline.Clock);
        cold.SetInput(0x20, 0x20);
        cold.MovePlayer(1, -1, false);
        Check(cold.Machine.Input == input && !cold.TryTalk(), "New input changed the active native cycle.");
        baseline.AdvanceTick(advanceClock: true);
        cold.AdvanceTick(advanceClock: false);
        Ff7SessionSave expected = baseline.Capture();
        Ff7SessionSave actual = cold.Capture();
        Check(!baseline.HasPendingTick && expected.Vm.CycleCursor is null && expected.Vm.Tick == save.Vm.Tick + 1 && expected.Process.Tick == save.Process.Tick + 1, "The remaining cycle did not complete exactly once.");
        Check(JsonSerializer.Serialize(baseline.Clock) == clock, "Resuming the VM repeated the field clock prefix.");
        Check(actual.Vm.Input is { Held: 0x20, Pressed: 0x20, Horizontal: 1, Vertical: -1, Run: false }, "Deferred input did not become available after the old cycle completed.");
        actual = actual with { Vm = actual.Vm with { Input = expected.Vm.Input } };
        Check(JsonSerializer.Serialize(actual) == JsonSerializer.Serialize(expected), "Deferred input affected the old cycle or native update tail.");
        baseline.AdvanceTick();
        Check(JsonSerializer.Serialize(baseline.Clock) != clock, "The next ordinary cycle did not resume its clock prefix.");

        using Ff7Session failedTail = Ff7Session.Restore(installation, save);
        failedTail.Host.NativeModelConfiguration = _ => throw new NotSupportedException("authored presentation-tail fault");
        Throws<NotSupportedException>(() => failedTail.AdvanceTick());
        Throws<InvalidOperationException>(() => failedTail.Capture());
        Throws<InvalidOperationException>(() => failedTail.AdvanceTick());
        Throws<InvalidDataException>(() => (save with { TickContinuation = null }).Validate());
        Throws<InvalidDataException>(() => (save with { Vm = save.Vm with { CycleCursor = null } }).Validate());
        Throws<InvalidDataException>(() => (save with { TickContinuation = new(input with { Run = !input.Run }) }).Validate());
        Check(hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The supplied standalone fixture changed.");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
