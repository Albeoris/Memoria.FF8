namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleElectrocute275Frame Frame, NativeBattleGraphicsState Graphics) PrepareNativeElectrocute275Bones(NativeBattleElectrocute275Frame frame, NativeBattleGraphicsState graphics, byte actor, bool compose)
    {
        (NativeBattleFileStaging staging, NativeBattleGraphicsState updated) = PrepareNativeLightningBones(frame.Staging ?? throw new NotSupportedException("Electrocute requires its addressed bone temporary memory."), graphics, actor, compose);
        return (frame with { Staging = staging }, updated);
    }

    private NativeBattleElectrocute275Frame ProjectNativeElectrocute275BoneTrail(NativeBattleElectrocute275Frame frame, NativeBattleGraphicsState graphics, byte actor, uint head, uint bankAddress, int bankLength)
    {
        NativeBattleEffectMemory memory = ProjectNativeLightningBoneTrail(frame.Memory, frame.Effect, frame.Staging ?? throw new NotSupportedException("Electrocute requires its current addressed memory."), graphics, actor, head, bankAddress, bankLength);
        return frame with { Memory = memory };
    }
}
