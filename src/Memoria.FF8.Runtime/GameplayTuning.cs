namespace Memoria.FF8.Runtime;

/// <summary>Save-owned optional numeric rules, independent of the resource package that requested them.</summary>
public sealed record GameplayTuning
{
    public int DrawBonus { get; init; }
    public int ApMultiplier { get; init; } = 1;
    public int ExperienceMultiplier { get; init; } = 1;
    public int GuardianExperienceMultiplier { get; init; } = 1;
    public bool UnlimitedMagic { get; init; }
    /// <summary>The default retains native per-action primary caps; other values override the primary damage cap.</summary>
    public int MaximumDamage { get; init; } = 9999;
    public RareItemProbabilities RareItem { get; init; } = new();
    public bool UnrestrictedScanRotation { get; init; }

    public void Validate()
    {
        if (DrawBonus is < 0 or > 100 || ApMultiplier is < 0 or > 100 || ExperienceMultiplier is < 0 or > 100 || GuardianExperienceMultiplier is < 0 or > 100 || MaximumDamage is < 0 or > 100000 || RareItem is null)
        {
            throw new InvalidDataException("Gameplay rule values exceed their supported ranges.");
        }
        RareItem.Validate();
    }
}

/// <summary>Percentages for the four Rare Item selection slots. The original 256-value random clock is retained.</summary>
public sealed record RareItemProbabilities
{
    public decimal First { get; init; } = 50m;
    public decimal Second { get; init; } = 44.53125m;
    public decimal Third { get; init; } = 5.46875m;
    public decimal Fourth { get; init; }

    public void Validate()
    {
        if (First is < 0 or > 100 || Second is < 0 or > 100 || Third is < 0 or > 100 || Fourth is < 0 or > 100 || First + Second + Third + Fourth != 100m)
        {
            throw new InvalidDataException("Rare Item probabilities must be nonnegative percentages totaling 100%.");
        }
    }

    public sbyte Select(byte sample)
    {
        Validate();
        decimal value = sample * 100m;
        return value < First * 256m ? (sbyte)0 : value < (First + Second) * 256m ? (sbyte)1 : value < (First + Second + Third) * 256m ? (sbyte)2 : (sbyte)3;
    }
}
