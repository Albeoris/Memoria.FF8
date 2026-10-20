using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

public enum ControlDevice
{
    Keyboard,
    Controller
}

public enum ControllerPresentation
{
    Automatic,
    Generic,
    Xbox,
    PlayStation,
    Nintendo
}

public sealed record ControllerSettings(float LeftStickDeadZone = 0.2f, float RightStickDeadZone = 0.2f, float WalkRunThreshold = 0.5f, ControllerPresentation Presentation = ControllerPresentation.Automatic, bool VibrationEnabled = true);

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
    SkipMovie, // Retired profile identifier; movie skipping follows Cancel.
    UiTab,
    UiPageUp,
    UiPageDown
}

public sealed record ControlActionDefinition(ControlAction Action, string Label, string Category);

public sealed record InputChord(Keys[] Keys, Buttons[] Buttons)
{
    [JsonIgnore]
    public string DisplayName => FormatDisplayName(ControllerPresentation.Generic);

    public string FormatDisplayName(ControllerPresentation presentation) => string.Join(" + ", Keys.OrderBy(key => IsModifier(key) ? 0 : 1).ThenBy(key => key).Select(KeyName).Concat(Buttons.Select(button => ButtonName(button, presentation))));

    private static bool IsModifier(Keys key) => key is Microsoft.Xna.Framework.Input.Keys.LeftControl or Microsoft.Xna.Framework.Input.Keys.RightControl or Microsoft.Xna.Framework.Input.Keys.LeftShift or Microsoft.Xna.Framework.Input.Keys.RightShift or Microsoft.Xna.Framework.Input.Keys.LeftAlt or Microsoft.Xna.Framework.Input.Keys.RightAlt or Microsoft.Xna.Framework.Input.Keys.LeftWindows or Microsoft.Xna.Framework.Input.Keys.RightWindows;

    private static string KeyName(Keys key) => key switch
    {
        Microsoft.Xna.Framework.Input.Keys.LeftControl => "Left Ctrl",
        Microsoft.Xna.Framework.Input.Keys.RightControl => "Right Ctrl",
        Microsoft.Xna.Framework.Input.Keys.LeftShift => "Left Shift",
        Microsoft.Xna.Framework.Input.Keys.RightShift => "Right Shift",
        Microsoft.Xna.Framework.Input.Keys.LeftAlt => "Left Alt",
        Microsoft.Xna.Framework.Input.Keys.RightAlt => "Right Alt",
        Microsoft.Xna.Framework.Input.Keys.NumPad0 => "N0",
        Microsoft.Xna.Framework.Input.Keys.NumPad1 => "N1",
        Microsoft.Xna.Framework.Input.Keys.NumPad2 => "N2",
        Microsoft.Xna.Framework.Input.Keys.NumPad3 => "N3",
        Microsoft.Xna.Framework.Input.Keys.NumPad4 => "N4",
        Microsoft.Xna.Framework.Input.Keys.NumPad5 => "N5",
        Microsoft.Xna.Framework.Input.Keys.NumPad6 => "N6",
        Microsoft.Xna.Framework.Input.Keys.NumPad7 => "N7",
        Microsoft.Xna.Framework.Input.Keys.NumPad8 => "N8",
        Microsoft.Xna.Framework.Input.Keys.NumPad9 => "N9",
        _ => key.ToString()
    };

