using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeEncounterTests
{
    public static void Run()
    {
        byte[] encoded = new byte[48];
        encoded[0] = 1;
        encoded[1] = 32;
        for (int index = 0; index < 6; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(2 + index * 2), (ushort)((index + 1) | 10 << 10));
        }
        PcFieldEncounterTable table = PcFieldEncounterTable.Read(encoded)[0];
        Check(table.Enabled && table.Rate == 32 && table.Standard[0] == new PcFieldEncounter(1, 10), "Native encounter table packed fields changed.");
        byte[] random = new byte[256];
        Ff7EncounterState state = new();
        for (int index = 0; index < 7; index++)
        {
            Ff7EncounterResult partial = Ff7NativeEncounters.Step(state, table, random, 4096, false);
            Check(!partial.CountStep && partial.Formation is null && partial.State.Danger == 0, "Encounter danger advanced before eight successful movement ticks.");
            state = partial.State;
        }
        Ff7EncounterResult disabled = Ff7NativeEncounters.Step(state, table, random, 4096, true);
        Check(disabled.CountStep && disabled.State.MovementCounter == 0 && disabled.State.Danger == 0 && disabled.State.RollIndex == 0, "Disabled encounters changed native step/RNG ownership.");
        state = new(MovementCounter: 224, Danger: 256, LastFormation: 1);
        Ff7EncounterResult encounter = Ff7NativeEncounters.Step(state, table, random, 4096, false);
        Check(encounter.Formation == 1 && encounter.Initiative == 4 && encounter.State.Danger == 384 && encounter.State.RollIndex == 2 && encounter.State.FormationIndex == 3, "Native danger, initiative, weighted selection or one repeated-formation reroll changed.");
        random.AsSpan().Fill(255);
        table = table with { Standard = [new(40, 32), .. table.Standard.Skip(1)] };
        encounter = Ff7NativeEncounters.Step(new(MovementCounter: 224, Danger: 65500), table, random, 65535, false);
        Check(encounter.State.Danger == unchecked((ushort)(65500 + 65535 / 32)), "Native unsigned danger word wrap changed.");
        random.AsSpan().Fill(20);
        encounter = Ff7NativeEncounters.Step(new(MovementCounter: 224, Danger: 64000, RollIndex: 255), table, random, 4096, false);
        Check(encounter.State.RollOffset == 13 && encounter.State.RollIndex == 1 && encounter.Formation == 40, "Native roll wrapping/offset or signed frequency accumulation changed.");
        Ff7EncounterState cold = JsonSerializer.Deserialize<Ff7EncounterState>(JsonSerializer.Serialize(encounter.State))!;
        Check(JsonSerializer.Serialize(Ff7NativeEncounters.Step(cold, table, random, 4096, false)) == JsonSerializer.Serialize(Ff7NativeEncounters.Step(encounter.State, table, random, 4096, false)), "Cold encounter continuation changed the next step.");
        RunInstalled();
    }

    private static void RunInstalled()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "smkin_1");
        PcFieldEncounterTable table = PcFieldEncounterTable.Read(field.Container.GetSection(PcFieldSectionKind.Encounters).Span)[0];
        Check(table.Enabled && table.Rate > 0, "The original reactor approach must retain its active encounter table.");
        Ff7EncounterState state = new();
        List<ushort> selected = [];
        byte[] random = native.NativeFieldRandomTable.ToArray();
        for (int tick = 0; tick < 2000; tick++)
        {
            Ff7EncounterResult result = Ff7NativeEncounters.Step(state, table, random, 4096, false);
            state = result.State;
            if (result.Formation is ushort formation)
            {
                selected.Add(formation);
                Check(table.Standard.Concat(table.Special).Any(entry => entry.Formation == formation), "Native encounter selected a formation absent from the field table.");
                state = state with { Danger = 0, MovementCounter = 0 };
            }
        }
        Check(selected.Count > 0, "The original reactor approach never selected an encounter.");
        Console.WriteLine("installed reactor encounters=" + selected.Count + " first=" + selected[0]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
