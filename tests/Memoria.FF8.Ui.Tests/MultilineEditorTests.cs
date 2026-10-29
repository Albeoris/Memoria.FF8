namespace Memoria.FF8.Ui.Tests;

public sealed class MultilineEditorTests
{
    [Fact]
    public void DeletionTreatsEmojiAndCombiningMarksAsTextElements()
    {
        UiMultilineEditor editor = new();
        editor.SetText("A😀e\u0301");
        editor.Select(editor.Text.Length, 0);
        editor.Command(UiEditCommand.Backspace);
        Assert.Equal("A😀", editor.Text);
        editor.Command(UiEditCommand.Backspace);
        Assert.Equal("A", editor.Text);
        editor.Command(UiEditCommand.Undo);
        Assert.Equal("A😀", editor.Text);
    }

    [Fact]
    public void FirstEditingInputCanSwitchFromReferenceWithoutDroppingText()
    {
        UiMultilineEditor editor = new();
        editor.SetText("reference");
        editor.ReadOnly = true;
        editor.BeforeEdit = () =>
        {
            editor.ReadOnly = false;
            editor.SetText("draft");
            editor.Select(5, 0);
        };
        editor.Insert("日");
        Assert.Equal("draft日", editor.Text);
    }

    [Fact]
    public void CompositionDoesNotInterpretConfirmOrCancelAsEditorCommands()
    {
        UiMultilineEditor editor = new();
        editor.SetText("base");
        editor.SetComposition("に");
        Assert.True(editor.Command(UiEditCommand.Confirm));
        Assert.True(editor.Command(UiEditCommand.Cancel));
        Assert.Equal("base", editor.Text);
        editor.Insert("日");
        Assert.False(editor.IsComposing);
        Assert.Equal("日base", editor.Text);
    }

    [Fact]
    public void ClipboardSelectionAndUndoAreDraftLocal()
    {
        UiMultilineEditor editor = new();
        string clipboard = "";
        editor.WriteClipboard = value => clipboard = value;
        editor.ReadClipboard = () => clipboard;
        editor.SetText("one\ntwo");
        editor.Select(0, 3);
        editor.Command(UiEditCommand.Cut);
        Assert.Equal("one", clipboard);
        Assert.Equal("\ntwo", editor.Text);
        editor.Command(UiEditCommand.Undo);
        Assert.Equal("one\ntwo", editor.Text);
        editor.Command(UiEditCommand.Redo);
        Assert.Equal("\ntwo", editor.Text);
        editor.Command(UiEditCommand.Paste);
        Assert.Equal("one\ntwo", editor.Text);
    }
}
