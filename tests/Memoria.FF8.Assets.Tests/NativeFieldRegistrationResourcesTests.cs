using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldRegistrationResourcesTests
{
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
