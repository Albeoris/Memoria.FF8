using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDevour62State([property: JsonRequired] int? PrimaryDrawBase, [property: JsonRequired] int? AlternatePrimaryDrawBase, [property: JsonRequired] int? SecondaryDrawBase, [property: JsonRequired] int? AlternateSecondaryDrawBase, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] int? AuxiliaryCursor, [property: JsonRequired] uint Counter2601918, [property: JsonRequired] uint Counter2601770)
{
    //A null arena offset represents the loader-established native null pointer.
    public static NativeBattleDevour62State FromColdProcess(uint counter2601918, uint counter2601770) => new(null, null, null, null, null, null, null, counter2601918, counter2601770);

    public void Validate()
    {
        if (PrimaryDrawBase is < 0 or > NativeBattleEffectArena.Length || AlternatePrimaryDrawBase is < 0 or > NativeBattleEffectArena.Length || SecondaryDrawBase is < 0 or > NativeBattleEffectArena.Length || AlternateSecondaryDrawBase is < 0 or > NativeBattleEffectArena.Length || PrimaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || SecondaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || AuxiliaryCursor is < 0 or > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Devour requires bounded physical arena pointers.");
        }
    }
}
