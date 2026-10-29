using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeModelResourceRehydrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RehydrationUsesCurrentValidatedMchAndRetainsSampledPresentation(bool autoAnimation)
    {
        using Inputs input = new(autoAnimation);
        NativeFieldRegistrationResources changed = WithValidatedTrailingPadding(input.Source);
        NativeFieldRegistrationStep old = NativeBattleStorageSnapshots.Capture(input.Initial);
        string retainedBefore = NativeBattleSnapshotJson.Serialize(old.Memory);
        NativeFieldRetainedPresentation presentation = input.Presentation[0];
        NativeCrtRandomState random = new(12345);
        NativeFieldRegistrationStep rebuilt = NativeFieldRegistrationMachine.RehydrateInactiveResources(changed, old.State, old.Memory, input.Actors, input.Presentation);
        Assert.Equal(retainedBefore, NativeBattleSnapshotJson.Serialize(old.Memory));
        Assert.Equal(old.State.FieldId, rebuilt.State.FieldId);
        Assert.Equal(old.State.ProgramHash, rebuilt.State.ProgramHash);
        Assert.Equal(old.State.Instances.Select(instance => (instance.ObjectId, instance.ModelId)), rebuilt.State.Instances.Select(instance => (instance.ObjectId, instance.ModelId)));
        Assert.NotEqual(old.State.Resources[0].SourceHash, rebuilt.State.Resources[0].SourceHash);
        Assert.Contains(rebuilt.State.Resources[0].SourceHash, changed.MainCharacters.Select(source => source.Sha256));
        NativeFieldRegisteredInstance instance = Assert.Single(rebuilt.State.Instances);
        NativeBattleFileStaging storage = rebuilt.Memory.FileStaging!;
        Assert.Equal(presentation.Frame, (ushort?)BinaryPrimitives.ReadUInt16LittleEndian(storage.Read(instance.Address + 82, 2)));
        Assert.Equal(presentation.AutoAnimation ? presentation.BaseAnimation : presentation.Animation, BinaryPrimitives.ReadUInt16LittleEndian(storage.Read(instance.Address + 126, 2)));
        Assert.Equal((byte)0, storage.Read(instance.Address + 96, 1)[0] & 1);
        Assert.Equal(presentation.RendererMode, storage.Read(instance.Address + 97, 1)[0]);
        Assert.Equal(presentation.RootTranslationZ, BinaryPrimitives.ReadInt16LittleEndian(storage.Read(instance.Address + 98, 2)));
        Assert.Equal((byte)0, storage.Read(instance.Address + 114, 1)[0] & 0xF0);
        uint packet = BinaryPrimitives.ReadUInt32LittleEndian(storage.Read(instance.Address + 88, 4));
        Assert.Equal(new[] { presentation.Red, presentation.Green, presentation.Blue }, storage.Read(packet + 4, 3));
        NativeBattleFieldResourceReloadResult sampled = NativeFieldRegistrationMachine.RestoreRetainedPresentation(rebuilt.State, rebuilt.Memory, input.Presentation, random);
        Assert.Equal(random, sampled.CrtRandomBefore);
        Assert.Equal(random, sampled.CrtRandomAfter);
        rebuilt.State.Validate(rebuilt.Memory);
    }

    [Fact]
    public void RebuildingSameSourceIsAllowedWithoutInitializingAnotherEyeSequence()
    {
        using Inputs input = new();
        NativeFieldRegistrationStep rebuilt = NativeFieldRegistrationMachine.RehydrateInactiveResources(input.Source, input.Initial.State, input.Initial.Memory, input.Actors, input.Presentation);
        Assert.Equal(input.Initial.State.Resources.Select(resource => (resource.ModelId, resource.SourceHash, resource.SourceOffset)), rebuilt.State.Resources.Select(resource => (resource.ModelId, resource.SourceHash, resource.SourceOffset)));
        Assert.Equal(input.Presentation[0].Frame, (ushort?)BinaryPrimitives.ReadUInt16LittleEndian(rebuilt.Memory.FileStaging!.Read(rebuilt.State.Instances[0].Address + 82, 2)));
    }

    [Theory]
    [InlineData("logical-eyes")]
    [InlineData("physical-eyes")]
    [InlineData("copy-history")]
    [InlineData("plane-progress")]
    [InlineData("pending-vram")]
    public void ActiveProgressRejectsBeforeRetainedStateCanChange(string condition)
    {
        using Inputs input = new();
        NativeBattleRetainedMemory retained = input.Initial.Memory;
        NativeFieldRetainedPresentation[] presentation = input.Presentation;
        if (condition == "logical-eyes")
        {
            presentation = [presentation[0] with { EyeMode = 0x80000 }];
        }
        else if (condition == "physical-eyes")
        {
            retained = retained with { FileStaging = retained.FileStaging!.Write(input.Initial.State.Instances[0].Address + 114, [0x10]) };
        }
        else if (condition == "copy-history")
        {
            NativeFieldTextureAnimations animations = retained.FieldTextureAnimations!;
            NativeFieldRegisteredTexture texture = animations.Textures[0].Texture;
            retained = retained with { FieldTextureAnimations = animations.Copy(texture.X, texture.Y, 1, 1, texture.X, texture.Y) };
        }
        else if (condition == "plane-progress")
        {
            NativeFieldTexturePlanes planes = NativeFieldTexturePlanes.FromReset(NativeFieldRuntimeProfile.RemasteredEnImageHash);
            planes = planes with { Slots = planes.Slots.SetItem(0, new(false, 1, false)) };
            retained = retained with { FieldTextureAnimations = retained.FieldTextureAnimations! with { Planes = planes } };
        }
        else
        {
            NativeBattleEffectTextureState textures = retained.EffectMemory!.Textures!.EnqueueCopy505EB0(0, 0, 1, 1, 1, 0);
            retained = retained with { EffectMemory = retained.EffectMemory with { Textures = textures } };
        }
        string before = NativeBattleSnapshotJson.Serialize(retained);
        string bindings = NativeBattleSnapshotJson.Serialize(input.Initial.State);
        string actorState = NativeBattleSnapshotJson.Serialize(presentation);
        Assert.Throws<NotSupportedException>(() => NativeFieldRegistrationMachine.RehydrateInactiveResources(input.Source, input.Initial.State, retained, input.Actors, presentation));
        Assert.Equal(before, NativeBattleSnapshotJson.Serialize(retained));
        Assert.Equal(bindings, NativeBattleSnapshotJson.Serialize(input.Initial.State));
        Assert.Equal(actorState, NativeBattleSnapshotJson.Serialize(presentation));
    }

    private static NativeFieldRegistrationResources WithValidatedTrailingPadding(NativeFieldRegistrationResources original)
    {
        Assert.NotEmpty(original.MainCharacters);
        ImmutableArray<NativeFieldModelSource> sources = original.MainCharacters.Select(source =>
        {
            byte[] bytes = new byte[source.Data.Length + 32];
            source.Data.AsSpan().CopyTo(bytes);
            GameAssets.ValidateMainCharacterResource(new(new ResourceKey(source.Member), "mch", 1, "test-source", () => new MemoryStream(bytes, false)));
            return source with { Data = bytes.ToImmutableArray(), Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        }).ToImmutableArray();
        NativeFieldRegistrationResources changed = original with { MainCharacters = sources };
        changed.Validate();
        return changed;
    }

    private sealed class Inputs : IDisposable
    {
        private readonly GameAssets assets = new(TestResources.GameDirectory);
        public NativeFieldRegistrationResources Source { get; }
        public NativeFieldRetainedActor[] Actors { get; }
        public NativeFieldRetainedPresentation[] Presentation { get; }
        public NativeFieldRegistrationStep Initial { get; }

        public Inputs(bool autoAnimation = false)
        {
            FieldAssets field = assets.LoadField(215);
            Source = Assert.IsType<NativeFieldRegistrationResources>(field.NativeRegistration);
            ushort group = BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8, 2));
            Actors = [new(0, 0, group >> 7, (group >> 7) + (group & 127) + 1)];
            Presentation = [new(0, 0, 16, autoAnimation, 0, true, 17, -12, 91, 123, 147, false, false, 0)];
            Initial = NativeFieldRegistrationMachine.ReloadKind3(Source, Actors, NativeBattleRetainedMemory.FromColdProcess());
            Initial = Initial with { Memory = NativeFieldRegistrationMachine.RestoreRetainedPresentation(Initial.State, Initial.Memory, Presentation).Memory };
            Assert.Equal(0, Initial.Memory.EffectMemory!.Textures!.Count);
            Assert.All(Initial.Memory.FieldTextureAnimations!.Textures, texture => Assert.Empty(texture.Frames));
        }

        public void Dispose() => assets.Dispose();
    }
}
