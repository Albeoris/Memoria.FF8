using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleKiros262TrailState([property: JsonRequired] NativeBattleEffectArena Memory)
{
    public const uint Address = 0x2795DF8;
    public const int GatherA = 0;
    public const int GatherB = 0x40;
    public const int CurveA = 0x80;
    public const int CurveB = 0x180;
    public const int History = 0x2D0;

    public static NativeBattleKiros262TrailState FromColdProcess() => new(NativeBattleEffectArena.Clear());

    public void Validate()
    {
        if (Memory is null)
        {
            throw new InvalidDataException("Native trail requires its retained process bytes.");
        }
        Memory.Validate();
        if (Memory.Regions.Any(region => !Contains(region.Offset, region.Data.Length)) || Memory.UnknownRegions.Any(region => !Contains(region.Offset, region.Length)))
        {
            throw new InvalidDataException("Native trail process bytes intersect the adjacent callback bank or leave their actual workspace.");
        }
    }

    private static bool Contains(int offset, int count) => offset >= 0 && offset <= 0x288 - count || offset >= 0x298 && offset <= 0x400 - count;
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleKiros262TrailState? Kiros262Trail { get; init; }
}
