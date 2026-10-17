using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldRegistrationResourcesTests
{
    [Theory]
    [InlineData(74)]
    [InlineData(919)]
    public void Original_model_registration_executes_shared_memory_and_VRAM_writes_after_restore(int fieldId)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        FieldAssets field = game.LoadField(fieldId);
        NativeFieldRegistrationResources source = field.NativeRegistration!;
        Assert.NotNull(source.Loader);
        ushort group = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8, 2));
        NativeFieldRetainedActor[] actors = [new(0, 0, group >> 7, (group >> 7) + (group & 127) + 1)];
        NativeBattleRetainedMemory original = NativeBattleRetainedMemory.FromColdProcess();
        string before = JsonSerializer.Serialize(original);
        NativeFieldRegistrationStep first = NativeFieldRegistrationMachine.ReloadKind3(source, actors, original);
        NativeFieldRegistrationStep second = NativeFieldRegistrationMachine.ReloadKind3(JsonSerializer.Deserialize<NativeFieldRegistrationResources>(JsonSerializer.Serialize(source))!, actors, JsonSerializer.Deserialize<NativeBattleRetainedMemory>(before)!);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(before, JsonSerializer.Serialize(original));
        first.State.Validate(first.Memory);
        NativeFieldRegisteredResource registered = Assert.Single(first.State.Resources);
        Assert.Equal(field.Models[0].NativeParts!.SourceHash, registered.SourceHash);
        Assert.Equal(field.Models[0].NativeParts!.SourceOffset, registered.SourceOffset);
        Assert.NotEmpty(first.Uploads);
        Assert.Equal((short)960, first.Uploads[0].X);
        Assert.Equal((short)128, first.Uploads[0].Y);
        Assert.Equal((short)512, first.Uploads[1].X);
        Assert.Equal((short)240, first.Uploads[1].Y);
        Assert.Equal((ushort)0xAF, registered.Textures[0].TexturePage);
        Assert.Equal((ushort)0x3C20, registered.Textures[0].Clut);
        FieldAssets rebound = game.BindNativeRegisteredTextures(field, first.State, first.Memory.EffectMemory!.Textures!);
        Assert.Equal(field.Models[0].Textures.Count, rebound.Models[0].Textures.Count);
        Assert.NotSame(field.Models[0].Textures[0], rebound.Models[0].Textures[0]);
        Assert.Equal(field.Models[0].Textures[0].Width, rebound.Models[0].Textures[0].Width);
        Assert.Equal(field.Models[0].Textures[0].Height, rebound.Models[0].Textures[0].Height);
        Assert.Throws<InvalidDataException>(() => game.BindNativeRegisteredTextures(field, first.State with { FieldId = fieldId + 1 }, first.Memory.EffectMemory!.Textures!));
    }

    [Theory]
    [InlineData(74)]
    [InlineData(919)]
    public void Actual_field_ONE_and_referenced_MCH_sources_remain_original_registration_inputs(int fieldId)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        FieldAssets field = game.LoadField(fieldId);
        NativeFieldRegistrationResources source = Assert.IsType<NativeFieldRegistrationResources>(field.NativeRegistration);
        source.Validate();
        Assert.Equal(field.Id, source.FieldId);
        Assert.Equal(field.Name, source.FieldName);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(field.Jsm)), source.ProgramHash);
        Assert.Equal(field.Models.Count, source.Directory.Length);
        Assert.Equal($"field/{field.Name}/chara.one", source.One.Member);
        HashSet<string> hashes = source.MainCharacters.Select(model => model.Sha256).Append(source.One.Sha256).ToHashSet(StringComparer.Ordinal);
        for (int model = 0; model < field.Models.Count; model++)
        {
            NativeModelPartsDefinition parts = Assert.IsType<NativeModelPartsDefinition>(field.Models[model].NativeParts);
            Assert.Contains(parts.SourceHash, hashes);
            Assert.Equal(field.Models[model].Name, source.Directory[model].Name);
            NativeModelPartResourceState registered = NativeModelPartResourceState.Create(parts);
            registered.Validate(parts);
            for (int skin = 0; skin < parts.SkinBindings.Count; skin++)
            {
                bool selected = parts.Parts.Any(part => skin >= part.FirstSkinBinding && skin < part.FirstSkinBinding + part.SkinBindingCount);
                Assert.Equal(selected ? (ushort)0 : parts.SkinBindings[skin].OriginalFlags, registered.ReadSkinFlags(skin));
            }
        }
        NativeFieldRegistrationResources restored = JsonSerializer.Deserialize<NativeFieldRegistrationResources>(JsonSerializer.Serialize(source))!;
        restored.Validate();
        Assert.Equal(source.One.Sha256, restored.One.Sha256);
        Assert.Same(source, game.LoadField(fieldId).NativeRegistration);
    }
}
