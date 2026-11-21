using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeCurrentTouchCallbackTests
{
    private sealed class Host : IFieldHost
    {
        public FieldHostReply Execute(FieldHostRequest request) => new(false);
    }

    internal static void Run()
    {
        byte[] guard = [0x14, 0x50, 9, 1, 0, 2, 0, 0x80, 0x50, 10, 99, 0];
        Check(Observe(guard, guardValue: 1), "An actual current-variable no-effect branch and real native RET lacked a receipt.");
        Check(!Observe(guard, guardValue: 0), "An actually executed material write received a no-effect callback receipt.");
        Check(!Observe(guard, guardValue: 1, earlierWrite: true), "An earlier entity changed the branch guard, but stale initial variable facts still authorized the target callback.");
        byte[] key = [0x31, 0x20, 0, 5, 0x80, 0x50, 10, 99, 0];
        Check(Observe(key), "The actual unmatched KEY branch returning without effects was rejected.");
        Check(!Observe(key, pressed: 0x20), "The actual matched KEY material branch received a no-effect receipt.");
        foreach (byte[] body in new byte[][]
        {
            [0x40, 0, 1, 0],
            [0x01, 0, 1, 0],
            [0x24, 1, 0, 0],
            [0x5F, 0],
            [0x10, 1, 0x10, 1, 0x10, 1, 0x10, 1, 0x10, 1, 0x10, 1, 0x10, 1, 0x10, 1, 0]
        })
        {
            Check(!Observe(body), "Host work, child dispatch, yield or an unfinished native handler quantum acquired a return receipt.");
        }
        Check(!Observe(guard, guardValue: 1, wrongEntry: true), "An observed execution started at another entry than the declared callback.");
        Check(!Observe(guard, guardValue: 1, wrongAdmission: true), "Another script's external admission authorized the callback.");
        Check(!Observe(guard, guardValue: 1, admit: false), "A missing actual external admission acquired a receipt.");
        Console.WriteLine("Current touch witnesses authenticate actual native callback instructions and RET without stale guard assumptions or source mutation.");
    }

    private static bool Observe(byte[] body, byte guardValue = 0, ushort pressed = 0, bool earlierWrite = false, bool wrongEntry = false, bool wrongAdmission = false, bool admit = true)
    {
        const int entry = 4;
        int producer = entry + body.Length;
        byte[] code = [0, 0x5F, 0, 0, .. body, 0x80, 0x50, 9, 0, 0];
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[1][4] = entry;
        entries[0][1] = 2;
        FieldProgram program = new(code, entries);
        FieldMachine source = new(program, host: new Host());
        source.Initialize();
        source.Variables.WriteByte(FieldBank.MapByte, 9, guardValue);
        FieldVmSnapshot snapshot = source.Capture();
        if (earlierWrite)
        {
            snapshot.Entities[0] = snapshot.Entities[0] with { ProgramCounter = producer };
        }
        string before = JsonSerializer.Serialize(snapshot);
        FieldMachine machine = FieldMachine.Restore(program, snapshot, new Host());
        machine.SetInput(0, pressed, 0, pressed);
        using Ff7CurrentLineEntryWitness witness = new(machine, 1, 4, wrongEntry ? entry + 1 : entry);
        FieldExternalScriptAdmission admission = machine.RequestExternalScript(1, 4);
        if (admit)
        {
            witness.ObserveAdmission(wrongAdmission ? admission with { Script = 3 } : admission);
        }
        machine.AdvanceTick();
        bool complete = witness.Complete;
        Check(before == JsonSerializer.Serialize(snapshot) && code.AsSpan(entry, body.Length).SequenceEqual(body), "Callback observation mutated the retained source snapshot or script bytes.");
        return complete;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