    internal static string ButtonName(Buttons button, ControllerPresentation presentation = ControllerPresentation.Generic) => (button, presentation) switch
    {
        (Microsoft.Xna.Framework.Input.Buttons.A, ControllerPresentation.PlayStation) => "\u00D7",
        (Microsoft.Xna.Framework.Input.Buttons.B, ControllerPresentation.PlayStation) => "\u25CB",
        (Microsoft.Xna.Framework.Input.Buttons.X, ControllerPresentation.PlayStation) => "\u25A1",
        (Microsoft.Xna.Framework.Input.Buttons.Y, ControllerPresentation.PlayStation) => "\u25B3",
        (Microsoft.Xna.Framework.Input.Buttons.LeftShoulder, ControllerPresentation.PlayStation) => "L1",
        (Microsoft.Xna.Framework.Input.Buttons.RightShoulder, ControllerPresentation.PlayStation) => "R1",
        (Microsoft.Xna.Framework.Input.Buttons.LeftTrigger, ControllerPresentation.PlayStation) => "L2",
        (Microsoft.Xna.Framework.Input.Buttons.RightTrigger, ControllerPresentation.PlayStation) => "R2",
        (Microsoft.Xna.Framework.Input.Buttons.Back, ControllerPresentation.PlayStation) => "Share / Create",
        (Microsoft.Xna.Framework.Input.Buttons.Start, ControllerPresentation.PlayStation) => "Options",
        (Microsoft.Xna.Framework.Input.Buttons.LeftStick, ControllerPresentation.PlayStation) => "L3",
        (Microsoft.Xna.Framework.Input.Buttons.RightStick, ControllerPresentation.PlayStation) => "R3",
        (Microsoft.Xna.Framework.Input.Buttons.A, ControllerPresentation.Xbox) => "[A]",
        (Microsoft.Xna.Framework.Input.Buttons.B, ControllerPresentation.Xbox) => "[B]",
        (Microsoft.Xna.Framework.Input.Buttons.X, ControllerPresentation.Xbox) => "[X]",
        (Microsoft.Xna.Framework.Input.Buttons.Y, ControllerPresentation.Xbox) => "[Y]",
        (Microsoft.Xna.Framework.Input.Buttons.LeftShoulder, ControllerPresentation.Xbox) => "LB",
        (Microsoft.Xna.Framework.Input.Buttons.RightShoulder, ControllerPresentation.Xbox) => "RB",
        (Microsoft.Xna.Framework.Input.Buttons.LeftTrigger, ControllerPresentation.Xbox) => "LT",
        (Microsoft.Xna.Framework.Input.Buttons.RightTrigger, ControllerPresentation.Xbox) => "RT",
        (Microsoft.Xna.Framework.Input.Buttons.Back, ControllerPresentation.Xbox) => "View",
        (Microsoft.Xna.Framework.Input.Buttons.Start, ControllerPresentation.Xbox) => "Menu",
        (Microsoft.Xna.Framework.Input.Buttons.LeftStick, ControllerPresentation.Xbox) => "LS",
        (Microsoft.Xna.Framework.Input.Buttons.RightStick, ControllerPresentation.Xbox) => "RS",
        (Microsoft.Xna.Framework.Input.Buttons.A, ControllerPresentation.Nintendo) => "[B]",
        (Microsoft.Xna.Framework.Input.Buttons.B, ControllerPresentation.Nintendo) => "[A]",
        (Microsoft.Xna.Framework.Input.Buttons.X, ControllerPresentation.Nintendo) => "[Y]",
        (Microsoft.Xna.Framework.Input.Buttons.Y, ControllerPresentation.Nintendo) => "[X]",
        (Microsoft.Xna.Framework.Input.Buttons.LeftShoulder, ControllerPresentation.Nintendo) => "L",
        (Microsoft.Xna.Framework.Input.Buttons.RightShoulder, ControllerPresentation.Nintendo) => "R",
        (Microsoft.Xna.Framework.Input.Buttons.LeftTrigger, ControllerPresentation.Nintendo) => "ZL",
        (Microsoft.Xna.Framework.Input.Buttons.RightTrigger, ControllerPresentation.Nintendo) => "ZR",
        (Microsoft.Xna.Framework.Input.Buttons.Back, ControllerPresentation.Nintendo) => "Minus",
        (Microsoft.Xna.Framework.Input.Buttons.Start, ControllerPresentation.Nintendo) => "Plus",
        (Microsoft.Xna.Framework.Input.Buttons.LeftStick, ControllerPresentation.Nintendo) => "L Stick",
        (Microsoft.Xna.Framework.Input.Buttons.RightStick, ControllerPresentation.Nintendo) => "R Stick",
        (Microsoft.Xna.Framework.Input.Buttons.A, _) => "South button",
        (Microsoft.Xna.Framework.Input.Buttons.B, _) => "East button",
        (Microsoft.Xna.Framework.Input.Buttons.X, _) => "West button",
        (Microsoft.Xna.Framework.Input.Buttons.Y, _) => "North button",
        _ => button.ToString()
    };

