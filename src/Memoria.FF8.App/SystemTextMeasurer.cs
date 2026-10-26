namespace Memoria.FF8;

/// <summary>Adds game button-run advances to the shared portable text shaper.</summary>
internal sealed class SystemTextMeasurer(float size, string? fontFamily) : PortableTextShaper(size, fontFamily)
{
    public override float Measure(string value)
    {
        return ButtonPromptText.Contains(value)
            ? ButtonPromptText.Runs(value).Sum(run => run.IsButton ? ButtonPrompt.Advance(Measure(run.Text), LineHeight) : Measure(run.Text))
            : base.Measure(value);
    }
}
