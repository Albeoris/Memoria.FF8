using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99CameraResources(ImmutableArray<NativeBattleMoogleCameraSource> Sources)
{
    public const uint StateAddress = 0x256D600;

    public void Validate()
    {
        uint[] addresses = [0x152AC48, 0x152AC90, 0x152ACD8, 0x152AD20, 0x152AD68, 0x152AE40, 0x152AE88];
        if (Sources.IsDefault || Sources.Length != addresses.Length)
        {
            throw new InvalidDataException("Native Choco99 camera requires its seven original sources.");
        }
        for (int index = 0; index < addresses.Length; index++)
        {
            NativeBattleMoogleCameraSource source = Sources[index];
            if (source.Address != addresses[index] || source.Data.IsDefault || source.Data.Length != 0x48 || BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != 0 || BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(0x24)) != (index == 3 ? 1 : 0))
            {
                throw new InvalidDataException("Native Choco99 camera source contradicts its actual address, mode or scalar integration owner.");
            }
        }
    }
}
