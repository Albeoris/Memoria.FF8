using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeChocoboCameraTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure original Chocobo resources.");
        string fixture = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHOCOBO_CAMERA_ORACLE") ?? throw new InvalidOperationException("Configure the external native Chocobo camera oracle.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7ChocoboNativeData native = Ff7PcExecutableData.LoadChocoboData(installation);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(fixture));
        foreach (JsonElement source in document.RootElement.GetProperty("Cases").EnumerateArray())
        {
            byte[] parameters = Convert.FromHexString(source.GetProperty("Parameters").GetString()!);
            var course = Ff7ChocoboResources.LoadCourse(installation, parameters[7] != 0);
            FieldHostRequest request = new(1, 0, 0, 0, new FieldMinigameCommand(new(512, 0, 0, 80, 0), 1));
            Ff7ChocoboRuntime race = new(request, parameters, course, native, source.GetProperty("Seed").GetUInt32());
            Compare(race.Capture(), source.GetProperty("InitialCamera"), 0);
            foreach (JsonElement snapshot in source.GetProperty("Snapshots").EnumerateArray())
            {
                while (race.Tick < snapshot.GetProperty("Frame").GetInt32())
                {
                    long frame = race.Tick + 1;
                    ushort input = (ushort)(0xA0 | (frame % 2 != 0 ? 2 : 8));
                    if (frame == 2)
                    {
                        input |= 0x100;
                    }
                    if (race.SecondFinished)
                    {
                        input |= 0x800;
                    }
                    race.SetInput(input);
                    race.Advance();
                }
                Ff7ChocoboSnapshot state = race.Capture();
                Compare(state, snapshot, race.Tick);
                Check(race.RandomSeed == snapshot.GetProperty("FinalSeed").GetUInt32(), $"Camera presentation changed CRT ownership at frame{race.Tick}.");
                Ff7ChocoboSnapshot restored = JsonSerializer.Deserialize<Ff7ChocoboSnapshot>(JsonSerializer.Serialize(state))!;
                race = new(request, parameters, course, native, 0, restored);
                Check(JsonSerializer.Serialize(state) == JsonSerializer.Serialize(race.Capture()), "Cold camera restoration lost native filters or prior matrix.");
            }
            Check(race.Finished, "The native camera fixture did not reach its completed race.");
        }
        Console.WriteLine("PASS original Chocobo tracker, camera filters, previous/current matrices, visibility and cold continuation through race finish");
    }

    private static void Compare(Ff7ChocoboSnapshot state, JsonElement source, long frame)
    {
        Ff7ChocoboCameraSnapshot actual = state.Camera!;
        JsonElement camera = source.GetProperty("Camera");
        byte[] tracker = NativeChocoboSimulationTests.Encode(actual.Tracker);
        byte[] expectedTracker = Convert.FromHexString(source.GetProperty("Tracker").GetString()!);
        int difference = Enumerable.Range(0, tracker.Length).FirstOrDefault(index => tracker[index] != expectedTracker[index], -1);
        Check(difference == -1, $"Native Chocobo tracker mismatch frame{frame}/offset{difference:X}.");
        Point("TargetEye", actual.TargetEye);
        Point("TargetLook", actual.TargetLook);
        Point("CurrentEye", actual.CurrentEye);
        Point("CurrentLook", actual.CurrentLook);
        Scalar("Height", actual.TerrainHeight);
        Scalar("CullStart", actual.CullStart);
        Scalar("CullEnd", actual.CullEnd);
        Scalar("TargetRacer", actual.TargetRacer);
        Scalar("TrackerRacer", actual.TrackedRacer);
        Scalar("Type", actual.FinishOrbit ? 10 : -1);
        Scalar("Reset", actual.Reset ? 1 : 0);
        Matrix("CurrentView", actual.StaticView);
        Matrix("PreviousView", actual.AnimatedView);
        Check(actual.LastQuarterDepth.SequenceEqual(camera.GetProperty("LastQuarterDepth").EnumerateArray().Select(value => value.GetInt32())), $"Native Chocobo retained projection depths differ at frame{frame}: {string.Join(',', actual.LastQuarterDepth)}.");
        Check(actual.Visible.SequenceEqual(camera.GetProperty("Visible").EnumerateArray().Select(value => value.GetInt32() != 0)), $"Native Chocobo visibility differs at frame{frame}.");

        void Scalar(string key, int value) => Check(value == camera.GetProperty(key).GetInt32(), $"Native Chocobo camera {key} mismatch frame{frame}: expected{camera.GetProperty(key)}, actual{value}.");
        void Point(string key, Ff7ChocoboPoint value)
        {
            int[] expected = camera.GetProperty(key).EnumerateArray().Select(item => item.GetInt32()).ToArray();
            Check(value.X == expected[0] && value.Y == expected[1] && value.Z == expected[2], $"Native Chocobo camera {key} mismatch frame{frame}: expected{string.Join(',', expected)}, actual{value}.");
        }
        void Matrix(string key, float[] value)
        {
            byte[] expected = Convert.FromHexString(camera.GetProperty(key).GetString()!);
            for (int index = 0; index < value.Length; index++)
            {
                float original = BitConverter.ToSingle(expected, index * 4);
                Check(value[index] == original, $"Native Chocobo camera {key}[{index}] mismatch frame{frame}: expected{original}, actual{value[index]}.");
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