    internal bool Matches(KeyboardState keyboard, GamePadState controller) => Keys.All(keyboard.IsKeyDown) && Buttons.All(controller.IsButtonDown);
    internal int Count => Keys.Length + Buttons.Length;
    internal bool Contains(InputChord other) => other.Keys.All(Keys.Contains) && other.Buttons.All(Buttons.Contains);
    internal bool Same(InputChord other) => Count == other.Count && Contains(other);
}

/// <summary>Host device preferences. Game-owned native configuration and saved game state remain separate.</summary>
public sealed class ControlsProfile
{
    private sealed record Binding(ControlAction Action, ControlDevice Device, InputChord Chord);
    private sealed record Document(int Version, Binding[] Bindings, ControllerSettings? Controller = null);
    private readonly Binding[] bindings;
    private static readonly Binding[] PermanentBindings =
    [
        new(ControlAction.Confirm, ControlDevice.Keyboard, new InputChord([Keys.Enter], [])),
        new(ControlAction.Cancel, ControlDevice.Keyboard, new InputChord([Keys.Escape], [])),
        new(ControlAction.MoveUp, ControlDevice.Keyboard, new InputChord([Keys.Up], [])),
        new(ControlAction.MoveDown, ControlDevice.Keyboard, new InputChord([Keys.Down], [])),
        new(ControlAction.MoveLeft, ControlDevice.Keyboard, new InputChord([Keys.Left], [])),
        new(ControlAction.MoveRight, ControlDevice.Keyboard, new InputChord([Keys.Right], []))
    ];

    public static IReadOnlyList<ControlActionDefinition> Definitions { get; } = Enum.GetValues<ControlAction>().Where(action => action != ControlAction.SkipMovie).Select(action => new ControlActionDefinition(action, Label(action), action <= ControlAction.MoveRight ? "Movement" : action <= ControlAction.RotateRight ? "Original game" : "Memoria shortcuts")).ToArray();
    public static IReadOnlyList<Buttons> BindableButtons { get; } = [Buttons.A, Buttons.B, Buttons.X, Buttons.Y, Buttons.LeftShoulder, Buttons.RightShoulder, Buttons.LeftTrigger, Buttons.RightTrigger, Buttons.Back, Buttons.Start, Buttons.LeftStick, Buttons.RightStick, Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight];

    private ControlsProfile(IEnumerable<Binding> bindings, ControllerSettings? controller = null)
    {
        Controller = controller ?? new ControllerSettings();
        this.bindings = bindings.Where(binding => binding.Action != ControlAction.SkipMovie).Select(binding => binding with { Chord = Normalize(binding.Chord, binding.Device) }).ToArray();
        Validate();
    }

    public ControllerSettings Controller { get; }
    public float LeftStickDeadZone => Controller.LeftStickDeadZone;
    public float RightStickDeadZone => Controller.RightStickDeadZone;
    public float WalkRunThreshold => Controller.WalkRunThreshold;
    public bool VibrationEnabled => Controller.VibrationEnabled;

    public ControlsProfile WithControllerSettings(float leftStickDeadZone, float rightStickDeadZone, float walkRunThreshold, ControllerPresentation? presentation = null, bool? vibrationEnabled = null) => new(bindings, new ControllerSettings(leftStickDeadZone, rightStickDeadZone, walkRunThreshold, presentation ?? Controller.Presentation, vibrationEnabled ?? Controller.VibrationEnabled));

    public IReadOnlyList<InputChord> GetBindings(ControlAction action, ControlDevice device) => bindings.Where(binding => binding.Action == action && binding.Device == device).Select(binding => binding.Chord with { Keys = [.. binding.Chord.Keys], Buttons = [.. binding.Chord.Buttons] }).ToArray();

    public static IReadOnlyList<InputChord> GetPermanentBindings(ControlAction action, ControlDevice device) => PermanentBindings.Where(binding => binding.Action == action && binding.Device == device).Select(binding => binding.Chord with { Keys = [.. binding.Chord.Keys], Buttons = [.. binding.Chord.Buttons] }).ToArray();

    internal static bool IsPermanentBinding(ControlAction action, ControlDevice device, InputChord chord) => PermanentBindings.Any(binding => binding.Action == action && binding.Device == device && binding.Chord.Same(chord));

    private static bool UsesReservedKeyForAnotherAction(ControlAction action, ControlDevice device, InputChord chord) => PermanentBindings.Any(binding => binding.Action != action && binding.Device == device && binding.Chord.Keys.Any(chord.Keys.Contains));

