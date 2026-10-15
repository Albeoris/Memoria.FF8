using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

public enum ControlDevice
{
    Keyboard,
    Controller
}

public enum ControlAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Confirm,
    Cancel,
    Menu,
    Card,
    Select,
    Pause,
    CameraLeft,
    CameraRight,
    RotateLeft,
    RotateRight,
    SpeedToggle,
    DebugRoom,
    BattleDebug,
    BattleInstantVictory,
    BattleAdvanceTurn,
    QuickSave,
    QuickLoad,
    SaveMenu,
    LoadMenu,
    RecreateSave,
    SkipMovie,
    UiTab,
    UiPageUp,
    UiPageDown
}

public sealed record ControlActionDefinition(ControlAction Action, string Label, string Category);

public sealed record InputChord(Keys[] Keys, Buttons[] Buttons)
{
    [JsonIgnore]
    public string DisplayName => string.Join(" + ", Keys.OrderBy(key => IsModifier(key) ? 0 : 1).ThenBy(key => key).Select(KeyName).Concat(Buttons.Select(ButtonName)));

    private static bool IsModifier(Keys key) => key is Microsoft.Xna.Framework.Input.Keys.LeftControl or Microsoft.Xna.Framework.Input.Keys.RightControl or Microsoft.Xna.Framework.Input.Keys.LeftShift or Microsoft.Xna.Framework.Input.Keys.RightShift or Microsoft.Xna.Framework.Input.Keys.LeftAlt or Microsoft.Xna.Framework.Input.Keys.RightAlt or Microsoft.Xna.Framework.Input.Keys.LeftWindows or Microsoft.Xna.Framework.Input.Keys.RightWindows;

    private static string KeyName(Keys key) => key switch
    {
        Microsoft.Xna.Framework.Input.Keys.LeftControl => "Left Ctrl",
        Microsoft.Xna.Framework.Input.Keys.RightControl => "Right Ctrl",
        Microsoft.Xna.Framework.Input.Keys.LeftShift => "Left Shift",
        Microsoft.Xna.Framework.Input.Keys.RightShift => "Right Shift",
        Microsoft.Xna.Framework.Input.Keys.LeftAlt => "Left Alt",
        Microsoft.Xna.Framework.Input.Keys.RightAlt => "Right Alt",
        _ => key.ToString()
    };

    internal static string ButtonName(Buttons button) => button switch
    {
        Microsoft.Xna.Framework.Input.Buttons.A => "A / Cross",
        Microsoft.Xna.Framework.Input.Buttons.B => "B / Circle",
        Microsoft.Xna.Framework.Input.Buttons.X => "X / Square",
        Microsoft.Xna.Framework.Input.Buttons.Y => "Y / Triangle",
        Microsoft.Xna.Framework.Input.Buttons.LeftShoulder => "LB / L1",
        Microsoft.Xna.Framework.Input.Buttons.RightShoulder => "RB / R1",
        Microsoft.Xna.Framework.Input.Buttons.LeftTrigger => "LT / L2",
        Microsoft.Xna.Framework.Input.Buttons.RightTrigger => "RT / R2",
        _ => button.ToString()
    };

    internal bool Matches(KeyboardState keyboard, GamePadState controller) => Keys.All(keyboard.IsKeyDown) && Buttons.All(controller.IsButtonDown);
    internal int Count => Keys.Length + Buttons.Length;
    internal bool Contains(InputChord other) => other.Keys.All(Keys.Contains) && other.Buttons.All(Buttons.Contains);
    internal bool Same(InputChord other) => Count == other.Count && Contains(other);
}

/// <summary>Host device preferences. Game-owned native configuration and causal saves remain separate.</summary>
public sealed class ControlsProfile
{
    private sealed record Binding(ControlAction Action, ControlDevice Device, InputChord Chord);
    private sealed record Document(int Version, Binding[] Bindings);
    private readonly Binding[] bindings;

    public static IReadOnlyList<ControlActionDefinition> Definitions { get; } = Enum.GetValues<ControlAction>().Select(action => new ControlActionDefinition(action, Label(action), action <= ControlAction.MoveRight ? "Movement" : action <= ControlAction.RotateRight ? "Original game" : "Memoria shortcuts")).ToArray();
    public static IReadOnlyList<Buttons> BindableButtons { get; } = [Buttons.A, Buttons.B, Buttons.X, Buttons.Y, Buttons.LeftShoulder, Buttons.RightShoulder, Buttons.LeftTrigger, Buttons.RightTrigger, Buttons.Back, Buttons.Start, Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight];

