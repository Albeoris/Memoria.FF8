using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved DWORD1CFE750, loaded from the actual New Game template and updated by native weapon discovery.</summary>
public sealed record NativeWeaponRecipes([property: JsonRequired] string InitOutSha256, [property: JsonRequired] uint Bits)
{
    /// <summary>Weapons actually equipped or remodeled in this save, including while optional upgrade rules are disabled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public uint ObtainedWeaponsBits { get; init; }

    /// <summary>Recipes explicitly learned by viewing Weapons Monthly pages; native discovery bits do not establish magazine learning.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public uint MagazineRecipeBits { get; init; }

    public NativeWeaponRecipes RecordEquippedWeapons(NativeCharacterVitals vitals)
    {
        vitals.Validate();
        if (vitals.InitOutSha256 != InitOutSha256)
        {
            throw new InvalidDataException("Weapon ownership and character records have different initialization identities.");
        }
        uint obtained = ObtainedWeaponsBits;
        foreach (NativeCharacterVital record in vitals.Records)
        {
            if (record.Weapon < 28)
            {
                obtained |= 1u << record.Weapon;
            }
        }
        return this with { ObtainedWeaponsBits = obtained };
    }

    public void Validate()
    {
        if ((MagazineRecipeBits & ~Bits) != 0 || (MagazineRecipeBits & 0xF0000000u) != 0 || (ObtainedWeaponsBits & 0xF0000000u) != 0 || InitOutSha256 is not { Length: 64 } || InitOutSha256.Any(value => !Uri.IsHexDigit(value)))
        {
            throw new InvalidDataException("Native weapon recipes lack their original saved initialization identity.");
        }
    }
}
