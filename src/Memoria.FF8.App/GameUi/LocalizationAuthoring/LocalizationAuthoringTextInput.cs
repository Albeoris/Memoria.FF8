using System.Collections.Concurrent;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Combines UTF-16 host events and owns composition/closing-key drain independently of rendering.</summary>
public sealed class LocalizationAuthoringTextInput : IDisposable
{
    private readonly Action<string> commit;
    private readonly Action<string> compose;
    private readonly ConcurrentQueue<(bool Committed, string Text, bool Active)> platformEvents = new();
    private char? highSurrogate;
    private bool drainConfirm;
    private bool drainCancel;
    private bool disposed;
    private NativeSdl2CompositionSource? native;
    private WindowsImmCompositionSource? windows;
    private nint windowsHandle;
    public bool UsesPlatformCommittedText => OperatingSystem.IsWindows() && windows is { Problem: null } || native is { Problem: null };
    public LocalizationAuthoringTextInput(Action<string> committedText, Action<string> compositionText)
    {
        commit = committedText;
        compose = compositionText;
    }
    public bool IsComposing { get; private set; }
    public string? PlatformProblem { get; private set; }
    public void ReceiveCharacter(char character)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (UsesPlatformCommittedText)
        {
            return;
        }
        ProcessCharacter(character);
    }
    private void ProcessCharacter(char character, Action<string>? output = null, bool allowLineBreaks = false)
    {
        Action<string> emit = output ?? Emit;
        if (char.IsControl(character) && !(allowLineBreaks && character is '\r' or '\n'))
        {
            return;
        }
        if (char.IsHighSurrogate(character))
        {
            if (highSurrogate is not null)
            {
                emit("\uFFFD");
            }
            highSurrogate = character;
            return;
        }
        if (char.IsLowSurrogate(character))
        {
            string value = highSurrogate is char high ? new string([high, character]) : "\uFFFD";
            highSurrogate = null;
            emit(value);
            return;
        }
        if (highSurrogate is not null)
        {
            highSurrogate = null;
            emit("\uFFFD");
        }
        emit(character.ToString());
    }
    public void ReceiveText(string value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        System.Text.StringBuilder batch = new();
        foreach (char character in value)
        {
            ProcessCharacter(character, text => batch.Append(text), allowLineBreaks: true);
        }
        if (batch.Length != 0)
        {
            Emit(batch.ToString());
        }
    }
    private void Emit(string value)
    {
        if (IsComposing)
        {
            ReceiveComposition("");
        }
        commit(value);
    }
    public void ReceiveComposition(string text, bool? composing = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        bool active = composing ?? text.Length != 0;
        if (active || IsComposing)
        {
            drainConfirm = drainCancel = true;
        }
        IsComposing = active;
        compose(text);
    }
    public void ObserveCommandKeys(bool confirmDown, bool cancelDown)
    {
        if (!IsComposing)
        {
            if (!confirmDown)
            {
                drainConfirm = false;
            }
            if (!cancelDown)
            {
                drainCancel = false;
            }
        }
    }
    public bool ShouldSuppressCommand(UiEditCommand command)
    {
        bool confirmCommand = command is UiEditCommand.Confirm or UiEditCommand.LineBreak or UiEditCommand.PageBreak;
        return IsComposing || confirmCommand && drainConfirm || command == UiEditCommand.Cancel && drainCancel;
    }
    public void PollPlatform(nint windowHandle, bool active)
    {
        if (disposed)
        {
            return;
        }
        if (!active)
        {
            highSurrogate = null;
            if (IsComposing)
            {
                ReceiveComposition("");
            }
            if (OperatingSystem.IsWindows() && windows is not null)
            {
                windows.Enabled = false;
            }
            if (native is not null)
            {
                native.Enabled = false;
            }
            while (platformEvents.TryDequeue(out _))
            {
            }
            return;
        }
        if (OperatingSystem.IsWindows())
        {
#if MEMORIA_NATIVE
            nint hwnd = WindowsImmCompositionSource.FocusedWindow();
#else
            nint hwnd = windowHandle;
#endif
            if (hwnd != windowsHandle)
            {
                windows?.Dispose();
                windowsHandle = hwnd;
                windows = hwnd == 0 ? null : new WindowsImmCompositionSource(hwnd, text => platformEvents.Enqueue((true, text, false)), (text, composing) => platformEvents.Enqueue((false, text, composing)));
            }
            if (windows is not null)
            {
                windows.Enabled = true;
            }
            PlatformProblem = windows?.Problem ?? (hwnd == 0 ? "No active game window owns text input." : null);
        }
        else
        {
            native ??= new NativeSdl2CompositionSource(windowHandle, text => platformEvents.Enqueue((true, text, false)), text => platformEvents.Enqueue((false, text, text.Length != 0)));
            native.Enabled = true;
            PlatformProblem = native.Problem;
        }
        while (platformEvents.TryDequeue(out var item))
        {
            if (item.Committed)
            {
                ReceiveText(item.Text);
            }
            else
            {
                ReceiveComposition(item.Text, item.Active);
            }
        }
    }
    public void Reset()
    {
        highSurrogate = null;
        IsComposing = false;
        drainConfirm = drainCancel = false;
        while (platformEvents.TryDequeue(out _))
        {
        }
        compose("");
    }
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        if (OperatingSystem.IsWindows())
        {
            windows?.Dispose();
        }
        windows = null;
        native?.Dispose();
        native = null;
        while (platformEvents.TryDequeue(out _))
        {
        }
    }
}
