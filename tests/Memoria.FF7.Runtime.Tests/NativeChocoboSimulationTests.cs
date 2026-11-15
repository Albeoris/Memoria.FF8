using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
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
            Ff7ChocoboRacerState[]? previous = source.TryGetProperty("PreviousRacers", out JsonElement prior) ? prior.EnumerateArray().Select(item => Decode(Convert.FromHexString(item.GetString()!))).ToArray() : null;
            Ff7ChocoboSimulation simulation = new(Ff7ChocoboResources.LoadCourse(installation, source.GetProperty("ShortCourse").GetBoolean()), native, parameters, source.GetProperty("Seed").GetUInt32(), previous: previous);
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
            if (previous is not null)
            {
                Ff7ChocoboSimulationState previousState = new(previous, 0, 0, 0, true, 0, []);
                Ff7ChocoboCarryState carry = new(previousState, source.GetProperty("ShortCourse").GetBoolean(), source.GetProperty("PreviousRecoveryAlternation").GetInt32() != 0, source.GetProperty("PreviousRecoveryCount").GetInt32());
                FieldHostRequest request = new(1, 0, 0, 0, new FieldMinigameCommand(new(512, 0, 0, 80, 0), 1));
                Ff7ChocoboRuntime warm = new(request, parameters, Ff7ChocoboResources.LoadCourse(installation, parameters[7] != 0), native, source.GetProperty("Seed").GetUInt32(), carry: carry);
                Check(warm.RecoverySide == (source.GetProperty("RecoveryAlternation").GetInt32() != 0) && warm.RecoveryCount == source.GetProperty("RecoveryCount").GetInt32(), "Warm native initialization reset retained recovery input history.");
            }
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
        string lifecycleFixture = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHOCOBO_LIFECYCLE_ORACLE") ?? throw new InvalidOperationException("Configure the external native Chocobo lifecycle oracle.");
        using JsonDocument lifecycleDocument = JsonDocument.Parse(File.ReadAllBytes(lifecycleFixture));
        int lifecycleCases = 0;
        foreach (JsonElement source in lifecycleDocument.RootElement.GetProperty("Cases").EnumerateArray())
        {
            byte[] parameters = Convert.FromHexString(source.GetProperty("Parameters").GetString()!);
            var course = Ff7ChocoboResources.LoadCourse(installation, parameters[7] != 0);
            FieldHostRequest request = new(1, 0, 0, 0, new FieldMinigameCommand(new(512, 0, 0, 80, 0), 1));
            Ff7ChocoboRuntime runtime = new(request, parameters, course, native, source.GetProperty("Seed").GetUInt32());
            bool manual = source.GetProperty("InputMode").GetString() != "automatic";
            foreach (JsonElement snapshot in source.GetProperty("Snapshots").EnumerateArray())
            {
                while (runtime.Tick < snapshot.GetProperty("Frame").GetInt32())
                {
                    long frame = runtime.Tick + 1;
                    ushort input = manual ? (ushort)(0xA0 | (frame % 2 != 0 ? 2 : 8)) : (ushort)0;
                    if (manual && frame == 2)
                    {
                        input |= 0x100;
                    }
                    if (manual && runtime.SecondFinished)
                    {
                        input |= 0x800;
                    }
                    runtime.SetInput(input);
                    runtime.Advance();
                }
                Ff7ChocoboSnapshot state = runtime.Capture();
                JsonElement scalars = snapshot.GetProperty("Scalars");
                Dictionary<string, int> actualScalars = new()
                {
                    ["Mode"] = runtime.Phase == Ff7ChocoboPhase.Racing ? 3 : runtime.Phase == Ff7ChocoboPhase.Results ? 4 : 6,
                    ["FadeCurrent"] = state.FadeAlpha,
                    ["FadeDelta"] = state.FadeStep,
                    ["Countdown"] = state.Countdown,
                    ["Started"] = state.Started ? -1 : 0,
                    ["FinishTicks"] = runtime.FinishTimer,
                    ["SecondFinished"] = runtime.SecondFinished ? -1 : 0,
                    ["FirstRacer"] = runtime.FirstFinisher,
                    ["RecoveryAlternation"] = state.RecoverySide ? 1 : 0,
                    ["RecoveryCount"] = state.RecoveryCount,
                    ["PreviousInput"] = state.PreviousInput,
                    ["Held"] = state.Held,
                    ["FrameCounter"] = state.FrameCounter,
                    ["Exit"] = runtime.Finished ? 1 : 0
                };
                foreach (var scalar in actualScalars)
                {
                    Check(scalar.Value == scalars.GetProperty(scalar.Key).GetInt32(), $"Native Chocobo lifecycle mismatch case{lifecycleCases}/frame{runtime.Tick}/{scalar.Key}: expected{scalars.GetProperty(scalar.Key)}, actual{scalar.Value}.");
                }
                Check(runtime.RandomSeed == snapshot.GetProperty("FinalSeed").GetUInt32(), $"Native Chocobo lifecycle CRT mismatch case{lifecycleCases}/frame{runtime.Tick}.");
                JsonElement[] expected = snapshot.GetProperty("Racers").EnumerateArray().ToArray();
                for (int index = 0; index < expected.Length; index++)
                {
                    byte[] actual = Encode(state.Simulation.Racers[index]);
                    byte[] original = Convert.FromHexString(expected[index].GetString()!);
                    int difference = Enumerable.Range(0, original.Length).FirstOrDefault(offset => original[offset] != actual[offset], -1);
                    Check(difference == -1, $"Native Chocobo lifecycle differs case{lifecycleCases}/frame{runtime.Tick}/racer{index}/offset{difference:X}: expected{(difference < 0 ? 0 : original[difference]):X2}, actual{(difference < 0 ? 0 : actual[difference]):X2}.");
                }
                Ff7ChocoboSnapshot restored = JsonSerializer.Deserialize<Ff7ChocoboSnapshot>(JsonSerializer.Serialize(state))!;
                runtime = new(request, parameters, course, native, 0, restored);
                Check(JsonSerializer.Serialize(state) == JsonSerializer.Serialize(runtime.Capture()), "Cold Chocobo restoration changed native execution state.");
            }
            Check(runtime.Finished && runtime.Tick == source.GetProperty("CompletedFrame").GetInt32(), "The native Chocobo race did not finish at its original boundary.");
            lifecycleCases++;
        }
        Check(lifecycleCases >= 3, "The native lifecycle oracle lacks genuine wins and losses.");
        Console.WriteLine($"PASS native Chocobo lifecycle: {lifecycleCases} complete original races and repeated cold snapshot continuation");
    }

    private static Ff7ChocoboRacerState Decode(byte[] bytes)
    {
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
        int Integer(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
        Ff7ChocoboPoint Vector(int offset) => new(Word(offset), Word(offset + 2), Word(offset + 4));
        return new()
        {
            Path = Word(0),
            NextPath = Word(2),
            Speed = Word(4),
            Type = Word(6),
            PreviousY = Word(0xA),
            Position = Vector(0x10),
            RemainderX = Integer(0x18),
            RemainderZ = Integer(0x20),
            Target = Vector(0x28),
            Yaw = Word(0x32),
            TargetYaw = Word(0x3A),
            Origin = Vector(0x40),
            Dash = Word(0x48),
            SmoothedLane = Word(0x4A),
            Behavior = Word(0x4C),
            DesiredSpeed = Word(0x4E),
            LaneBias = Word(0x50),
            Guide = Word(0x52),
            EffectiveSpeed = Word(0x54),
            CruiseSpeed = Word(0x56),
            TopSpeed = Word(0x58),
            Acceleration = Word(0x5A),
            HandlingStep = Word(0x5C),
            SpeedResponse = Word(0x5E),
            Automatic = Word(0x60),
            Obedience = Word(0x62),
            Handling = Word(0x64),
            LaneStep = Word(0x66),
            Stamina = Integer(0x68),
            MaxStamina = Integer(0x6C),
            Placement = Word(0x70),
            AnimationCounter = Word(0x72),
            SecondaryAnimationCounter = Word(0x74),
            AnimationStep = Word(0x76),
            PreviousAnimation = Word(0x78),
            Lane = Word(0x7C),
            Finished = Word(0x7E),
            Animation = Word(0x80),
            Rank = Word(0x82),
            Stagnation = Word(0x84),
            TerrainMask = Word(0x86),
            Name = bytes.AsSpan(0x88, 8).ToArray(),
            Model = Word(0x90),
            Jockey = Word(0x92),
            SoundCounter = Word(0x94),
            OriginalLane = Word(0x96),
            LastSprint = Word(0x98),
            CanSprint = Word(0x9A),
            UnusedAbility = Word(0x9C),
            UnusedFlag = Word(0x9E),
            EarlySprint = Word(0xA0),
            FixedSpeed = Word(0xA2)
        };
    }

    internal static byte[] Encode(Ff7ChocoboRacerState racer)
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
