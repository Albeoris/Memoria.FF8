using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationAuthoringGeometryTests
{
    [Fact]
    public void DraftWindowPrefixChangesOnlyProjectedCoordinatesAndSize()
    {
        DialogueState native = new(0, 3, "Native", 0, 0);
        DialogueWindow original = new(0, 12, 18, 120, 48);
        string target = new FieldMessageContext(3, new(30, 40), size: new(180, 90)).FormatTag() + "Draft";
        DialogueWindow projected = RecreateGame.ProjectAuthoringWindow(native, original, target)!;
        Assert.Equal(new DialogueWindow(0, 30, 40, 180, 90), projected);
        Assert.Equal(new DialogueWindow(0, 12, 18, 120, 48), original);
        Assert.Same(original, RecreateGame.ProjectAuthoringWindow(native, original, "Draft"));
        Assert.Equal(new DialogueWindow(0, 30, 40, 180, 90), RecreateGame.ProjectAuthoringWindow(native, null, target));
        Assert.Equal("Native", native.Text);
    }

    [Fact]
    public void PreviewCursorUsesDistinctSemanticSourceLinesWithoutChangingNativeSelection()
    {
        DialogueVisualPage page = new(null, [new([], 0, 3, false), new([], 0, 7, true), new([], 0, 7, true), new([], 0, 11, true)], 160, 80, true);
        int nativeSelection = 7;
        Assert.Equal(11, RecreateGame.AuthoringPreviewChoiceLine(page, 1, nativeSelection));
        Assert.Equal(11, RecreateGame.AuthoringPreviewChoiceLine(page, 99, nativeSelection));
        Assert.Equal(7, nativeSelection);
        Assert.Equal(7, RecreateGame.AuthoringPreviewChoiceLine(page with { Lines = [] }, 1, nativeSelection));
    }
}
