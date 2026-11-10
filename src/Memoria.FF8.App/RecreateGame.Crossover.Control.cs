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
            case "world-autoplay":
                if (!InFf7)
                {
                    throw new InvalidOperationException("FFVII automatic play requires its active world.");
                }
                ff7Scene!.ApplyAutoPlayCommand(request.Name ?? "start");
                return new { ff7Scene.AutoPlayStatus };
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
            case "world-step-actions":
                if (!InFf7 || !options.ControlStep)
                {
                    throw new InvalidOperationException("FFVII world stepping requires its active world and --control-step.");
                }
                ControlAction[] actions = (request.Name ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(name => Enum.Parse<ControlAction>(name, true)).ToArray();
                ff7Scene!.StepActions(Math.Clamp(request.Frames, 1, 900), actions);
                return new { Tick = ff7Scene.Session?.Host.Process.Tick, ff7Scene.Error };
            case "world-switch":
                CombinedSessionSave? outgoing = InFf7 ? ff7Scene!.Capture() : null;
                if (request.Name is string appearance)
                {
                    CharacterAppearanceMapping[] mappings;
                    if (appearance == "Original")
                    {
                        mappings = [];
                    }
                    else
                    {
                        int target = Array.FindIndex(CrossoverCharacterCatalog.AppearanceNames, name => name.Equals(appearance, StringComparison.OrdinalIgnoreCase));
                        if (target < 0)
                        {
                            throw new NotSupportedException("Choose an available character in the world association editor.");
                        }
                        mappings = CharacterMappingAssociations.Assign(outgoing?.Mappings ?? crossover?.Mappings ?? [], 0, target);
                    }
                    if (outgoing is not null)
                    {
                        outgoing = outgoing with { Mappings = mappings };
                    }
                    else
                    {
                        pendingCrossoverMappings = mappings;
                    }
                }
                OpenCrossoverSetup(outgoing);
                return new { ActiveGame = InFf7 ? "FFVII" : "FFVIII", Open = true };
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
