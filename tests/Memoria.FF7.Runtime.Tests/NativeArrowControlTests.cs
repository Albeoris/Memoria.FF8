using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeArrowControlTests
{
    public static void Run()
    {
        foreach (int value in new[] { 1, 255 })
        {
            Ff7FieldHost host = Build();
            Equal(true, host.Execute(new(1, 0, 7, 16, new FieldControlCommand(FieldControlKind.CursorDisabled, value))).Completed);
            Equal(true, host.Process.CursorDisabled);
            Ff7FieldHost cold = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
            Equal(true, cold.Process.CursorDisabled);
            Equal(false, cold.CreateNextFieldProcess().CursorDisabled);
            string before = JsonSerializer.Serialize(cold.Capture());
            try
            {
                cold.Execute(new(2, 0, 7, 16, new FieldControlCommand(FieldControlKind.CursorDisabled, 0)));
                throw new Exception("Unimplemented pointer enabling must remain explicit.");
            }
            catch (NotSupportedException)
            {
                Equal(before, JsonSerializer.Serialize(cold.Capture()));
            }
        }
        var movie = new Ff7Movie(0, false, 0, false, null, CameraConsumption: new(-1, 0, 0));
        Ff7FieldHost playing = Build(new() { BlinkStateInitialized = true, NativeMusicTrack = 0, Movie = movie });
        Equal(false, playing.Execute(new(3, 0, 7, 16, new FieldMovieCommand(FieldMovieOperation.Play))).Completed);
        Equal(true, playing.Process.CursorDisabled);
        Equal(true, playing.Process.Movie!.Playing);
    }

    private static Ff7FieldHost Build(Ff7FieldProcess? process = null)
    {
        var field = new Ff7FieldResources("arrow-fixture", null!, null!, null!, [], [], null!, null!, null!);
        return new(field, (_, _) => 1, process ?? new() { BlinkStateInitialized = true, NativeMusicTrack = 0 });
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
