using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel112Resources(ImmutableArray<byte> StaticData, ImmutableArray<byte> ColdGlobals, ImmutableArray<NativeBattleSearchSpriteResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes)
{
    public const uint Address = 0x12C3618;
    public const uint EndAddress = 0x12DB750;
    public const uint GlobalsAddress = 0x2521918;
    public const int GlobalsLength = 4;

    public ImmutableArray<byte> Read(uint address, int length)
    {
        long offset = (long)address - Address;
        if (StaticData.IsDefault || StaticData.Length != EndAddress - Address || offset < 0 || length < 0 || offset > StaticData.Length - length)
        {
            throw new NotSupportedException("Native Duel resource read leaves its retained executable owner.");
        }
        return StaticData.AsSpan().Slice((int)offset, length).ToArray().ToImmutableArray();
    }

    public NativeBattleGfAnimationSource Hierarchy(uint address) => new(address, Read(address, checked((int)(EndAddress - address))));

    public ImmutableArray<byte> ReadActorDat()
    {
        ImmutableArray<byte> bytes = Read(Address, checked((int)(0x12C5650u - Address)));
        _ = NativeBattleDatDirectory.Section(bytes.AsSpan(), 2);
        return bytes;
    }

    public void Validate()
    {
        _ = Read(Address, checked((int)(EndAddress - Address)));
        if (ColdGlobals.IsDefault || ColdGlobals.Length != GlobalsLength || ColdGlobals.Any(value => value != 0) || Sprites.IsDefault || Sprites.Length != 2 || Meshes.IsDefault || Meshes.Length != 2)
        {
            throw new InvalidDataException("Native Duel resources require their original static image, loader globals and physical geometry directories.");
        }
    }
}
