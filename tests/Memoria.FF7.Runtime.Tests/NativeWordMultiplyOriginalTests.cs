using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWordMultiplyOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORD_MULTIPLY_SAVE") ?? throw new InvalidOperationException("Select a word arithmetic fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        int initialPc = session.Machine.Capture().Entities[0].ProgramCounter;
        HashSet<ushort> written = [];
        session.Machine.InstructionVariableAccessed += (variable, write) =>
        {
            if (write && variable.Bank == FieldBank.MapWord && variable.Word)
            {
                written.Add(variable.Operand);
            }
        };
        for (int callback = 0; callback < 16; callback++)
        {
            session.AdvanceTick(advanceClock: false);
        }
        int currentPc = session.Machine.Capture().Entities[0].ProgramCounter;
        Console.WriteLine($"Native word multiplication continuation: tick={session.Machine.Tick}, initialPc={initialPc:X4}, currentPc={currentPc:X4}, mapWordWrites={string.Join(',', written.Order())}, field={session.MapId}.");
        if (currentPc == initialPc || !written.Contains(10) || !written.Contains(12) || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The original word multiplication pair did not execute and continue without changing its source fixture.");
        }
    }
}
