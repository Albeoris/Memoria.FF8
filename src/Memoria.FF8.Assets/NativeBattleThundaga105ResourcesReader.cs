using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleThundaga105ResourcesReader
{
    public static NativeBattleThundaga105Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleThundagaFragmentPair>.Builder pairs = ImmutableArray.CreateBuilder<NativeBattleThundagaFragmentPair>();
        uint address = 0x130E55C;
        for (int index = 0; index < 14; index++)
        {
            NativeBattleStaticMeshResource mesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address);
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
            uint referenceAddress = checked(address + (uint)mesh.Bytes.Length);
            int referenceLength = checked(8 + count * 8);
            pairs.Add(new(mesh, new(referenceAddress, NativeBattleEffectStaticResourcesReader.Read(memory, referenceAddress, referenceLength))));
            address = checked(referenceAddress + (uint)referenceLength);
        }
        if (address != 0x131136C)
        {
            throw new InvalidDataException("Native Thundaga requires all fourteen complete original mesh and reference pairs.");
        }
        NativeBattleModuleResource stationary = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x131136Cu);
        NativeBattleModuleResource moving = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x13115DCu);
        if (stationary.Bytes.Length != 0x270 || moving.Bytes.Length != 0x144)
        {
            throw new InvalidDataException("Native Thundaga sprite leaves its original complete frame resource.");
        }
        return new(Allocation(0x1311720, 0xF3C), new(0x13009C0, NativeBattleEffectStaticResourcesReader.Read(memory, 0x13009C0, 0xDB9C)), stationary, moving, Allocation(0x131265C, 4), Allocation(0x1312660, 0x20), pairs.ToImmutable());

        NativeBattleEffectHeapAllocation Allocation(uint location, int length) => new($"FF8_EN:{location:X}", NativeBattleEffectStaticResourcesReader.Read(memory, location, length));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleThundaga105Resources? nativeThundaga105Resources;
    public NativeBattleThundaga105Resources ReadNativeThundaga105Resources() => nativeThundaga105Resources ??= NativeBattleThundaga105ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
