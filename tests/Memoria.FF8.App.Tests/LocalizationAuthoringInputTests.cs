using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationAuthoringInputTests
{
    [Fact]
    public void SurrogateEventsBecomeOneCommittedUnicodeScalar()
    {
        List<string> committed = [];
        using LocalizationAuthoringTextInput input = new(committed.Add);
        input.ReceiveCharacter('\uD83D');
        Assert.Empty(committed);
        input.ReceiveCharacter('\uDE00');
        Assert.Equal(new[] { "😀" }, committed);
        input.ReceiveText("日e\u0301");
        Assert.Equal("😀日e\u0301", string.Concat(committed));
    }

    [Fact]
    public void FullCommittedTextPreservesMultilineWhileCharacterEnterRemainsACommand()
    {
        List<string> committed = [];
        using LocalizationAuthoringTextInput input = new(committed.Add);
        input.ReceiveCharacter('\r');
        input.ReceiveCharacter('\n');
        Assert.Empty(committed);
        input.ReceiveText("日😀\r\nSecond\nThird");
        Assert.Equal("日😀\r\nSecond\nThird", Assert.Single(committed));
    }

    [Fact]
    public void FocusResetDoesNotJoinSurrogatesAcrossEditingSessions()
    {
        List<string> committed = [];
        using LocalizationAuthoringTextInput input = new(committed.Add);
        input.ReceiveCharacter('\uD83D');
        input.Reset();
        input.ReceiveCharacter('\uDE00');
        Assert.Equal(new[] { "�" }, committed);
    }

    [Fact]
    public void CustomPageBreakBindingRoundTripsAndDoesNotActivateConfirm()
    {
        AuthoringShortcutSettings settings = new AuthoringShortcutSettings().Rebind(UiEditCommand.PageBreak, new(Keys.F8, Control: true));
        string json = JsonSerializer.Serialize(settings);
        Assert.Contains("F8", json);
        AuthoringShortcutSettings restored = JsonSerializer.Deserialize<AuthoringShortcutSettings>(json)!;
        AuthoringKeyboardInput input = new();
        input.Configure(restored);
        Assert.Equal(UiEditCommand.PageBreak, Assert.Single(input.Poll(new KeyboardState(Keys.LeftControl, Keys.F8), TimeSpan.Zero)).Command);
        input.Poll(new KeyboardState(), TimeSpan.Zero);
        Assert.Empty(input.Poll(new KeyboardState(Keys.LeftControl, Keys.Enter), TimeSpan.Zero));
        Assert.Throws<InvalidDataException>(() => restored.Rebind(UiEditCommand.PageBreak, new(Keys.Enter)));
    }

    [Fact]
    public void OnlyNavigationAndDeletionRepeatAndResetDrainsHeldKeys()
    {
        AuthoringKeyboardInput input = new();
        Assert.Equal(UiEditCommand.Left, Assert.Single(input.Poll(new KeyboardState(Keys.Left), TimeSpan.Zero)).Command);
        Assert.Empty(input.Poll(new KeyboardState(Keys.Left), TimeSpan.FromMilliseconds(399)));
        Assert.Equal(UiEditCommand.Left, Assert.Single(input.Poll(new KeyboardState(Keys.Left), TimeSpan.FromMilliseconds(1))).Command);
        Assert.Equal(UiEditCommand.Left, Assert.Single(input.Poll(new KeyboardState(Keys.Left), TimeSpan.FromMilliseconds(40))).Command);
        input.Poll(new KeyboardState(), TimeSpan.Zero);
        Assert.Equal(UiEditCommand.Confirm, Assert.Single(input.Poll(new KeyboardState(Keys.Enter), TimeSpan.Zero)).Command);
        Assert.Empty(input.Poll(new KeyboardState(Keys.Enter), TimeSpan.FromSeconds(2)));
        input.Reset(new KeyboardState(Keys.Delete));
        Assert.Empty(input.Poll(new KeyboardState(Keys.Delete), TimeSpan.FromSeconds(1)));
        input.Poll(new KeyboardState(), TimeSpan.Zero);
        Assert.Equal(UiEditCommand.Delete, Assert.Single(input.Poll(new KeyboardState(Keys.Delete), TimeSpan.Zero)).Command);
    }

    [Fact]
    public void TogglePickingWorksWithoutOwningGameplayKeyboard()
    {
        using LocalizationWorkbenchController workbench = new() { Enabled = true, JournalExpanded = true };
        Assert.False(workbench.OwnsKeyboard);
        Assert.True(workbench.PickText);
        Assert.True(workbench.HandleCommand(UiEditCommand.TogglePicking));
        Assert.False(workbench.PickText);
    }

    [Fact]
    public void ClipboardCallbacksPreserveUnicodeAndRawControls()
    {
        using LocalizationWorkbenchController workbench = new();
        string clipboard = "{Color Red}日本語😀";
        workbench.PasteText = () => clipboard;
        workbench.CopyText = value => clipboard = value;
        workbench.Editor.Command(UiEditCommand.Paste);
        Assert.Equal("{Color Red}日本語😀", workbench.Editor.Text);
        workbench.Editor.Command(UiEditCommand.SelectAll);
        workbench.Editor.Command(UiEditCommand.Copy);
        Assert.Equal("{Color Red}日本語😀", clipboard);
    }

    [Fact]
    public void ClipboardServiceInitializesOnceAndChecksFailures()
    {
        int initialized = 0;
        string clipboard = "";
        using LocalizationAuthoringClipboard service = new(() => initialized++, text =>
        {
            clipboard = text;
            return true;
        }, () => clipboard, () => "");
        service.Copy("日本語😀");
        Assert.Equal("日本語😀", service.Paste());
        Assert.Equal(1, initialized);
        using LocalizationAuthoringClipboard unavailable = new(() => { }, _ => false, () => "", () => "locked");
        Assert.Contains("locked", Assert.Throws<IOException>(() => unavailable.Copy("text")).Message);
    }

    [Fact]
    public void ClipboardFailureIsReportedWithoutLosingEditorText()
    {
        using LocalizationWorkbenchController workbench = new();
        workbench.Editor.SetText("Target😀");
        workbench.CopyText = _ => throw new IOException("clipboard locked");
        workbench.PasteText = () => throw new IOException("clipboard locked");
        workbench.Editor.Command(UiEditCommand.SelectAll);
        workbench.Editor.Command(UiEditCommand.Copy);
        workbench.Editor.Command(UiEditCommand.Paste);
        Assert.Equal("Target😀", workbench.Editor.Text);
        Assert.Equal("clipboard locked", workbench.ClipboardProblem);
    }

}
