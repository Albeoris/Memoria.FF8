using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRenzokukenMetadataTests
{
    [Fact]
    public void Embedded_Renzokuken_body_and_linked_weapon_reuse_geometry_and_decode_their_own_animation_sections()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleModel body = assets.LoadPlayer(0);
        BattleModel weapon = assets.ResolveWeaponModel(0, game.ReadNewGameCharacterVitals().Records[0].Weapon, body) ?? throw new InvalidDataException("Installed Squall weapon has no linked model.");

        foreach (var resource in game.ReadNativeRenzokukenResources())
        {
            BattleRenzokukenModels changed = assets.ReadNativeRenzokukenModels(resource, body, weapon);

            Assert.Same(body.Bones, changed.Body.Bones);
            Assert.Same(body.Meshes, changed.Body.Meshes);
            Assert.Same(weapon.Bones, changed.Weapon!.Bones);
            Assert.Same(weapon.Meshes, changed.Weapon.Meshes);
            Assert.Same(weapon.Sequences, changed.Weapon.Sequences);
            Assert.NotNull(changed.Body.NativePoseSource);
            Assert.NotNull(changed.Weapon.NativePoseSource);
            Assert.Equal(body.NativePoseSource!.Skeleton, changed.Body.NativePoseSource.Skeleton);
            Assert.Equal(weapon.NativePoseSource!.Skeleton, changed.Weapon.NativePoseSource.Skeleton);
            Assert.False(body.NativePoseSource.Animations.SequenceEqual(changed.Body.NativePoseSource.Animations));
            Assert.False(weapon.NativePoseSource.Animations.SequenceEqual(changed.Weapon.NativePoseSource.Animations));
            NativeBattleGraphicsResource bound = resource.BindGraphicsResource(body.NativePoseSource);
            Assert.Equal(changed.Body.NativePoseSource.Animations, bound.PoseSource!.Animations);
            Assert.Equal(body.Bones.Count, bound.PoseSource.Sample(0, 1, 0).Bones.Length);
            Assert.Equal(weapon.Bones.Count, changed.Weapon.NativePoseSource.Sample(0, 1, 0).Bones.Length);
            Assert.Equal(resource.AnimationFrameCounts, changed.Body.Animations.Select(animation => (byte)animation.Frames.Count));
            Assert.Equal(changed.Body.Animations.Count, changed.Weapon.Animations.Count);
            Assert.NotEmpty(changed.Body.Pose(0, 0));
            Assert.NotEmpty(changed.Weapon.Pose(0, 0));
        }
    }

    [Fact]
    public void Native_effect_services_replace_the_actor_resource_and_supply_four_through_eight_contacts()
    {
        using GameAssets game = new(TestResources.GameDirectory);

        var resources = game.ReadNativeRenzokukenResources();

        Assert.Equal(16, resources.Length);
        Assert.Equal(new[] { 4, 5, 6, 7, 8 }, resources.Take(5).Select(resource => resource.Timeline.InputThresholds.Length));
        Assert.Equal(new ushort[] { 160, 141, 259, 332, 333, 159, 161, 334, 335, 336, 337, 339, 340, 341, 342, 343 }, resources.Select(resource => resource.MagicId));
        foreach (var resource in resources)
        {
            Assert.True(resource.GraphicsResource.Contains(1));
            Assert.True(resource.GraphicsResource.Contains(2));
            Assert.Equal((byte)1, resource.ShowBar);
            Assert.Equal(resource.Timeline.InputThresholds.Length + 1, resource.Segments.Length);
            Assert.True(resource.Timeline.TotalPoseFrames > 0);
            BattleSequenceSet programs = new(resource.GraphicsResource.SequenceSection.ToArray());
            Assert.Equal(resource.Segments, programs.ReadRenzokukenBlock(1, resource.AbOffset, resource.AnimationFrameCounts).Segments);
        }
    }

    [Fact]
    public void Every_installed_enemy_selector_has_its_original_actor_and_target_resource_banks()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        Dictionary<ushort, BattleNativeRenzokukenResource> resources = game.ReadNativeRenzokukenResources().ToDictionary(resource => resource.MagicId);
        HashSet<ushort> selected = [];
        for (int model = 0; model < 144; model++)
        {
            foreach (ushort magic in assets.ReadNativeRenzokukenMagicIds(model).Skip(2))
            {
                Assert.True(resources.ContainsKey(magic), $"Enemy model {model} selects unavailable Renzokuken resource {magic}.");
                selected.Add(magic);
            }
        }
        Assert.Equal(resources.Keys.Order(), selected.Order());
        (int Model, ushort Effect, uint ActorAddress, uint TargetAddress)[] special =
        [
            (92, 159, 0xEA3A28, 0xEA5D5C),
            (91, 161, 0xE9B324, 0xE9DFD8),
            (67, 334, 0xE71864, 0xE74CB0),
            (77, 335, 0xE6CC48, 0xE6EC84),
            (90, 336, 0xE640FC, 0xE673E4),
            (117, 337, 0xE5DF7C, 0xE6093C),
            (112, 339, 0xE567AC, 0xE58FCC),
            (126, 340, 0xE5110C, 0xE53000),
            (84, 341, 0xE4F048, 0xE505F0),
            (124, 342, 0xE47704, 0xE49F98),
            (122, 343, 0xE426D0, 0xE4421C)
        ];
        foreach (var row in special)
        {
            Assert.All(assets.ReadNativeRenzokukenMagicIds(row.Model).Skip(2), magic => Assert.Equal(row.Effect, magic));
            BattleNativeRenzokukenResource actor = resources[row.Effect];
            Assert.Equal(row.ActorAddress, actor.ResourceAddress);
            Assert.Equal(row.Effect == 339 ? 2 : 1, actor.TargetResources.Length);
            BattleNativeRenzokukenTargetResource target = actor.TargetResources[0];
            Assert.Equal(row.TargetAddress, target.ResourceAddress);
            BattleModel original = assets.LoadEnemy(row.Model);
            NativeBattleGraphicsResource bound = target.BindGraphicsResource(original.NativePoseSource!);
            Assert.Equal(original.NativePoseSource!.Skeleton, bound.PoseSource!.Skeleton);
            Assert.Equal(target.AnimationFrameCounts, NativeBattlePoseDecoder.ReadFrameCounts(bound.PoseSource.Animations.AsSpan()));
            Assert.True(bound.Contains(1));
            Assert.Equal(original.Bones.Count, bound.PoseSource.Sample(0, 1, 0).Bones.Length);
        }
        Assert.Equal(0xE5CC50u, resources[339].TargetResources[1].ResourceAddress);
        Assert.All(resources.Values.Where(resource => resource.MagicId is 160 or 141 or 259 or 332 or 333), resource => Assert.Empty(resource.TargetResources));
    }

    [Fact]
    public void Installed_target_selectors_and_Squall_entry_program_are_available_without_guessing_hit_count()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleModel body = assets.LoadPlayer(0);
        int weapon = game.ReadNewGameCharacterVitals().Records[0].Weapon;
        BattleSequenceSet sequences = assets.LoadPlayerSequences(0, weapon, body);
        ushort[] selectors = assets.ReadNativeRenzokukenMagicIds(61).ToArray();
        byte[] entry = sequences.ReadRawProgram(21).ToArray();

        Assert.Equal(7, selectors.Length);
        Assert.NotEmpty(entry);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RENZOKUKEN_PROBE_OUTPUT");
        if (output is not null)
        {
            var candidates = Enumerable.Range(1, 255).Where(sequences.Contains).Select(sequence => new { Sequence = sequence, Program = sequences.ReadRawProgram(sequence).ToArray() }).Where(program => program.Program.Contains((byte)0xAB)).Select(program => new { program.Sequence, Hex = Convert.ToHexString(program.Program) }).ToArray();
            File.WriteAllText(output, JsonSerializer.Serialize(new { Enemy = 61, IndexedMagicSelectors = selectors, Character = 0, Weapon = weapon, EntrySequence = 21, EntryHex = Convert.ToHexString(entry), PoseFrames = body.Animations.Select(animation => animation.Frames.Count).ToArray(), UnclassifiedAbByteCandidatePrograms = candidates }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void Reached_AB_scan_owns_initial_one_BYTE_wrap_segments_and_original_frame_counts()
    {
        BattleSequenceSet sequences = new([1, 0, 4, 0, 0xAB, 1, 0, 0xA0, 1, 0xA0, 2, 0xA1, 0xA2]);
        BattleAnimation[] animations = [Animation(20), Animation(14), Animation(8)];

        BattleNativeRenzokukenBlock block = sequences.ReadRenzokukenBlock(1, 0, animations);

        Assert.Equal((byte)1, block.ShowBar);
        Assert.Equal(new byte[] { 21, 14, 8 }, block.Segments);
        Assert.Equal(new short[] { 65, 121 }, block.Timeline.InputThresholds);
        Assert.Equal((short)45, block.Timeline.TotalPoseFrames);
        Assert.Equal(8, block.EndOffset);
        BattleSequenceSet wrapped = new([1, 0, 4, 0, 0xAB, 0, 0, 1, 0xA0, 2, 0xA1]);
        BattleNativeRenzokukenBlock overflow = wrapped.ReadRenzokukenBlock(1, 0, [Animation(255), Animation(2), Animation(8)]);
        Assert.Equal(new byte[] { 2, 8 }, overflow.Segments);
        Assert.Throws<InvalidDataException>(() => sequences.ReadRenzokukenBlock(1, 1, animations));
    }

    private static BattleAnimation Animation(int frames)
    {
        return new(Enumerable.Repeat(new BattlePose(Vector3.Zero, [], []), frames).ToArray());
    }
}
