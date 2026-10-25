using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal sealed record PreparedBattleEntry(GameState Source, PreparedNativeBattleMusicEntry? Music, NativeBattleMusicEntryPreparation? CommittedMusic, BattleSession Battle);
    private NativeBattleMusicEntryResources? battleMusicEntryResources;
    private long synchronizedNativeBattleCallbacks;

    private NativeBattleMusicEntryResources BattleMusicEntryResources => battleMusicEntryResources ??= new NativeMusicCatalog(assets.GameDirectory).ReadBattleEntryResources();

    private BattleSession StartNativeBattleSession(int encounter, ushort flags, byte seed, BattleAtbMode mode)
    {
        return CommitPreparedBattleEntry(PrepareBattleEntry(encounter, flags, seed, mode));
    }

    internal PreparedBattleEntry PrepareBattleEntry(int encounter, ushort flags, byte seed, BattleAtbMode mode, PreparedNativeBattleMusicEntry? selectedMusic = null)
    {
        GameState source = session.State;
        PreparedNativeBattleMusicEntry prepared = selectedMusic ?? Events.PrepareNativeBattleMusicEntry(checked((ushort)encounter), flags, BattleMusicEntryResources, session.State.NativeBattleMemory?.MusicProcess?.ReturnChannel);
        BattleSession candidate = PrepareBattleEntrySession(encounter, flags, seed, mode, prepared.Native);
        return new(source, prepared, null, candidate);
    }

    internal PreparedBattleEntry PrepareCommittedBattleEntry(int encounter, ushort flags, byte seed, BattleAtbMode mode, NativeBattleMusicEntryPreparation music)
    {
        GameState source = session.State;
        ValidateCommittedBattleEntryMusic(music);
        if (music.Receipt.Encounter != encounter || music.Receipt.Flags != flags)
        {
            throw new InvalidOperationException("Committed native music identifies a different battle entry.");
        }
        BattleSession candidate = PrepareBattleEntrySession(encounter, flags, seed, mode, music);
        return new(source, null, music, candidate);
    }

    private BattleSession PrepareBattleEntrySession(int encounter, ushort flags, byte seed, BattleAtbMode mode, NativeBattleMusicEntryPreparation music)
    {
        BattleDefinition definition = CreateEncounterBattle(encounter, seed, flags) with { NativeGlobalSoundProcess = music.SoundProcess, NativeMusicProcess = music.MusicProcess };
        definition = PrepareDiagnosticCapabilities(definition);
        return AttachNativeEffectResources(CreateLiveBattleSession(definition, session.State.GameplayPreferences with { AtbMode = mode }));
    }

    private void ValidateCommittedBattleEntryMusic(NativeBattleMusicEntryPreparation music)
    {
        if (!ReferenceEquals(Events.Scene.NativeBattleMusicEntry, music.Receipt) || !ReferenceEquals(Events.Scene.MusicProcess, music.MusicProcess.Buffers) || !ReferenceEquals(session.State.NativeBattleMemory?.MusicProcess, music.MusicProcess) || !ReferenceEquals(session.GlobalSoundProcess, music.SoundProcess))
        {
            throw new InvalidOperationException("Asynchronous battle preparation requires the same already committed native music receipt and live owners.");
        }
    }

    internal BattleSession CommitPreparedBattleEntry(PreparedBattleEntry entry)
    {
        if (!ReferenceEquals(session.State, entry.Source))
        {
            throw new InvalidOperationException("Prepared battle entry no longer matches its suspended session.");
        }
        BattleSession candidate = entry.Battle;
        synchronizedNativeBattleCallbacks = 0;
        void Publish()
        {
            //48B7E0 refreshes the active characters and calls495EC0 before publishing the entry banks.
            NativeCharacterVitals vitals = session.State.CharacterVitals!.RebuildParty();
            session.SetCharacterVitals(vitals);
            session.SetBattle(candidate.Capture());
        }
        if (entry.Music is { } prepared && entry.CommittedMusic is null)
        {
            Events.CommitNativeBattleMusicEntry(prepared, _ => Publish());
        }
        else if (entry.Music is null && entry.CommittedMusic is { } committed)
        {
            ValidateCommittedBattleEntryMusic(committed);
            Publish();
        }
        else
        {
            throw new InvalidOperationException("Battle entry requires exactly one prepared or already committed native music receipt.");
        }
        Events.SynchronizeNativeBattleMusicProcess(candidate.NativeMusicProcess);
        return candidate;
    }

    internal NativeBattleMusicEntryPreparation BeginBattleEntryMusic(int encounter, ushort flags)
    {
        PreparedNativeBattleMusicEntry prepared = Events.PrepareNativeBattleMusicEntry(checked((ushort)encounter), flags, BattleMusicEntryResources, session.State.NativeBattleMemory?.MusicProcess?.ReturnChannel);
        Events.CommitNativeBattleMusicEntry(prepared, session.PublishNativeBattleEntryMusic);
        return prepared.Native;
    }

    private void PublishBattleSnapshot(BattleSession? active)
    {
        if (active is not null)
        {
            SynchronizeNativeBattleHostCallbacks(active);
        }
        session.SetLiveBattle(active);
        Events.SynchronizeNativeBattleMusicProcess(session.State.Battle?.NativeMusicProcess);
    }

    internal void SynchronizeBattlePresentation()
    {
        if (battle is { } active)
        {
            SynchronizeNativeBattleHostCallbacks(active);
            session.AcceptBattleAchievementFacts(active.EnemyDefeats);
            CompleteNativeDefeatIfReady();
        }
    }

    internal void CaptureBattleState()
    {
        if (battle is not null)
        {
            PublishBattleSnapshot(battle);
        }
    }

    private void SynchronizeNativeBattleHostCallbacks(BattleSession active)
    {
        long executed = active.NativeGraphics is null ? active.Frame : active.NativeHostCallbacks ?? throw new NotSupportedException("Native battle host transport requires its executed47CF60 callback ledger; explicitly migrate the named older checkpoint.");
        long remaining = executed - synchronizedNativeBattleCallbacks;
        if (remaining < 0)
        {
            throw new InvalidDataException("Native battle callbacks moved behind their published host clock.");
        }
        while (remaining > 0)
        {
            int callbacks = (int)Math.Min(remaining, 30000);
            if (diagnosticBattle is not null)
            {
                session.SetExamTimer((session.ExamTimer ?? throw new InvalidDataException("Diagnostic battle lost its process clock.")).AdvanceCallbacks(NativeTimerCallbackKind.Battle, callbacks));
            }
            else if (Events.PendingMenuBattle is not null)
            {
                Events.AdvanceMenuBattleCallbacks(callbacks);
            }
            else if (session.State.PendingBattleEncounter?.ScriptedOwner is not null)
            {
                Events.AdvanceInteractiveBattleCallbacks(callbacks);
            }
            else if (session.State.RandomEncounters?.Pending is { Scene: SceneKind.Field })
            {
                Events.AdvanceRandomBattleCallbacks(callbacks);
            }
            else if (session.State.RandomEncounters?.Pending is { Scene: SceneKind.WorldMap })
            {
                session.SetExamTimer(session.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.Battle, callbacks));
                session.SetWorldProcess(session.State.WorldProcess!.AdvanceMainLoopCallbacks(callbacks));
            }
            else
            {
                throw new InvalidDataException("Native battle callbacks have no suspended field/world encounter owner.");
            }
            synchronizedNativeBattleCallbacks += callbacks;
            remaining -= callbacks;
        }
        if (active.NativeExamTimer is { } timer)
        {
            if (session.ExamTimer is not { } root || !root.HasSameClock(timer))
            {
                throw new InvalidDataException("Native battle timer differs from its executed host clock.");
            }
            session.SetExamTimer(timer);
        }
    }

    private void ValidateBattleMusicHostState(BattleSession active)
    {
        if (active.NativeMusicProcess is { } music && !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(music.Buffers), JsonSerializer.SerializeToElement(Events.Scene.MusicProcess)))
        {
            throw new InvalidDataException("Saved battle music buffers disagree with the same live physical owner projected by its field/world host.");
        }
    }
}
