using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect101Resources(NativeBattleGfAnimationSource MainAct, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        if (MainAct.Address != NativeBattleEffect101Profile.MainActAddress || MainAct.Data.IsDefault || MainAct.Data.Length != 0x2CD4 || Camera.Identity != "FF8_EN:13248C0" || Camera.Data.IsDefault || Camera.Data.Length != 0x11C || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0x11C || Sound != 0x64218)
        {
            throw new InvalidDataException("Effect101 requires its complete original addressed ACT, camera and sound.");
        }
    }
    public NativeBattleGfAnimationSource Act(uint address)
    {
        Validate();
        return address == MainAct.Address ? MainAct : throw new NotSupportedException("Effect101 selected an unsupported ACT source.");
    }
}
