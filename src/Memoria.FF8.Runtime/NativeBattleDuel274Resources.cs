using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel274Resources(ImmutableArray<byte> StaticData, ImmutableArray<NativeBattleSearchSpriteResource> Sprites)
{
    public const uint Address = 0x10E2998;
    public const uint EndAddress = 0x110E5D0;
    public ImmutableArray<NativeBattleStaticMeshResource> Meshes { get; init; } = [];
    public ImmutableArray<NativeBattleEffectFileAsset> Files { get; init; } = [];

    public ImmutableArray<byte> ReadCameraBank()
    {
        ushort offset = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(Read(0x110BE74, 2).AsSpan());
        uint address = checked(0x110BE70u + offset);
        return Read(address, checked((int)(EndAddress - address)));
    }

    public ImmutableArray<byte> Read(uint address, int length)
    {
        long offset = (long)address - Address;
        if (StaticData.IsDefault || StaticData.Length != EndAddress - Address || offset < 0 || length < 0 || offset > StaticData.Length - length)
        {
            throw new NotSupportedException("Native Duel opening read leaves its retained executable resource owner.");
        }
        return StaticData.AsSpan().Slice((int)offset, length).ToArray().ToImmutableArray();
    }

    public NativeBattleGfAnimationSource Hierarchy(uint address) => new(address, Read(address, checked((int)(EndAddress - address))));

    public ImmutableArray<byte> ReadActorDat()
    {
        ImmutableArray<byte> bytes = Read(Address, 0x4DF4);
        _ = NativeBattleDatDirectory.Section(bytes.AsSpan(), 2);
        return bytes;
    }

    public void Validate()
    {
        _ = Read(Address, checked((int)(EndAddress - Address)));
        if (Sprites.IsDefault || Sprites.Length != 6 || Sprites.Select(sprite => sprite.Address).Distinct().Count() != 6 || Meshes.IsDefault || Meshes.Length != 3 || Files.IsDefault || Files.Length != 2 || Files[0].FileId != 715 || Files[1].FileId != 716)
        {
            throw new InvalidDataException("Native Duel opening requires its six original projected sprite resources and three physical meshes.");
        }
    }
}
