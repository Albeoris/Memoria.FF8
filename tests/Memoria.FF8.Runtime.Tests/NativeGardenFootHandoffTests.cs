using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeGardenFootHandoffTests
{
    [Fact]
    public void CompletedNativeHandoffRestoresRetainedOwnersAndClearsOnlyTheCacheEntries()
    {
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess().AdvanceMainLoopCallbacks(1000) with { CameraCooldown = -9, CarCameraFollowSpeed = 24 };
        NativeWorldPathPosition parked = new(20480, -194, 4600);
        NativeWorldPathPosition target = new(21180, -200, 4600);
        NativeGardenTransferState transfer = NativeGardenTransferState.Create(10, 2, 0, target) with { Counter = 31, LoweringStep = 26, InitialGardenPosition = new(20480, -1000, 4600), InitialGroundHeight = -200, LastGardenPosition = parked, LastLoweringFrame = 40 };
        NativeGardenWorldObject[] objects = [new(0, 0, parked, 0, -32, 0), new(2, 0, default, 0, 0, 0), new(64, 0, parked, 0, -32, 0), new(65, 2, parked, 0, -32, 0)];
        ushort[] words = [0xFF01, 0, 0xFF02, 740, 0xFF03, 900, 0xFF04, 0, 0xFF13, 0, 0xFF13, 2, 0xFF13, 3, 0xFF13, 64, 0xFF13, 65, 0xFF05, 0];
        string hash = new('A', 64);
        NativeSavedWorldPose pose = new(20480, -4600, -194, 4064);
        NativeGardenWorldAllocation allocation = new(1, NativeGardenWorldAllocation.AllocationRule, hash, hash, hash, 8, 740, 900, 750, 0, 0, words.ToImmutableArray(), objects.ToImmutableArray(), 0, 2, 3, new WorldTransition(12, 48, -1), pose, pose, 0, 0, 0) { GuardianForce13Flags = 1 };
        NativeWorldGroundIdentity current = new(0, 0, 0, 0);
        NativeWorldGroundIdentity previous = new(0, 0, 0, 1);
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(current) with { Alternatives = [new(1, [current, previous, current], current, previous) { FailedSelections = 3 }] };
        NativeGardenWorldRuntimeState garden = NativeGardenWorldRuntimeState.CreateFresh(NativeSavedWorldState.CreateNewGame(), allocation, cache, process, new(NativeFieldRuntimeProfile.FF8EnExecutableHash, -112, 0, -341, -7168), currentGroundHeight: -200) with { GroundYaw = -160, Transfer = transfer };
        garden = garden with { Forest = garden.Forest with { RandomCursor = 17, RandomOffset = 3, Particles = [new(0, 10, 992, -8, 1992, -2048, 0, 4092, 1, 12, true) { Motion = new(0, -8, -7, -8) }] } };
        string before = JsonSerializer.Serialize(garden);
        NativeFootWorldRuntimeState foot = NativeFootWorldRuntimeState.CreateGardenDismount(garden, transfer, process, 0);
        Assert.Equal(NativeFootWorldRuntimeState.GardenDismountRule, foot.Rule);
        Assert.Equal((byte)0, foot.AnimationVariant);
        Assert.Null(foot.ReloadSourceWorldFrame);
        Assert.Same(garden.Forest, foot.Forest);
        Assert.Equal(-160, foot.GroundYaw);
        Assert.Equal(0, foot.PreviousHorizontalInput);
        NativeWorldGroundAlternative cleared = Assert.Single(foot.GroundCache!.Alternatives);
        Assert.Empty(cleared.Entries);
        Assert.Equal((1, 3, current, previous), (cleared.PreferredGroup, cleared.FailedSelections, cleared.CurrentGround, cleared.PreviousGround));
        Assert.Equal(before, JsonSerializer.Serialize(garden));
        NativeFootWorldRuntimeState restored = JsonSerializer.Deserialize<NativeFootWorldRuntimeState>(JsonSerializer.Serialize(foot))!;
        restored.RequireNativeGroundState();
        restored.RequireSupportedNormalFoot();
        restored.GardenDismount!.Validate(garden, process);
        Assert.Equal((short)-32, restored.GardenDismount.FootBodyYaw);
        Assert.Equal(40, restored.GardenDismount.Transfer.LastLoweringFrame);
        Assert.Equal(JsonSerializer.Serialize(foot), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (restored with { GardenDismount = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { Rule = NativeFootWorldRuntimeState.FirstLoadRule }).Validate());
        Assert.Throws<InvalidOperationException>(() => NativeFootWorldRuntimeState.CreateGardenDismount(garden, transfer with { Counter = 30 }, process, 0));
    }
}
