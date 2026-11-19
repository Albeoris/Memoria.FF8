using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRouteForkResourceTests
{
    internal static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_FAIR_SAVE") ?? throw new InvalidOperationException("Select an external native navigation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session source = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!);
        string before = JsonSerializer.Serialize(source.Capture());
        Ff7SessionSave snapshot = source.CaptureAutoPlayNavigation();
        string nativeBefore = JsonSerializer.Serialize(snapshot);
        string definitions = Definitions(source);
        Check(Ff7AutoplayDriver.ReadNativeRouteFingerprint(source.Capture()) == Ff7AutoplayDriver.ReadNativeRouteFingerprint(source), "Omitting observational capture changed the existing native proof fingerprint.");

        long allocated = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch elapsed = Stopwatch.StartNew();
        using Ff7Session cold = Ff7Session.Restore(installation, snapshot);
        double coldSeconds = elapsed.Elapsed.TotalSeconds;
        long coldAllocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        allocated = GC.GetAllocatedBytesForCurrentThread();
        elapsed.Restart();
        using Ff7Session first = source.RestoreAutoPlayNavigation(snapshot);
        double forkSeconds = elapsed.Elapsed.TotalSeconds;
        long forkAllocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        using Ff7Session second = source.RestoreAutoPlayNavigation(snapshot);

        Check(!ReferenceEquals(source.Machine, first.Machine) && !ReferenceEquals(source.Host, first.Host) && !ReferenceEquals(source.Host.Process.Actors, first.Host.Process.Actors), "A fork shared mutable native execution owners.");
        Check(!ReferenceEquals(source.Host.Process.PaletteRows, first.Host.Process.PaletteRows) && !ReferenceEquals(source.Host.Process.PaletteRows[0], first.Host.Process.PaletteRows[0]), "A fork shared mutable palette presentation buffers.");
        Check(!ReferenceEquals(source.Models, first.Models), "A fork shared its model binding array.");
        Check(!ReferenceEquals(source.Field, cold.Field), "Public cold restore skipped its fresh resource validation.");
        if (OperatingSystem.IsWindows())
        {
            Check(ReferenceEquals(source.Field, first.Field) && source.Models.Zip(first.Models).All(pair => ReferenceEquals(pair.First, pair.Second)), "A source-scoped fork redecoded its immutable field/model definitions.");
            Check(ReferenceEquals(Member(source, "executable"), Member(first, "executable")), "A source-scoped fork reloaded its immutable recognized execution data.");
            foreach (string name in new[] { "fields", "characters" })
            {
                LgpArchive original = (LgpArchive)Member(source, name)!;
                LgpArchive reader = (LgpArchive)Member(first, name)!;
                Check(!ReferenceEquals(original, reader) && ReferenceEquals(original.Entries, reader.Entries), "Independent archive readers did not share only the validated immutable catalog.");
            }
        }
        Check(first.AutoPlayState is null && second.AutoPlayState is null && first.EventCoverage is null && second.EventCoverage is null, "A detached fork retained observational state.");
        Check(nativeBefore == JsonSerializer.Serialize(first.Capture()) && nativeBefore == JsonSerializer.Serialize(second.Capture()), "Constructing forks changed native state.");
        first.AdvanceTick(false);
        cold.AdvanceTick(false);
        string advanced = JsonSerializer.Serialize(first.Capture());
        Check(advanced == JsonSerializer.Serialize(cold.Capture()), "A fork's next native callback differed from independent cold restore.");
        Check(before == JsonSerializer.Serialize(source.Capture()) && nativeBefore == JsonSerializer.Serialize(second.Capture()), "Advancing one fork changed its source or sibling VM/pose/lighting state.");
        Check(nativeBefore == JsonSerializer.Serialize(snapshot), "A native callback mutated its input graph-node snapshot.");
        Check(definitions == Definitions(source) && definitions == Definitions(first) && definitions == Definitions(second), "A native callback mutated shared field/model/animation definitions.");
        source.Dispose();
        first.Dispose();
        second.AdvanceTick(false);
        Check(advanced == JsonSerializer.Serialize(second.Capture()), "Disposing a parent or sibling invalidated a retained fork.");
        Check(nativeBefore == JsonSerializer.Serialize(snapshot), "Sibling callbacks mutated their shared input graph-node snapshot.");
        MutatePresentationAndBlockers(second);
        Check(nativeBefore == JsonSerializer.Serialize(snapshot), "Palette or walkmesh commands mutated their input graph-node snapshot.");
        Check(advanced == JsonSerializer.Serialize(cold.Capture()), "Sibling palette or walkmesh commands mutated an independent cold session.");
        Check(definitions == Definitions(second), "Palette or walkmesh commands mutated shared resource definitions.");
        using Ff7Session retained = second.RestoreAutoPlayNavigation(snapshot);
        Check(nativeBefore == JsonSerializer.Serialize(retained.Capture()), "A later graph edge restored a corrupted parent snapshot.");
        second.Dispose();
        retained.AdvanceTick(false);
        Check(advanced == JsonSerializer.Serialize(retained.Capture()) && nativeBefore == JsonSerializer.Serialize(snapshot), "A descendant depended on disposed sibling readers or mutated its graph-node snapshot.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The resource reuse check changed its standalone source.");
        Console.WriteLine($"Source-scoped fork resources passed: cold {coldSeconds:F4}s/{coldAllocated} allocated bytes; fork {forkSeconds:F4}s/{forkAllocated} allocated bytes. One next native callback matched cold restore; source, sibling and definitions unchanged.");
    }

    private static void MutatePresentationAndBlockers(Ff7Session session)
    {
        long id = session.Host.Process.Receipts.Select(receipt => receipt.Request.Id).DefaultIfEmpty().Max() + 100;
        FieldCommand[] commands =
        [
            new FieldPaletteCommand(FieldPaletteOperation.Store, 0, 0, 1),
            new FieldPaletteCommand(FieldPaletteOperation.Add, 0, 0, 1, Red: 1),
            new FieldPaletteCommand(FieldPaletteOperation.Load, 0, 0, 1),
            new FieldTriangleCommand(0, (session.Host.Process.BlockedTriangles[0] & 1) == 0)
        ];
        foreach (FieldCommand command in commands)
        {
            Check(session.Host.Execute(new(id++, 0, 0, 0, command)).Completed, "A bounded host mutation did not complete.");
        }
    }

    private static string Definitions(Ff7Session session)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { session.Field, session.Models }, new JsonSerializerOptions { IncludeFields = true });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static object? Member(Ff7Session session, string name)
    {
        return typeof(Ff7Session).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session);
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
