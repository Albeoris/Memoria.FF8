using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverPreflightParticipantSnapshotTests
{
    [Fact]
    public void One_preflight_reads_each_host_and_native_binding_once_and_a_new_probe_reads_again()
    {
        CrossoverNativePreflightParticipants snapshot = new();
        BattleModelOwner targetOwner = new BattleForeignEnemyModel(new("target"));
        BattleModelOwner participantOwner = new BattleForeignEnemyModel(new("participant"));
        BattleModelOwner reboundOwner = new BattleForeignEnemyModel(new("rebound"));
        int reads = 0;
        NativeBattleForeignActor Read(BattleModelOwner owner)
        {
            reads++;
            return Actor(owner, checked((short)reads));
        }
        NativeBattleForeignActor target = CrossoverNativePreflightParticipants.Read(snapshot, 4, targetOwner, () => Read(targetOwner));
        NativeBattleForeignActor participant = CrossoverNativePreflightParticipants.Read(snapshot, 5, participantOwner, () => Read(participantOwner));
        for (int callback = 0; callback < 4096; callback++)
        {
            Assert.Same(target, CrossoverNativePreflightParticipants.Read(snapshot, 4, targetOwner, () => Read(targetOwner)));
            Assert.Same(participant, CrossoverNativePreflightParticipants.Read(snapshot, 5, participantOwner, () => Read(participantOwner)));
        }
        Assert.Equal(2, reads);
        Assert.NotSame(target, CrossoverNativePreflightParticipants.Read(snapshot, 4, reboundOwner, () => Read(reboundOwner)));
        Assert.NotSame(target, CrossoverNativePreflightParticipants.Read(snapshot, 6, targetOwner, () => Read(targetOwner)));
        Assert.Equal(4, reads);
        CrossoverNativePreflightParticipants nextProbe = new();
        NativeBattleForeignActor refreshed = CrossoverNativePreflightParticipants.Read(nextProbe, 4, targetOwner, () => Read(targetOwner));
        Assert.NotSame(target, refreshed);
        Assert.Equal(5, refreshed.Anchor.X);
        Assert.Equal(5, reads);
    }

    [Fact]
    public void Null_snapshot_provider_path_refreshes_the_pose_on_every_callback()
    {
        BattleModelOwner targetOwner = new BattleForeignEnemyModel(new("target"));
        int reads = 0;
        NativeBattleForeignActor Read()
        {
            reads++;
            return Actor(targetOwner, checked((short)reads));
        }
        NativeBattleForeignActor first = CrossoverNativePreflightParticipants.Read(null, 4, targetOwner, Read);
        NativeBattleForeignActor second = CrossoverNativePreflightParticipants.Read(null, 4, targetOwner, Read);
        Assert.Equal(2, reads);
        Assert.NotSame(first, second);
        Assert.Equal(1, first.Anchor.X);
        Assert.Equal(2, second.Anchor.X);
    }

    [Fact]
    public void Snapshot_is_lazy_and_does_not_replace_or_cache_provider_failures()
    {
        CrossoverNativePreflightParticipants snapshot = new();
        BattleModelOwner targetOwner = new BattleForeignEnemyModel(new("target"));
        int reads = 0;
        NotSupportedException expected = new("Original participant geometry is unavailable.");
        NativeBattleForeignActor Read()
        {
            reads++;
            if (reads == 1)
            {
                throw expected;
            }
            return Actor(targetOwner, 7);
        }
        Assert.Equal(0, reads);
        Assert.Same(expected, Assert.Throws<NotSupportedException>(() => CrossoverNativePreflightParticipants.Read(snapshot, 4, targetOwner, Read)));
        NativeBattleForeignActor actor = CrossoverNativePreflightParticipants.Read(snapshot, 4, targetOwner, Read);
        Assert.Same(actor, CrossoverNativePreflightParticipants.Read(snapshot, 4, targetOwner, Read));
        Assert.Equal(2, reads);
    }

    private static NativeBattleForeignActor Actor(BattleModelOwner owner, short position)
    {
        NativeBattleVector point = new(position, 0, 0);
        return new(owner, new(point, point, 0, 0, 4096), new(position, 0, 0, 0), (_, _) => point);
    }
}
