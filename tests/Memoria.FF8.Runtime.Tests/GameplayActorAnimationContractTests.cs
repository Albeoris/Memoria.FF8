using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplayActorAnimationContractTests
{
    [Fact]
    public void Older_explicit_end_documents_keep_their_meaning_without_the_optional_final_pose_flag()
    {
        GameplaySaveActorAnimation original = new(1, 64, 16, 0, GameplaySaveActorAnimationFlags.Complete | GameplaySaveActorAnimationFlags.Frozen, 0, 0, 0, 0, 0, 64);
        string json = JsonSerializer.Serialize(original);

        GameplaySaveActorAnimation restored = JsonSerializer.Deserialize<GameplaySaveActorAnimation>(json)!;

        restored.Validate();
        Assert.DoesNotContain("UseResourceLastPose", json, StringComparison.Ordinal);
        Assert.False(restored.UseResourceLastPose);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Declared_resource_relative_final_pose_survives_serialization_without_a_cached_end()
    {
        GameplaySaveActorAnimation original = new(1, 64, 16, 0, GameplaySaveActorAnimationFlags.Complete | GameplaySaveActorAnimationFlags.Frozen, 0, 0, 0, 0, 0) { UseResourceLastPose = true };

        GameplaySaveActorAnimation restored = JsonSerializer.Deserialize<GameplaySaveActorAnimation>(JsonSerializer.Serialize(original))!;

        restored.Validate();
        Assert.Equal(original, restored);
        Assert.Null(restored.End);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Resource_relative_final_pose_cannot_replace_unfinished_or_unknown_playback(bool complete, bool frozen, bool knownPhase)
    {
        GameplaySaveActorAnimationFlags flags = (complete ? GameplaySaveActorAnimationFlags.Complete : 0) | (frozen ? GameplaySaveActorAnimationFlags.Frozen : 0);
        GameplaySaveActorAnimation value = new(1, knownPhase ? (ushort)64 : null, 16, 0, flags, 0, 0, 0, 0, 0) { UseResourceLastPose = true };

        Assert.Throws<InvalidDataException>(value.Validate);
    }

    [Fact]
    public void Resource_relative_final_pose_cannot_override_an_explicit_authored_end()
    {
        GameplaySaveActorAnimation value = new(1, 64, 16, 0, GameplaySaveActorAnimationFlags.Complete | GameplaySaveActorAnimationFlags.Frozen, 0, 0, 0, 0, 0, 64) { UseResourceLastPose = true };

        Assert.Throws<InvalidDataException>(value.Validate);
    }
}
