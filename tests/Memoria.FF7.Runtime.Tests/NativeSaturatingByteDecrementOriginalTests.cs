using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSaturatingByteDecrementOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_BYTE_DECREMENT_SAVE") ?? throw new InvalidOperationException("Select a native arithmetic entry fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        if (!session.Field.Scripts.Data.Span.Slice(0x619, 3).SequenceEqual(new byte[] { 0x7C, 5, 0x11 }))
        {
            throw new InvalidOperationException("The reached native arithmetic packet differs from the fixture.");
        }
        int? before = null;
        bool completed = false;
        bool decremented = false;
        session.Machine.InstructionVariableAccessed += (variable, write) =>
        {
            if (!write && variable is { Bank: FieldBank.MapByte, Operand: 0x11, Word: false })
            {
                before = session.Machine.Variables.CopyMap()[0x11];
            }
        };
        session.Machine.InstructionExecuted = (entity, priority, pc, opcode) =>
        {
            if (entity == 13 && priority == 7 && pc == 0x619 && opcode == 0x7C && before is int value)
            {
                decremented = session.Machine.Variables.CopyMap()[0x11] == Math.Max(0, value - 1);
            }
            else if (decremented && entity == 13 && priority == 7 && pc == 0x61C)
            {
                completed = true;
            }
        };
        for (int tick = 0; tick < 4 && !completed; tick++)
        {
            session.AdvanceTick(advanceClock: false);
        }
        Console.WriteLine($"Native byte decrement: field={session.MapId}, tick={session.Machine.Tick}, before={before}, after={session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x11)}, followingInstruction={completed}");
        if (!completed || session.MapId != 710 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The original entry did not execute its saturating decrement and immediate next instruction.");
        }
    }
}
