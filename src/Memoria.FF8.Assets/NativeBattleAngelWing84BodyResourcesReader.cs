using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleAngelWing84BodyResourcesReader
{
    public static NativeBattleAngelWing84BodyResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(memory, 0x1572BB0, 0x3230);
        int[] sections = [0x18, 0x448, 0x2268, 0x3230, 0x3230];
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != 4)
        {
            throw new InvalidDataException("Native Angel Wing body requires its original four-section model.");
        }
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(bytes.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Native Angel Wing model directory differs from its authenticated source extent.");
            }
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        NativeBattleEffectOwnedModelResource model = new(0x1572BB0, bytes, pose, geometry);
        ImmutableArray<byte> caster = NativeBattleEffectStaticResourcesReader.Read(memory, 0x1576020, 0x2410);
        if (BinaryPrimitives.ReadUInt32LittleEndian(caster.AsSpan()) != 3)
        {
            throw new InvalidDataException("Native Angel Wing caster requires its original three-section override.");
        }
        for (int index = 0; index < 4; index++)
        {
            _ = NativeBattleDatDirectory.Section(caster.AsSpan(), index);
        }
        ImmutableArray<ushort> bones = Enumerable.Range(0, 14).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(checked(0x1572B90u + (uint)index * 2), 2))).ToImmutableArray();
        uint Word(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        NativeBattleAngelWing84BodyResources resources = new(model, new("FF8_EN:1576020", caster), bones, Word(0xB8B9A8), Word(0x15720AC)) { Transition40 = Word(0xB8B7D8) };
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAngelWing84BodyResources? nativeAngelWing84BodyResources;
    public NativeBattleAngelWing84BodyResources ReadNativeAngelWing84BodyResources() => nativeAngelWing84BodyResources ??= NativeBattleAngelWing84BodyResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
