using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePetrifyStare127Resources(NativeBattleGfAnimationSource FirstAct, NativeBattleGfAnimationSource SecondAct, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        if (FirstAct.Address != NativeBattlePetrifyStare127Profile.FirstActAddress || FirstAct.Data.IsDefault || FirstAct.Data.Length != 0x44C8 || SecondAct.Address != NativeBattlePetrifyStare127Profile.SecondActAddress || SecondAct.Data.IsDefault || SecondAct.Data.Length != 0x5828 || Camera.Identity != "FF8_EN:12414AC" || Camera.Data.IsDefault || Camera.Data.Length != 0xEC || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0xEC || Sound != 0x61AC6)
        {
            throw new InvalidDataException("Petrify Stare requires its complete original addressed ACT, camera and sound resources.");
        }
    }
}
