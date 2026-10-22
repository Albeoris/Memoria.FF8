using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEerieSoundWave130Resources(NativeBattleGfAnimationSource MainAct, NativeBattleGfAnimationSource TravelAct, NativeBattleGfAnimationSource ImpactAct, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        if (MainAct.Address != NativeBattleEerieSoundWave130Profile.MainActAddress || MainAct.Data.IsDefault || MainAct.Data.Length != 6000 || TravelAct.Address != NativeBattleEerieSoundWave130Profile.TravelActAddress || TravelAct.Data.IsDefault || TravelAct.Data.Length != 3940 || ImpactAct.Address != NativeBattleEerieSoundWave130Profile.ImpactActAddress || ImpactAct.Data.IsDefault || ImpactAct.Data.Length != 1960 || Camera.Identity != "FF8_EN:1223810" || Camera.Data.IsDefault || Camera.Data.Length != 160 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 160 || Sound != 0x64197)
        {
            throw new InvalidDataException("Eerie Sound Wave requires its complete original addressed ACT, camera and sound resources.");
        }
    }
}
