using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldRegistrationResourcesTests
{
    [Fact]
    public void Original_cavern_retained_actor_order_registers_three_models_and_an_empty_instance()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        FieldAssets field = game.LoadField(130);
        NativeFieldRegistrationResources source = field.NativeRegistration!;
        int groupCount = field.Jsm[0] + field.Jsm[1] + field.Jsm[2] + field.Jsm[3];
        ushort[] groups = Enumerable.Range(0, groupCount).Select(index => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8 + index * 2, 2))).OrderBy(group => group >> 7).ToArray();
        (int Object, int Model)[] bindings = [(0, 0), (5, 5), (9, 9), (11, -1)];
        NativeFieldRetainedActor[] actors = bindings.Select(binding => new NativeFieldRetainedActor(binding.Object, binding.Model, groups[binding.Object] >> 7, (groups[binding.Object] >> 7) + (groups[binding.Object] & 127) + 1)).ToArray();
        // Declared cold resource component: these retained bindings do not migrate or reconstruct a historical field save.
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        NativeFieldRegistrationStep first = NativeFieldRegistrationMachine.ReloadKind3(source, actors, cold);
        NativeFieldRegistrationStep restored = NativeFieldRegistrationMachine.ReloadKind3(NativeBattleSnapshotJson.Deserialize<NativeFieldRegistrationResources>(NativeBattleSnapshotJson.Serialize(source))!, actors, NativeBattleSnapshotJson.Deserialize<NativeBattleRetainedMemory>(NativeBattleSnapshotJson.Serialize(cold))!);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(first), NativeBattleSnapshotJson.Serialize(restored));
        Assert.Equal(new[] { 0, 5, 9 }, first.State.Resources.Select(resource => resource.ModelId));
        Assert.Equal(bindings.Select(binding => binding.Object), first.State.Instances.Select(instance => instance.ObjectId));
        Assert.Equal(bindings.Select(binding => binding.Model), first.State.Instances.Select(instance => instance.ModelId));
        NativeFieldRegisteredInstance empty = first.State.Instances[^1];
        Assert.Equal(uint.MaxValue, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(first.Memory.FileStaging!.Read(empty.Address, 4)));
        //531D40 leaves WORD12 untouched here; the original retained input slab supplies this known value.
        Assert.Equal((ushort?)1024, empty.EmptyTerminal);
        Assert.Equal((ushort)1024, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(first.Memory.FileStaging.Read(empty.Address + 12, 2)));
        foreach (NativeFieldRegisteredResource resource in first.State.Resources)
        {
            Assert.Equal(field.Models[resource.ModelId].NativeParts!.SourceHash, resource.SourceHash);
            Assert.Equal(field.Models[resource.ModelId].NativeParts!.SourceOffset, resource.SourceOffset);
        }
        FieldAssets projected = game.BindNativeRegisteredTextures(field, first.State, first.Memory.EffectMemory!.Textures!);
        foreach (int model in new[] { 0, 5, 9 })
        {
            Assert.Equal(field.Models[model].Textures.Count, projected.Models[model].Textures.Count);
            Assert.NotSame(field.Models[model].Textures[0], projected.Models[model].Textures[0]);
        }
        first.State.Validate(first.Memory);
        NativeFieldRegisteredResource squall = first.State.Resources[0];
        uint eyeBank = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(first.Memory.FileStaging!.Read(squall.Address + 20, 4));
        short blinkWord = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(first.Memory.FileStaging.Read(eyeBank + 10, 2));
        NativeFieldRetainedPresentation[] presentation = bindings.Select(binding => new NativeFieldRetainedPresentation(binding.Object, 0, 16, false, 0, true, 17, 0, 128, 128, 128, false, false, binding.Object == 0 ? 0x80000u : 0u) { EyeBlinkParameter611 = 128 }).ToArray();
        NativeCrtRandomState random = new(12345);
        NativeBattleFieldResourceReloadResult blink = NativeFieldRegistrationMachine.RestoreRetainedPresentation(first.State, first.Memory, presentation, random);
        NativeFieldRegisteredInstance actor = first.State.Instances[0];
        byte firstTimer;
        byte secondTimer;
        NativeCrtRandomState expected = random;
        if (blinkWord < 0)
        {
            firstTimer = secondTimer = unchecked((byte)blinkWord);
        }
        else
        {
            expected = expected.Draw(out ushort firstDraw);
            expected = expected.Draw(out ushort secondDraw);
            firstTimer = (byte)(firstDraw % 8 + 1);
            secondTimer = (byte)(secondDraw % 8 + 1);
            Assert.Equal((ushort)128, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(blink.Memory.FileStaging!.Read(eyeBank + 10, 2)));
        }
        Assert.Equal(random, blink.CrtRandomBefore);
        Assert.Equal(expected, blink.CrtRandomAfter);
        Assert.Equal(firstTimer, blink.Memory.FileStaging!.Read(actor.Address + 102, 1)[0]);
        Assert.Equal(secondTimer, blink.Memory.FileStaging.Read(actor.Address + 116, 1)[0]);
        Assert.Equal(0x10, blink.Memory.FileStaging.Read(actor.Address + 114, 1)[0] & 0xF0);
        Assert.Equal(first.Memory.EffectMemory!.Textures, blink.Memory.EffectMemory!.Textures);
    }

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
        NativeBattleRetainedMemory original = NativeBattleStorageSnapshots.Capture(NativeBattleRetainedMemory.FromColdProcess());
        string before = NativeBattleSnapshotJson.Serialize(original);
        NativeFieldRegistrationStep first = NativeFieldRegistrationMachine.ReloadKind3(source, actors, original);
        NativeFieldRegistrationStep second = NativeFieldRegistrationMachine.ReloadKind3(NativeBattleSnapshotJson.Deserialize<NativeFieldRegistrationResources>(NativeBattleSnapshotJson.Serialize(source))!, actors, NativeBattleSnapshotJson.Deserialize<NativeBattleRetainedMemory>(before)!);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(first), NativeBattleSnapshotJson.Serialize(second));
        Assert.Equal(before, NativeBattleSnapshotJson.Serialize(original));
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
            Assert.Empty(registered.Translations);
            for (int skin = 0; skin < parts.SkinBindings.Count; skin++)
            {
                bool selected = parts.Parts.Any(part => skin >= part.FirstSkinBinding && skin < part.FirstSkinBinding + part.SkinBindingCount);
                Assert.Equal(selected ? (ushort)0 : parts.SkinBindings[skin].OriginalFlags, parts.ReadInitializedSkinFlags(skin));
            }
        }
        NativeFieldRegistrationResources restored = NativeBattleSnapshotJson.Deserialize<NativeFieldRegistrationResources>(NativeBattleSnapshotJson.Serialize(source))!;
        restored.Validate();
        Assert.Equal(source.One.Sha256, restored.One.Sha256);
        Assert.Same(source, game.LoadField(fieldId).NativeRegistration);
    }
}
