namespace Albeoris.Games.FF8.Imaging.Tim;

/// <summary>A conventional palette derived from one row of a TIM CLUT.</summary>
public sealed class TimPalette : IReadOnlyList<Rgba32>
{
    internal TimPalette(ReadOnlySpan<UInt16> colors)
    {
        Colors = new Rgba32[colors.Length];
        for (Int32 index = 0; index < colors.Length; index++)
            Colors[index] = Rgba32.FromPsxBgr555(colors[index]);
    }

    public Rgba32[] Colors { get; }
    public Int32 Count => Colors.Length;
    public Rgba32 this[Int32 index] => Colors[index];

    public IEnumerator<Rgba32> GetEnumerator() => ((IEnumerable<Rgba32>)Colors).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => Colors.GetEnumerator();
}
