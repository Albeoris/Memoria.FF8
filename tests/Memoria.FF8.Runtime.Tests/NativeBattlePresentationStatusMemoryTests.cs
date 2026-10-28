using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePresentationStatusMemoryTests
{
    [Fact]
    public void EarlyEffect80ContactUsesTheRetainedPairBeforeRefreshingCurrentStatuses()
    {
        BattleSession battle = new(Definition(), BattleAtbMode.Normal);
        NativeBattleStatusMirror previous = battle.NativePresentationStatusMemory.Rows[0];

        Publish(battle);

        Assert.Equal(previous.Saved, battle.NativePartyStatusMirrors.Saved[0]);
        Assert.Equal(previous.Battle, battle.NativePartyStatusMirrors.Battle[0]);
        Assert.Equal(new NativeBattleStatusMirror(0x40, 0x2000), battle.NativePresentationStatusMemory.Rows[0]);
        BattleSession unknown = new(Definition() with { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess() with { PresentationStatusMemory = null } }, BattleAtbMode.Normal);
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Publish(unknown));
        Assert.IsType<NotSupportedException>(error.InnerException);
    }

    [Fact]
    public void SnapshotRestoreKeepsIndependentPhysicalOwnersAndRejectsContradictoryProjections()
    {
        BattleSession original = new(Definition(), BattleAtbMode.Normal);
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(original.Capture()))!;
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(snapshot);

        Publish(first);

        Assert.Equal(new NativeBattleStatusMirror(0x20, 0x4000), second.NativePresentationStatusMemory.Rows[0]);
        Assert.Equal(second.NativePresentationStatusMemory.Rows[0], snapshot.NativePresentationStatusMemory!.Rows[0]);
        Assert.NotEqual(first.NativePresentationStatusMemory.Rows[0], second.NativePresentationStatusMemory.Rows[0]);
        NativeBattlePresentationStatusMemory contradictory = snapshot.NativePresentationStatusMemory.Observe(0, new(1, 2));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativePresentationStatusMemory = contradictory }));
    }

    [Fact]
    public void MissingCurrentBankRestoresOnlyExplicitActorRowsAndNeverReusesInitialColdHistory()
    {
        BattleSnapshot snapshot = new BattleSession(Definition(), BattleAtbMode.Normal).Capture();
        snapshot = snapshot with { NativePresentationStatusMemory = null, Definition = snapshot.Definition with { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess() } };

        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);

        Assert.Equal(new NativeBattleStatusMirror(0x20, 0x4000), restored.NativePresentationStatusMemory.Rows[0]);
        Assert.Equal(new NativeBattleStatusMirror(null, null), restored.NativePresentationStatusMemory.Rows[1]);
        Assert.Equal(new NativeBattleStatusMirror(null, null), restored.NativePresentationStatusMemory.Rows[6]);
    }

    [Fact]
    public void PhysicalMirrorsSurviveReleaseReallocationAndNextBattleEntry()
    {
        BattleSession battle = new(Definition(), BattleAtbMode.Normal);
        NativeBattleStatusMirror retained = battle.NativePresentationStatusMemory.Rows[3];
        MethodInfo allocate = typeof(BattleSession).GetMethod("AllocateNativeEnemy", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _ = allocate.Invoke(battle, [battle.Combatants[0], 0, 3, false]);
        Assert.Equal(retained, battle.NativePresentationStatusMemory.Rows[3]);
        BattleSnapshot snapshot = battle.Capture();
        Assert.Equal(retained, snapshot.Combatants.Single(actor => actor.Id.StartsWith("native-allocation-", StringComparison.Ordinal)).NativeStatus!.Presentation);
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemory.Capture(snapshot, NativeFieldRuntimeProfile.FF8EnExecutableHash);
        memory = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(memory))!;
        BattleSession next = new(Definition() with { NativeRetainedMemory = memory }, BattleAtbMode.Normal);
        Assert.Equal(retained, next.NativePresentationStatusMemory.Rows[3]);
        Assert.Equal(new NativeBattleStatusMirror(0x20, 0x4000), next.NativePresentationStatusMemory.Rows[0]);
    }

    [Fact]
    public void FailedCandidatePublicationCannotReplaceTheCurrentPhysicalBank()
    {
        BattleSession current = new(Definition(), BattleAtbMode.Normal);
        BattleSession candidate = new(Definition(), BattleAtbMode.Normal);
        FieldInfo ownerField = typeof(BattleSession).GetField("nativePresentationStatusMemory", BindingFlags.NonPublic | BindingFlags.Instance)!;
        object owner = ownerField.GetValue(candidate)!;
        owner.GetType().GetProperty("State")!.SetValue(owner, candidate.NativePresentationStatusMemory with { Rows = candidate.NativePresentationStatusMemory.Rows.SetItem(0, new NativeBattleStatusMirror(0, null)) });
        MethodInfo publish = typeof(BattleSession).GetMethod("PublishCandidate", BindingFlags.NonPublic | BindingFlags.Instance)!;
        NativeBattlePresentationStatusMemory original = current.NativePresentationStatusMemory;

        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => publish.Invoke(current, [candidate]));

        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Same(original, current.NativePresentationStatusMemory);
    }

    private static void Publish(BattleSession battle)
    {
        // Effect80's506690 contact reaches this same493D80/493840 status service.
        NativeBattleGraphicsState graphics = new([], ["party"], 0, 0);
        MethodInfo publish = typeof(BattleSession).GetMethod("PublishNativeReadyStatus", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _ = publish.Invoke(battle, [graphics, new NativeBattleContactHalf(0, 0, 0, 0, 0, 0, 0)]);
    }

    private static BattleDefinition Definition()
    {
        NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 100, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        NativeBattlePresentationStatusMemory mirrors = NativeBattlePresentationStatusMemory.FromColdProcess().Observe(0, new(0x20, 0x4000)).Observe(3, new(0x80, 0x200));
        return new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess() with { PresentationStatusMemory = mirrors },
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, NativeStatus0 = 0x40, NativeStatus1 = 0x2000 },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, NativeTemplate = new(1, Enumerable.Repeat(level, 101).ToArray()) { Information254 = 0 } }
            ]
        };
    }
}
