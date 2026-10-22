using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBadBreath131Resources(NativeBattleGfAnimationSource Target, NativeBattleModuleResource ParticleSprite, NativeBattleEffectHeapAllocation Camera, ImmutableArray<byte> MeshAInitial, ImmutableArray<byte> MeshBInitial, ImmutableArray<short> TrigTable, ImmutableArray<uint> Gradient, uint ActorColor, uint Sound)
{
    public void Validate()
    {
        if (Target.Address != 0x121BB58 || Target.Data.IsDefault || Target.Data.Length != 0xC7C || Int(Target.Data, 0) != 1 || Int(Target.Data, 4) != 0x8F8 || Int(Target.Data, 0x8F8) != 3 || Int(Target.Data, 0x8FC) != 34)
        {
            throw new InvalidDataException("Bad Breath requires its original target ACT directory and programme.");
        }
        if (ParticleSprite.Address != 0x1219078 || ParticleSprite.Bytes.IsDefault || ParticleSprite.Bytes.Length != 480 || BinaryPrimitives.ReadUInt16LittleEndian(ParticleSprite.Bytes.AsSpan().Slice(8, 2)) != 18)
        {
            throw new InvalidDataException("Bad Breath requires its original particle frame directory.");
        }
        ParticleSprite.Validate();
        if (Camera.Identity != "FF8_EN:121C7D4" || Camera.Data.IsDefault || Camera.Data.Length != 420 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6, 2)) != 420)
        {
            throw new InvalidDataException("Bad Breath requires its original camera programme.");
        }

        ValidateMesh(MeshAInitial);
        ValidateMesh(MeshBInitial);
        if (TrigTable.IsDefault || TrigTable.Length != 8192 || Gradient.IsDefault || Gradient.Length != 4 || Sound != 0x64194)
        {
            throw new InvalidDataException("Bad Breath requires its original integer waveform, colour cycle and sound.");
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
