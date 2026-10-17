using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private ImmutableArray<uint> nativeFieldPlacementWords;

    public FieldAssets BindNativeRegisteredTextures(FieldAssets field, NativeFieldRegistrationState registration, NativeBattleEffectTextureState textures)
    {
        if (registration.FieldId != field.Id || field.NativeRegistration is not { } source || registration.ProgramHash != source.ProgramHash || registration.OneHash != source.One.Sha256)
        {
            throw new InvalidDataException("Native registered textures do not belong to this original field source.");
        }
        byte[] bank = textures.ReadVram();
        FieldModel[] models = field.Models.ToArray();
        foreach (NativeFieldRegisteredResource resource in registration.Resources)
        {
            if ((uint)resource.ModelId >= models.Length || models[resource.ModelId].NativeParts is not { } parts || parts.SourceHash != resource.SourceHash || parts.SourceOffset != resource.SourceOffset || resource.Textures.Length != models[resource.ModelId].Textures.Count)
            {
                throw new InvalidDataException("Native registered texture bindings disagree with their original model source.");
            }
            models[resource.ModelId] = models[resource.ModelId] with { Textures = resource.Textures.Select(Decode).ToArray() };
        }
        return field with { Models = models };

        PixelImage Decode(NativeFieldRegisteredTexture texture)
        {
            if (texture.Mode > 2 || texture.WidthWords <= 0 || texture.Height <= 0)
            {
                throw new InvalidDataException("Native registered texture has an invalid pixel depth or rectangle.");
            }
            int shift = 2 - texture.Mode;
            PixelImage image = new(texture.WidthWords << shift, texture.Height);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    ushort packed = Word(texture.X + (x >> shift), texture.Y + y);
                    ushort color = texture.Mode switch
                    {
                        0 => Word(texture.PaletteX + ((packed >> ((x & 3) * 4)) & 15), texture.PaletteY),
                        1 => Word(texture.PaletteX + ((packed >> ((x & 1) * 8)) & 255), texture.PaletteY),
                        _ => packed
                    };
                    image[x, y] = Rgba32.FromPsxBgr555(color);
                }
            }
            return image;
        }

        ushort Word(int x, int y)
        {
            if (x is < 0 or >= 1024 || y is < 0 or >= 512)
            {
                throw new InvalidDataException("Native registered texture sampling leaves shared VRAM.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(bank.AsSpan((y * 1024 + x) * 2, 2));
        }
    }

    private NativeFieldRegistrationLoaderInputs ReadNativeFieldRegistrationInputs(int fieldId, byte[] information, byte[] program, byte[] colors)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || information.Length != 676)
        {
            throw new NotSupportedException("Native field registration requires the authenticated original information record.");
        }
        if (nativeFieldPlacementWords.IsDefault)
        {
            byte[] image = ReadNativeExecutable();
            using PEReader reader = new(new MemoryStream(image, false));
            long rva = 0xB80880 - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 192 <= section.VirtualAddress + section.SizeOfRawData);
            int offset = checked((int)(rva - section.VirtualAddress + section.PointerToRawData));
            nativeFieldPlacementWords = Enumerable.Range(0, 48).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset + index * 4, 4))).ToImmutableArray();
        }
        string name = fieldNames[fieldId];
        NativeFieldModelSource Source(string member, byte[] data) => new($"field/{name}/{member}", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)), data.ToImmutableArray());
        return new(ReadNativeFieldAllocationPrefix(fieldId), information[13], Source(name + ".jsm", program), Source(name + ".pcb", colors), nativeFieldPlacementWords.Take(16).ToImmutableArray(), nativeFieldPlacementWords.Skip(information[13] == 0 ? 16 : 32).Take(16).ToImmutableArray());
    }
}
