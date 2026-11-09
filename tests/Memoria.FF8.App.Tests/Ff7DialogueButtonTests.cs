using Memoria.FF7.Formats;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.Tests;

public sealed class Ff7DialogueButtonTests
{
    [Theory]
    [InlineData(0xF6, 0x20)]
    [InlineData(0xF7, 0x10)]
    [InlineData(0xF8, 0x80)]
    [InlineData(0xF9, 0x40)]
    public void OriginalTextButtonsUseConfiguredBindingAndSharedInlineLayout(byte code, ushort mask)
    {
        FieldTextToken token = Assert.Single(FieldText.Read(new byte[] { code, 0xFF }));
        Assert.Equal(FieldTextTokenKind.ControllerButton, token.Kind);
        string label = Ff7DialogueButtons.Label(token.Code, actual => actual == mask ? "Custom Key" : throw new InvalidOperationException("Wrong native button identity."));
        DialogueRun run = new(label, Color.White, IsButton: true);
        Assert.Equal("Custom Key", label);
        Assert.Equal(ButtonPrompt.Measure(label, text => text.Length * 6, 12), DialogueLayout.MeasureRun(run, text => text.Length * 6, 12));
        int remaining = 1;
        Assert.Equal(label, ButtonPrompt.VisibleText(run, ref remaining));
        Assert.Equal(0, remaining);
    }
}
