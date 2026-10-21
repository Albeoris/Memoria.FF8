using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDevour62SoundSource(uint Address, uint Value);

public sealed record NativeBattleDevour62Resources(NativeBattleEffectHeapAllocation Animation, ImmutableArray<NativeBattleDevour62SoundSource> SoundSources)
{
    public void Validate()
    {
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != 1968 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != 1968 || SoundSources.IsDefault || SoundSources.Length != 9)
        {
            throw new InvalidDataException("Devour requires its complete original camera and nine sound sources.");
        }
        uint[] values = [0x494CA, 0x494BD, 0x494B7, 0x494BF, 0x494B1, 0x494AE, 0x494B4, 0x494BA, 0x494DF];
        for (int index = 0; index < SoundSources.Length; index++)
        {
            if (SoundSources[index] is null || SoundSources[index].Address != 0x1596290u + (uint)index * 4 || SoundSources[index].Value != values[index])
            {
                throw new InvalidDataException("Devour sound directory must retain its original descriptor pointers.");
            }
        }
    }

    public NativeBattleModuleSound Sound(int index)
    {
        if ((uint)index >= 9 || SoundSources.IsDefault || SoundSources.Length != 9)
        {
            throw new InvalidDataException("Devour sound selection requires its clamped original outcome index.");
        }
        return new(SoundSources[index].Value, 0, null) { Pan = 128, RawPan = 128 };
    }
}
