using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeChocoboSimulationTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure original Chocobo resources.");
        string fixture = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHOCOBO_ORACLE") ?? throw new InvalidOperationException("Configure the external native Chocobo oracle.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7ChocoboNativeData native = Ff7PcExecutableData.LoadChocoboData(installation);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(fixture));
        int count = 0;
        foreach (JsonElement source in document.RootElement.GetProperty("Cases").EnumerateArray())
        {
            byte[] parameters = Convert.FromHexString(source.GetProperty("Parameters").GetString()!);
            byte[] before = parameters.ToArray();
            Ff7ChocoboSimulation simulation = new(Ff7ChocoboResources.LoadCourse(installation, source.GetProperty("ShortCourse").GetBoolean()), native, parameters, source.GetProperty("Seed").GetUInt32());
            Check(simulation.RandomSeed == source.GetProperty("FinalSeed").GetUInt32(), "Chocobo initialization changed the native CRT draw contract.");
            JsonElement[] expected = source.GetProperty("Racers").EnumerateArray().ToArray();
            for (int index = 0; index < expected.Length; index++)
            {
                byte[] actual = Encode(simulation.Racers[index]);
                byte[] original = Convert.FromHexString(expected[index].GetString()!);
                int difference = Enumerable.Range(0, original.Length).FirstOrDefault(offset => original[offset] != actual[offset], -1);
                Check(difference == -1, $"Native Chocobo initialization differs at case{count}/racer{index}/offset{difference:X}: expected{(difference < 0 ? 0 : original[difference]):X2}, actual{(difference < 0 ? 0 : actual[difference]):X2}.");
            }
            Check(parameters.SequenceEqual(before), "Chocobo initialization mutated caller-owned parameters.");
            count++;
        }
        Check(count >= 3, "The native initialization oracle lacks varied classes, modes and seeds.");
        Console.WriteLine($"PASS native Chocobo initialization: {count} original cases, six complete racers and exact CRT ownership");
        string motionFixture = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHOCOBO_MOTION_ORACLE") ?? throw new InvalidOperationException("Configure the external native Chocobo motion oracle.");
        using JsonDocument motionDocument = JsonDocument.Parse(File.ReadAllBytes(motionFixture));
        int motionCases = 0;
        foreach (JsonElement source in motionDocument.RootElement.GetProperty("Cases").EnumerateArray())
        {
            Ff7ChocoboSimulation simulation = new(Ff7ChocoboResources.LoadCourse(installation, source.GetProperty("ShortCourse").GetBoolean()), native, Convert.FromHexString(source.GetProperty("Parameters").GetString()!), source.GetProperty("Seed").GetUInt32());
            if (source.GetProperty("ManualOverride").GetBoolean())
            {
                simulation.Racers[0].Automatic = 0;
            }
            JsonElement[] snapshots = source.GetProperty("Snapshots").EnumerateArray().ToArray();
            int frame = 0;
            foreach (JsonElement snapshot in snapshots)
            {
                while (frame < snapshot.GetProperty("Frame").GetInt32())
                {
                    frame++;
                    JsonElement input = source.GetProperty("Schedule").EnumerateArray().Single(item => frame >= item.GetProperty("FirstFrame").GetInt32() && frame <= item.GetProperty("LastFrame").GetInt32());
                    simulation.AdvanceRacers(input.GetProperty("Started").GetBoolean(), input.GetProperty("Held").GetUInt16(), source.GetProperty("CountdownBase").GetInt32() + frame);
                    simulation.Racers[0].SoundCounter--;
                }
                Check(simulation.RandomSeed == snapshot.GetProperty("FinalSeed").GetUInt32(), $"Native Chocobo motion CRT mismatch case{motionCases}/frame{frame}: {simulation.RandomSeed}.");
                JsonElement[] expected = snapshot.GetProperty("Racers").EnumerateArray().ToArray();
                for (int index = 0; index < expected.Length; index++)
                {
                    byte[] actual = Encode(simulation.Racers[index]);
                    byte[] original = Convert.FromHexString(expected[index].GetString()!);
                    int difference = Enumerable.Range(0, original.Length).FirstOrDefault(offset => original[offset] != actual[offset], -1);
                    Check(difference == -1, $"Native Chocobo motion differs at case{motionCases}/frame{frame}/racer{index}/offset{difference:X}: expected{(difference < 0 ? 0 : original[difference]):X2}, actual{(difference < 0 ? 0 : actual[difference]):X2}.");
                }
            }
            motionCases++;
        }
        Check(motionCases >= 5, "The native motion oracle lacks varied classes, modes and input schedules.");
        Console.WriteLine($"PASS native Chocobo motion: {motionCases} original cases through256 callbacks, exact racers and CRT");
    }

    private static byte[] Encode(Ff7ChocoboSimulation.Racer racer)
    {
        byte[] result = new byte[0xA4];
        void Word(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(offset), value);
        void Integer(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), value);
        void Vector(int offset, (short X, short Y, short Z) value)
        {
            Word(offset, value.X);
            Word(offset + 2, value.Y);
            Word(offset + 4, value.Z);
        }
        Word(0, racer.Path);
        Word(2, racer.NextPath);
        Word(4, racer.Speed);
        Word(6, racer.Type);
        Word(0xA, racer.PreviousY);
        Vector(0x10, racer.Position);
        Integer(0x18, racer.RemainderX);
        Integer(0x20, racer.RemainderZ);
        Vector(0x28, racer.Target);
        Word(0x32, racer.Yaw);
        Word(0x3A, racer.TargetYaw);
        Vector(0x40, racer.Origin);
        Word(0x48, racer.Dash);
        Word(0x4A, racer.SmoothedLane);
        Word(0x4C, racer.Behavior);
        Word(0x4E, racer.DesiredSpeed);
        Word(0x50, racer.LaneBias);
        Word(0x52, racer.Guide);
        Word(0x54, racer.EffectiveSpeed);
        Word(0x56, racer.CruiseSpeed);
        Word(0x58, racer.TopSpeed);
        Word(0x5A, racer.Acceleration);
        Word(0x5C, racer.HandlingStep);
        Word(0x5E, racer.SpeedResponse);
        Word(0x60, racer.Automatic);
        Word(0x62, racer.Obedience);
        Word(0x64, racer.Handling);
        Word(0x66, racer.LaneStep);
        Integer(0x68, racer.Stamina);
        Integer(0x6C, racer.MaxStamina);
        Word(0x70, racer.Placement);
        Word(0x72, racer.AnimationCounter);
        Word(0x74, racer.SecondaryAnimationCounter);
        Word(0x76, racer.AnimationStep);
        Word(0x78, racer.PreviousAnimation);
        Word(0x7C, racer.Lane);
        Word(0x7E, racer.Finished);
        Word(0x80, racer.Animation);
        Word(0x82, racer.Rank);
        Word(0x84, racer.Stagnation);
        Word(0x86, racer.TerrainMask);
        racer.Name.CopyTo(result, 0x88);
        Word(0x90, racer.Model);
        Word(0x92, racer.Jockey);
        Word(0x94, racer.SoundCounter);
        Word(0x96, racer.OriginalLane);
        Word(0x98, racer.LastSprint);
        Word(0x9A, racer.CanSprint);
        Word(0x9C, racer.UnusedAbility);
        Word(0x9E, racer.UnusedFlag);
        Word(0xA0, racer.EarlySprint);
        Word(0xA2, racer.FixedSpeed);
        return result;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
