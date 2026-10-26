using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect30Scatter(short Center, short Span, short Count);

public sealed record NativeBattleEffect30Resources(NativeBattleEffectHeapAllocation Camera, NativeBattleCuragaExecutableSource Executable, ImmutableDictionary<uint, ImmutableArray<byte>> Meshes, NativeBattleModuleResource ParticleSprite, ImmutableArray<NativeBattleEffect30Scatter> Scatter, uint Sound)
{
    public void Validate()
    {
        if (Camera is null || Camera.Data.IsDefault || Camera.Data.Length != NativeBattleEffect30Profile.CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan()) != 2 || Executable is null || Executable.ResourceAddress != NativeBattleEffect30Profile.SourceAddress || Executable.Data.IsDefault || Executable.Data.Length != NativeBattleEffect30Profile.SourceLength || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("Effect30 requires its original embedded camera and uninitialized addressed node bank.");
        }
        if (Meshes is null || Meshes.Count != 3 || Scatter.IsDefault || Scatter.Length != 19 || ParticleSprite is null || ParticleSprite.Address != 0x160DE10 || ParticleSprite.Bytes.IsDefault || ParticleSprite.Bytes.Length != 196 || BinaryPrimitives.ReadUInt16LittleEndian(ParticleSprite.Bytes.AsSpan().Slice(8)) != 7)
        {
            throw new InvalidDataException("Effect30 requires its three addressed beam meshes and nineteen scatter rows.");
        }
        foreach (uint address in new uint[] { 0x160D538, 0x160DCF8, 0x160DC50 })
        {
            if (!Meshes.TryGetValue(address, out ImmutableArray<byte> mesh) || mesh.IsDefault || mesh.Length < 8)
            {
                throw new InvalidDataException("Effect30 is missing a selected addressed beam mesh.");
            }
            uint groups = BinaryPrimitives.ReadUInt32LittleEndian(mesh.AsSpan());
            uint vertices = BinaryPrimitives.ReadUInt32LittleEndian(mesh.AsSpan().Slice(4));
            if (groups < 8 || groups > mesh.Length - 4 || vertices > (mesh.Length - 8) / 8)
            {
                throw new InvalidDataException("Effect30 beam mesh directories leave their original resource.");
            }
        }
    }
}
