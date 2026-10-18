using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco100CameraResources(ImmutableArray<NativeBattleMoogleCameraSource> Sources)
{
    public const uint StateAddress = 0x25652E8;

    public void Validate()
    {
        if (Sources.IsDefault || Sources.Length != 8)
        {
            throw new InvalidDataException("Native Choco100 camera requires its eight actual source rows.");
        }
        for (int index = 0; index < Sources.Length; index++)
        {
            NativeBattleMoogleCameraSource source = Sources[index];
            if (source.Address != 0x1529CD8u + (uint)(index * 0x48) || source.Data.IsDefault || source.Data.Length != 0x48 || BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != 0 || BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(0x24)) != 0)
            {
                throw new InvalidDataException("Native Choco100 camera source contradicts its actual scalar mode0 profile.");
            }
        }
    }
}
