using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModelModInstalledTests
{
    [Fact]
    public void InstalledWideMotionClipKeepsNativeJointMatricesAfterSelectedReencoding()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Set MEMORIA_FF8_GAME_DIR to run the optional installed resource check.");
        using BattleAssets assets = new(game!);
        BattleModel original = assets.LoadPlayer(0, 0);
        int slot = Enumerable.Range(0, original.Animations.Count).MaxBy(index => original.Animations[index].Frames.Max(frame => frame.Rotations.Zip(original.Animations[index].Frames[0].Rotations).Sum(pair => 1 - Math.Abs(Quaternion.Dot(Rotation(pair.First), Rotation(pair.Second))))));
        Assert.True(original.Animations[slot].Frames.Count > 2);
        EditableModel editable = ModelExchange.FromBattle(original);
        EditableAnimation changed = editable.Animations[slot] with { Channels = editable.Animations[slot].Channels.Select(channel => channel.Path == "translation" && editable.Bones[channel.Bone].Parent < 0 ? channel with { Values = channel.Values.Select(value => value + new Vector4(original.Scale / 100, 0, 0, 0)).ToArray() } : channel).ToArray() };
        ModelModReplacement replacement = new("battle/" + original.Name, editable, false, [new("embedded", slot, changed)]);
        _ = ModelMods.Preview(editable, replacement);
        ModelModInstallPlan plan = ModelModInstaller.Plan([replacement]);
        ModFile[] files = plan.Files.Select(file => new ModFile("sample", ModFormat.Memoria, file.RelativePath, 1, () => file.Content)).ToArray();
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out _));
        BattleModel result = mods.Apply("battle/" + original.Name, original);
        int count = original.Animations[slot].Frames.Count;
        foreach (int frame in new[] { 0, count / 2, count - 1 }.Distinct())
        {
            NativeBattleDecodedPose expected = original.NativePoseSource!.Sample(checked((byte)slot), checked((byte)(frame + 1)), 0);
            NativeBattleDecodedPose actual = result.NativePoseSource!.Sample(checked((byte)slot), checked((byte)(frame + 1)), 0);
            Assert.InRange(Math.Abs(actual.Translation.X - (expected.Translation.X - 1)), 0, 1);
            Assert.InRange(Math.Abs(actual.Translation.Y - expected.Translation.Y), 0, 1);
            Assert.InRange(Math.Abs(actual.Translation.Z - expected.Translation.Z), 0, 1);
            for (int bone = 0; bone < expected.Bones.Length; bone++)
            {
                Matrix4x4 a = Matrix(expected.Bones[bone]);
                Matrix4x4 b = Matrix(actual.Bones[bone]);
                foreach (Vector3 axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
                {
                    Assert.True(Vector3.Distance(Vector3.TransformNormal(axis, a), Vector3.TransformNormal(axis, b)) < 0.005f, $"Native reencoding changed clip {slot}, frame {frame}, bone {bone} beyond angular/scale quantization tolerance.");
                }
                Assert.InRange(Math.Abs(actual.Bones[bone].Scale.X - expected.Bones[bone].Scale.X), 0, 1);
                Assert.InRange(Math.Abs(actual.Bones[bone].Scale.Y - expected.Bones[bone].Scale.Y), 0, 1);
                Assert.InRange(Math.Abs(actual.Bones[bone].Scale.Z - expected.Bones[bone].Scale.Z), 0, 1);
            }
        }

        static Quaternion Rotation(Vector3 angles) => Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationZ(angles.Z) * Matrix4x4.CreateRotationX(-angles.X) * Matrix4x4.CreateRotationY(-angles.Y));
        static Matrix4x4 Matrix(NativeBattleBonePose bone)
        {
            Vector3 angles = new Vector3(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z) * (MathF.Tau / 4096);
            return Matrix4x4.CreateScale(new Vector3(bone.Scale.X, bone.Scale.Y, bone.Scale.Z) / 1024) * Matrix4x4.CreateFromQuaternion(Rotation(angles));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void InstalledReducedGeometryRoundTripRetainsSuppressedBodyPartAsExplicitContext(int character)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Set MEMORIA_FF8_GAME_DIR to run the optional installed resource check.");
        using GameAssets discovery = new(game!);
        ModelCatalogEntry selected = discovery.EnumerateModelCatalog().First(entry => entry.ResourcePath.StartsWith($"battle/d{character:x}c", StringComparison.OrdinalIgnoreCase));
        ModelAssetDiscoveryItem[] items = discovery.DiscoverModelAssets(selected, TestContext.Current.CancellationToken).ToArray();
        ModelAssetDiscoveryItem body = items.First(item => item.Kind == ModelAssetDiscoveryKind.Object && item.OwnerId == selected.Id);
        ModelAssetDiscoveryItem weapon = items.First(item => item.Kind == ModelAssetDiscoveryKind.Object && item.UsesOwnerSkeleton);
        Assert.Equal("mesh/1", weapon.ReplacementPart);
        EditableModel originalBody = body.Load!();
        EditableModel originalWeapon = weapon.Load!();
        ModelAssetImportInventory imported = ModelAssetExchange.InspectGlb(ModelAssetExchange.ExportGlb(new([
            new(body.OwnerId, "original", "body", "body-rig", originalBody),
            new(weapon.Id, "original", "weapon", "body-rig", originalWeapon, true, "alternatives", weapon.ReplacementPart)
        ], [])));
        EditableModel incomingBody = imported.Objects.Single(item => item.Target == body.OwnerId).Model;
        Assert.DoesNotContain(incomingBody.Meshes, mesh => mesh.NativePart == 1);
        ModelModReplacement bodyReplacement = new(body.OwnerId, incomingBody, true, []) { PreserveNativeParts = [1] };
        EditableModel preview = ModelMods.Preview(originalBody, bodyReplacement);
        Assert.Contains(preview.Meshes, mesh => mesh.NativePart == 1);
        EditableModel incomingWeapon = imported.Objects.Single(item => item.Target == weapon.Id).Model;
        EditableModel weaponPreview = ModelMods.Preview(originalWeapon, new(weapon.Id, incomingWeapon, true, []));
        ModelModInstallPlan plan = ModelModInstaller.Plan([bodyReplacement with { Model = preview }, new(weapon.Id, weaponPreview, true, [])]);
        ModFile[] files = plan.Files.Select(file => new ModFile("sample", ModFormat.Memoria, file.RelativePath, 1, () => file.Content)).ToArray();
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out _));
        using BattleAssets assets = new(game!, modelOverrides: mods);
        BattleModel model = assets.LoadModel(Path.GetFileName(selected.ResourcePath));
        Assert.Contains(model.EditableOverride!.Meshes, mesh => mesh.NativePart == 1);
        int weaponId = assets.WeaponDefinitions.First(item => item.CharacterId == character).Id;
        NativeBattleGraphicsResource native = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = character, Costume = 0, WeaponId = weaponId });
        Assert.True(native.LinkedGeometryUsesBodySkeleton);
        Assert.Equal(2, plan.Files.Count);
        EditableModel composed = ModelAssetExchange.Compose(new([
            new(body.OwnerId, "resolved", "body", "body-rig", model.EditableOverride),
            new(weapon.Id, "resolved", "weapon", "body-rig", weaponPreview, true, "alternatives", weapon.ReplacementPart)
        ], []));
        Assert.DoesNotContain(composed.Meshes, mesh => mesh.ObjectTarget == body.OwnerId && mesh.NativePart == 1);
    }

    [Fact]
    public void InstalledGeometryOnlyRetainsNativeControlDataAndUsesEditedRenderVertices()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Set MEMORIA_FF8_GAME_DIR to run the optional installed resource check.");
        using BattleAssets originals = new(game!);
        BattleModel original = originals.LoadPlayer(0, 0);
        EditableModel editable = ModelExchange.FromBattle(original);
        EditableModel edited = editable with { Meshes = editable.Meshes.Select(mesh => mesh with { Vertices = mesh.Vertices.Select(vertex => vertex with { Position = vertex.Position * 1.1f }).ToArray() }).ToArray() };
        ModelModInstallPlan plan = ModelModInstaller.Plan([new("battle/" + original.Name, edited, true, [])]);
        ModFile[] files = plan.Files.Select(file => new ModFile("sample", ModFormat.Memoria, file.RelativePath, 1, () => file.Content)).ToArray();
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out _));
        using BattleAssets assets = new(game!, modelOverrides: mods);
        BattleModel modified = assets.LoadPlayer(0, 0);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0, Costume = 0 });
        Assert.NotNull(resource.Geometry);
        Assert.Equal(original.NativePoseSource!.Animations.ToArray(), resource.PoseSource!.Animations.ToArray());
        Assert.NotNull(modified.NativeControlMeshes);
        Assert.NotEqual(original.Pose(0, 0).SelectMany(mesh => mesh).First(), modified.Pose(0, 0).SelectMany(mesh => mesh).First());
        NativeBattleModelDatSource staged = assets.ReadNativeModelDatSource(0, false, 0);
        Assert.Equal(originals.ReadNativeModelDatSource(0, false, 0).Bytes.ToArray(), staged.Bytes.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void InstalledEmbeddedAndExternalOverridesReachNativeGameplayResourceBoundaries(int character)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Set MEMORIA_FF8_GAME_DIR to run the optional installed resource check.");
        using GameAssets discovery = new(game!);
        ModelCatalogEntry selected = discovery.EnumerateModelCatalog().First(entry => entry.ResourcePath.StartsWith($"battle/d{character:x}c", StringComparison.OrdinalIgnoreCase));
        ModelAssetDiscoveryItem[] discovered = discovery.DiscoverModelAssets(selected, TestContext.Current.CancellationToken).ToArray();
        ModelAssetDiscoveryItem[] clips = discovered.Where(item => item.Kind == ModelAssetDiscoveryKind.Clip && item.OwnerId == selected.Id && item.Error is null).ToArray();
        ModelAssetDiscoveryItem[] banks = clips.GroupBy(item => item.Bank).Select(group => group.First()).ToArray();
        if (character == 0)
        {
            Assert.Contains(banks, bank => bank.Bank == "renzokuken-160");
            Assert.Contains(banks, bank => bank.Bank == "finisher-165");
        }
        else if (character == 9)
        {
            Assert.Contains(banks, bank => bank.Bank.StartsWith("victory-", StringComparison.Ordinal));
        }
        else if (character is 7 or 8 or 10)
        {
            string unresolved = character switch { 7 => "actor-228", 8 => "actor-261", _ => "actor-263" };
            Assert.Contains(discovered, item => item.Kind == ModelAssetDiscoveryKind.Diagnostic && item.Id.Contains(unresolved, StringComparison.Ordinal) && item.Error is not null);
            Assert.DoesNotContain(banks, bank => bank.Bank == unresolved);
        }
        else
        {
            string expected = character switch { 1 => "duel-112", 2 => "shot-188", 3 => "actor-71", 4 => "actor-86", 6 => "actor-200", _ => throw new InvalidOperationException() };
            Assert.Contains(banks, bank => bank.Bank == expected);
        }
        List<ModelModReplacement> replacements = [];
        Dictionary<string, EditableModel> originals = [];
        foreach (ModelAssetDiscoveryItem bank in banks)
        {
            EditableModel model = bank.Load!();
            originals.Add(bank.Bank, model);
            EditableAnimation animation = model.Animations[bank.ClipIndex];
            EditableAnimation changed = animation with { Channels = animation.Channels.Select(channel => channel.Path == "translation" && model.Bones[channel.Bone].Parent < 0 ? channel with { Values = channel.Values.Select(value => value + new Vector4(0.25f, 0, 0, 0)).ToArray() } : channel).ToArray() };
            ModelModReplacement replacement = new(selected.Id, model, false, [new(bank.Bank, bank.ClipIndex, changed)]);
            _ = ModelMods.Preview(model, replacement);
            replacements.Add(replacement);
        }
        ModelModInstallPlan plan = ModelModInstaller.Plan(replacements);
        ModFile[] files = plan.Files.Select(file => new ModFile("sample", ModFormat.Memoria, file.RelativePath, 1, () => file.Content)).ToArray();
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out var diagnostics), string.Join("; ", diagnostics.Select(item => item.Message)));
        using BattleAssets assets = new(game!, modelOverrides: mods);
        using BattleAssets originalAssets = new(game!);
        BattleCombatantDefinition actor = new() { IsPlayer = true, ModelId = character, WeaponId = assets.WeaponDefinitions.FirstOrDefault(weapon => weapon.CharacterId == character)?.Id, Costume = 0 };
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(actor);
        NativeBattleGraphicsResource originalResource = originalAssets.ReadNativeGraphicsResource(actor);
        Assert.NotEqual(originalResource.PoseSource!.Sample(0, 1, 0).Translation, resource.PoseSource!.Sample(0, 1, 0).Translation);
        Assert.Equal(banks.Length - 1, resource.AnimationBankOverrides.Length);
        foreach (NativeBattleAnimationBankOverride bank in resource.AnimationBankOverrides)
        {
            NativeBattlePoseSource source = System.Text.Json.JsonSerializer.Deserialize<NativeBattlePoseSource>(originals[bank.Bank].NativeMetadata.GetProperty("NativePoseSource"))!;
            NativeBattleGraphicsResource resolved = (resource with { PoseSource = source }).ApplyAnimationBankOverrides(bank.Bank);
            Assert.NotEqual(source.Sample(0, 1, 0).Translation, resolved.PoseSource!.Sample(0, 1, 0).Translation);
        }
        NativeBattleModelDatSource staged = assets.ReadNativeModelDatSource((byte)character, false, 0);
        NativeBattleModelDatSource originalStaged = originalAssets.ReadNativeModelDatSource((byte)character, false, 0);
        Assert.NotEqual(originalStaged.Bytes.Length, staged.Bytes.Length);
        Assert.Equal(resource.PoseSource.Animations.ToArray(), staged.Bytes.AsSpan().Slice(staged.Section(2), staged.Section(3) - staged.Section(2)).ToArray());
    }
}
