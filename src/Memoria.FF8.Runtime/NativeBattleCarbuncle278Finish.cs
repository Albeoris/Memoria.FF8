using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The creature's four sound selectors and the root's final actor restoration.</summary>
public static class NativeBattleCarbuncle278Finish
{
    public static int? SoundIndex681ACE(ushort counter) => counter switch
    {
        1 => 0,
        86 => 1,
        145 => 2,
        215 => 3,
        _ => null
    };

    public static NativeBattleEffectMemory WriteCompletion508660(NativeBattleEffectMemory memory, uint address, byte value)
    {
        if (address != 0x25081D8)
        {
            throw new NotSupportedException("Native Carbuncle texture completion requires its actual module BYTE address.");
        }
        NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(memory);
        return memory with { Carbuncle278 = state with { Done81D8 = value } };
    }

    public static ImmutableArray<NativeBattleGraphicsModel> Restore681199(ImmutableArray<NativeBattleGraphicsModel> models, int lift)
    {
        return models.Select(model =>
        {
            if (model.Index < 3)
            {
                return model with { Flags0 = unchecked((ushort)(model.Flags0 & 0xFFFB)) };
            }
            if ((model.Flags0 & 2) == 0)
            {
                return model;
            }
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Carbuncle restoration requires each loaded enemy's current Z WORD.");
            return model with { Transform = transform with { Position28 = transform.Position28 with { Z = unchecked((short)(transform.Position28.Z - lift)) } } };
        }).ToImmutableArray();
    }
}
