using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshSwordTrailResources([property: JsonRequired] ImmutableArray<uint> Colors)
{
    public void Validate()
    {
        if (Colors.IsDefault || !Colors.AsSpan().SequenceEqual(new uint[] { 0x3A602020, 0x3A407070, 0x3A903030, 0x3A303090 }))
        {
            throw new InvalidDataException("Native sword trail requires its four authenticated G4 color DWORDs.");
        }
    }
}
