using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private NativeBattleMusicEntryResources? battleMusicEntryResources;
    private long synchronizedNativeBattleCallbacks;

    private NativeBattleMusicEntryResources BattleMusicEntryResources => battleMusicEntryResources ??= new NativeMusicCatalog(assets.GameDirectory).ReadBattleEntryResources();

    private BattleSession StartNativeBattleSession(int encounter, ushort flags, byte seed, BattleAtbMode mode)
    {
        PreparedNativeBattleMusicEntry prepared = Events.PrepareNativeBattleMusicEntry(checked((ushort)encounter), flags, BattleMusicEntryResources, session.State.NativeBattleMemory?.MusicProcess?.ReturnChannel);
        BattleDefinition definition = CreateEncounterBattle(encounter, seed, flags) with { NativeGlobalSoundProcess = prepared.Native.SoundProcess, NativeMusicProcess = prepared.Native.MusicProcess };
        BattleSession candidate = AttachNativeEffectResources(CreateLiveBattleSession(definition, session.State.GameplayPreferences with { AtbMode = mode }));
        synchronizedNativeBattleCallbacks = 0;
        Events.CommitNativeBattleMusicEntry(prepared, _ =>
        {
            //48B7E0 refreshes the active characters and calls495EC0 before publishing the entry banks.
            NativeCharacterVitals vitals = session.State.CharacterVitals!.RebuildParty();
            session.SetNativePartyMenuOwners(vitals, session.State.Economy!, session.State.ShotAvailability!, session.State.SalaryPresentation!);
            session.SetBattle(candidate.Capture());
        });
        Events.SynchronizeNativeBattleMusicProcess(candidate.NativeMusicProcess);
        return candidate;
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
            CompleteNativeDefeatIfReady();
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
            if (Events.PendingMenuBattle is not null)
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
