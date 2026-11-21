using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLineEnterDepartureTests
{
    internal static void Run()
    {
        Ff7Line line = new(0, 5, 0, 0, 0, 50, 0, 0, Touch: true);
        Ff7Actor inside = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, Triangle = 0, X4096 = 0, Y4096 = 10 << 12, CollisionRadius = 20 };
        Ff7Actor outside = inside with { Y4096 = 30 << 12 };
        string before = JsonSerializer.Serialize(new { line, inside, outside });
        Check(Ff7AutoplayDriver.AllowsLineEnterMove(line, inside, outside), "Departing an already consumed native Touch was treated as a new Enter.");
        Check(Ff7AutoplayDriver.AllowsLineEnterMove(line, inside, inside with { Y4096 = 5 << 12 }), "Movement within an existing Touch was treated as a new Enter.");
        Check(!Ff7AutoplayDriver.AllowsLineEnterMove(line with { Enter = true }, inside, outside), "An actual pending Enter was discarded while departing.");
        Check(!Ff7AutoplayDriver.AllowsLineEnterMove(line with { Touch = false }, inside, outside), "The current source pose's unreported Enter was discarded.");
        Check(!Ff7AutoplayDriver.AllowsLineEnterMove(line, outside, inside), "A persisted old Touch allowed departure and reentry to bypass the next native Enter.");
        Check(Ff7AutoplayDriver.AllowsLineEnterMove(line with { Touch = false }, outside, outside with { Y4096 = 40 << 12 }), "Movement outside contact was blocked by unrelated facing or proximity.");
        Ff7Actor boundary = inside with { Y4096 = 20 << 12 };
        Check(Ff7AutoplayDriver.AllowsLineEnterMove(line with { Touch = false }, boundary, outside), "The exact strict-radius boundary was treated as contact.");
        Check(!Ff7AutoplayDriver.AllowsLineEnterMove(line, boundary, inside), "The strict-radius boundary concealed an actual new entry.");
        Ff7Line elevated = line with { Z1 = 300, Z2 = 300, Touch = false };
        Check(Ff7AutoplayDriver.AllowsLineEnterMove(elevated, inside, inside), "An invalid native height projection was treated as LINE contact.");
        Check(before == JsonSerializer.Serialize(new { line, inside, outside }), "Enter admission changed native actors or LINE flags.");

        PcWalkmeshTriangle triangle = new(0, new(-100, -100, 0), new(100, -100, 0), new(0, 100, 0), -1, -1, -1);
        Ff7FieldResources field = new("line-enter-fixture", null!, null!, null!, [], [triangle], null!, null!, null!);
        Ff7FieldHost Host(Ff7Line actual) => new(field, (_, _) => 1, new() { Actors = [inside], Lines = [actual] });
        Ff7FieldHost leaving = Host(line);
        leaving.UpdateLineContacts(inside, outside, moving: false);
        Check(leaving.Process.Lines.Single() is { Touch: true, Enter: false }, "The actual departure callback invented a native Enter.");
        leaving.UpdateLineContacts(outside, outside, moving: false);
        Check(leaving.Process.Lines.Single() is { Touch: false, Enter: false, Leave: true }, "The next actual source pose did not clear Touch while retaining the real Leave.");
        Check(!Ff7AutoplayDriver.AllowsLineEnterMove(leaving.Process.Lines.Single(), outside, inside), "The updated native flags failed to protect reentry.");

        Ff7FieldHost entering = Host(line);
        entering.UpdateLineContacts(outside, inside, moving: false);
        Check(entering.Process.Lines.Single() is { Touch: false, Enter: false }, "Native contact incorrectly used the future destination pose in the first callback.");
        entering.UpdateLineContacts(inside, inside, moving: false);
        Check(entering.Process.Lines.Single() is { Touch: true, Enter: true }, "The following native source pose failed to publish the protected Enter.");
        Ff7FieldHost stale = Host(line with { Touch = false });
        stale.UpdateLineContacts(inside, outside, moving: false);
        Check(stale.Process.Lines.Single().Enter, "The native host failed to publish the unreported Enter at the current pose.");
        Console.WriteLine("Native LINE Enter departure, reentry, strict contact and host callback ordering checks passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