    private ControlsProfile(IEnumerable<Binding> bindings)
    {
        this.bindings = bindings.Select(binding => binding with { Chord = Normalize(binding.Chord, binding.Device) }).ToArray();
        Validate();
    }

    public IReadOnlyList<InputChord> GetBindings(ControlAction action, ControlDevice device) => bindings.Where(binding => binding.Action == action && binding.Device == device).Select(binding => binding.Chord with { Keys = [.. binding.Chord.Keys], Buttons = [.. binding.Chord.Buttons] }).ToArray();

    public IReadOnlyList<ControlAction> FindConflicts(ControlAction action, ControlDevice device, InputChord chord)
    {
        chord = Normalize(chord, device);
        return bindings.Where(binding => binding.Action != action && binding.Device == device && binding.Chord.Same(chord)).Select(binding => binding.Action).Distinct().ToArray();
    }

    public ControlsProfile WithBinding(ControlAction action, ControlDevice device, InputChord chord, bool replaceConflicts = false)
    {
        chord = Normalize(chord, device);
        IReadOnlyList<ControlAction> conflicts = FindConflicts(action, device, chord);
        if (conflicts.Count != 0 && !replaceConflicts)
        {
            throw new InvalidOperationException("Already assigned to " + string.Join(", ", conflicts.Select(Label)) + ".");
        }
        return new ControlsProfile(bindings.Where(binding => binding.Device != device || binding.Action != action && !conflicts.Contains(binding.Action)).Append(new Binding(action, device, chord)));
    }

    public ControlsProfile WithoutBinding(ControlAction action, ControlDevice device)
    {
        return new ControlsProfile(bindings.Where(binding => binding.Action != action || binding.Device != device));
    }

    public ControlsProfile Reset(ControlDevice device) => new(bindings.Where(binding => binding.Device != device).Concat(CreateDefault().bindings.Where(binding => binding.Device == device)));

    public static ControlsProfile CreateDefault()
    {
        List<Binding> result = [];
        void Key(ControlAction action, params Keys[] keys)
        {
            foreach (Keys key in keys)
            {
                result.Add(new Binding(action, ControlDevice.Keyboard, new InputChord([key], [])));
            }
        }
        void Pad(ControlAction action, params Buttons[] buttons)
        {
            result.Add(new Binding(action, ControlDevice.Controller, new InputChord([], buttons)));
        }
        Key(ControlAction.MoveUp, Keys.Up);
        Key(ControlAction.MoveDown, Keys.Down);
        Key(ControlAction.MoveLeft, Keys.Left);
        Key(ControlAction.MoveRight, Keys.Right);
        Key(ControlAction.Confirm, Keys.X);
        Key(ControlAction.Cancel, Keys.C);
        Key(ControlAction.Menu, Keys.V);
        Key(ControlAction.Card, Keys.S);
        Key(ControlAction.Select, Keys.J);
        Key(ControlAction.Pause, Keys.A);
        Key(ControlAction.CameraLeft, Keys.D);
        Key(ControlAction.CameraRight, Keys.F);
        Key(ControlAction.RotateLeft, Keys.H);
        Key(ControlAction.RotateRight, Keys.G);
        Key(ControlAction.SpeedToggle, Keys.F1);
        Key(ControlAction.DebugRoom, Keys.F2);
        Key(ControlAction.BattleInstantVictory, Keys.F3);
        Key(ControlAction.BattleDebug, Keys.F4);
        Key(ControlAction.QuickSave, Keys.F5);
        Key(ControlAction.SaveMenu, Keys.F6);
        Key(ControlAction.QuickLoad, Keys.F9);
        Key(ControlAction.LoadMenu, Keys.F10);
        Key(ControlAction.RecreateSave, Keys.R);
        Key(ControlAction.SkipMovie, Keys.Escape, Keys.Space);
        Key(ControlAction.BattleAdvanceTurn, Keys.Space);
        Key(ControlAction.UiTab, Keys.Tab);
        Key(ControlAction.UiPageUp, Keys.PageUp);
        Key(ControlAction.UiPageDown, Keys.PageDown);
        Pad(ControlAction.MoveUp, Buttons.DPadUp);
        Pad(ControlAction.MoveDown, Buttons.DPadDown);
        Pad(ControlAction.MoveLeft, Buttons.DPadLeft);
        Pad(ControlAction.MoveRight, Buttons.DPadRight);
        Pad(ControlAction.Confirm, Buttons.A);
        Pad(ControlAction.Cancel, Buttons.B);
        Pad(ControlAction.Menu, Buttons.Y);
        Pad(ControlAction.Card, Buttons.X);
        Pad(ControlAction.Select, Buttons.Back);
        Pad(ControlAction.Pause, Buttons.Start);
        Pad(ControlAction.CameraLeft, Buttons.LeftTrigger);
        Pad(ControlAction.CameraRight, Buttons.RightTrigger);
        Pad(ControlAction.RotateLeft, Buttons.LeftShoulder);
        Pad(ControlAction.RotateRight, Buttons.RightShoulder);
        return new ControlsProfile(result);
    }

