using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ModelResourceMappingTests
{
    private static readonly ResourceSourceDescriptor Donor = new("selected", GameDataEdition.Remastered, ["native-field-model-sheet", "native-battle-body-sheet"]);

    [Fact]
    public void FieldMappingPreservesRuntimeOwnerAndUsesDonorGeometry()
    {
        FieldModel primary = Field();
        FieldModel donor = Field() with { Vertices = [new(new(25, 0, 0), 0, 0)], Faces = [new([0, 0, 0], [new(12, 13), new(12, 13), new(12, 13)], 0, false)], NativeParts = Parts('B', 9, 0) };
        FieldModel mapped = ModelResourceMapping.Select(primary, donor, Donor);
        Assert.Equal(donor.Vertices, mapped.Vertices);
        Assert.Same(donor.Faces, mapped.Faces);
        Assert.Same(donor.NativeParts, mapped.NativeParts);
        Assert.Same(primary.NativeParts, mapped.NativeRegistrationParts);
        Assert.Same(primary.Animations, mapped.Animations);
        Assert.Same(primary.NativeEyes, mapped.NativeEyes);
        Assert.Same(primary.ControlBones, mapped.ControlBones);
        Assert.Same(Donor, mapped.TextureSourceDescriptor);
        Assert.Equal(donor.Pose(0, 0), mapped.Pose(0, 0));
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("control")]
    [InlineData("bone")]
    [InlineData("material")]
    [InlineData("animation")]
    [InlineData("skin")]
    public void FieldMappingRejectsIncompatibleContracts(string mismatch)
    {
        FieldModel primary = Field();
        FieldModel donor = mismatch switch
        {
            "identity" => primary with { Name = "d999" },
            "control" => primary with { ControlBones = new(0, 0, 0, 1) },
            "bone" => primary with { Bones = [new(-1, 1)] },
            "material" => primary with { TextureRegions = [new(CanvasWidth: 4, CanvasHeight: 1)] },
            "animation" => primary with { Animations = [new([new(Vector3.One, [Vector3.Zero])])] },
            _ => primary with { NativeParts = Parts('B', 0, 1), Faces = [new([0, 0, 0], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)] }
        };
        Assert.Throws<NotSupportedException>(() => ModelResourceMapping.Select(primary, donor, Donor));
    }

    [Fact]
    public void FieldMappingRejectsDifferentScriptPartControls()
    {
        short[] words = new short[16];
        FieldModel primary = Field() with { NativeParts = new(new string('A', 64), 0, [new(words)], [new(0, 1, 0, 0)]) };
        short[] changed = words.ToArray();
        changed[12] = 1;
        FieldModel donor = primary with { NativeParts = new(new string('B', 64), 0, [new(changed)], [new(0, 1, 0, 0)]) };
        Assert.Throws<NotSupportedException>(() => ModelResourceMapping.Select(primary, donor, Donor));
    }

    [Fact]
    public void FieldVisualResourceRetainsOwnSkinOrderAndSeparateRegistrationOwner()
    {
        FieldModel primary = Field() with { Bones = [new(-1, 0), new(0, 1)], NativeParts = new(new string('A', 64), 0, [], [new(0, 1, 0, 0), new(1, 1, 1, 0)]) };
        FieldModel donor = primary with { NativeParts = new(new string('B', 64), 20, [], [new(0, 3, 1, 0), new(3, 3, 0, 0)]), Vertices = [new(Vector3.One, 1, 0)] };
        FieldModel selected = ModelResourceMapping.Select(primary, donor, Donor);
        Assert.Equal(0, selected.Vertices[0].SkinBinding);
        Assert.Same(donor.NativeParts, selected.NativeParts);
        Assert.Same(primary.NativeParts, selected.NativeRegistrationParts);
    }

    [Fact]
    public void SelectedVisualPartUsesItsOwnAdditionalVisibleSkinAndNativeCounter()
    {
        short[] primaryWords = new short[16];
        primaryWords[1] = 1;
        short[] selectedWords = primaryWords.ToArray();
        selectedWords[1] = 2;
        FieldModel primary = Field() with { Bones = [new(-1, 0), new(0, 1)], NativeParts = new(new string('A', 64), 0, [new(primaryWords)], [new(0, 1, 0, 0)]) };
        FieldModel donor = primary with { NativeParts = new(new string('B', 64), 20, [new(selectedWords)], [new(0, 1, 0, 0), new(1, 1, 1, 0)]), Vertices = [new(Vector3.Zero, 0, 0), new(Vector3.One, 1, 1)], Faces = [new([0, 1, 1], [Vector2.Zero, Vector2.One, Vector2.One], 0, false)] };
        FieldModel selected = ModelResourceMapping.Select(primary, donor, Donor);
        NativeModelPartResourceState state = NativeModelPartResourceState.Create(selected.NativeParts!).StartTranslation(selected.NativeParts!, 0, 10, 0, 0, 30);
        NativeModelPartStep step = state.Step(selected.NativeParts!);
        Assert.Equal(2, step.SkinTranslations.Count);
        Assert.Equal(3, step.State.Translations[0].Counter);
        Assert.Equal(1, selected.Vertices[1].SkinBinding);
        Assert.Same(primary.NativeParts, selected.NativeRegistrationParts);
    }

    [Fact]
    public void BattleMappingPreservesNativeControlGeometryAndAnimation()
    {
        BattleModel primary = Battle();
        BattleModel donor = Battle() with { Meshes = [new([new(new(20, 0, 0), 0)], [new([0, 0, 0], [Vector2.One, Vector2.One, Vector2.One], 0, default)])] };
        BattleModel selected = ModelResourceMapping.Select(primary, donor, Donor);
        Assert.Same(donor.Meshes, selected.Meshes);
        Assert.Same(primary.Meshes, selected.NativeControlMeshes);
        Assert.Same(primary.Animations, selected.Animations);
        Assert.Same(primary.Sequences, selected.Sequences);
        Assert.Same(primary.NativePoseSource, selected.NativePoseSource);
        Assert.Same(Donor, selected.TextureSourceDescriptor);
    }

    [Fact]
    public void BattleMappingRejectsDifferentNativeSections()
    {
        BattleModel primary = Battle() with { NativePoseSource = new([1, 2], [3, 4]) };
        BattleModel donor = primary with { NativePoseSource = new([1, 2], [3, 5]) };
        Assert.Throws<NotSupportedException>(() => ModelResourceMapping.Select(primary, donor, Donor));
    }

    [Fact]
    public void ClassicEyeProjectionUsesCompleteOriginalBaseAndOpenClosedOpenWords()
    {
        byte[] bank = new byte[20];
        bank[2] = 8;
        bank[3] = 2;
        bank[4] = 1;
        bank[5] = 2;
        bank[12] = 2;
        bank[16] = 4;
        FieldModel model = Field() with { NativeEyes = new(new string('A', 64), 0, 0, bank, new(1, 3, 1, [3, 0, 2, 0, 1, 0])) };
        NativeFieldRegisteredResource resource = new(0, 0, 0, 0, 0, 0, new string('A', 64), 0, [new(1, 32, 10, 3, 1, 0, 0, 0, 0)]);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(32, 10, 3, 1, model.NativeEyes.Texture!.Words);
        Assert.Null(GameAssets.ReadClassicFieldModelVariant(model, resource, textures));
        textures = textures.EnqueueCopy505EB0(33, 10, 1, 1, 32, 10).Drain505D20().State;
        Assert.Equal("eyes-closed", GameAssets.ReadClassicFieldModelVariant(model, resource, textures));
        textures = textures.EnqueueCopy505EB0(34, 10, 1, 1, 32, 10).Drain505D20().State;
        Assert.Null(GameAssets.ReadClassicFieldModelVariant(model, resource, textures));
        textures = textures.Upload45BD30(33, 10, 1, 1, [3, 0]);
        Assert.Throws<NotSupportedException>(() => GameAssets.ReadClassicFieldModelVariant(model, resource, textures));
    }

    private static FieldModel Field() => new("d000", [new(-1, 0)], [new(Vector3.Zero, 0, 0)], [], [new PixelImage(1, 1)], [new([new(Vector3.Zero, [Vector3.Zero])])])
    {
        TextureRegions = [new(CanvasWidth: 1, CanvasHeight: 1)],
        TextureResourceName = "d000",
        MainCharacterTextures = true,
        NativeParts = Parts('A', 0, 0)
    };

    private static NativeModelPartsDefinition Parts(char hash, int offset, short bone) => new(new string(hash, 64), offset, [], [new(0, 1, bone, 0)]);

    private static BattleModel Battle() => new("d0c000.dat", [new(-1, 0)], [new([new(Vector3.Zero, 0)], [])], [new PixelImage(1, 1)], [new([new(Vector3.Zero, [Vector3.Zero], [Vector3.One])])], 1)
    {
        TextureRegions = [new(CanvasWidth: 1, CanvasHeight: 1)]
    };
}
