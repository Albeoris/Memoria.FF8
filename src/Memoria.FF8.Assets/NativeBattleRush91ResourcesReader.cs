using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRush91ResourcesReader
{
    public static NativeBattleRush91Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> bytes = Read(0x1543880, 0x38B4);
        int[] sections = [0x18, 0x688, 0x3344, 0x38A8, 0x38B4];
        if (U32(bytes, 0) != 4)
        {
            throw new InvalidDataException("Native Rush model requires its original four-section source.");
        }
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(bytes.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Native Rush model section disagrees with its complete original extent.");
            }
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        NativeBattleEffectOwnedModelResource model = new(0x1543880, bytes, pose, geometry);
        NativeBattleSearchSpriteResource sprite = new(0x15430E8, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x15430E8));
        if (sprite.Bytes.Length != 0x144 || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != 12)
        {
            throw new InvalidDataException("Native Rush projected sprite requires its complete twelve-frame source.");
        }
        NativeBattleStaticMeshResource particle = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x154322Cu);
        if (particle.Bytes.Length != 0x60)
        {
            throw new InvalidDataException("Native Rush particle mesh disagrees with its original extent.");
        }
        ImmutableArray<byte> nodes = Read(0x16CB3FC, 0xEF0);
        if (U32(nodes, 0) != 0)
        {
            throw new InvalidDataException("Native Rush neutral resource requires its cold, unfixed source.");
        }
        for (int index = 0; index < 16; index++)
        {
            uint definition = index switch { 0 => 0xAE8, 1 => 0xC40, 2 => 0xD98, _ => 0 };
            uint spriteOffset = index == 0 ? 0x104u : 0;
            uint meshOffset = index switch { 0 => 0x298, 2 => 0x368, _ => 0 };
            if (U32(nodes, 4 + index * 4) != definition || U32(nodes, 0x44 + index * 4) != spriteOffset || U32(nodes, 0x84 + index * 4) != meshOffset || U32(nodes, 0xC4 + index * 4) != 0)
            {
                throw new InvalidDataException("Native Rush neutral resource requires its complete original four directories.");
            }
        }
        NativeBattleGfAnimationSource program = new(0x16CC2EC, Read(0x16CC2EC, 0x35C4));
        if (U32(program.Data, 0) != 2 || U32(program.Data, 4) != 0x33F8 || U32(program.Data, 8) != 0x10 || U32(program.Data, 12) != 0x22EC)
        {
            throw new InvalidDataException("Native Rush program requires its complete original geometry and animation directories.");
        }
        CheckGeometry(0x10, 0x10EC, 180);
        CheckGeometry(0x22EC, 0x96C, 100);
        return new(Allocation(0x154328C, 0x5F4), model, Allocation(0x16CF8B0, 0x8420), Sound(0xB8B9A8), sprite, particle, new("FF8_EN:16CB3FC", nodes), program, Allocation(0x1547134, 0x10), NativeBattleRushOverlayProgramReader.Read(executable), Sound(0x15430E4));

        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        NativeBattleEffectHeapAllocation Allocation(uint address, int length) => new($"FF8_EN:{address:X}", Read(address, length));
        uint Sound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        void CheckGeometry(int offset, uint directory, uint vertices)
        {
            if (U32(program.Data, offset) != directory || U32(program.Data, offset + 4) != vertices || U32(program.Data, offset + 8) != 3)
            {
                throw new InvalidDataException("Native Rush program requires its original twelve-byte three-shape geometry header.");
            }
        }
    }

    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleRush91Resources? nativeRush91Resources;
    public NativeBattleRush91Resources ReadNativeRush91Resources() => nativeRush91Resources ??= NativeBattleRush91ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
