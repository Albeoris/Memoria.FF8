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
    private short battleBigShotChargeRotation;
    private short battleBigShotCounterRotation;
    private short battleSearchScopeScaleCounter;
    private short battleSilkDirection = 1;
    private byte battleCameraPresentationCounter;
    private readonly Dictionary<byte, Ff7BattleCameraScriptResources> battleCameraScripts = [];

    public Ff7BattleSession? Battle { get; private set; }
    private Ff7EnemyExtensionCatalog? enemyExtensions;
    private Func<IReadOnlyList<byte>>? enemyExtensionPartyLevels;

    public void ConfigureEnemyExtensions(Ff7EnemyExtensionCatalog catalog, bool enabled, Func<IReadOnlyList<byte>>? partyLevels = null)
    {
        enemyExtensions = enabled ? catalog : null;
        enemyExtensionPartyLevels = partyLevels;
    }

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
            battleArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
            bool isBossBattle = Ff7BattleAnimationResources.HasBossDeath(formation, battleArchive);
            Progress = Ff7DebugVictory.Apply(Progress, formation, battleKernel!, FieldGeneration, request.Id, guestStoryCharacters: GuestFieldVitals?.StoryCharacters);
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
        Battle = new(formation, Progress, battleKernel!, executable.NativeMateriaStatModifiers, request.Id, flags, animations: battleAnimations, limits: battleLimits, beatRushSymbols: beatRushSymbols, requestMode: Host.Process.BattleRequestMode, musicTrack: track > 0 ? track : null, bigShotChargeRotation: battleBigShotChargeRotation, bigShotCounterRotation: battleBigShotCounterRotation, searchScopeScaleCounter: battleSearchScopeScaleCounter, cameraScripts: LoadBattleCameraScripts(Ff7NativeAtb.ResolveLayout(formation.Layout, formation.BattleFlags, (flags & 4) != 0)), cameraPresentationCounter: battleCameraPresentationCounter, silkDirection: battleSilkDirection, enemyExtensions: enemyExtensions, enemyExtensionPartyLevels: enemyExtensionPartyLevels?.Invoke(), initialSaveBank1: Ff7BattleSession.CaptureNativeSaveBank(Machine.Variables));
        return new(false);
    }

    private void RestoreBattle(Ff7BattleSnapshot snapshot)
    {
        SceneFormation formation = GetBattleFormation(snapshot.FormationId);
        LoadBattlePresentationResources();
        Battle = new(formation, Progress, battleKernel!, executable.NativeMateriaStatModifiers, snapshot.RequestId, snapshot: snapshot, animations: battleAnimations, limits: battleLimits, beatRushSymbols: beatRushSymbols, cameraScripts: LoadBattleCameraScripts(snapshot.Layout), cameraPresentationCounter: battleCameraPresentationCounter, silkDirection: battleSilkDirection, initialSaveBank1: Ff7BattleSession.CaptureNativeSaveBank(Machine.Variables));
    }

    private Ff7BattleCameraScriptResources LoadBattleCameraScripts(byte layout)
    {
        if (!battleCameraScripts.TryGetValue(layout, out Ff7BattleCameraScriptResources? scripts))
        {
            scripts = Ff7BattleCameraResources.LoadScripts(installation, layout);
            battleCameraScripts.Add(layout, scripts);
        }
        return scripts;
    }

    private void LoadBattlePresentationResources()
    {
        battleArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        battleMagicArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        battleAnimations ??= new(battleArchive, Ff7PcExecutableData.LoadBattlePlacement(installation), battleMagicArchive, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        battleLimits ??= Ff7PcExecutableData.LoadBattleLimits(installation);
        beatRushSymbols ??= Ff7PcExecutableData.LoadBeatRushReel(installation);
    }

    public bool ConfirmBattleVictory()
    {
        return ConfirmGuestBattleVictory([]);
    }

    public bool ConfirmGuestBattleVictory(Ff7BattleGuestVitalsBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return ConfirmGuestBattleVictory([binding]);
    }

    public bool ConfirmGuestBattleVictory(IReadOnlyList<Ff7BattleGuestVitalsBinding> guests)
    {
        if (Battle is not { Outcome: Ff7BattleOutcome.Victory, ResultReady: true } battle || !battle.GuestVitalsBindings.SequenceEqual(guests))
        {
            return false;
        }
        Ff7Progress result = battle.PublishVictory(Progress, FieldGeneration);
        int nativeResult = battle.ResolveNativeFieldResult(0);
        battle.PublishNativeSaveWrites(Machine.Variables);
        Host.PublishBattleResult(battle.RequestId, nativeResult);
        Progress = result;
        battleBigShotChargeRotation = battle.BigShotChargeRotation;
        battleBigShotCounterRotation = battle.BigShotCounterRotation;
        battleSearchScopeScaleCounter = battle.SearchScopeScaleCounter;
        battleSilkDirection = battle.SilkDirection;
        battleCameraPresentationCounter = battle.CameraPresentationCounter;
        Battle = null;
        return true;
    }

    public bool ConfirmBattleEscape()
    {
        return ConfirmGuestBattleEscape([]);
    }

    public bool ConfirmGuestBattleEscape(Ff7BattleGuestVitalsBinding binding) => ConfirmGuestBattleEscape([binding]);

    public bool ConfirmGuestBattleEscape(IReadOnlyList<Ff7BattleGuestVitalsBinding> guests)
    {
        if (Battle is not { Outcome: Ff7BattleOutcome.Escaped, ResultReady: true } battle || !battle.GuestVitalsBindings.SequenceEqual(guests))
        {
            return false;
        }
        Ff7Progress result = battle.PublishVitals(Progress) with { BattleEscapes = unchecked((ushort)(Progress.BattleEscapes + 1)) };
        int nativeResult = battle.ResolveNativeFieldResult(Ff7NativeBattleEscape.FieldResult);
        battle.PublishNativeSaveWrites(Machine.Variables);
        Host.PublishBattleResult(battle.RequestId, nativeResult);
        Progress = result;
        battleBigShotChargeRotation = battle.BigShotChargeRotation;
        battleBigShotCounterRotation = battle.BigShotCounterRotation;
        battleSearchScopeScaleCounter = battle.SearchScopeScaleCounter;
        battleSilkDirection = battle.SilkDirection;
        battleCameraPresentationCounter = battle.CameraPresentationCounter;
        Battle = null;
        return true;
    }

    public bool ConfirmBattleDefeat()
    {
        return ConfirmGuestBattleDefeat([]);
    }

    public bool ConfirmGuestBattleDefeat(Ff7BattleGuestVitalsBinding binding) => ConfirmGuestBattleDefeat([binding]);

    public bool ConfirmGuestBattleDefeat(IReadOnlyList<Ff7BattleGuestVitalsBinding> guests)
    {
        if (Battle is not { Outcome: Ff7BattleOutcome.Defeat, ReturnsAfterDefeat: true, ResultReady: true } battle || !battle.GuestVitalsBindings.SequenceEqual(guests))
        {
            return false;
        }
        Ff7Progress result = battle.PublishVitals(Progress);
        int nativeResult = battle.ResolveNativeFieldResult(1);
        battle.PublishNativeSaveWrites(Machine.Variables);
        Host.PublishBattleResult(battle.RequestId, nativeResult);
        Progress = result;
        battleBigShotChargeRotation = battle.BigShotChargeRotation;
        battleBigShotCounterRotation = battle.BigShotCounterRotation;
        battleSearchScopeScaleCounter = battle.SearchScopeScaleCounter;
        battleSilkDirection = battle.SilkDirection;
        battleCameraPresentationCounter = battle.CameraPresentationCounter;
        Battle = null;
        return true;
    }
}
