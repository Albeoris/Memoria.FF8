using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class ModelLinkedImportIntegrationTests
{
    [Fact]
    public void BlenderBodyClipOnlyInstallsIntoNativeGameplayWithoutChangingOtherSlotsOrWeapon()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_LINKED_MODEL_GLB");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(File.Exists(source) && Directory.Exists(game), "Set optional game and Blender-returned linked GLB inputs.");
        ModelAssetImportInventory inventory = ModelAssetExchange.InspectGlb(File.ReadAllBytes(source));
        ModelAssetObject body = inventory.Objects.Single(item => item.Target.StartsWith("battle/d0c", StringComparison.Ordinal));
        ModelAssetObject weapon = inventory.Objects.First(item => item.Target.StartsWith("battle/d0w", StringComparison.Ordinal));
        ModelAssetClip clip = inventory.Clips.First(item => item.ModelTarget == body.Target && item.Target.Contains("/embedded/", StringComparison.Ordinal));
        int slot = int.Parse(clip.Target[(clip.Target.LastIndexOf('/') + 1)..]);
        using BattleAssets originals = new(game);
        BattleModel original = originals.LoadModel(body.Target[7..]);
        int weaponId = int.Parse(weapon.Target.AsSpan(10, 3));
        BattleModel originalWeapon = originals.LoadWeapon(0, weaponId, original);
        EditableModel target = ModelExchange.FromBattle(original);
        int rootJoint = ModelExchange.NativeJointOrder(body.Model).First(index => body.Model.Bones[index].Parent < 0 || !body.Model.Bones[body.Model.Bones[index].Parent].IsJoint);
        EditableAnimation edited = clip.Animation with
        {
            Channels = clip.Animation.Channels.Select(channel => channel.Bone == rootJoint && channel.Path == "translation" ? channel with { Values = channel.Values.Select(value => value + new Vector4(2, 0, 0, 0)).ToArray() } : channel).ToArray()
        };
        ModelModReplacement replacement = new(body.Target, body.Model, false, [new("embedded", slot, edited)]);
        EditableModel preview = ModelMods.Preview(target, replacement);
        Assert.Same(target.Meshes, preview.Meshes);
        for (int index = 0; index < target.Animations.Count; index++)
        {
            if (index != slot)
            {
                Assert.Same(target.Animations[index], preview.Animations[index]);
            }
        }
        ModelModInstallPlan plan = ModelModInstaller.Plan([replacement]);
        Assert.Single(plan.Files);
        Assert.EndsWith($".clips/embedded/{slot}.glb", plan.Files[0].RelativePath);
        string workspace = Path.Combine(Path.GetTempPath(), "memoria-linked-import-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(workspace, "Mods");
        string allowed = Path.Combine(mods, "Memoria");
        string package = Path.Combine(allowed, "linked-test");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "mod.ini"), "[Mod]\nEnabled=1\nPriority=19\n");
        File.WriteAllText(Path.Combine(package, "unrelated.txt"), "retain");
        try
        {
            ModelModInstaller.Install(allowed, package, plan);
            Assert.Equal("retain", File.ReadAllText(Path.Combine(package, "unrelated.txt")));
            Assert.Contains("Priority=19", File.ReadAllText(Path.Combine(package, "mod.ini")));
            using GameResourceHost host = new(game, workspace, watch: false, modsDirectory: mods);
            Assert.Contains(host.ModelMods.Variants, variant => variant.ModelId == body.Target && variant.ClipSlot == slot);
            using BattleAssets runtime = new(game, host.Resources, host);
            BattleModel changed = runtime.LoadModel(body.Target[7..]);
            BattleModel untouchedWeapon = runtime.LoadWeapon(0, weaponId, changed);
            Assert.NotNull(changed.NativePoseSource);
            Assert.NotNull(original.NativePoseSource);
            Assert.NotEqual(original.NativePoseSource.Animations, changed.NativePoseSource.Animations);
            Assert.Equal(original.NativePoseSource.Skeleton, changed.NativePoseSource.Skeleton);
            NativeBattleDecodedPose nativeBefore = original.NativePoseSource.Sample((byte)slot, 1, 0);
            NativeBattleDecodedPose nativeAfter = changed.NativePoseSource.Sample((byte)slot, 1, 0);
            Assert.NotEqual(nativeBefore.Translation, nativeAfter.Translation);
            Assert.Equal(original.Animations.Count, changed.Animations.Count);
            Assert.Equal(originalWeapon.NativePoseSource!.Animations, untouchedWeapon.NativePoseSource!.Animations);
            for (int index = 0; index < original.Animations.Count; index++)
            {
                if (index == slot)
                {
                    continue;
                }
                NativeBattleDecodedPose nativeOriginal = original.NativePoseSource.Sample((byte)index, 1, 0);
                NativeBattleDecodedPose nativeUnchanged = changed.NativePoseSource.Sample((byte)index, 1, 0);
                Assert.Equal(nativeOriginal.Translation, nativeUnchanged.Translation);
                Assert.Equal(nativeOriginal.Bones, nativeUnchanged.Bones);
                Assert.Equal(original.Animations[index].Frames.Count, changed.Animations[index].Frames.Count);
                for (int frame = 0; frame < original.Animations[index].Frames.Count; frame++)
                {
                    BattlePose before = original.Animations[index].Frames[frame];
                    BattlePose after = changed.Animations[index].Frames[frame];
                    Assert.Equal(before.Translation, after.Translation);
                    Assert.Equal(before.Rotations, after.Rotations);
                    Assert.Equal(before.Scales, after.Scales);
                }
            }
            Assert.NotEqual(original.Animations[slot].Frames[0].Translation, changed.Animations[slot].Frames[0].Translation);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }
    [Fact]
    public void ClipOnlyPreviewRemapsIdentityHelpersAndRetainsUnselectedGeometryAndSlots()
    {
        EditableBone root = new("bone_0", -1, Vector3.Zero, Quaternion.Identity, Vector3.One) { NativeIndex = 0 };
        EditableBone child = new("bone_1", 0, Vector3.UnitY, Quaternion.Identity, Vector3.One) { NativeIndex = 1 };
        EditableAnimation idle = new("animation_0", [new(0, "translation", [0], [Vector4.Zero])]);
        EditableAnimation other = new("animation_1", [new(1, "rotation", [0], [new(0, 0, 0, 1)])]);
        EditableModel original = new("target", "battle", [root, child], [], [], [idle, other], JsonSerializer.SerializeToElement(new { Bones = new[] { new BattleBone(-1, 1), new BattleBone(0, 1) }, Scale = 1f }));
        EditableModel incoming = original with
        {
            Bones = [new EditableBone("exporter_root", -1, Vector3.Zero, Quaternion.Identity, Vector3.One, false), root with { Parent = 0 }, child with { Parent = 1 }],
            Animations = []
        };
        EditableAnimation selected = new("qualified clip", [new(1, "translation", [0], [new(2, 0, 0, 0)])]);
        EditableModel preview = ModelMods.Preview(original, new("battle/d0c000.dat", incoming, false, [new("embedded", 0, selected)]));
        Assert.Same(original.Bones, preview.Bones);
        Assert.Same(original.Meshes, preview.Meshes);
        Assert.Same(other, preview.Animations[1]);
        Assert.Equal(0, preview.Animations[0].Channels[0].Bone);
        Assert.Equal(new Vector4(2, 0, 0, 0), preview.Animations[0].Channels[0].Values[0]);
    }

}
