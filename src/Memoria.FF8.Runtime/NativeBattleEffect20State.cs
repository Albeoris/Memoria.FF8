using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect20State([property: JsonRequired] int? SourceArenaOffset, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] int? AuxiliaryCursor, [property: JsonRequired] uint Counter27268F8, [property: JsonRequired] uint Counter2722960)
{
    public static NativeBattleEffect20State FromColdProcess(uint counter27268F8, uint counter2722960) => new(null, null, null, null, counter27268F8, counter2722960);

    public void Validate()
    {
        if (SourceArenaOffset is < 0 or > NativeBattleEffectArena.Length - 2 * NativeBattleEffect20Profile.DrawStep || PrimaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || SecondaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || AuxiliaryCursor is < 0 or > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Effect20 cursors exceed their original arena owner.");
        }
    }
}
