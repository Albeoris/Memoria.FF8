using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99CameraYProgram(uint Address, ImmutableArray<short> Values);
public sealed record NativeBattleChoco99MeteoResources(NativeBattleChoco97SpriteResources Meteo, ImmutableArray<NativeBattleChoco97SpriteResources> Secondary, NativeBattleChoco99CameraYProgram CameraY, NativeBattleFullscreenOverlayProgram Overlay)
{
    public void Validate()
    {
        Require(Meteo, 0x152A460, 0x2AC, 15);
        uint[] addresses = [0x152A70C, 0x152A7D0, 0x152A894];
        if (Secondary.IsDefault || Secondary.Length != addresses.Length || CameraY.Address != 0x152AEF0 || CameraY.Values.IsDefault || CameraY.Values.Length != 9 || Overlay.Address != 0x152AF04 || Overlay.Commands.IsDefault || Overlay.Commands.Length != 4)
        {
            throw new InvalidDataException("Native Choco99 meteo resources require their exact bounded external owners.");
        }
        for (int index = 0; index < addresses.Length; index++)
        {
            Require(Secondary[index], addresses[index], 0xC4, 7);
        }
    }

    private static void Require(NativeBattleChoco97SpriteResources sprite, uint address, int bytes, ushort frames)
    {
        if (sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != bytes || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Native Choco99 meteo sprite contradicts its actual projected resource extent.");
        }
    }
}
