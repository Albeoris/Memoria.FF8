using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325State([property: JsonRequired] uint Pause5180, [property: JsonRequired] uint Action5184, [property: JsonRequired] uint Caster5188, [property: JsonRequired] uint PacketCursor51D0, [property: JsonRequired] int CenterZ51D4, [property: JsonRequired] int CenterX51D8, [property: JsonRequired] int FarZ51DC, [property: JsonRequired] int MidZ51E0, [property: JsonRequired] uint Control51E4, [property: JsonRequired] uint Flash51E8, [property: JsonRequired] uint PreviousFlash51EC, [property: JsonRequired] byte Mode51F0, [property: JsonRequired] uint ParticleCursor51F4, [property: JsonRequired] uint ModelBuffer5208)
{
    public static NativeBattleDiablos325State FromColdProcess() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public void Validate()
    {
        uint actor = unchecked(Caster5188 - 0x1D972C0);
        if (Action5184 is not (0 or 0x1D99A78) || Caster5188 != 0 && (actor % 0x9C != 0 || actor / 0x9C >= 7) || ModelBuffer5208 != 0 && ModelBuffer5208 != NativeBattleEffectNodeMemory.ArenaAddress || !Cursor(PacketCursor51D0) || !Cursor(ParticleCursor51F4))
        {
            throw new InvalidDataException("Native Diablos module pointers leave their original action, actor or effect arena owners.");
        }

        static bool Cursor(uint address) => address == 0 || address >= NativeBattleEffectNodeMemory.ArenaAddress && (ulong)address - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length;
    }
}

public sealed record NativeBattleDiablos325Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDiablos325State? Diablos325 { get; init; }
}
