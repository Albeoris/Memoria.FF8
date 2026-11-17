using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{

    private static NativeBattleScanByteOwner CaptureNativeScanDescriptor208(BattleCombatant actor)
    {
        NativeBattleScanByteOwner row = new(new byte[208].ToImmutableArray(), new bool[208].ToImmutableArray());
        row = row.WriteDword(0x18, unchecked((uint)actor.CurrentHp)).WriteDword(0x1C, unchecked((uint)actor.MaximumHp)).WriteDword(0x7C, actor.NativeCombatantFlags).WriteByte(0xBB, checked((byte)NativeModelId(actor))).WriteByte(0xBC, checked((byte)actor.Level));
        if (actor.ElementDefenses.Count != 8)
        {
            throw new NotSupportedException("Native Scan requires all eight allocated element-defense WORDs.");
        }
        for (int element = 0; element < 8; element++)
        {
            row = row.WriteWord(0x44 + element * 2, checked((ushort)actor.ElementDefenses[element]));
        }
        return row.WriteByte(0xBD, checked((byte)EffectiveStat(actor, BattleStatId.Strength))).WriteByte(0xBE, checked((byte)EffectiveStat(actor, BattleStatId.Vitality))).WriteByte(0xBF, checked((byte)actor.Magic)).WriteByte(0xC0, checked((byte)EffectiveStat(actor, BattleStatId.Spirit))).WriteByte(0xC1, checked((byte)actor.Speed)).WriteByte(0xC3, checked((byte)actor.Evasion));
    }

    private static NativeBattleScanByteOwner ReadNativeScanExtra71(BattleCombatant actor)
    {
        NativeBattleScanByteOwner bytes = new(new byte[71].ToImmutableArray(), new bool[71].ToImmutableArray());
        if (actor.NativeDisplayNameIndex is not int selected)
        {
            return bytes.WriteByte(0, 0);
        }
        byte[] name = actor.NativeAi?.NameBuffers[selected] ?? throw new NotSupportedException("Native Scan extra text requires its actual AI name buffer.");
        return bytes.Write(0, name);
    }
}
