using System.Buffers.Binary;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattlePandemona291ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return Enumerable.Range(719, 8).Concat(new[] { 360, 361, 362 }).Select(id =>
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)id * 4), 4));
            byte[] bytes = memory.Read(address, 128);
            int end = bytes.AsSpan().IndexOf((byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Native Pandemona requires its original terminated archive names.");
            }
            return new NativeBattleEffectFileAsset(checked((short)id), Encoding.ASCII.GetString(bytes.AsSpan(0, end)), []);
        }).ToImmutableArray();
    }

    public static NativeBattlePandemona291Resources Read(byte[] executable) => Read(new NativeExecutableMemory(executable));

    public static NativeBattlePandemona291Resources Read(IGameNativeMemorySource memory)
    {
        NativeBattlePandemona291Resources resources = new(new("pandemona-first-tim", memory.Read(0x13BBBEC, 0x31620).ToImmutableArray()), new("pandemona-second-tim", memory.Read(0x13ED20C, 0x2220).ToImmutableArray()));
        uint[] addresses = [0x13A0E58, 0x13A4F88, 0x13A6508, 0x13A8508, 0x13AB5C0, 0x13AC1B8, 0x13ACDB0];
        int[] lengths = [0xD08, 0x618, 0x918, 0x938, 0x3F8, 0x3F8, 0x668];
        ImmutableArray<NativeBattlePandemona291Animation> animations = addresses.Select((address, index) => new NativeBattlePandemona291Animation(address, memory.Read(address, lengths[index]).ToImmutableArray())).ToImmutableArray();
        ImmutableArray<byte> vibrationBytes = memory.Read(0x13B7A5C, 0x14E).ToImmutableArray();
        ImmutableArray<int> offsets = Enumerable.Range(0, 64).Select(index => System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan().Slice(index * 4, 4))).ToImmutableArray();
        NativeVibrationBank vibration = new(memory.SourceHash, offsets, vibrationBytes);
        vibration.Validate();
        uint[] trailAddresses = [0x139FAF8, 0x139FC30, 0x139FD68, 0x139FEA0];
        ImmutableArray<NativeBattleStaticMeshResource> trails = trailAddresses.Select((address, index) => new NativeBattleStaticMeshResource((byte)index, address, memory.Read(address, 0x134).ToImmutableArray())).ToImmutableArray();
        resources = resources with { AnimationSources = animations, Vibration = vibration, OpeningSound = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x13B7A48, 4)), SurfaceProgram = new(0x13AFF48, memory.Read(0x13AFF48, 0xD54).ToImmutableArray()), TrailModels = trails, TrailTrigonometry = memory.Read(0x13B7BB8, 0x4000).ToImmutableArray(), SurfaceTextureKinds = memory.Read(0x13EF440, 1).ToImmutableArray() };
        uint[] layerAddresses = [0x13A0E58, 0x13A4F88, 0x13A6508];
        int[] layerLengths = [0x3428, 0xF68, 0x16E8];
        uint[] targetAddresses = [0x13A4280, 0x13A5EF0, 0x13A7BF0];
        int[] targetLengths = [0xD08, 0x618, 0x918];
        resources = resources with { LayerGeometry = layerAddresses.Select((address, index) => new NativeBattleStaticMeshResource((byte)index, address, memory.Read(address, layerLengths[index]).ToImmutableArray())).ToImmutableArray(), LayerTargets = targetAddresses.Select((address, index) => new NativeBattleStaticMeshResource((byte)index, address, memory.Read(address, targetLengths[index]).ToImmutableArray())).ToImmutableArray(), LayerSprite = new(0x13AFCC0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x13AFCC0)) };
        resources = resources with { VortexFloorGeometry = new(0, 0x139FFE8, memory.Read(0x139FFE8, 0x7A8).ToImmutableArray()), VortexFloorTarget = memory.Read(0x13A0790, 0x1E8).ToImmutableArray() };
        resources = resources with { VortexOverlayGeometry = new(0, 0x13A0978, memory.Read(0x13A0978, 0x4E0).ToImmutableArray()) };
        resources = resources with { BodySound = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x13B7A4C, 4)) };
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattlePandemona291Resources? nativePandemona291Resources;

    public NativeBattlePandemona291Resources ReadNativePandemona291Resources()
    {
        if (nativePandemona291Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattlePandemona291ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativePandemona291Resources = NativeBattlePandemona291ResourcesReader.Read(executable) with { Files = files };
        }
        return nativePandemona291Resources;
    }
}
