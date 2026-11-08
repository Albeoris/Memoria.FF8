using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class EffectComponentTerminalTextureTrackerTests
{
    [Fact]
    public void Resident_completion_requires_two_presentations_and_an_advanced_native_callback()
    {
        EffectComponentTerminalTextureTracker tracker = new();
        Assert.False(tracker.Observe(Observation(10), 20, 100));
        Assert.False(tracker.Observe(Observation(10), 21, 110));
        Assert.False(tracker.Observe(Observation(10), 22, 120));
        Assert.True(tracker.Observe(Observation(11), 23, 130));
        BattleComponentTerminalTextureCertificate certificate = tracker.Certificate(101, new('b', 64), new(1, 5, "party", BattleAbility.Magic, 1, 101, 101, 2), null);
        Assert.True(certificate.Completed);
        Assert.False(certificate.Drained);
        Assert.True(certificate.ResidentTasksRemain);
        Assert.Equal("terminal-texture-quiescence", certificate.CompletionKind);
        Assert.Equal(3, certificate.TailPresentedFrames);
        Assert.Equal(1, certificate.NativeCallbacksAdvanced);
        Assert.Equal(100, tracker.BoundaryTimestamp);
        Assert.Equal(certificate.First.ResidentFingerprint, certificate.Last.ResidentFingerprint);
    }

    [Fact]
    public void Changed_resident_bytes_or_other_owners_restart_the_observed_tail()
    {
        EffectComponentTerminalTextureTracker tracker = new();
        Assert.False(tracker.Observe(Observation(10), 20, 100));
        Assert.False(tracker.Observe(Observation(11) with { ResidentFingerprint = new('c', 64) }, 21, 110));
        Assert.False(tracker.Observe(Observation(12), 22, 120));
        Assert.False(tracker.Observe(Observation(13) with { Owners = Observation(13).Owners with { Files = 1 } }, 23, 130));
        Assert.Null(tracker.BoundaryTimestamp);
        Assert.False(tracker.Observe(Observation(14), 24, 140));
        Assert.False(tracker.Observe(Observation(15), 25, 150));
        Assert.True(tracker.Observe(Observation(16), 26, 160));
        Assert.Equal(140, tracker.BoundaryTimestamp);
    }

    [Fact]
    public void Retained_physical_packet_facts_are_allowed_but_missing_or_out_of_range_owners_are_not()
    {
        BattleComponentTextureQuiescenceObservation current = Observation(10);
        Assert.True(current.IsEligible);
        Assert.Equal((byte)1, current.Owners.ActionHeaders);
        Assert.Equal((byte)1, current.Owners.ContactRows);
        Assert.False((current with { RawBlockers = default }).IsEligible);
        Assert.False((current with { PhysicalCommandBanksEmpty = false }).IsEligible);
        Assert.False((current with { Owners = current.Owners with { RetainedActiveHeader = NativeBattleActionPacketsState.HeaderCapacity } }).IsEligible);
        Assert.False((current with { Owners = current.Owners with { RetainedActiveContact = NativeBattleActionPacketsState.ContactCapacity + 1 } }).IsEligible);
        Assert.False((current with { Owners = current.Owners with { Files = null } }).IsEligible);
    }

    internal static BattleComponentTextureQuiescenceObservation Observation(long callback)
    {
        BattleDiagnosticInvocationOwners owners = new(0, 0, 0, 0, false, true, 0, false, 0, 0, false, 0, false, false, false, false)
        {
            ObservedOriginalEffect = true,
            SelectedReturnedDescriptor = 0,
            ActionHeaders = 1,
            ContactRows = 1,
            RetainedActiveHeader = 0,
            RetainedActiveContact = 0,
            OriginalPools = []
        };
        BattleDiagnosticTextureTask task = new(0, 1, 0, null, NativeBattleAuxiliaryKind.TextureAnimation5057D0, 0xF0, 32, 0x1D972C0, 14, 844, 1552, 255, 0, 3, 2, 14);
        BattleDiagnosticTextureQuiescence textures = new(16, 1, 0, 0, true, [task]);
        return new(101, callback, 0, 0, owners, true, textures, new('a', 64), ["effect-or-auxiliary-task"]);
    }
}
