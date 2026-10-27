using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed record NativeFieldTextureBinding(FieldAssets Field, NativeFieldTextureAnimations? Animations);

public sealed partial class GameAssets
{
    private ImmutableArray<uint> nativeFieldPlacementWords;
    private long nativeRegisteredTextureRevision;

    public FieldAssets BindNativeRegisteredTextures(FieldAssets field, NativeFieldRegistrationState registration, NativeBattleEffectTextureState textures, NativeFieldTextureAnimations? animations = null)
    {
        return BindNativeRegisteredTexturesCore(field, registration, textures, animations).Field;
    }

    public NativeFieldTextureBinding BindNativeRegisteredTextureState(FieldAssets field, NativeFieldRegistrationState registration, NativeBattleEffectTextureState textures, NativeFieldTextureAnimations? animations, int modelId)
    {
        if (!registration.Resources.Any(resource => resource.ModelId == modelId))
        {
            throw new InvalidDataException("Native material binding requires an actually registered model.");
        }
        return BindNativeRegisteredTexturesCore(field, registration, textures, PrepareNativeFieldMaterialBinding(field, registration, animations, modelId));
    }

    public NativeFieldTextureAnimations? PrepareNativeFieldMaterialBinding(FieldAssets field, NativeFieldRegistrationState registration, NativeFieldTextureAnimations? animations, int modelId)
    {
        if (field.NativeRegistration is not { } source || source.ExecutableHash != NativeFieldRuntimeProfile.RemasteredEnImageHash)
        {
            return animations;
        }
        if (registration.FieldId != field.Id || registration.ProgramHash != source.ProgramHash || registration.OneHash != source.One.Sha256 || animations?.OneHash != registration.OneHash)
        {
            throw new InvalidDataException("Native material binding belongs to another field registration.");
        }
        NativeFieldTexturePlanes planes = animations.Planes ?? throw new NotSupportedException("Native material binding requires its retained plane cache.");
        NativeFieldRegisteredResource selected = registration.Resources.Single(resource => resource.ModelId == modelId);
        Dictionary<int, NativeFieldTexturePageIdentity> identities = [];
        foreach (NativeFieldRegisteredTexture texture in registration.Resources.SelectMany(resource => resource.Textures))
        {
            uint identity = texture.PlaneIdentity ?? throw new NotSupportedException("Native material binding requires its original upload source identity.");
            int page = texture.TexturePage & 31;
            NativeFieldTexturePageIdentity pair = identities.GetValueOrDefault(page) ?? new(0, 0);
            identities[page] = (texture.Y & 255) == 0 ? pair with { First = identity } : pair with { Other = identity };
        }
        FieldModel model = field.Models[modelId];
        foreach (int ordinal in model.Faces.Select(face => face.Texture).Distinct())
        {
            NativeFieldRegisteredTexture texture = selected.Textures[ordinal];
            _ = NativeMaterialHalf(model, ordinal, texture);
            int page = texture.TexturePage & 31;
            planes = planes.Bind(page, identities[page]);
        }
        return ReferenceEquals(planes, animations.Planes) ? animations : animations with { Planes = planes };
    }

    private static bool? NativeMaterialHalf(FieldModel model, int ordinal, NativeFieldRegisteredTexture texture)
    {
        bool? half = null;
        int offset = (texture.Y & 255) << 8 | (texture.X & 63) << (2 - texture.Mode);
        foreach (ModelFace face in model.Faces.Where(face => face.Texture == ordinal))
        {
            var first = face.TextureCoordinates[0];
            ushort firstPacked = unchecked((ushort)(((int)first.X | (int)first.Y << 8) + offset));
            bool current = (firstPacked >> 8) < 128;
            if (half is { } previous && previous != current)
            {
                throw new NotSupportedException("A native field material spanning both renderer plane halves requires per-face plane selection.");
            }
            foreach (var uv in face.TextureCoordinates.Take(face.Quad ? 4 : 3))
            {
                ushort packed = unchecked((ushort)(((int)uv.X | (int)uv.Y << 8) + offset));
                int v = packed >> 8;
                if (current ? v > 128 : v < 128)
                {
                    throw new NotSupportedException("A native field face spanning both renderer plane halves requires per-vertex plane selection.");
                }
            }
            half = current;
        }
        return half;
    }

