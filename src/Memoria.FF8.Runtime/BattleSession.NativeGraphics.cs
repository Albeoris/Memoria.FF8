namespace Memoria.FF8.Runtime;

/// <summary>Executable-owned48D200 command252/id1 packet;MagicId is opcode30's BYTE operand, not a kernel action index.</summary>
public sealed record BattleNativeGraphicCommand(byte MagicId);

public sealed partial class BattleSession
{

    private byte NativeGraphicsTargetFlags()
    {
        return (byte)(FindInstalledNativeAction(8, 1).AttackFlags & 0x80);
    }

}
