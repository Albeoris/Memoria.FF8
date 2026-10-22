using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHypnotize133Resources(NativeBattleEffectHeapAllocation Camera, NativeBattleGfAnimationSource MainAct, ImmutableArray<NativeBattleGfAnimationSource> AnchoredActs, ImmutableArray<NativeBattleGfAnimationSource> FadingActs, ImmutableArray<NativeBattleModuleAnchor> AnchoredOffsets, NativeBattleModuleResource Sprite, uint Sound)
{
    public void Validate()
    {
        if (Camera.Identity != "FF8_EN:1212A30" || Camera.Data.IsDefault || Camera.Data.Length != 0xAC || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0xAC || Sound != 0x64286 || AnchoredActs.IsDefault || AnchoredActs.Length != 8 || FadingActs.IsDefault || FadingActs.Length != 8 || AnchoredOffsets.IsDefault || AnchoredOffsets.Length != 8 || AnchoredOffsets.Any(anchor => anchor is null))
        {
            throw new InvalidDataException("Hypnotize requires its complete original camera, sound and addressed ACT tables.");
        }
        RequireAct(MainAct, 0x1207958, 0x12D8);
        for (int index = 0; index < 8; index++)
        {
            RequireAct(AnchoredActs[index], 0x1208C30u + (uint)(index * 0xCB4), 0xCB4);
            RequireAct(FadingActs[index], 0x120F1D0u + (uint)(index * 0x70C), 0x70C);
        }
        if (Sprite.Address != 0x12077B8 || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != 0x1A0 || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 14)
        {
            throw new InvalidDataException("Hypnotize requires its complete original projected sprite.");
        }
        for (int frame = 0; frame < 14; frame++)
        {
            _ = Sprite.ProjectedFrame571C80(frame);
        }
    }
    public NativeBattleGfAnimationSource Act(uint address)
    {
        Validate();
        return address == MainAct.Address ? MainAct : AnchoredActs.Concat(FadingActs).SingleOrDefault(source => source.Address == address) ?? throw new NotSupportedException("Hypnotize selected an unsupported ACT source.");
    }
    private static void RequireAct(NativeBattleGfAnimationSource source, uint address, int length)
    {
        if (source.Address != address || source.Data.IsDefault || source.Data.Length != length)
        {
            throw new InvalidDataException("Hypnotize requires its complete original ACT source.");
        }
    }
}
