using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect209ResourcesReader
{
    public static NativeBattleEffect209Resources Read(byte[] executable, IReadOnlyDictionary<int, NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleStaticMeshResource mesh in meshes.Values)
        {
            fronts.Add(mesh.Address, mesh.Bytes);
        }
        fronts.Add(TimAddress, Bytes(TimAddress, TimLength));
        fronts.Add(CameraAddress, Bytes(CameraAddress, CameraLength));
        fronts.Add(VibrationAddress, Bytes(VibrationAddress, VibrationLength));
        ImmutableArray<NativeBattleStageVariant137Entry>.Builder variants = ImmutableArray.CreateBuilder<NativeBattleStageVariant137Entry>(10);
        for (int index = 0; index < 10; index++)
        {
            int fileId = U16(0xB8BB78 + (uint)(index * 2));
            uint rotation = U32(0xB8BB4C + (uint)((index + 1) * 4));
            variants.Add(new(fileId, checked((int)U32(0xB8BB8C + (uint)(index * 4))), U32(U32(0xB8BB00 + (uint)(index * 4))), U32(U32(0xB8BB24 + (uint)((index + 1) * 4))), rotation));
            if (rotation is not (0x1D99BE8 or 0x1D99BEA or 0x1D99BEC or 0x1D99BEE))
            {
                fronts.TryAdd(rotation, Bytes(rotation, 2));
            }
        }
        ImmutableArray<NativeBattleEffect209StageDepth> depth = Enumerable.Range(0, 4).Select(index => new NativeBattleEffect209StageDepth(unchecked((int)U32(StageDepthRowsAddress + (uint)(index * 8))), checked((byte)U32(StageDepthRowsAddress + (uint)(index * 8 + 4))))).ToImmutableArray();
        NativeBattleEffect209Resources result = new(new("effect209-tim", fronts[TimAddress]), new("effect209-camera", fronts[CameraAddress]), new("effect209-vibration", fronts[VibrationAddress]), meshes, Colors(InitialColorsAddress, 8), Colors(CycleColorsAddress, 24), Colors(ScrollColorsAddress, 18), depth, Colors(SoundAddress, 2), variants.ToImmutable(), files.ToImmutableDictionary(), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<uint> Colors(uint address, int count) => Enumerable.Range(0, count).Select(index => U32(address + (uint)(index * 4))).ToImmutableArray();
        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ushort U16(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect209Resources? nativeEffect209Resources;

    public NativeBattleEffect209Resources ReadNativeEffect209Resources()
    {
        if (nativeEffect209Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            NativeExecutableMemory memory = new(executable);
            using IFlArchive battle = OpenArchive("battle");
            Dictionary<int, NativeBattleEffectFileAsset> files = new();
            for (int index = 0; index < 10; index++)
            {
                ushort fileId = BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(0xB8BB78 + (uint)(index * 2), 2));
                uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCC + (uint)(fileId * 4), 4));
                byte[] bytes = memory.Read(pointer, 128);
                int length = bytes.AsSpan().IndexOf((byte)0);
                if (length < 1)
                {
                    throw new InvalidDataException("Effect209 stage file lacks its original directory name.");
                }
                string name = Encoding.ASCII.GetString(bytes.AsSpan(0, length));
                files.Add(fileId, new(checked((short)fileId), name, Read(battle, name).ToImmutableArray()));
            }
            nativeEffect209Resources = NativeBattleEffect209ResourcesReader.Read(executable, files) with
            {
                DecodeReloadStage = (fileId, bytes) => BattleResourceReader.ReadStage(fileId, bytes).NativeSource ?? throw new InvalidDataException("Effect209 stage reload requires its decoded current stage source.")
            };
            nativeEffect209Resources.Validate();
        }
        return nativeEffect209Resources;
    }
}
