using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco98CameraResources(ImmutableArray<NativeBattleMoogleCameraSource> Sources, NativeBattleMoogleCameraCurve Curve)
{
    public const uint StateAddress = 0x2574C48;

    public void Validate()
    {
        uint[] addresses = [0x152B3D0, 0x152B418, 0x152B460, 0x152B4F0, 0x152B538];
        if (Sources.IsDefault || Sources.Length != addresses.Length || Curve.Address != 0x152B4A8 || Curve.Values.IsDefault || Curve.Values.Length != 36 || Curve.Values[0] != 0 || Curve.Values[^1] != 4096 || Curve.Values.Take(35).Any(value => value >= 4096))
        {
            throw new InvalidDataException("Native Choco camera requires its five original sources and bounded interpolation curve.");
        }
        for (int index = 0; index < Sources.Length; index++)
        {
            NativeBattleMoogleCameraSource source = Sources[index];
            if (source.Address != addresses[index] || source.Data.IsDefault || source.Data.Length != 0x48 || BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(0x24)) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != (index < 3 ? 0u : Curve.Address))
            {
                throw new InvalidDataException("Native Choco camera source disagrees with its actual mode and curve owner.");
            }
        }
    }
}
