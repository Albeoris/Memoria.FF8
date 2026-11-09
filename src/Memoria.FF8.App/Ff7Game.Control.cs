using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Game
{
    private ControlQueue? controls;
    private string? pendingCapture;
    private Keys[] controlledKeys = [];
    private int remainingInputFrames;
    private Memoria.FF8.Assets.GameAssets? controlAppearanceAssets;

    private void RestoreStandalone(CombinedSessionSave saved)
    {
        if (saved.Mappings.Length > 0)
        {
            controlAppearanceAssets ??= new Memoria.FF8.Assets.GameAssets(launchOptions!.GameDirectory);
        }
        Memoria.FF8.Rendering.IFf7ActorAppearance? selected = Ff7AppearanceSelection.Create(saved.Mappings, installation, controlAppearanceAssets);
        scene.Continue(saved);
        scene.SetAppearance(saved.Mappings, selected);
    }

    private KeyboardState ReadControlKeys()
    {
        if (remainingInputFrames > 0)
        {
            remainingInputFrames--;
            return windowModeHotkey.Filter(new KeyboardState(controlledKeys));
        }
        return windowModeHotkey.Filter(launchOptions?.AcceptsPhysicalInput == false ? default : Keyboard.GetState());
    }

    private void UpdateWindowModeHotkey(KeyboardState keys, bool active)
    {
        if (windowModeHotkey.Read(keys, active))
        {
            scene.ToggleWindowMode();
        }
    }

    private object DispatchControl(ControlRequest request)
    {
        if (updateSave.Paused && request.Command.ToLowerInvariant() is not ("status" or "capture" or "quit"))
        {
            throw new InvalidOperationException("The game is paused for an update. Cancel the update before changing the game.");
        }
        switch (request.Command.ToLowerInvariant())
        {
            case "status":
                return new { Game = "FFVII", Field = scene.Session?.Field.Name, Tick = scene.Session?.Host.Process.Tick, scene.CanSave, scene.Error, Process = scene.Session?.Host.Process, Progress = scene.Session?.Progress };
            case "autoplay":
                scene.ApplyAutoPlayCommand(request.Name ?? "start");
                return new { scene.AutoPlayStatus };
            case "new-game":
                scene.StartNewGame();
                return new { Started = true };
            case "actions":
                controlledKeys = scene.KeysForActions((request.Name ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(name => Enum.Parse<ControlAction>(name, true))).GetPressedKeys();
                remainingInputFrames = Math.Clamp(request.Frames, 1, 3600);
                return new { Frames = remainingInputFrames };
            case "input":
                controlledKeys = (request.Name ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Enum.Parse<Keys>).ToArray();
                remainingInputFrames = Math.Clamp(request.Frames, 1, 3600);
                return new { Frames = remainingInputFrames };
            case "map-cloud":
                CharacterAppearanceMapping[] mappings = request.Name switch
                {
                    "Squall" => [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, Ff7ModelAppearance.StandardFieldSquallProfileVersion)],
                    "Original" => [],
                    _ => throw new ArgumentException("Name must be Squall or Original.")
                };
                controlAppearanceAssets ??= new Memoria.FF8.Assets.GameAssets(launchOptions!.GameDirectory);
                scene.SetAppearance(mappings, Ff7AppearanceSelection.Create(mappings, installation, controlAppearanceAssets));
                return new { Mappings = mappings };
            case "step":
                if (launchOptions?.ControlStep != true)
                {
                    throw new InvalidOperationException("Explicit stepping requires --control-step.");
                }
                int frames = Math.Clamp(request.Frames, 1, 900);
                scene.StepDiagnostic(frames, ReadControlKeys);
                return new { Frames = frames, Tick = scene.Session?.Host.Process.Tick, scene.Error };
            case "capture":
                pendingCapture = Path.GetFullPath(request.Path ?? throw new ArgumentException("Capture requires Path."));
                return new { Path = pendingCapture };
            case "save":
                if (!scene.CanSave)
                {
                    throw new InvalidOperationException("Saving is unavailable at the current FFVII boundary.");
                }
                goto case "snapshot";
            case "snapshot":
                string path = Path.GetFullPath(request.Path ?? throw new ArgumentException("Snapshot requires Path."));
                CombinedSessionCodec.Save(path, scene.Capture());
                return new { Path = path, Tick = scene.Session?.Host.Process.Tick, PlayerSaveAllowed = scene.CanSave };
            case "load":
                using (FileStream source = File.OpenRead(request.Path ?? throw new ArgumentException("Load requires Path.")))
                {
                    CombinedSessionSave saved = CombinedSessionCodec.Read(source);
                    if (saved.Ff8Payload is { Length: > 0 })
                    {
                        throw new NotSupportedException("Use Continue from the title menu to load a save containing both worlds.");
                    }
                    RestoreStandalone(saved);
                }
                return new { Field = scene.Session?.Field.Name, Tick = scene.Session?.Host.Process.Tick };
            case "quit":
                Exit();
                return new { Exiting = true };
            default:
                throw new ArgumentException("FFVII controls: status, new-game, input, step, capture, save, snapshot, load, quit.");
        }
    }
}
