using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MessageVariableRenderingTests
{
    [Fact]
    public void NativeUnsignedVariablesAndOrdinaryTagsUseOneLiveStringForMeasurementAndDraw()
    {
        const string template = "{Char Squall}{Line}Conduct {Var 49} Pt.{Line}Rank {Var55}{End}";
        NativeMessageVariables bank = NativeMessageVariables.FromColdProcess().WithWrite(1, 100).WithWrite(7, 9);
        Assert.Equal("Squall\nConduct 100 Pt.\nRank 9", DialogueTextFormatter.Format(template, messageVariables: bank));
        NativeMessageVariables changed = bank.WithWrite(1, -1);
        Assert.Equal("Squall\nConduct 4294967295 Pt.\nRank 9", DialogueTextFormatter.Format(template, messageVariables: changed));
        Assert.Equal("4,294,967,295/FFFFFFFF", DialogueTextFormatter.Format("{Var 33}/{Var65}", messageVariables: changed));
        Assert.Throws<NotSupportedException>(() => DialogueTextFormatter.Format(template));
        Assert.Equal(bank, bank.WithWrite(1, 100));
    }
}