    public IReadOnlyList<ControlAction> FindConflicts(ControlAction action, ControlDevice device, InputChord chord)
    {
        chord = Normalize(chord, device);
        return bindings.Where(binding => binding.Action != action && binding.Device == device && binding.Chord.Same(chord)).Select(binding => binding.Action).Distinct().ToArray();
    }

    public ControlsProfile WithBinding(ControlAction action, ControlDevice device, InputChord chord, bool replaceConflicts = false)
    {
        chord = Normalize(chord, device);
        if (UsesReservedKeyForAnotherAction(action, device, chord))
        {
            throw new InvalidOperationException("Enter is reserved for Confirm, Escape for Cancel, and arrow keys for their movement directions, including in key combinations.");
        }
        IReadOnlyList<ControlAction> conflicts = FindConflicts(action, device, chord);
        if (conflicts.Count != 0 && !replaceConflicts)
        {
            throw new InvalidOperationException("Already assigned to " + string.Join(", ", conflicts.Select(Label)) + ".");
        }
        return new ControlsProfile(bindings.Where(binding => binding.Device != device || binding.Action != action && !conflicts.Contains(binding.Action)).Append(new Binding(action, device, chord)), Controller);
    }

    public ControlsProfile WithoutBinding(ControlAction action, ControlDevice device)
    {
        return new ControlsProfile(bindings.Where(binding => binding.Action != action || binding.Device != device), Controller);
    }

    public ControlsProfile Reset(ControlDevice device) => new(bindings.Where(binding => binding.Device != device).Concat(CreateDefault().bindings.Where(binding => binding.Device == device)), device == ControlDevice.Controller ? new ControllerSettings() : Controller);

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
        Binding[] held = bindings.Concat(PermanentBindings).Where(binding => binding.Chord.Matches(keyboard, controller)).ToArray();
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
        Binding[] loaded = document.Bindings.Where(binding => binding.Action != ControlAction.SkipMovie).Select(binding => binding with { Chord = Normalize(binding.Chord, binding.Device) }).Where(binding => !UsesReservedKeyForAnotherAction(binding.Action, binding.Device, binding.Chord)).ToArray();
        return new ControlsProfile(loaded, document.Controller);
    }

    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new Document(1, bindings, Controller), JsonOptions));
        File.Move(temporary, path, true);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    private void Validate()
    {
        if (!float.IsFinite(LeftStickDeadZone) || LeftStickDeadZone < 0 || LeftStickDeadZone > 0.5f || !float.IsFinite(RightStickDeadZone) || RightStickDeadZone < 0 || RightStickDeadZone > 0.5f || !float.IsFinite(WalkRunThreshold) || WalkRunThreshold < 0.1f || WalkRunThreshold > 0.95f || !Enum.IsDefined(Controller.Presentation))
        {
            throw new ArgumentException("Stick deadzones must be between 0% and 50%; run threshold must be between 10% and 95%.");
        }

        foreach (Binding binding in bindings)
        {
            if (!Enum.IsDefined(binding.Action) || !Enum.IsDefined(binding.Device))
            {
                throw new InvalidDataException("Unknown controls action or device.");
            }
        }
        if (!bindings.Any(binding => binding.Device == ControlDevice.Controller && binding.Action == ControlAction.Confirm) || !bindings.Any(binding => binding.Device == ControlDevice.Controller && binding.Action == ControlAction.Cancel))
        {
            throw new InvalidOperationException("Confirm and Cancel must remain assigned. Assign another key/button first, or restore defaults.");
        }
    }

    private static InputChord Normalize(InputChord chord, ControlDevice device)
    {
        if (chord is null || chord.Keys is null || chord.Buttons is null || chord.Count == 0 || chord.Count > 4 || device == ControlDevice.Keyboard && chord.Buttons.Length != 0 || device == ControlDevice.Controller && chord.Keys.Length != 0 || chord.Keys.Any(key => key == Keys.None || !Enum.IsDefined(key)) || chord.Buttons.Any(button => !BindableButtons.Contains(button)))
        {
            throw new ArgumentException("Use one to four keys or controller buttons. Stick directions are reserved for movement, camera and vehicle control; stick clicks can be assigned.");
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
