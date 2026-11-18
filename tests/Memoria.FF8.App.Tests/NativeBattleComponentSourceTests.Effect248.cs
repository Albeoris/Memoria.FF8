using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect248Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EffectId != 248 || request.EnemyId != 71 || request.StageId != 25)
        {
            throw new InvalidDataException("The component requires its original scripted caster and encounter stage.");
        }
        BattleSession source = world.StartDiagnosticBattle(new(164, request.Seed, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            Fixture = new([], [], true) { CombatParty = [0, 255, 255] }
        });
        BattleCombatant original = source.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == 101 && actor.SceneSlot == 0);
        int attacks = 0;
        for (int callback = 0; callback < 18000 && source.Outcome == BattleOutcome.Active; callback++)
        {
            BattlePresentationProcess process = source.Presentation!;
            if (process.ActiveActionToken is not null && process.ActionPackets.PublishedHeaders().Any(entry => entry.Slot.Effect == 248))
            {
                BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 248);
                int[] models = Enumerable.Range(3, 3).Select(index =>
                {
                    BattleModelOwner owner = process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)index))) ?? throw new InvalidDataException("The original scripted component lacks an allocated model operand.");
                    return (int?)process.Graphics.ActorModels.Get(owner).ModelId4 ?? -1;
                }).Order().ToArray();
                if (attacks == 0 || original.IsAlive || !models.SequenceEqual(new[] { 87, 87, 88 }) || process.Graphics.ActorModels.Get(admission.Actor).ModelId4 != 87)
                {
                    throw new InvalidDataException("The component requires its actual defeated encounter owner and native soldier allocation chain.");
                }
                return source;
            }
            if (source.NativeEntryReady && source.ActiveAction is null && source.QueuedActions.Count == 0 && original.IsActive && original.IsAlive && !original.Untargetable && !original.Suspended)
            {
                BattleCombatant? ready = source.Combatants.FirstOrDefault(actor => actor.IsPlayer && source.CanChoosePlayerCommand(actor.Id));
                if (ready is not null)
                {
                    if (++attacks > 256)
                    {
                        throw new InvalidDataException("The original scripted component exceeded its bounded ordinary attack preparation.");
                    }
                    source.QueueAttack(ready.Id, original.Id);
                }
            }
            source.Tick();
        }
        throw new InvalidDataException("The original encounter did not publish its unstarted scripted component within the preparation budget.");
    }
}
