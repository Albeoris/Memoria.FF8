using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicForeignEntityAnchorTests
{
    [Fact]
    public void Entity_anchor_uses_current_host_pose_and_publishes_full_mac_registers_without_native_resource_rows()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "source", Name = "Source", IsPlayer = true }, new() { Id = "target", Name = "Target" }] }, BattleAtbMode.Normal);
        NativeBattleModelTransform original = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 4096);
        NativeBattleModelTransform current = original with { Position28 = new(10000, 20000, 30000) };
        int reads = 0;
        NativeBattlePosedActor pose = new(NativeBattleForeignPlacement.World(original, 0, 0, 0), [new(10, 20, 30)], (selector, fraction) =>
        {
            Assert.Equal((byte)240, selector);
            Assert.Equal((short)4096, fraction);
            reads++;
            return new(10, 20, 3000);
        });
        BattleModelOwner owner = new BattleForeignEnemyModel(new("host"));
        NativeBattleForeignActor foreign = new(owner, original, default!, (_, _) => throw new InvalidOperationException()) { Pose = pose };
        typeof(BattleSession).GetField("presentationForeignActors", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, new Dictionary<BattleModelOwner, NativeBattleForeignActor> { [owner] = foreign });
        typeof(BattleSession).GetField("nativeEffectMemory", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, NativeBattleEffectMemory.FromColdProcess());
        BattleActorModels models = new([new(owner, new NativeBattleGraphicsModel(2) { Transform = current, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0 })]);
        NativeBattleGraphicsState graphics = new(models, NativeBattleModelBoundary.FromRetainedMemory(null), 0, 0, NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena));
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        MethodInfo read = typeof(BattleSession).GetMethod("ReadNativeCinematicEntityAnchor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        NativeBattleCinematicEntityAnchor result = (NativeBattleCinematicEntityAnchor)read.Invoke(battle, [frame, graphics, owner, (short)240])!;
        Assert.Equal(1, reads);
        Assert.Equal(10010, result.Point.X);
        Assert.Equal(20020, result.Point.Y);
        Assert.Equal(33000, result.Point.Z);
        Assert.Equal(new int?[] { 10010, 20020, 33000 }, battle.NativeEffectMemory!.ScriptedColorRegisters!.Mac.ToArray());
        Assert.Equal(33000u, result.Frame.ReadTemporaryDword(0xF8));
        Assert.Null(battle.NativeModelCpuResources);
        Assert.Null(battle.NativeFileStaging);
    }
}
