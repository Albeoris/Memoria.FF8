using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect83OwnedModelSourceTests
{
    [Fact]
    public void First_explicit_cold_publication_stages_the_complete_original_owner()
    {
        NativeBattleEffectOwnedModelResource resource = Source();
        NativeBattleEffect83Frame frame = Frame(NativeBattleEffect83State.FromColdProcess());
        NativeBattleEffect83Frame published = NativeBattleEffect83Models.BindOriginalSource(frame, resource);
        Assert.True(published.State.OwnedModelSourceBound);
        Assert.Equal(resource.Bytes.ToArray(), published.Staging!.Read(resource.Address, resource.Bytes.Length));
        Assert.False(frame.State.OwnedModelSourceBound);
        Assert.Throws<NotSupportedException>(() => frame.Staging!.Read(resource.Address, 1));
    }

    [Fact]
    public void Reentry_and_json_restore_preserve_current_pose_and_matrix_bytes()
    {
        NativeBattleEffectOwnedModelResource resource = Source();
        NativeBattleEffect83Frame first = NativeBattleEffect83Models.BindOriginalSource(Frame(NativeBattleEffect83State.FromColdProcess()), resource);
        first = first with { Staging = first.Staging!.Write(resource.Address + 25, [0x72, 0xA4]) };
        NativeBattleEffect83State restoredState = JsonSerializer.Deserialize<NativeBattleEffect83State>(JsonSerializer.Serialize(first.State))!;
        NativeBattleFileStaging restoredStaging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(first.Staging))!;
        NativeBattleEffect83Frame restored = first with { State = restoredState, Staging = restoredStaging };
        NativeBattleEffect83Frame rebound = NativeBattleEffect83Models.BindOriginalSource(restored, resource);
        Assert.Same(restored, rebound);
        Assert.Equal(new byte[] { 0x72, 0xA4 }, rebound.Staging!.Read(resource.Address + 25, 2));
        Assert.True(rebound.State.OwnedModelSourceBound);
    }

    [Fact]
    public void Unknown_origin_and_missing_current_staging_are_not_reconstructed()
    {
        NativeBattleEffectOwnedModelResource resource = Source();
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect83Models.BindOriginalSource(Frame(NativeBattleEffect83State.FromUnknown()), resource));
        NativeBattleEffect83Frame boundUnknown = Frame(NativeBattleEffect83State.FromUnknown() with { OwnedModelSourceBound = true });
        NativeBattleEffect83Frame retained = NativeBattleEffect83Models.BindOriginalSource(boundUnknown, resource);
        Assert.Same(boundUnknown, retained);
        Assert.Throws<NotSupportedException>(() => retained.Staging!.Read(resource.Address + 1, 1));
    }

    private static NativeBattleEffect83Frame Frame(NativeBattleEffect83State state) => new(NativeBattleEffectMemory.FromColdProcess(), null!, new(1), state) { Staging = NativeBattleFileStaging.Unknown() };
    private static NativeBattleEffectOwnedModelResource Source() => new(NativeBattleEffect83Profile.ModelAddress, Enumerable.Range(0, NativeBattleEffect83Profile.ModelLength).Select(index => unchecked((byte)(index * 17 + 3))).ToImmutableArray(), null!, []);
}
