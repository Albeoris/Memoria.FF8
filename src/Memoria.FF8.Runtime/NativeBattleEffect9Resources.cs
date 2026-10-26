using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect9Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleGfAnimationSource Primary, NativeBattleGfAnimationSource Secondary, NativeBattleModuleResource Particle, uint Sound, ImmutableArray<byte> BirthSchedule, NativeBattleFullscreenOverlayProgram Overlay)
{
    public void Validate()
    {
        if (!string.Equals(MainTim.Name, "mag008.tim", StringComparison.OrdinalIgnoreCase) || MainTim.Data.IsDefault || MainTim.Data.Length != 168480 || Camera.Data.IsDefault || Camera.Data.Length != 134 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 12)
        {
            throw new InvalidDataException("Effect9 requires its original TIM and bounded camera registration source.");
        }
        if (Primary.Address != 0x18155F0 || Primary.Data.IsDefault || Primary.Data.Length != 10488 || Secondary.Address != 0x1817EE8 || Secondary.Data.IsDefault || Secondary.Data.Length != 3216)
        {
            throw new InvalidDataException("Effect9 requires its two original Gf programmes.");
        }
        if (Particle.Address != 0x1634B84 || Particle.Bytes.IsDefault || Particle.Bytes.Length != 300 || BirthSchedule.IsDefaultOrEmpty || BirthSchedule.Length != 11 || BirthSchedule[^1] != 255 || BirthSchedule.Take(10).Any(value => value != 1) || Overlay.Address != 0x1634F00 || Overlay.Commands.IsDefault || Overlay.Commands.Length != 4 || Overlay.Commands[^1] != 0xFF000000)
        {
            throw new InvalidDataException("Effect9 requires its current particle and overlay programmes.");
        }
        for (int phase = 0; phase < 11; phase++)
        {
            _ = Particle.ProjectedFrame571C80(phase);
        }
    }

    public NativeBattleGfAnimationSource ReadSource(uint address)
    {
        return address switch
        {
            0x18155F0 => Primary,
            0x1817EE8 => Secondary,
            _ => throw new NotSupportedException("Effect9 Gf task selected an unsupported source.")
        };
    }
}
