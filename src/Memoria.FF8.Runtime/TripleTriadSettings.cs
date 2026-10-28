namespace Memoria.FF8.Runtime;

public enum TripleTriadAssistance
{
    Off,
    Direct,
    Rules,
    Combo,
    Hand
}

/// <summary>Save-owned Triple Triad rules. Existing saves retain the original board and hand sizes.</summary>
public sealed record TripleTriadSettings
{
    public int Width { get; init; } = 3;
    public int Height { get; init; } = 3;
    public int HandSize { get; init; } = 5;
    public int BlockedCellCount { get; init; }
    public int RandomRedraws { get; init; }
    public TripleTriadAssistance Assistance { get; init; }
    public bool UseModifiedSamePlus { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int MinimumHandSize => (Width * Height - BlockedCellCount + 1) / 2;

    public TripleTriadSettings NormalizeForDifficulty(BattleDifficulty difficulty)
    {
        int width = difficulty == BattleDifficulty.Hardcore ? Math.Max(4, Width) : Width;
        int height = difficulty == BattleDifficulty.Hardcore ? Math.Max(4, Height) : Height;
        int blocked = Math.Clamp(BlockedCellCount, difficulty == BattleDifficulty.Hardcore ? 2 : 0, width * height / 3);
        return this with
        {
            Width = width,
            Height = height,
            BlockedCellCount = blocked,
            HandSize = Math.Max(HandSize, (width * height - blocked + 1) / 2),
            RandomRedraws = difficulty == BattleDifficulty.Hardcore ? Math.Min(1, RandomRedraws) : RandomRedraws,
            Assistance = difficulty == BattleDifficulty.Hardcore && Assistance > TripleTriadAssistance.Direct ? TripleTriadAssistance.Direct : Assistance
        };
    }

    public void Validate(BattleDifficulty difficulty = BattleDifficulty.Normal)
    {
        if (Width is < 2 or > 5 || Height is < 2 or > 5 || HandSize < MinimumHandSize || HandSize > 50 || BlockedCellCount < 0 || BlockedCellCount > Width * Height / 3 || RandomRedraws is < 0 or > 3 || !Enum.IsDefined(Assistance))
        {
            throw new InvalidDataException("Invalid Triple Triad settings.");
        }
        if (difficulty == BattleDifficulty.Hardcore && (Width < 4 || Height < 4 || BlockedCellCount < 2 || RandomRedraws > 1 || Assistance > TripleTriadAssistance.Direct))
        {
            throw new InvalidDataException("Hardcore requires a board of at least 4 by 4, at least two blocked cells, at most one redraw and at most Direct assistance.");
        }
    }
}
