using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect65ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return FileIds.Select(id =>
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)id * 4), 4));
            byte[] bytes = memory.Read(pointer, 128);
            int end = bytes.AsSpan().IndexOf((byte)0);
            string expected = id <= 362 ? $"MA8DEF_{id - 360}.TIM" : $"MAG064_H.0{id - 391}";
            if (end < 1 || Encoding.ASCII.GetString(bytes.AsSpan(0, end)) != expected)
            {
                throw new InvalidDataException("Effect65 streamed-file directory changed.");
            }
            return new NativeBattleEffectFileAsset(id, expected, []);
        }).ToImmutableArray();
    }

    public static NativeBattleEffect65Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag064.tim\0")
        {
            throw new InvalidDataException("Effect65 initializer texture name changed.");
        }
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != names.Length || files.Where((file, index) => file.FileId != names[index].FileId || file.Name != names[index].Name).Any())
        {
            throw new InvalidDataException("Effect65 inputs disagree with their native streamed-file directory.");
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(mesh => mesh.Address);
        Dictionary<uint, ImmutableArray<byte>> fronts = new();
        fronts.Add(VibrationAddress, Bytes(VibrationAddress, VibrationLength));
        fronts.Add(SpriteAddress, Bytes(SpriteAddress, SpriteLength));
        fronts.Add(0x133AB6C, Bytes(0x133AB6C, 24));
        fronts.Add(ScaleAddress, Bytes(ScaleAddress, 12));
        fronts.Add(0x133AB98, Bytes(0x133AB98, 16));
        fronts.Add(0x133ABB0, Bytes(0x133ABB0, 16));
        fronts.Add(0x1342D2C, Bytes(0x1342D2C, 20));
        fronts.Add(DistanceAddress, Bytes(DistanceAddress, 16384));
        fronts.Add(TrigonometryAddress, Bytes(TrigonometryAddress, 16384));
        foreach (NativeBattleStaticMeshResource mesh in meshes.Values)
        {
            fronts.Add(mesh.Address, mesh.Bytes);
        }
        for (int index = 0; index < 4; index++)
        {
            uint orbit = checked(0x133AC40u + (uint)(index * 20));
            fronts.Add(orbit, Bytes(orbit, 18));
            uint strip = checked(0x133AC90u + (uint)(index * 36));
            fronts.Add(strip, Bytes(strip, 12));
            fronts.Add(strip + 14, Bytes(strip + 14, 14));
            fronts.Add(strip + 34, Bytes(strip + 34, 2));
        }
        ImmutableArray<byte> vibration = fronts[VibrationAddress];
        NativeBattleEffect65Resources result = new(mainTim, new(Convert.ToHexString(SHA256.HashData(vibration.AsSpan())), [0, 256], vibration), new(0, SpriteAddress, 0, 0, fronts[SpriteAddress]), meshes, fronts.ToImmutableDictionary(), files.ToImmutableDictionary(file => file.FileId));
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect65Resources? nativeEffect65Resources;

    public NativeBattleEffect65Resources ReadNativeEffect65Resources()
    {
        if (nativeEffect65Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleEffect65ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect65Resources = NativeBattleEffect65ResourcesReader.Read(executable, new("mag064.tim", Read(magic, "mag064.tim").ToImmutableArray()), files);
        }
        return nativeEffect65Resources;
    }
}
