using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99ParticleResources(NativeBattleChoco97SpriteResources Trail, NativeBattleChoco97SpriteResources Radial)
{
    public void Validate()
    {
        Require(Trail, 0x152A958);
        Require(Radial, 0x152A31C);
    }

    private static void Require(NativeBattleChoco97SpriteResources resource, uint address)
    {
        if (resource.Address != address || resource.Bytes.IsDefault || resource.Bytes.Length != 0x144 || BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(8)) != 12)
        {
            throw new InvalidDataException("Native Choco99 particle requires its actual bounded twelve-frame projected sprite.");
        }
    }
}
