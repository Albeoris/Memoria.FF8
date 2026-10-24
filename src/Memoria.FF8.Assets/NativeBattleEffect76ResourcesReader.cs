using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect76ResourcesReader
{
    public static NativeBattleEffect76Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim, NativeBattleEffectFileAsset stageFile)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag075.tim\0")
        {
            throw new InvalidDataException("Effect76 initializer texture name changed.");
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(source => source.Address);
        Dictionary<uint, ImmutableArray<byte>> fronts = new();
        foreach ((uint address, int length) in StaticRanges.AddRange(new (uint Address, int Length)[] { (CameraAddress, CameraLength), (MusicAddress, MusicLength), (VibrationAddress, VibrationLength) }))
        {
            fronts.Add(address, Bytes(address, length));
        }
        foreach (NativeBattleStaticMeshResource mesh in meshes.Values)
        {
            fronts.Add(mesh.Address, mesh.Bytes);
        }
        NativeBattleEffect76Resources result = new(mainTim, new("effect76-camera", fronts[CameraAddress]), new("effect76-music", fronts[MusicAddress]), new("effect76-vibration", fronts[VibrationAddress]), BinaryPrimitives.ReadUInt32LittleEndian(fronts[SoundAddress].AsSpan()), meshes, fronts.ToImmutableDictionary(), stageFile);
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect76Resources? nativeEffect76Resources;

    public NativeBattleEffect76Resources ReadNativeEffect76Resources()
    {
        if (nativeEffect76Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            NativeExecutableMemory memory = new(executable);
            uint namePointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + StageFileId * 4), 4));
            byte[] nameBytes = memory.Read(namePointer, 128);
            int length = nameBytes.AsSpan().IndexOf((byte)0);
            if (length < 1 || Encoding.ASCII.GetString(nameBytes.AsSpan(0, length)) != "A0STG038.X")
            {
                throw new InvalidDataException("Effect76 stage input disagrees with its original file directory.");
            }
            using IFlArchive magic = OpenArchive("magic");
            using IFlArchive battle = OpenArchive("battle");
            NativeBattleEffectFileAsset stage = new(StageFileId, "A0STG038.X", Read(battle, "A0STG038.X").ToImmutableArray());
            nativeEffect76Resources = NativeBattleEffect76ResourcesReader.Read(executable, new("mag075.tim", Read(magic, "mag075.tim").ToImmutableArray()), stage) with
            {
                DecodeReloadStage = bytes => BattleResourceReader.ReadStage(StageFileId, bytes).NativeSource ?? throw new InvalidDataException("Effect76 stage reload requires its decoded current stage source.")
            };
            nativeEffect76Resources.Validate();
        }
        return nativeEffect76Resources;
    }
}
