using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldCameraOcclusionTests
{
    public static void Run(Ff7Installation installation, Ff7SessionSave saved)
    {
        Ff7WorldProcess world = saved.World!;
        Ff7WorldProcess lowCamera = world with { ViewMode = 2, Camera = new(3936, 1795, 7029, 307), CameraOcclusion = null, Input = world.Input! with { Horizontal = 0, Vertical = 0, Held = 0, Pressed = 0, PreviousControlHeld = 0, CameraHeadingTarget = 3936 } };
        using Ff7Session original = Ff7Session.Restore(installation, saved with { World = lowCamera });
        using Ff7Session cold = Ff7Session.Restore(installation, Cold(original.Capture()));
        int smallestOffset = 0;
        int originalPitch = original.World!.Process.Camera!.Pitch;
        for (int callback = 0; callback < 120; callback++)
        {
            original.AdvanceTick(advanceClock: false);
            cold.AdvanceTick(advanceClock: false);
            Ff7WorldCameraOcclusion feedback = original.World!.Process.CameraOcclusion!;
            smallestOffset = Math.Min(smallestOffset, feedback.PitchOffset);
            Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Native terrain occlusion feedback must cold-continue identically on every callback without render-rate input.");
            if (callback == 50)
            {
                cold.World!.Restore(Cold(original.Capture()).World!);
            }
        }
        Check(smallestOffset < 0 && original.World!.Process.Camera!.Pitch < originalPitch, "The actual low Midgar camera must detect the original terrain, debounce its native latch and raise its pitch profile.");
        Console.WriteLine($"PASS original Midgar terrain camera feedback and exact cold continuation (120 callbacks, minimum pitch offset {smallestOffset})");
    }

    private static Ff7SessionSave Cold(Ff7SessionSave state)
    {
        return JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(state))!;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
