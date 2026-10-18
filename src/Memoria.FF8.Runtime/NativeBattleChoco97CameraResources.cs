using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97CameraResources(ImmutableArray<NativeBattleMoogleCameraSource> Sources)
{
    public const uint StateAddress = 0x257AEC8;

    public void Validate()
    {
        if (Sources.IsDefault || Sources.Length != 4)
        {
            throw new InvalidDataException("Native Choco camera requires its four original source rows.");
        }
        for (int index = 0; index < Sources.Length; index++)
        {
            NativeBattleMoogleCameraSource source = Sources[index];
            if (source.Address != 0x152BA38u + (uint)(index * 0x48) || source.Data.IsDefault || source.Data.Length != 0x48)
            {
                throw new InvalidDataException("Native Choco camera source leaves its original four-row directory.");
            }
            if (BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != 0 || BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(0x24)) != 0)
            {
                throw new NotSupportedException("Native Choco camera requires its reached curve-free anchor-mode-zero sources.");
            }
        }
    }
}
