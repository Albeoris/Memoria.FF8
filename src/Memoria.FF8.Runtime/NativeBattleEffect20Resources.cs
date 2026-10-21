using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect20Resources(NativeBattleEffectHeapAllocation Animation, uint Sound, NativeBattleFullscreenOverlayProgram Overlay, ImmutableArray<short> EmissionAges, ImmutableArray<NativeBattleVector> EmissionOffsets, NativeBattleMoogle96VibrationResource Vibration, NativeBattleStaticMeshResource Mesh, NativeBattleModuleResource Sprite)
{
    public void Validate()
    {
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != 180 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != 180 || Overlay is null || Overlay.Address != 0x1629F14 || !Overlay.Commands.SequenceEqual(new uint[] { 0xC0C0C0, 0x808080, 0x404040, 0xFF000000 }) || EmissionAges.IsDefault || EmissionAges.Length != 10 || EmissionAges[^1] != short.MaxValue || EmissionOffsets.IsDefault || EmissionOffsets.Length != 12)
        {
            throw new InvalidDataException("Effect20 requires its complete original camera, overlay and emission tables.");
        }
        if (Mesh is null || Mesh.Address != 0x1629E00 || Mesh.Bytes.IsDefault || Mesh.Bytes.Length != 96 || BinaryPrimitives.ReadUInt32LittleEndian(Mesh.Bytes.AsSpan()) != 40 || BinaryPrimitives.ReadUInt32LittleEndian(Mesh.Bytes.AsSpan().Slice(4)) != 4 || Sprite is null || Sprite.Address != 0x1629C54 || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != 428 || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 16)
        {
            throw new InvalidDataException("Effect20 requires its original four-vertex mesh and sixteen-frame sprite.");
        }
        for (int frame = 0; frame < 16; frame++)
        {
            _ = Sprite.ProjectedFrame571C80(frame);
        }
        if (Vibration is null || Vibration.Address != 0x17F36A0 || Vibration.Bank is null)
        {
            throw new InvalidDataException("Effect20 requires its original vibration resource owner.");
        }
        Vibration.Bank.Validate();
        if (!Vibration.Bank.Offsets.SequenceEqual(new[] { 0, 0x100 }) || Vibration.Bank.Bytes.Length != 0x11A || Vibration.Bank.ReadWord(0x100) != 20 || Vibration.Bank.ReadWord(0x102) != 0)
        {
            throw new InvalidDataException("Effect20 requires its complete reached vibration pattern and actual interpolation lookahead.");
        }
    }
}
