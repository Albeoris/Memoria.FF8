using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ExternalBankEditedGeometryTests
{
    [Fact]
    public void InstalledExternalBankRetainsEditedGeometryAndReplacesOnlyMappedClipChannels()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(directory), "Set optional installed game resources.");
        using GameAssets game = new(directory);
        CaptureOverrides capture = new();
        using BattleAssets assets = new(directory, modelOverrides: capture);
        BattleModel originalBody = assets.LoadPlayer(0);
        BattleModel originalWeapon = assets.ResolveWeaponModel(0, game.ReadNewGameCharacterVitals().Records[0].Weapon, originalBody) ?? throw new InvalidDataException("The selected body requires its standard weapon.");
        BattleModel body = Edit(originalBody);
        BattleModel weapon = Edit(originalWeapon);
        BattleNativeRenzokukenResource resource = game.ReadNativeRenzokukenResources().First();
        BattleRenzokukenModels decoded = assets.ReadNativeRenzokukenModels(resource, originalBody, originalWeapon);
        capture.Banks.Clear();
        BattleRenzokukenModels changed = assets.ReadNativeRenzokukenModels(resource, body, weapon);
        Assert.Equal(2, capture.Banks.Count);
        Check(body, decoded.Body, changed.Body);
        Check(weapon, decoded.Weapon!, changed.Weapon!);
        Assert.All(capture.Banks, item => Assert.NotNull(item.EditableOverride));
    }

    private static BattleModel Edit(BattleModel original)
    {
        EditableModel source = ModelExchange.FromBattle(original);
        EditableModel edited = source with
        {
            Bones = new[] { new EditableBone("helper", -1, Vector3.Zero, Quaternion.Identity, Vector3.One, false) }.Concat(source.Bones.Select(bone => bone with { Parent = bone.Parent < 0 ? 0 : bone.Parent + 1 })).ToArray(),
            Meshes = source.Meshes.Select(mesh => mesh with
            {
                Transparent = true,
                Vertices = mesh.Vertices.Select(vertex => vertex with { Position = vertex.Position + Vector3.UnitX, Joints = vertex.Joints + new Vector4(1, 0, 0, 0) }).ToArray()
            }).ToArray(),
            Animations = []
        };
        return ModelExchange.ToBattle(edited, original);
    }

    private static void Check(BattleModel before, BattleModel decoded, BattleModel actual)
    {
        EditableModel edited = before.EditableOverride!;
        EditableModel retained = actual.EditableOverride!;
        Assert.Same(edited.Meshes, retained.Meshes);
        Assert.Same(edited.Textures, retained.Textures);
        Assert.Same(edited.Bones, retained.Bones);
        Assert.Equal(decoded.NativePoseSource!.Animations.ToArray(), actual.NativePoseSource!.Animations.ToArray());
        Assert.Equal(decoded.Animations.Count, retained.Animations.Count);
        Assert.All(retained.Meshes, mesh => Assert.True(mesh.Transparent));
        EditableModel expected = ModelExchange.FromBattle(decoded);
        for (int animation = 0; animation < expected.Animations.Count; animation++)
        {
            Assert.Equal(expected.Animations[animation].Duration, retained.Animations[animation].Duration);
            for (int channel = 0; channel < expected.Animations[animation].Channels.Count; channel++)
            {
                EditableAnimationChannel source = expected.Animations[animation].Channels[channel];
                EditableAnimationChannel target = retained.Animations[animation].Channels[channel];
                Assert.Equal(source.Bone + 1, target.Bone);
                Assert.Equal(source.Times, target.Times);
                Assert.Equal(source.Values, target.Values);
            }
        }
        Assert.Equal(actual.Meshes.Count, actual.Pose(0, 0).Count);
        Assert.Equal(actual.Meshes.Count, actual.PoseNative(actual.NativePoseSource.Sample(0, 1, 0)).Count);
    }

    private sealed class CaptureOverrides : IModelOverrideSource
    {
        public List<BattleModel> Banks { get; } = [];
        public FieldModel Apply(string id, FieldModel model) => model;
        public BattleModel Apply(string id, BattleModel model) => model;
        public BattleStage Apply(string id, BattleStage model) => model;
        public WorldStaticModel Apply(string id, WorldStaticModel model) => model;
        public BattleModel ApplyBattleBank(string id, string bank, BattleModel model)
        {
            Banks.Add(model);
            return model;
        }
    }
}

