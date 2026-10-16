using System.Globalization;

namespace Memoria.FF8.Ui;

/// <summary>Presentation-only typewriter progress measured in Unicode text elements, shared across styled text runs.</summary>
public sealed class UiTextReveal
{
    private double revealedCharacters;

    public int VisibleCharacters => (int)Math.Min(int.MaxValue, revealedCharacters);

    public void Update(TimeSpan delta, int characterCount, double charactersPerSecond, bool instant = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(characterCount);
        if (!double.IsFinite(charactersPerSecond) || charactersPerSecond < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(charactersPerSecond));
        }
        revealedCharacters = instant ? characterCount : Math.Min(characterCount, revealedCharacters + Math.Max(0, delta.TotalSeconds) * charactersPerSecond);
    }

    public bool RevealAll(int characterCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(characterCount);
        bool revealed = revealedCharacters < characterCount;
        revealedCharacters = characterCount;
        return revealed;
    }

    public void Reset()
    {
        revealedCharacters = 0;
    }

    public static int CharacterCount(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return StringInfo.ParseCombiningCharacters(text).Length;
    }

    public static string VisiblePrefix(string text, ref int remaining)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(remaining);
        int[] elements = StringInfo.ParseCombiningCharacters(text);
        int count = Math.Min(remaining, elements.Length);
        remaining -= count;
        return count == elements.Length ? text : text[..elements[count]];
    }
}
