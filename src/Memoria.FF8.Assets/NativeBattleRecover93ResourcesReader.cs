using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRecover93ResourcesReader
{
    public static NativeBattleRecover93Resources Read(byte[] executable)
    {
        return Read(executable, NativeBattleAngeloRestorationProfile.Recover93);
    }

    internal static NativeBattleRecover93Resources Read(byte[] executable, NativeBattleAngeloRestorationProfile profile)
    {
        if (profile.Effect is not (93 or 94))
        {
            throw new NotSupportedException("Native restoration resources require a proved physical profile.");
        }
        NativeExecutableMemory memory = new(executable);
        bool reverse = profile.Effect == 94;
        uint modelAddress = reverse ? 0x153407Cu : 0x1538B38u;
        uint animationAddress = reverse ? 0x1533E88u : 0x1538728u;
        uint timAddress = reverse ? 0x16ADE90u : 0x16B8990u;
        uint programAddress = reverse ? 0x16AD980u : 0x16B8480u;
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(memory, modelAddress, 0x4694);
        int[] sections = [0x18, 0x688, 0x3344, 0x4688, 0x4694];
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != 4)
        {
            throw new InvalidDataException("Native Recover model requires its original four-section source.");
        }
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(bytes.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Native Recover model section disagrees with its complete original extent.");
            }
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        NativeBattleEffectOwnedModelResource model = new(modelAddress, bytes, pose, geometry);
        NativeBattleGfAnimationSource program = new(programAddress, NativeBattleEffectStaticResourcesReader.Read(memory, programAddress, 0x510));
        if (U32(0) != 2 || U32(4) != 0x1B8 || U32(8) != 0x10 || U32(12) != 0xE4)
        {
            throw new InvalidDataException("Native Recover program requires its original bounded geometry and animation directories.");
        }
        foreach (int offset in new int[] { 0x10, 0xE4 })
        {
            if (U32(offset) != 0x54 || U32(offset + 4) != 9 || U32(offset + 8) != 1)
            {
                throw new InvalidDataException("Native Recover program requires its original12-byte single-shape geometry headers.");
            }
        }
        return new(new($"FF8_EN:{animationAddress:X}", NativeBattleEffectStaticResourcesReader.Read(memory, animationAddress, reverse ? 0x1F4 : 0x410)), model, new($"FF8_EN:{timAddress:X}", NativeBattleEffectStaticResourcesReader.Read(memory, timAddress, 0x8420)), Sound(0xB8B9A8), NativeBattleRecover93NodeResourcesReader.Read(executable, profile), program, Sound(reverse ? 0x1533E80u : 0x1538720u), Sound(reverse ? 0x1533E84u : 0x1538724u), Sound(reverse ? 0x16B62B0u : 0x16C0DB0u), Sound(reverse ? 0x16B62B4u : 0x16C0DB4u));

        uint Sound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(program.Data.AsSpan().Slice(offset));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRecover93Resources? nativeRecover93Resources;
    public NativeBattleRecover93Resources ReadNativeRecover93Resources() => nativeRecover93Resources ??= NativeBattleRecover93ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
