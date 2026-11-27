using System.Globalization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private string? globalSceneClockPath;
    private SceneLightingClock? globalSceneClock;
    private double globalSceneClockBaseline;

    internal void ConfigureGlobalSceneClock(GlobalSettingsPaths settings)
    {
        globalSceneClockPath = Path.Combine(settings.DirectoryPath, "scene-lighting-clock.txt");
    }

    internal SceneLightingClock ReadGlobalSceneClock(SceneLightingCycle cycle)
    {
        if (globalSceneClock is null)
        {
            globalSceneClock = new(cycle, LoadGlobalSceneClock(), 0);
            globalSceneClockBaseline = globalSceneClock.Seconds;
        }
        return globalSceneClock;
    }

    internal void PersistGlobalSceneClock()
    {
        if (globalSceneClockPath is not string path || globalSceneClock is not { } clock || clock.Seconds == globalSceneClockBaseline)
        {
            return;
        }
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temporary, clock.Seconds.ToString("R", CultureInfo.InvariantCulture));
            File.Move(temporary, path, overwrite: true);
            globalSceneClockBaseline = clock.Seconds;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Mods.ReportFailure(new("SceneLighting.ClockPreferences", error.Message));
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Mods.ReportFailure(new("SceneLighting.ClockPreferences", error.Message));
            }
        }
    }

    private SceneClockSnapshot? LoadGlobalSceneClock()
    {
        if (globalSceneClockPath is not string path || !File.Exists(path))
        {
            return null;
        }
        try
        {
            string content = File.ReadAllText(path);
            if (double.TryParse(content, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && double.IsFinite(seconds) && seconds >= 0)
            {
                return new(seconds, false);
            }
            Mods.ReportFailure(new("SceneLighting.ClockPreferences", "The stored global scene clock phase is invalid."));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Mods.ReportFailure(new("SceneLighting.ClockPreferences", error.Message));
        }
        return null;
    }
}
