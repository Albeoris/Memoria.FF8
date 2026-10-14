using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved DWORD1CFE750, loaded from the actual New Game template and updated by native weapon discovery.</summary>
public sealed record NativeWeaponRecipes([property: JsonRequired] string InitOutSha256, [property: JsonRequired] uint Bits)
{
    public void Validate()
    {
        if (InitOutSha256 is not { Length: 64 } || InitOutSha256.Any(value => !Uri.IsHexDigit(value)))
        {
            throw new InvalidDataException("Native weapon recipes lack their original saved initialization identity.");
        }
    }
}
