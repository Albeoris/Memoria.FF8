using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuake38RetainedCellTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BattlePartyActorModel Party = new(new("party"));
    private static readonly BattleEnemyActorModel FirstEnemy = new(new("first-enemy"));
    private static readonly BattleEnemyActorModel SecondEnemy = new(new("second-enemy"));
    private static readonly BattleEnemyActorModel ExtraEnemy = new(new("extra-enemy"));

    [Theory]
    [InlineData(BattleContactTaskStorageContract.OriginalArena, 4, true)]
    [InlineData(BattleContactTaskStorageContract.ExpandableArena, 3, false)]
    public void Bounce_visits_unbound_cells_only_in_original_order(BattleContactTaskStorageContract contract, int activeBounces, bool visitsOriginalHoles)
    {
        var fixture = Create(contract);
        Invoke(fixture.Execution, "CaptureFlags");
        Invoke(fixture.Execution, "CaptureTransforms");
        Invoke(fixture.Execution, "BounceTargets", fixture.Task);
        NativeBattleGraphicsState actual = ReadGraphics(fixture.Execution);
        NativeCrtRandomState random = new(1);
        short secondX = 0;
        int secondBounce = contract == BattleContactTaskStorageContract.OriginalArena ? 2 : 1;
        for (int bounce = 0; bounce < activeBounces; bounce++)
        {
            random = random.Draw(out _).Draw(out _).Draw(out _).Draw(out ushort x).Draw(out _);
            if (bounce == secondBounce)
            {
                secondX = (short)((x & 0x7F) - 64);
            }
        }
        Assert.Equal(secondX, actual.ActorModels.Get(SecondEnemy).Transform!.Position28.X);
        Assert.Equal(random, typeof(BattleSession).GetField("nativeCrtRandom", PrivateInstance)!.GetValue(fixture.Session));
        NativeBattleModelSlot last = NativeBattleModelSlot.Decode(6);
        Assert.Equal(visitsOriginalHoles ? (short)0 : (short)1, actual.NativeBindings.ReadOriginalCell(last, actual.ActorModels).Transform!.Position28.Y);
        Assert.Equal(visitsOriginalHoles, fixture.State.SavedUnboundCells.ContainsKey(last));
        Invoke(fixture.Execution, "RestoreTransforms");
        actual = ReadGraphics(fixture.Execution);
        Assert.Equal((short)1, actual.NativeBindings.ReadOriginalCell(last, actual.ActorModels).Transform!.Position28.Y);
        Assert.Equal((short)1, actual.ActorModels.Get(SecondEnemy).Transform!.Position28.Y);
    }

    [Theory]
    [InlineData(BattleContactTaskStorageContract.OriginalArena, 0x1020)]
    [InlineData(BattleContactTaskStorageContract.ExpandableArena, 0x20)]
    public void Original_enemy_restores_shifted_flags_while_expandable_restores_its_own(BattleContactTaskStorageContract contract, ushort expected)
    {
        var fixture = Create(contract);
        Invoke(fixture.Execution, "CaptureFlags");
        fixture.Task.WriteByte(41, 8);
        Invoke(fixture.Execution, "Sequence", 0u, fixture.Task);
        NativeBattleGraphicsState graphics = ReadGraphics(fixture.Execution);
        Assert.Equal(expected, graphics.ActorModels.Get(FirstEnemy).Flags0);
        NativeBattleModelSlot firstHole = NativeBattleModelSlot.Decode(4);
        Assert.Equal(contract == BattleContactTaskStorageContract.OriginalArena ? (ushort)0x1000 : (ushort)0, graphics.NativeBindings.ReadOriginalCell(firstHole, graphics.ActorModels).Flags0);
        Assert.Throws<InvalidOperationException>(() => graphics.NativeBindings.WriteUnboundCell(NativeBattleModelSlot.Decode(3), new(0)));
    }

    [Fact]
    public void Unbound_transforms_rollback_and_survive_only_private_restore()
    {
        NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(4);
        NativeBattleQuake38State state = NativeBattleQuake38State.FromColdProcess();
        NativeBattleQuake38SavedActor original = NativeBattleQuake38SavedActor.FromColdProcess() with { RotationPadding18 = null };
        state.SetSavedUnboundCells(ImmutableDictionary<NativeBattleModelSlot, NativeBattleQuake38SavedActor>.Empty.Add(slot, original));
        state.BeginCheckpoint();
        state.UpdateSavedUnboundCell(slot, original with { Velocity = 32 });
        state.RollbackCheckpoint();
        Assert.Equal(original, state.ReadSavedUnboundCell(slot));
        NativeBattleQuake38State detached = NativeBattleStorageSnapshots.Capture(state);
        Assert.Equal(original, NativeBattleStorageSnapshots.Restore(detached).ReadSavedUnboundCell(slot));
        NativeBattleStorageRestoreContext fresh = new(NativeBattleStorageRestorePurpose.NewBattle);
        Assert.Empty(fresh.Map(detached).SavedUnboundCells);
        Assert.Equal(original, state.ReadSavedUnboundCell(slot));
    }

    private static (BattleSession Session, object Execution, NativeBattleQuake38State State, NativeBattleModulePoolTask Task) Create(BattleContactTaskStorageContract contract)
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [new() { Id = "party", Name = "Ally", IsPlayer = true }, new() { Id = "first-enemy", Name = "Enemy", ModelId = 30 }],
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess()
        }, BattleAtbMode.TurnBased);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleQuake38State state = memory.Quake38!;
        state.SetWord(0x278C8E8, 8192).SetWord(0x278C8E6, 8192);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, memory);
        typeof(BattleSession).GetField("nativeCrtRandom", PrivateInstance)!.SetValue(session, new NativeCrtRandomState(1));
        NativeBattleGraphicsModel[] cells = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index switch { 0 => 0x1020, 1 => 0x1000, 3 => 0x20, _ => 0 }) { Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, Transform = new(default, new(0, 1, 0), 0, 0, 4096) }).ToArray();
        NativeBattleModelBoundary boundary = new(cells);
        BattleActorModels models = contract == BattleContactTaskStorageContract.ExpandableArena ? new([new(ExtraEnemy, cells[3])]) : new([]);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(0), Party, models);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(3), FirstEnemy, models);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(5), SecondEnemy, models);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(models.Roster, NativeBattleRetainedActionPackets.Fresh(), boundary.CaptureAliases(models));
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(Party, 1, 7, -1, 38, 0, null));
        (packets, _) = packets.AppendContact(new(new(FirstEnemy, 4, 0, 0, 0, 0, 0), null));
        (packets, _) = packets.Publish(1);
        typeof(BattleSession).GetField("nativeActionPackets", PrivateInstance)!.SetValue(session, packets);
        var parameters = new NativeBattleEffectParameters(Party, 0, BattleModelTargets.From([FirstEnemy]), header, 0, 0);
        var resources = new NativeBattleQuake38Resources(null!, null!, null!, null!, null!, new byte[1024].ToImmutableArray(), [], [], [], [], [], 0, 0);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 64, 1);
        pool.Append(0x8D8800);
        pool.Slots[0].WriteByte(42, 0);
        var effect = new NativeBattleOriginalEffectState(38, 1, new(38, 0x8D8510, 0) { Quake38Resources = resources }, parameters, pool.DescriptorAddress) { Pools = [pool] };
        NativeBattleGraphicsState graphics = new(models, boundary, 0, 0, NativeBattleEffectsState.Clear(contract)) { OriginalEffect = effect };
        Type implementation = typeof(BattleSession).GetNestedType("NativeQuake38Execution", BindingFlags.NonPublic)!;
        object execution = Activator.CreateInstance(implementation, session, graphics)!;
        return (session, execution, state, pool.Slots[0]);
    }

    private static void Invoke(object execution, string method, params object[] arguments)
    {
        execution.GetType().GetMethod(method, PrivateInstance)!.Invoke(execution, arguments);
    }

    private static NativeBattleGraphicsState ReadGraphics(object execution) => (NativeBattleGraphicsState)execution.GetType().GetField("graphics", PrivateInstance)!.GetValue(execution)!;
}
