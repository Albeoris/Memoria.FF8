using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRenzokukenMetadataTests
{
    [Fact]
    public void Native_effect_services_replace_the_actor_resource_and_supply_four_through_eight_contacts()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);

        var resources = game.ReadNativeRenzokukenResources();

        Assert.Equal(5, resources.Length);
        Assert.Equal(new[] { 4, 5, 6, 7, 8 }, resources.Select(resource => resource.Timeline.InputThresholds.Length));
        Assert.Equal(new ushort[] { 160, 141, 259, 332, 333 }, resources.Select(resource => resource.MagicId));
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
    public void Installed_target_selectors_and_Squall_entry_program_are_available_without_guessing_hit_count()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
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
