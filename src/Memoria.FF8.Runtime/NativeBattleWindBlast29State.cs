using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWindBlast29State([property: JsonRequired] NativeBattleCuragaState? Nodes, [property: JsonRequired] uint Counter26D74E8, [property: JsonRequired] uint Counter26D6B44)
{
    [JsonIgnore]
    public int SourceArenaOffset => Nodes?.SourceArenaOffset ?? throw new NotSupportedException("Wind Blast requires its executed source pointer.");
    [JsonIgnore]
    public int? PrimaryDrawCursor => Nodes?.PrimaryDrawCursor;
    [JsonIgnore]
    public int? SecondaryDrawCursor => Nodes?.SecondaryDrawCursor;

    public static NativeBattleWindBlast29State FromColdProcess(uint counter26D74E8, uint counter26D6B44) => new(null, counter26D74E8, counter26D6B44);

    public void Validate()
    {
        if (Nodes is { } nodes && (nodes.SourceArenaOffset < 0 || nodes.SourceArenaOffset > NativeBattleEffectArena.Length - 2 * NativeBattleWindBlast29Profile.DrawStep || nodes.PrimaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || nodes.SecondaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || nodes.AuxiliaryCursor < 0 || nodes.AuxiliaryCursor > NativeBattleEffectArena.Length || nodes.CopiedCamera is not null))
        {
            throw new InvalidDataException("Wind Blast requires bounded cursors and its canonical shared camera owner.");
        }
    }
}
