using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CactuarWorldBattleReturnTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparedReloadUsesPostBattleGuardianForceFlagsWithoutPublishingLiveState(bool acquired)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = new(GameSession.Load(TestResources.FixturePath("cactuar-world-contact-source")));
        using GameWorld world = new(session, TestResources.GameDirectory, sharedAssets: assets, validateWorldPresentation: false);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        WorldMapScene previous = Assert.IsType<WorldMapScene>(world.WorldScene);
        WorldState state = previous.State;
        NativeRagnarokWorldRuntimeState runtime = state.NativeRagnarokRuntime!;
        int index = runtime.Objects.Select((item, index) => (item, index)).Single(pair => pair.item.ShapeId == 94).index;
        NativeWorldFootCameraState camera = runtime.ProjectionCamera!;
        NativeRagnarokWorldObject body = runtime.Objects[index] with { Position = new(camera.X, camera.NativeHeight, -camera.RawZ) };
        session.SetWorldState(state with { NativeRagnarokRuntime = runtime with { Objects = runtime.Objects.SetItem(index, body), Cactuar = new(2, 1) } });
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal((ushort)712, session.State.RandomEncounters!.Pending!.Formation);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        // Preparation only requires the terminal result selector; execute no combat or effects.
        typeof(BattleSession).GetProperty(nameof(BattleSession.Outcome))!.SetValue(battle, acquired ? BattleOutcome.Victory : BattleOutcome.Escaped);
        try
        {
            GameState departure = session.State;
            NativeCharacterVitals vitals = departure.CharacterVitals!;
            Assert.Equal(0, vitals.GuardianForces[13].Flags & 1);
            NativeCharacterVitals projected = acquired ? vitals.AcquireGuardianForce(13) : vitals;
            Action publish = (Action)typeof(GameWorld).GetMethod("PrepareNativeWorldBattleReturn", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [battle, departure.PendingBattleEncounter!, projected])!;
            WorldMapScene prepared = Assert.Single(publish.Target!.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(field => field.GetValue(publish.Target)).OfType<WorldMapScene>(), scene => !ReferenceEquals(scene, previous));
            using (prepared)
            {
                Assert.Same(departure, session.State);
                Assert.Same(previous, world.WorldScene);
                Assert.Equal(0, session.State.CharacterVitals!.GuardianForces[13].Flags & 1);
                NativeRagnarokWorldRuntimeState reloaded = prepared.State.NativeRagnarokRuntime!;
                Assert.Equal(acquired ? 1 : 0, reloaded.Allocation.GuardianForce13Flags & 1);
                Assert.Equal(!acquired, reloaded.Objects.Any(item => item.ShapeId == 94));
                Assert.Equal(!acquired, reloaded.Cactuar is not null);
                Assert.Equal((departure.World!.X, departure.World.Z, departure.World.BodyYaw), (prepared.State.X, prepared.State.Z, prepared.State.BodyYaw));
                Assert.Equal(NativeFootWorldRuntimeState.BattleReloadRule, prepared.State.NativeFootRuntime!.Rule);
            }
        }
        finally
        {
            typeof(BattleSession).GetProperty(nameof(BattleSession.Outcome))!.SetValue(battle, BattleOutcome.Active);
        }
    }
}
