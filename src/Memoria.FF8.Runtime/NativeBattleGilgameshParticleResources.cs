using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshParticleResources(NativeBattleModuleResource SourceA, NativeBattleModuleResource SourceB, NativeBattleModuleResource Sparks, NativeBattleModuleResource Streak)
{
    public void Validate()
    {
        ValidateSprite(SourceA, 9, 16, 0x224);
        ValidateSprite(SourceB, 9, 16, 0x210);
        ValidateSprite(Sparks, 6, 16, 0x1AC);
        ValidateSprite(Streak, 6, 1, 0x28);
    }

    private static void ValidateSprite(NativeBattleModuleResource resource, byte kind, int frames, int extent)
    {
        if (resource is null || resource.Bytes.IsDefault || resource.Bytes.Length != extent || resource.Kind != kind || BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Native Gilgamesh particles require their complete original projected sprite.");
        }
        for (int phase = 0; phase < frames; phase++)
        {
            _ = resource.ProjectedFrame571C80(phase);
        }
    }
}