    internal IReadOnlyList<(ControlAction Action, ControlDevice Device, InputChord Chord)> Match(KeyboardState keyboard, GamePadState controller)
    {
        Binding[] held = bindings.Where(binding => binding.Chord.Matches(keyboard, controller)).ToArray();
        return held.Where(binding => !held.Any(other => other.Device == binding.Device && other.Chord.Count > binding.Chord.Count && other.Chord.Contains(binding.Chord))).Select(binding => (binding.Action, binding.Device, binding.Chord)).ToArray();
    }

    public static ControlsProfile Load(string path)
    {
        if (!File.Exists(path))
        {
            return CreateDefault();
        }
        Document document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("Empty controls profile.");
        if (document.Version != 1 || document.Bindings is null)
        {
            throw new InvalidDataException("Unsupported controls profile.");
        }
        return new ControlsProfile(document.Bindings);
    }

    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new Document(1, bindings), JsonOptions));
        File.Move(temporary, path, true);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    private void Validate()
    {
        foreach (Binding binding in bindings)
        {
            if (!Enum.IsDefined(binding.Action) || !Enum.IsDefined(binding.Device))
            {
                throw new InvalidDataException("Unknown controls action or device.");
            }
        }
        foreach (ControlDevice device in Enum.GetValues<ControlDevice>())
        {
            if (!bindings.Any(binding => binding.Device == device && binding.Action == ControlAction.Confirm) || !bindings.Any(binding => binding.Device == device && binding.Action == ControlAction.Cancel))
            {
                throw new InvalidOperationException("Confirm and Cancel must remain assigned. Assign another key/button first, or restore defaults.");
            }
        }
    }

    private static InputChord Normalize(InputChord chord, ControlDevice device)
    {
        if (chord is null || chord.Keys is null || chord.Buttons is null || chord.Count == 0 || chord.Count > 4 || device == ControlDevice.Keyboard && chord.Buttons.Length != 0 || device == ControlDevice.Controller && chord.Keys.Length != 0 || chord.Keys.Any(key => key == Keys.None || !Enum.IsDefined(key)) || chord.Buttons.Any(button => !BindableButtons.Contains(button)))
        {
            throw new ArgumentException("Use one to four keys or controller buttons. Sticks are reserved for movement and camera rotation.");
        }
        return new InputChord(chord.Keys.Distinct().Order().ToArray(), chord.Buttons.Distinct().Order().ToArray());
    }

    private static string Label(ControlAction action) => action switch
    {
        ControlAction.Confirm => "Confirm / Talk",
        ControlAction.Cancel => "Cancel / Walk",
        ControlAction.Card => "Card / Details",
        ControlAction.Select => "Select / Display",
        ControlAction.CameraLeft => "Escape battle (left)",
        ControlAction.CameraRight => "Camera mode / Escape battle (right)",
        ControlAction.RotateLeft => "Rotate world left / Previous character",
        ControlAction.RotateRight => "Rotate world right / Gunblade / Next character",
        ControlAction.SpeedToggle => "Event speed",
        ControlAction.DebugRoom => "Debug room",
        ControlAction.BattleInstantVictory => "Battle victory",
        ControlAction.BattleDebug => "Battle laboratory",
        ControlAction.QuickSave => "Quick save",
        ControlAction.SaveMenu => "Save menu",
        ControlAction.QuickLoad => "Quick load",
        ControlAction.LoadMenu => "Load menu",
        _ => System.Text.RegularExpressions.Regex.Replace(action.ToString(), "([a-z])([A-Z])", "$1 $2")
    };
}
