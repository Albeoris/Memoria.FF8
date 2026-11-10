using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private IReadOnlyList<SceneFormation>? battleFormations;
    private IReadOnlyList<KernelSection>? battleKernel;
    private LgpArchive? battleArchive;
    private LgpArchive? battleMagicArchive;
    private Ff7BattleAnimationResources? battleAnimations;
    private IReadOnlyList<Ff7BattleAttack>? battleLimits;
    private IReadOnlyList<byte>? beatRushSymbols;

    public Ff7BattleSession? Battle { get; private set; }

    public void SetFullCombatEnabled(bool enabled)
    {
        Progress = Progress with { FullCombatEnabled = enabled };
    }

    private SceneFormation GetBattleFormation(int id)
    {
        battleFormations ??= SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        battleKernel ??= KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        return battleFormations.SingleOrDefault(formation => formation.FormationId == id) ?? throw new InvalidDataException("FFVII battle formation is absent from the installed scene archive.");
    }

    private FieldHostReply BeginFieldBattle(FieldHostRequest request)
    {
        FieldBattleCommand command = request.Command as FieldBattleCommand ?? throw new InvalidDataException("FFVII combat requires an owned battle request.");
        SceneFormation formation = GetBattleFormation(command.Formation);
        if (!Progress.FullCombatEnabled)
        {
            Progress = Ff7DebugVictory.Apply(Progress, formation, battleKernel!, FieldGeneration, request.Id);
            return new(true);
        }
        if (Battle is not null)
        {
            throw new InvalidOperationException("An FFVII field battle already owns this session.");
        }
        LoadBattlePresentationResources();
        ushort flags = Host.Process.BattleRequestFlags ?? unchecked((ushort)(Host.Process.BattleMode2 != 0 ? Host.Process.BattleMode2 : Host.Process.BattleMode));
        flags |= Host.StartingEncounterInitiative;
        int track = !Host.Process.BattleMusicSuppressed && Host.Process.NextBattleMusicTrack >= 0 ? Host.ResolveNativeMusicTrack(Host.Process.NextBattleMusicTrack) : 0;
        Battle = new(formation, Progress, battleKernel!, executable.NativeMateriaStatModifiers, request.Id, flags, animations: battleAnimations, limits: battleLimits, beatRushSymbols: beatRushSymbols, requestMode: Host.Process.BattleRequestMode, musicTrack: track > 0 ? track : null);
        return new(false);
    }

    private void RestoreBattle(Ff7BattleSnapshot snapshot)
    {
        SceneFormation formation = GetBattleFormation(snapshot.FormationId);
        LoadBattlePresentationResources();
        Battle = new(formation, Progress, battleKernel!, executable.NativeMateriaStatModifiers, snapshot.RequestId, snapshot: snapshot, animations: battleAnimations, limits: battleLimits, beatRushSymbols: beatRushSymbols);
    }

    private void LoadBattlePresentationResources()
    {
        battleArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        battleMagicArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        battleAnimations ??= new(battleArchive, Ff7PcExecutableData.LoadBattlePlacement(installation), battleMagicArchive);
        battleLimits ??= Ff7PcExecutableData.LoadBattleLimits(installation);
        beatRushSymbols ??= Ff7PcExecutableData.LoadBeatRushReel(installation);
    }

    public bool ConfirmBattleVictory()
    {
        if (Battle is not { Outcome: Ff7BattleOutcome.Victory } battle)
        {
            return false;
        }
        Ff7Progress result = battle.PublishVictory(Progress, FieldGeneration);
        Host.PublishBattleResult(battle.RequestId, 0);
        Progress = result;
        Battle = null;
        return true;
    }

    public bool ConfirmBattleEscape()
    {
        if (Battle is not { Outcome: Ff7BattleOutcome.Escaped } battle)
        {
            return false;
        }
        Ff7Progress result = battle.PublishVitals(Progress) with { BattleEscapes = unchecked((ushort)(Progress.BattleEscapes + 1)) };
        Host.PublishBattleResult(battle.RequestId, Ff7NativeBattleEscape.FieldResult);
        Progress = result;
        Battle = null;
        return true;
    }

    public bool ConfirmBattleDefeat()
    {
        if (Battle is not { Outcome: Ff7BattleOutcome.Defeat, ReturnsAfterDefeat: true } battle)
        {
            return false;
        }
        Ff7Progress result = battle.PublishVitals(Progress);
        Host.PublishBattleResult(battle.RequestId, 1);
        Progress = result;
        Battle = null;
        return true;
    }
}
