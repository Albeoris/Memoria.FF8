using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeChoiceServiceAudioTests
{
    public static void Run()
    {
        foreach (byte operation in new byte[] { 0x28, 0xC0, 0xC1 })
        {
            byte[] body = LiteralAudio(operation);
            FieldChoiceServiceProof proof = Inspect(body);
            Check(proof.Complete, $"A supported literal native audio footprint was rejected: {operation:X2}: {proof.Reason}");
            Check(proof.SoundPanPublication == (operation == 0x28), "Music-volume commands acquired the sound-pan publication allowance.");
            Check(!proof.PalettePresentation && proof.ScratchMapBytes.Length == 0, "A literal audio command invented palette or variable-write allowances.");
            for (int bankByte = 1; bankByte <= 3; bankByte++)
            {
                foreach (byte selector in new byte[] { 0x01, 0x10 })
                {
                    byte[] dynamic = (byte[])body.Clone();
                    dynamic[bankByte] = selector;
                    Check(!Inspect(dynamic).Complete, "A dynamic bank selector broadened the literal audio footprint.");
                }
            }
        }
        foreach (byte operation in new byte[] { 0x20, 0x29, 0x2A, 0xA0, 0xC2, 0xF0, 0xFF })
        {
            Check(!Inspect(LiteralAudio(operation)).Complete, $"An unclassified audio operation entered the service certificate: {operation:X2}");
        }
        Check(!Inspect([0xF1, 0, 3, 0, 64]).Complete, "A different sound opcode borrowed the literal AKAO certificate.");
        Console.WriteLine("PASS literal native service audio footprints and dynamic/unknown rejection");
    }

    private static byte[] LiteralAudio(byte operation)
    {
        byte parameter0 = operation == 0x28 ? (byte)64 : operation == 0xC0 ? (byte)127 : (byte)4;
        byte parameter1 = operation == 0x28 ? (byte)3 : (byte)127;
        return [0xF2, 0, 0, 0, operation, parameter0, parameter1, 0, 0, 0, 0, 0, 0, 0];
    }

    private static FieldChoiceServiceProof Inspect(byte[] body)
    {
        byte[] source = [0, .. body, 0];
        byte[] beforeCode = (byte[])source.Clone();
        ushort[][] entries = [new ushort[32]];
        entries[0][2] = 1;
        FieldMachine machine = new(new FieldProgram(source, entries));
        machine.Initialize();
        string beforeState = JsonSerializer.Serialize(machine.Capture());
        FieldChoiceServiceProof proof = machine.ProveChoiceService(0, 1, [new(0, 2)]);
        Check(beforeCode.AsSpan().SequenceEqual(source) && beforeState == JsonSerializer.Serialize(machine.Capture()), "Audio footprint analysis changed bytecode or native state.");
        return proof;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