    private NativeFieldTextureBinding BindNativeRegisteredTexturesCore(FieldAssets field, NativeFieldRegistrationState registration, NativeBattleEffectTextureState textures, NativeFieldTextureAnimations? animations)
    {
        if (registration.FieldId != field.Id || field.NativeRegistration is not { } source || registration.ProgramHash != source.ProgramHash || registration.OneHash != source.One.Sha256)
        {
            throw new InvalidDataException("Native registered textures do not belong to this original field source.");
        }
        if (animations is not null && animations.OneHash != registration.OneHash)
        {
            throw new InvalidDataException("Field texture copy history belongs to another registration source.");
        }
        bool nativePlanes = source.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash;
        NativeFieldTexturePlanes? planes = nativePlanes ? animations?.Planes ?? throw new NotSupportedException("Registered Remastered field textures require their retained native plane state.") : null;
        planes?.Validate();
        TexturePageMember[] members = registration.Resources.SelectMany(resource => resource.Textures.Select((texture, ordinal) =>
        {
            FieldModel model = field.Models[resource.ModelId];
            int frame = animations is null ? -2 : animations.Textures.SingleOrDefault(animation => animation.Texture.X == texture.X && animation.Texture.Y == texture.Y)?.CurrentFrame ?? throw new InvalidDataException("Registered field texture has no retained copy-history owner.");
            return new TexturePageMember(model.MainCharacterTextures ? "main" : "second", model.TextureResourceName ?? throw new NotSupportedException("Native field pages require their original source model names."), ordinal, texture.X, texture.Y, texture.WidthWords, texture.Height, 4 << texture.Mode, frame);
        })).DistinctBy(member => (member.X, member.Y)).OrderBy(member => member.X + member.Y * 1024).ToArray();
        long revision = -checked(++nativeRegisteredTextureRevision);
        FieldModel[] models = field.Models.ToArray();
        foreach (NativeFieldRegisteredResource resource in registration.Resources)
        {
            if ((uint)resource.ModelId >= models.Length || models[resource.ModelId].NativeParts is not { } parts || parts.SourceHash != resource.SourceHash || parts.SourceOffset != resource.SourceOffset || resource.Textures.Length != models[resource.ModelId].Textures.Count)
            {
                throw new InvalidDataException("Native registered texture bindings disagree with their original model source.");
            }
            FieldModel model = models[resource.ModelId] with { Textures = resource.Textures.Select(Decode).ToArray() };
            TextureSourcePageAddress[] pages = resource.Textures.Select(texture =>
            {
                int page = texture.TexturePage & 31;
                int pageX = (page & 15) * 64;
                int pageY = (page >> 4) * 256;
                int shift = 2 - texture.Mode;
                int pageWidthWords = 256 >> shift;
                TexturePageMember[] visible = members.Where(member => member.BitsPerPixel == 4 << texture.Mode && member.X < pageX + pageWidthWords && member.X + member.WidthWords > pageX && member.Y < pageY + 256 && member.Y + member.Height > pageY).ToArray();
                return new TextureSourcePageAddress(page, (texture.X - pageX) << shift, texture.Y - pageY, texture.WidthWords << shift, texture.Height, texture.PaletteX, texture.PaletteY, visible);
            }).ToArray();
            string?[]? variants = null;
            if (planes is not null)
            {
                model = model with { TextureSourceVariant = NativeFieldModelTextureSheets.Variant(model.TextureResourceName, field.Id) };
                variants = new string?[resource.Textures.Length];
                for (int ordinal = 0; ordinal < resource.Textures.Length; ordinal++)
                {
                    NativeFieldRegisteredTexture texture = resource.Textures[ordinal];
                    int page = texture.TexturePage & 31;
                    bool? firstHalf = NativeMaterialHalf(model, ordinal, texture);
                    variants[ordinal] = firstHalf is { } half && planes.IsClosed(page, half) ? "eyes-closed" : null;
                }
            }
            models[resource.ModelId] = RegisterFieldModelTextures(field.Name, resource.ModelId, model, revision, pages, variants);
        }
        NativeFieldTextureAnimations? updated = planes is null || ReferenceEquals(planes, animations!.Planes) ? animations : animations with { Planes = planes };
        return new(field with { Models = models }, updated);

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
            return textures.ReadVramWord(x, y);
        }
    }

    private FieldModel RegisterFieldModelTextures(string fieldName, int index, FieldModel model, long? revision = null, IReadOnlyList<TextureSourcePageAddress>? pages = null, IReadOnlyList<string?>? requestedVariants = null)
    {
        model = model with { TextureSourceVariant = NativeFieldModelTextureSheets.Variant(model.TextureResourceName, Array.IndexOf(fieldNames, fieldName)) };
        string registration = revision.HasValue ? "/registered" : "";
        string prefix = model.TextureResourceName is { } name ? $"model/{index}/{(model.MainCharacterTextures ? "main" : "second")}/{name}{registration}/texture" : $"model/{index}{registration}/texture";
        string layout = model.TextureResourceName is null ? "classic-model" : "classic-field-model";
        ResourceKey owner = new($"field/{fieldName}/chara.one");
        AssetHandle<TextureValue>[] handles = model.Textures.Select((image, ordinal) => textureScope.Register(new TextureKey(owner, $"{prefix}/{ordinal}"), image, layout, revision: revision, nativePage: pages?[ordinal], nativeSheet: NativeFieldModelTextureSheets.Address(model, ordinal), requestedVariant: requestedVariants?[ordinal])!).ToArray();
        return model with { Textures = new ResourceTextureList(handles) };
    }

    private NativeFieldRegistrationLoaderInputs ReadNativeFieldRegistrationInputs(int fieldId, byte[] information, byte[] program, byte[]? colors, int? allocationPrefixBytes = null)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || information.Length != 676)
        {
            throw UnsupportedRuntime("Native field registration requires the authenticated original information record.");
        }
        if (nativeFieldPlacementWords.IsDefault)
        {
            byte[] words = ReadNativeMemory().Read(0xB80880, 192);
            nativeFieldPlacementWords = Enumerable.Range(0, 48).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(words.AsSpan(index * 4, 4))).ToImmutableArray();
        }
        string name = fieldNames[fieldId];
        NativeFieldModelSource Source(string member, byte[] data) => new($"field/{name}/{member}", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)), data.ToImmutableArray());
        return new(allocationPrefixBytes ?? ReadNativeFieldAllocationPrefix(fieldId), information[13], Source(name + ".jsm", program), colors is null ? null : Source(name + ".pcb", colors), nativeFieldPlacementWords.Take(16).ToImmutableArray(), nativeFieldPlacementWords.Skip(information[13] == 0 ? 16 : 32).Take(16).ToImmutableArray());
    }
}
