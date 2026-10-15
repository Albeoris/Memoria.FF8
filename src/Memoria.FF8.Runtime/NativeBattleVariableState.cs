using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved native AI DWORDs80..87 at1CFE93C..1CFE958, separate from per-battle variables96..103.</summary>
public sealed record NativeBattleVariableState([property: JsonRequired] ImmutableArray<uint> Values)
{
    public static NativeBattleVariableState CreateNewGame()
    {
        // Native56D9F0 ->47E410 clears these saved bytes. The2812-byte init.out does not reach them.
        return new(ImmutableArray.Create<uint>(0, 0, 0, 0, 0, 0, 0, 0));
    }

    public void Validate()
    {
        if (Values.IsDefault || Values.Length != 8)
        {
            throw new InvalidDataException("Saved native battle variables require all eight original DWORD owners.");
        }
    }
}
