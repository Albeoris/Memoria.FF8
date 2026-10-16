using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record NativeRagnarokCameraDefaults(string ExecutableHash, short OffsetX, short OffsetY, short OffsetZ)
{
    public void Validate()
    {
        if (ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || OffsetX != 0 || OffsetZ >= 0)
        {
            throw new InvalidDataException("Ragnarok camera requires authenticated558C20 entity50 offsets.");
        }
    }
}

public sealed partial class WorldAssets
{
    private FieldModel? ragnarokModel;

    public FieldModel GetRagnarokModel()
    {
        if (ragnarokModel is null)
        {
            //546E10 maps entity50 to original animated chara.one shape1.
            FieldModel model = WorldAssetReader.ReadWorldCharacter(Read("chara.one"), 1);
            ragnarokModel = model with { Textures = textureScope.RegisterList(new ResourceKey("world/chara.one"), model.Textures, "classic-model", "model/1") };
        }
        return ragnarokModel;
    }

    public NativeRagnarokCameraDefaults ReadRagnarokCameraDefaults()
    {
        byte[] executable = contactExecutable ??= File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe"));
        string hash = Convert.ToHexString(SHA256.HashData(executable));
        if (hash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new InvalidDataException("Ragnarok projection constants require the authenticated original executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        int rva = checked((int)(0xC76D38 - reader.PEHeaders.PEHeader!.ImageBase));
        SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 6 <= section.VirtualAddress + section.SizeOfRawData);
        ReadOnlySpan<byte> words = executable.AsSpan(rva - section.VirtualAddress + section.PointerToRawData, 6);
        var result = new NativeRagnarokCameraDefaults(hash, BinaryPrimitives.ReadInt16LittleEndian(words), BinaryPrimitives.ReadInt16LittleEndian(words[2..]), BinaryPrimitives.ReadInt16LittleEndian(words[4..]));
        result.Validate();
        return result;
    }
}
