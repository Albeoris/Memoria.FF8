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

    public void SetNativeBattleSpeed(byte speed)
    {
        if (Battle is not null)
        {
            throw new InvalidOperationException("Native FFVII battle speed can change only outside an encounter.");
        }
        Progress = Progress with { NativeBattleSpeed = speed };
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
        Battle = CreateBattle(formation, request.Id, flags, Host.Process.BattleRequestMode, track, Machine.Variables);
        return new(false);
    }

    private Ff7BattleSession CreateBattle(SceneFormation formation, long requestId, ushort flags, byte requestMode, int track, FieldVariables variables, Ff7BattleSnapshot? previousFormation = null)
    {
        return new(formation, Progress, battleKernel!, executable.NativeMateriaStatModifiers, requestId, flags, animations: battleAnimations, limits: battleLimits, beatRushSymbols: beatRushSymbols, requestMode: requestMode, musicTrack: track > 0 ? track : null, bigShotChargeRotation: previousFormation?.BigShotChargeRotation ?? battleBigShotChargeRotation, bigShotCounterRotation: previousFormation?.BigShotCounterRotation ?? battleBigShotCounterRotation, searchScopeScaleCounter: previousFormation?.SearchScopeScaleCounter ?? battleSearchScopeScaleCounter, cameraScripts: LoadBattleCameraScripts(Ff7NativeAtb.ResolveLayout(formation.Layout, formation.BattleFlags, (flags & 4) != 0)), cameraPresentationCounter: previousFormation?.Camera?.Scripts.PresentationCounter ?? battleCameraPresentationCounter, silkDirection: previousFormation?.SilkDirection ?? battleSilkDirection, enemyExtensions: enemyExtensions, enemyExtensionPartyLevels: enemyExtensionPartyLevels?.Invoke(), initialSaveBank1: Ff7BattleSession.CaptureNativeSaveBank(variables), previousFormation: previousFormation);
    }

    private void AdvanceBattle(bool waitForCommand, bool confirmLimit, bool escapeHeld, bool turnBased)
    {
        Ff7BattleSession battle = Battle!;
        battle.Advance(waitForCommand, confirmLimit, escapeHeld, turnBased);
        if (!battle.FormationTransitionReady)
        {
            return;
        }
        if (battle.GuestVitalsBindings.Count != 0)
        {
            throw new NotSupportedException("FFVII formation transitions with a guest battle owner are unsupported.");
        }
        Ff7BattleSnapshot previous = battle.Capture();
        SceneFormation successor = GetBattleFormation(battle.Formation.NextBattleId);
        Battle = CreateBattle(successor, battle.RequestId, previous.RequestFlags, previous.RequestMode, previous.EntryMusicTrack ?? 0, BattleVariables(), previous);
    }

    private bool TryBeginWorldBattle(Ff7WorldRuntime world)
    {
        if (world.PendingBattle is not { } request)
        {
            return false;
        }
        WorldBattleStarting?.Invoke();
        world.CompleteSpecialBattleDeparture(request);
        SceneFormation formation = GetBattleFormation(request.Formation);
        FieldVariables variables = world.PrepareBattleVariables(request);
        if (!Progress.FullCombatEnabled)
        {
            Ff7Progress result = Ff7DebugVictory.Apply(Progress, formation, battleKernel!, FieldGeneration, request.Id, guestStoryCharacters: GuestFieldVitals?.StoryCharacters);
            if (request.FieldReturnSelector is ushort selector)
            {
                AdoptWorldField(world, world.ResolveFieldEntry(selector), new FieldVariables(world.CaptureNativeWorldExit(), new byte[256]), result, battleResult: 0);
            }
            else if (request.ReloadsWorld)
            {
                Ff7WorldRuntime destination = PrepareWorldBattleReturn(world, request, null);
                World = destination;
                Progress = result;
                world.Dispose();
            }
            else
            {
                world.ResumeWorldBattle(request, variables);
                Progress = result;
            }
            return true;
        }
        LoadBattlePresentationResources();
        int track = world.Process.Audio?.LastPublishedTrack ?? 0;
        Battle = CreateBattle(formation, request.Id, request.Flags, 0, track, variables);
        return true;
    }

    private void RestoreBattle(Ff7BattleSnapshot snapshot)
    {
        SceneFormation formation = GetBattleFormation(snapshot.FormationId);
        if (snapshot.InitialFormationId is int initial)
        {
            HashSet<int> visited = [];
            SceneFormation step = GetBattleFormation(initial);
            bool reached = false;
            while (step.NextBattleId != -1 && visited.Add(step.FormationId))
            {
                step = GetBattleFormation(step.NextBattleId);
                if (step.FormationId == formation.FormationId)
                {
                    reached = true;
                    break;
                }
            }
            if (!reached)
            {
                throw new InvalidDataException("FFVII battle snapshot does not follow its original formation chain.");
            }
        }
        LoadBattlePresentationResources();
        Battle = new(formation, Progress, battleKernel!, executable.NativeMateriaStatModifiers, snapshot.RequestId, snapshot: snapshot, animations: battleAnimations, limits: battleLimits, beatRushSymbols: beatRushSymbols, cameraScripts: LoadBattleCameraScripts(snapshot.Layout), cameraPresentationCounter: battleCameraPresentationCounter, silkDirection: battleSilkDirection, initialSaveBank1: Ff7BattleSession.CaptureNativeSaveBank(BattleVariables()));
    }

    private FieldVariables BattleVariables() => World is { PendingBattle: { } request } world ? world.PrepareBattleVariables(request) : Machine.Variables;

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

    public Ff7VictoryProjection PreviewBattleVictory()
    {
        Ff7BattleSession battle = Battle ?? throw new InvalidOperationException("FFVII has no battle result to preview.");
        return battle.ProjectVictory(Progress, FieldGeneration);
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
        if (World is null)
        {
            Host.ValidateBattleResultOwner(battle.RequestId);
        }
        Ff7Progress result = battle.ProjectVictory(Progress, FieldGeneration).Progress;
        if (TryFinishWorldBattle(result, battle))
        {
            return true;
        }
        int nativeResult = battle.ResolveNativeFieldResult(0);
        FinishFieldBattle(result, battle, nativeResult);
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
        if (TryFinishWorldBattle(result, battle))
        {
            return true;
        }
        int nativeResult = battle.ResolveNativeFieldResult(Ff7NativeBattleEscape.FieldResult);
        FinishFieldBattle(result, battle, nativeResult);
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
        if (TryFinishWorldBattle(result, battle))
        {
            return true;
        }
        int nativeResult = battle.ResolveNativeFieldResult(1);
        FinishFieldBattle(result, battle, nativeResult);
        return true;
    }

    private void FinishFieldBattle(Ff7Progress result, Ff7BattleSession battle, int nativeResult)
    {
        Host.ValidateBattleResultOwner(battle.RequestId);
        if (battle.Outcome is Ff7BattleOutcome.Escaped or Ff7BattleOutcome.Defeat)
        {
            result = result with { FieldBattleResults = [.. (result.FieldBattleResults ?? []), new(FieldGeneration, battle.RequestId, battle.Formation.FormationId, battle.Outcome)] };
        }
        battle.PublishNativeSaveWrites(Machine.Variables);
        Host.PublishBattleResult(battle.RequestId, nativeResult);
        FinishBattle(result, battle);
    }

    private bool TryFinishWorldBattle(Ff7Progress result, Ff7BattleSession battle)
    {
        if (World is not { PendingBattle: { } occurrence } world)
        {
            return false;
        }
        world.ValidateBattleOwner(battle.RequestId);
        if (battle.Outcome != Ff7BattleOutcome.Victory)
        {
            result = result with { WorldBattleResults = [.. result.WorldBattleResults, new(FieldGeneration, battle.RequestId, battle.Formation.FormationId, battle.Outcome)] };
        }
        if (occurrence.FieldReturnSelector is ushort selector && battle.Outcome == Ff7BattleOutcome.Victory)
        {
            FieldVariables exitVariables = new(world.CaptureNativeWorldExit(), new byte[256]);
            battle.ApplyNativeSaveWrites(exitVariables);
            AdoptWorldField(world, world.ResolveFieldEntry(selector), exitVariables, result, battle.ResolveNativeFieldResult(0), battle);
            return true;
        }
        if (occurrence.ReloadsWorld || occurrence.ReturnsToField && battle.Outcome == Ff7BattleOutcome.Escaped)
        {
            Ff7WorldRuntime destination = PrepareWorldBattleReturn(world, occurrence, battle);
            FinishBattle(result, battle);
            World = destination;
            world.Dispose();
            return true;
        }
        FieldVariables variables = world.PrepareBattleVariables(occurrence);
        battle.PublishNativeSaveWrites(variables);
        world.ResumeWorldBattle(occurrence, variables, battle.Outcome == Ff7BattleOutcome.Escaped);
        FinishBattle(result, battle);
        return true;
    }

    private Ff7WorldRuntime PrepareWorldBattleReturn(Ff7WorldRuntime world, Ff7WorldBattleRequest occurrence, Ff7BattleSession? battle)
    {
        FieldVariables exitVariables = new(world.CaptureNativeWorldExit(), new byte[256]);
        battle?.ApplyNativeSaveWrites(exitVariables);
        byte[] persistent = exitVariables.CopyPersistent();
        byte[] party = persistent.AsSpan(0x109, 3).ToArray();
        Ff7WorldRuntime destination = new(installation, persistent, occurrence.FieldReturnSelector ?? world.Process.EntrySelector, party, world.Resources.World, entryModule: 1)
        {
            EncounterSeed = Clock.ElapsedSeconds
        };
        if (battle?.Outcome == Ff7BattleOutcome.Escaped)
        {
            destination.SetBattleReturnEscape();
        }
        return destination;
    }

    private void FinishBattle(Ff7Progress result, Ff7BattleSession battle)
    {
        Progress = result;
        battleBigShotChargeRotation = battle.BigShotChargeRotation;
        battleBigShotCounterRotation = battle.BigShotCounterRotation;
        battleSearchScopeScaleCounter = battle.SearchScopeScaleCounter;
        battleSilkDirection = battle.SilkDirection;
        battleCameraPresentationCounter = battle.CameraPresentationCounter;
        Battle = null;
    }
}
