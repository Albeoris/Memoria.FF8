using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBadBreath300Resources(NativeBattleGfAnimationSource Target, NativeBattleModuleResource ParticleSprite, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation CasterDat, ImmutableArray<byte> MeshAInitial, ImmutableArray<byte> MeshBInitial, ImmutableArray<short> TrigTable, ImmutableArray<uint> Gradient, ImmutableArray<byte> MouthAnchor, uint Sound)
{
    public void Validate()
    {
        if (Target.Address != 0x1042304 || Target.Data.IsDefault || Target.Data.Length != 0xC7C || Int(Target.Data, 0) != 1 || Int(Target.Data, 4) != 0x8F8 || Int(Target.Data, 0x8F8) != 3 || Int(Target.Data, 0x8FC) != 26)
        {
            throw new InvalidDataException("Bad Breath requires its original target ACT directory and programme.");
        }
        if (ParticleSprite.Address != 0x103F824 || ParticleSprite.Bytes.IsDefault || ParticleSprite.Bytes.Length != 480 || BinaryPrimitives.ReadUInt16LittleEndian(ParticleSprite.Bytes.AsSpan().Slice(8, 2)) != 18)
        {
            throw new InvalidDataException("Bad Breath requires its original particle frame directory.");
        }
        ParticleSprite.Validate();
        if (Camera.Identity != "FF8_EN:1042F80" || Camera.Data.IsDefault || Camera.Data.Length != 372 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6, 2)) != 372 || CasterDat.Identity != "FF8_EN:103DCB0" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74)
        {
            throw new InvalidDataException("Bad Breath requires its original camera and caster binding directory.");
        }
        uint[] directory = [3, 0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < directory.Length; index++)
        {
            if (unchecked((uint)Int(CasterDat.Data, index * 4)) != directory[index])
            {
                throw new InvalidDataException("Bad Breath caster binding directory is incomplete.");
            }
        }
        ValidateMesh(MeshAInitial);
        ValidateMesh(MeshBInitial);
        if (TrigTable.IsDefault || TrigTable.Length != 8192 || Gradient.IsDefault || Gradient.Length != 4 || MouthAnchor.IsDefault || MouthAnchor.Length != 8 || Sound != 0x55781)
        {
            throw new InvalidDataException("Bad Breath requires its original integer waveform, colour cycle, mouth selector and sound.");
        }
    }

    public static void ValidateMesh(ImmutableArray<byte> mesh)
    {
        if (mesh.IsDefault || mesh.Length != 5248 || Int(mesh, 0) != 1328 || Int(mesh, 4) != 165)
        {
            throw new InvalidDataException("Bad Breath requires its complete original mutable strip mesh.");
        }
    }
    private static int Int(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
}
