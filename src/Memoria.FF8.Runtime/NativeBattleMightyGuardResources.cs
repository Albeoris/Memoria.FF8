using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMightyGuardResources(ushort Effect, NativeBattleEffectHeapAllocation Animation, uint Sound, NativeBattleCuragaExecutableSource NodeSource, ImmutableArray<byte>? CasterDat)
{
    public void Validate()
    {
        NativeBattleMightyGuardProfile profile = NativeBattleMightyGuardProfile.ForEffect(Effect);
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != profile.CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != profile.CameraLength || Sound != profile.Sound || NodeSource is null || NodeSource.ResourceAddress != profile.SourceAddress || NodeSource.Data.IsDefault || NodeSource.Data.Length != profile.SourceLength || BinaryPrimitives.ReadUInt32LittleEndian(NodeSource.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("Mighty Guard requires its own original camera, sound and uninitialized source bank.");
        }
        if (Effect == 73)
        {
            if (CasterDat is not { IsDefault: false, Length: 0x1B74 } dat || BinaryPrimitives.ReadUInt32LittleEndian(dat.AsSpan()) != 3)
            {
                throw new InvalidDataException("Blue Magic Mighty Guard requires its complete original caster resource.");
            }
            uint[] offsets = [0x14, 0x1648, 0x1698, 0x1B74];
            for (int index = 0; index < offsets.Length; index++)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(dat.AsSpan().Slice(4 + index * 4)) != offsets[index])
                {
                    throw new InvalidDataException("Blue Magic Mighty Guard's caster resource has an invalid section directory.");
                }
            }
        }
        else if (CasterDat is not null)
        {
            throw new InvalidDataException("Enemy Mighty Guard has no embedded caster binding.");
        }
    }
}
