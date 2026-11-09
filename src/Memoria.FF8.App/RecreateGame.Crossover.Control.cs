using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private object DispatchCrossoverControl(ControlRequest request)
    {
        switch (request.Command.ToLowerInvariant())
        {
            case "world-status":
                return new { ActiveGame = InFf7 ? "FFVII" : "FFVIII", UpdatePaused = updateSave.Paused, SwitchUnavailable = InFf7 ? ff7Scene?.CanSave == true ? null : "Saving is unavailable." : Ff8WorldSwitchUnavailable(), Ff7Field = ff7Scene?.Session?.Field.Name, Ff7Tick = ff7Scene?.Session?.Host.Process.Tick, Ff7Error = ff7Scene?.Error, Mappings = crossover?.Mappings ?? [] };
            case "world-open":
                OpenCrossoverSetup();
                return new { Open = true };
            case "world-ui":
                (crossoverSetup ?? throw new InvalidOperationException("Open the world setup first.")).Apply(Enum.Parse<Memoria.FF8.GameUi.MenuInput>(request.Name ?? throw new ArgumentException("A menu input name is required."), true));
                return new { Open = crossoverSetup is not null };
            case "world-step":
                if (!InFf7 || !options.ControlStep)
                {
                    throw new InvalidOperationException("FFVII world stepping requires its active world and --control-step.");
                }
                Keys[] keys = (request.Name ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Enum.Parse<Keys>).ToArray();
                ff7Scene!.StepDiagnostic(Math.Clamp(request.Frames, 1, 900), new KeyboardState(keys));
                return new { Tick = ff7Scene.Session?.Host.Process.Tick, ff7Scene.Error };
            case "world-switch":
                if (InFf7)
                {
                    SwitchToFf8(ff7Scene!.Capture());
                }
                else
                {
                    if (request.Name is string appearance)
                    {
                        pendingCrossoverMappings = appearance switch
                        {
                            "Squall" => [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, Ff7ModelAppearance.StandardFieldSquallProfileVersion)],
                            "Original" => [],
                            _ => throw new NotSupportedException("Only Cloud to Squall appearance is currently supported.")
                        };
                    }
                    SwitchToFf7();
                    CloseCrossoverSetup();
                }
                return new { ActiveGame = InFf7 ? "FFVII" : "FFVIII" };
            case "world-snapshot":
                string path = Path.GetFullPath(request.Path ?? throw new ArgumentException("Snapshot requires Path."));
                CombinedSessionSave saved = InFf7 ? ff7Scene!.Capture() : (crossover ?? new CombinedSessionSave()) with { ActiveGame = SessionGame.Ff8, Ff8Payload = CaptureFf8Payload(), Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec };
                CombinedSessionCodec.Save(path, saved);
                return new { Path = path, saved.ActiveGame };
            default:
                throw new ArgumentException("World controls: world-status, world-open, world-switch, world-snapshot.");
        }
    }
}
