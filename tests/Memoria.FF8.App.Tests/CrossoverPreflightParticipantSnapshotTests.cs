using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverPreflightParticipantSnapshotTests
{
    [Fact]
    public void One_preflight_reads_each_host_and_native_binding_once_and_a_new_probe_reads_again()
    {
        CrossoverNativePreflightParticipants snapshot = new();
        int reads = 0;
        NativeBattleForeignActor Read(int index)
        {
            reads++;
            return Actor(index, checked((short)reads));
        }
        NativeBattleForeignActor target = CrossoverNativePreflightParticipants.Read(snapshot, 4, 3, () => Read(3));
        NativeBattleForeignActor participant = CrossoverNativePreflightParticipants.Read(snapshot, 5, 4, () => Read(4));
        for (int callback = 0; callback < 4096; callback++)
        {
            Assert.Same(target, CrossoverNativePreflightParticipants.Read(snapshot, 4, 3, () => Read(3)));
            Assert.Same(participant, CrossoverNativePreflightParticipants.Read(snapshot, 5, 4, () => Read(4)));
        }
        Assert.Equal(2, reads);
        Assert.NotSame(target, CrossoverNativePreflightParticipants.Read(snapshot, 4, 5, () => Read(5)));
        Assert.NotSame(target, CrossoverNativePreflightParticipants.Read(snapshot, 6, 3, () => Read(3)));
        Assert.Equal(4, reads);
        CrossoverNativePreflightParticipants nextProbe = new();
        NativeBattleForeignActor refreshed = CrossoverNativePreflightParticipants.Read(nextProbe, 4, 3, () => Read(3));
        Assert.NotSame(target, refreshed);
        Assert.Equal(5, refreshed.Anchor.X);
        Assert.Equal(5, reads);
    }

    [Fact]
    public void Null_snapshot_provider_path_refreshes_the_pose_on_every_callback()
    {
        int reads = 0;
        NativeBattleForeignActor Read()
        {
            reads++;
            return Actor(3, checked((short)reads));
        }
        NativeBattleForeignActor first = CrossoverNativePreflightParticipants.Read(null, 4, 3, Read);
        NativeBattleForeignActor second = CrossoverNativePreflightParticipants.Read(null, 4, 3, Read);
        Assert.Equal(2, reads);
        Assert.NotSame(first, second);
        Assert.Equal(1, first.Anchor.X);
        Assert.Equal(2, second.Anchor.X);
    }

    [Fact]
    public void Snapshot_is_lazy_and_does_not_replace_or_cache_provider_failures()
    {
        CrossoverNativePreflightParticipants snapshot = new();
        int reads = 0;
        NotSupportedException expected = new("Original participant geometry is unavailable.");
        NativeBattleForeignActor Read()
        {
            reads++;
            if (reads == 1)
            {
                throw expected;
            }
            return Actor(3, 7);
        }
        Assert.Equal(0, reads);
        Assert.Same(expected, Assert.Throws<NotSupportedException>(() => CrossoverNativePreflightParticipants.Read(snapshot, 4, 3, Read)));
        NativeBattleForeignActor actor = CrossoverNativePreflightParticipants.Read(snapshot, 4, 3, Read);
        Assert.Same(actor, CrossoverNativePreflightParticipants.Read(snapshot, 4, 3, Read));
        Assert.Equal(2, reads);
    }

    private static NativeBattleForeignActor Actor(int index, short position)
    {
        NativeBattleVector point = new(position, 0, 0);
        return new(index, new(point, point, 0, 0, 4096), new(position, 0, 0, 0), (_, _) => point);
    }
}
