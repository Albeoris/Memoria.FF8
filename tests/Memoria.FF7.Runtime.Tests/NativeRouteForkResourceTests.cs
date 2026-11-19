using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
        using Ff7Session source = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(stream).Ff7!);
        string before = JsonSerializer.Serialize(source.Capture());
        string nativeBefore = JsonSerializer.Serialize(source.Capture() with { AutoPlay = null, EventCoverage = null });
        long baseline = GC.GetTotalMemory(true);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        using (Ff7Session first = source.ForkAutoPlayNavigation())
        {
            long firstRetained = GC.GetTotalMemory(true) - baseline;
            long firstAllocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            using (Ff7Session second = source.ForkAutoPlayNavigation())
            {
                long secondRetained = GC.GetTotalMemory(true) - baseline;
                Console.WriteLine($"Two-fork profile: first retained delta={firstRetained}, first allocated={firstAllocated}, two retained delta={secondRetained}, total allocated={GC.GetAllocatedBytesForCurrentThread() - allocated} bytes.");
                Console.WriteLine($"Resource identity: field={ReferenceEquals(source.Field, first.Field)}, models={ReferenceEquals(source.Models, first.Models)}, executable={ReferenceEquals(Member(source, "executable"), Member(first, "executable"))}; mutable VM={ReferenceEquals(source.Machine, first.Machine)}, host={ReferenceEquals(source.Host, first.Host)}, actors={ReferenceEquals(source.Host.Process.Actors, first.Host.Process.Actors)}.");
                var graphs = typeof(Ff7Session).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(field => (Name: field.Name, Root: field.GetValue(first))).Where(entry => entry.Root is not null && entry.Root is not Delegate).Select(entry => (entry.Name, Type: entry.Root!.GetType().Name, Payload: ArrayPayload(entry.Root))).OrderByDescending(entry => entry.Payload).Take(12).ToArray();
                foreach (var graph in graphs)
                {
                    Console.WriteLine($"Resource array payload (overlapping roots): {graph.Name}, {graph.Type}, {graph.Payload} bytes.");
                }
                Check(first.AutoPlayState is null && second.AutoPlayState is null && first.EventCoverage is null && second.EventCoverage is null, "A detached native fork retained a duplicate or shared observational policy ledger.");
                Check(before == JsonSerializer.Serialize(source.Capture()) && nativeBefore == JsonSerializer.Serialize(first.Capture()) && nativeBefore == JsonSerializer.Serialize(second.Capture()), "Constructing two detached forks changed native state beyond the two observational ledgers.");
            }
        }
        Check(before == JsonSerializer.Serialize(source.Capture()) && bytes.SequenceEqual(File.ReadAllBytes(path)), "The bounded two-fork profile changed its source or standalone bytes.");
        Console.WriteLine($"Two-fork profile released; full-GC managed heap={GC.GetTotalMemory(true)} bytes; source unchanged.");
    }

    private static object? Member(Ff7Session session, string name)
    {
        return typeof(Ff7Session).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session);
    }

    private static long ArrayPayload(object root)
    {
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        Stack<object> pending = new();
        pending.Push(root);
        long bytes = 0;
        while (pending.TryPop(out object? current))
        {
            Type type = current.GetType();
            if (current is Delegate or Stream or Type || type.IsPrimitive || type.IsEnum || !visited.Add(current))
            {
                continue;
            }
            if (current is string text)
            {
                bytes += text.Length * 2L;
                continue;
            }
            if (current is Array array)
            {
                Type element = type.GetElementType()!;
                bytes += array.LongLength * (int)typeof(NativeRouteForkResourceTests).GetMethod(nameof(ElementSize), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(element).Invoke(null, null)!;
                if (element.IsPrimitive || element.IsEnum || !element.IsValueType && element == typeof(string))
                {
                    if (element == typeof(string))
                    {
                        foreach (object? value in array)
                        {
                            if (value is not null)
                            {
                                pending.Push(value);
                            }
                        }
                    }
                    continue;
                }
                bool containsReferences = (bool)typeof(NativeRouteForkResourceTests).GetMethod(nameof(ContainsReferences), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(element).Invoke(null, null)!;
                if (containsReferences)
                {
                    foreach (object? value in array)
                    {
                        if (value is not null)
                        {
                            pending.Push(value);
                        }
                    }
                }
                continue;
            }
            for (Type? owner = type; owner is not null; owner = owner.BaseType)
            {
                foreach (FieldInfo field in owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!field.FieldType.IsPointer && field.GetValue(current) is object value)
                    {
                        pending.Push(value);
                    }
                }
            }
        }
        return bytes;
    }

    private static int ElementSize<T>()
    {
        return Unsafe.SizeOf<T>();
    }

    private static bool ContainsReferences<T>()
    {
        return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
