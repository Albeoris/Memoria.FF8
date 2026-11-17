using System.Text.Json;
using Memoria.FF7.Events;

internal static class StochasticPresentationTests
{
    internal static void Run()
    {
        byte[] code = [0, 0x99, 5, 11, 0x14, 0x50, 11, 128, 3, 6, 0x40, 3, 52, 0x10, 4, 0x40, 3, 113, 0];
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        FieldSpatialRandomDomain domain = new(Enumerable.Range(0, 256).Select(value => (byte)value).ToArray(), 1, 0);
        FieldMachine Machine(byte[] bytes) => new(new(bytes, [entries]));
        FieldMachine machine = Machine(code);
        machine.Initialize();
        string before = JsonSerializer.Serialize(machine.Capture());
        Check(!machine.AnalyzeScript(0, 1).Complete, "Ordinary random analysis became permissive.");
        FieldScriptAnalysis proved = machine.AnalyzeStochasticPresentation(0, 1, domain);
        Check(proved.Complete && proved.StochasticPresentation is not null, proved.Reason ?? "Presentation proof failed.");
        Check(machine.AnalyzeEntryDependencies(0, 1, proved).Reads.Length == 0, "Dominated random output became an external condition.");
        Check(before == JsonSerializer.Serialize(machine.Capture()), "Presentation analysis changed native state.");
        byte[] material = code.ToArray();
        material[15] = 0x33;
        material[16] = 1;
        material[17] = 0;
        Check(!Machine(material).AnalyzeStochasticPresentation(0, 1, domain).Complete, "Random material effects were admitted.");
        byte[] alias = code.ToArray();
        alias[4] = 0x16;
        Check(!Machine(alias).AnalyzeStochasticPresentation(0, 1, domain).Complete, "Random word aliases were admitted.");
        byte[] persistent = code.ToArray();
        persistent[2] = 1;
        Check(!Machine(persistent).AnalyzeStochasticPresentation(0, 1, domain).Complete, "Persistent random output was admitted.");
        byte[] nonconverging = code.ToArray();
        nonconverging[13] = 0;
        Check(!Machine(nonconverging).AnalyzeStochasticPresentation(0, 1, domain).Complete, "Separate random returns were admitted.");
        byte[] loop = code.ToArray();
        loop[13] = 0x12;
        loop[14] = 0;
        Check(!Machine(loop).AnalyzeStochasticPresentation(0, 1, domain).Complete, "A random-dependent cycle was admitted.");
        byte[] uninitialized = [0, 0x14, 0x50, 11, 128, 3, 1, .. code[1..]];
        Check(!Machine(uninitialized).AnalyzeStochasticPresentation(0, 1, domain).Complete, "An incoming random scratch dependency was hidden.");
        byte[] request = code.ToArray();
        request[15] = 1;
        Check(!Machine(request).AnalyzeStochasticPresentation(0, 1, domain).Complete, "A random-dependent request was admitted.");
        byte[] unknown = code.ToArray();
        unknown[15] = 0xFF;
        Check(!Machine(unknown).AnalyzeStochasticPresentation(0, 1, domain).Complete, "An unknown random branch was admitted.");
        FieldMachine cold = FieldMachine.Restore(new(code, [entries]), machine.Capture());
        Check(cold.AnalyzeStochasticPresentation(0, 1, domain).StochasticPresentation?.Fingerprint == proved.StochasticPresentation?.Fingerprint, "Cold proof changed its native contract identity.");
        FieldScriptAnalysis legacy = JsonSerializer.Deserialize<FieldScriptAnalysis>("{\"Complete\":true,\"Reason\":null,\"Fingerprint\":\"x\",\"ChangesStory\":false,\"Reads\":[],\"ProgramCounters\":[],\"DestinationFields\":[]}")!;
        Check(legacy.StochasticPresentation is null, "An absent optional descriptor invented stochastic support.");
        Console.WriteLine("PASS stochastic presentation convergence, material/alias rejection and native analysis purity");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
