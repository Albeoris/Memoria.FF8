using System.Buffers;
using System.Text;

namespace Memoria.FF8.Runtime;

/// <summary>Stable subjects shared by native naming and explicit research naming inputs.</summary>
public static class GameNames
{
    public const int GrieverSubject = 43;
    private static readonly string[] CharacterDefaults = ["Squall", "Zell", "Irvine", "Quistis", "Rinoa", "Selphie", "Seifer", "Edea", "Laguna", "Kiros", "Ward"];
    private static readonly string[] GuardianForceDefaults = ["Quezacotl", "Shiva", "Ifrit", "Siren", "Brothers", "Diablos", "Carbuncle", "Leviathan", "Pandemona", "Cerberus", "Alexander", "Doomtrain", "Bahamut", "Cactuar", "Tonberry", "Eden"];

    public static int CharacterSubject(int identity) => identity switch
    {
        0 => 0,
        4 => 1,
        >= 1 and <= 10 => 32 + identity,
        _ => throw new ArgumentOutOfRangeException(nameof(identity))
    };

    public static int GuardianForceSubject(int identity) => (uint)identity < 16 ? 16 + identity : throw new ArgumentOutOfRangeException(nameof(identity));

    public static bool IsValidSubject(int subject) => subject is 0 or 1 or 2 or GrieverSubject or >= 16 and <= 31 or >= 33 and <= 42 && subject != 36;

    public static string DefaultName(int subject) => subject switch
    {
        0 => CharacterDefaults[0],
        1 => CharacterDefaults[4],
        2 => "Angelo",
        GrieverSubject => "Griever",
        >= 16 and <= 31 => GuardianForceDefaults[subject - 16],
        >= 33 and <= 42 when subject != 36 => CharacterDefaults[subject - 32],
        _ => throw new ArgumentOutOfRangeException(nameof(subject))
    };

    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Any(char.IsControl))
        {
            throw new ArgumentException("A name cannot contain control characters.", nameof(text));
        }
        string name = text.Trim();
        if (!IsValidDraft(name))
        {
            throw new ArgumentException("A name can contain at most twelve Unicode characters without controls, invalid Unicode or markup braces.", nameof(text));
        }
        if (name.Length == 0)
        {
            throw new ArgumentException("A name cannot be blank.", nameof(text));
        }
        return name;
    }

    public static bool IsValidDraft(string? text)
    {
        if (text is null)
        {
            return false;
        }
        int count = 0;
        ReadOnlySpan<char> remaining = text.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out Rune rune, out int consumed) != OperationStatus.Done || Rune.IsControl(rune) || rune.Value is '{' or '}')
            {
                return false;
            }
            remaining = remaining[consumed..];
            count++;
            if (count > 12)
            {
                return false;
            }
        }
        return true;
    }
}
