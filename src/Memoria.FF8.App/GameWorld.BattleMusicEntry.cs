using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private NativeBattleMusicEntryResources? battleMusicEntryResources;

    private NativeBattleMusicEntryResources BattleMusicEntryResources => battleMusicEntryResources ??= new NativeMusicCatalog(assets.GameDirectory).ReadBattleEntryResources();

    private BattleSession StartNativeBattleSession(int encounter, ushort flags, byte seed, BattleAtbMode mode)
    {
        PreparedNativeBattleMusicEntry prepared = Events.PrepareNativeBattleMusicEntry(checked((ushort)encounter), flags, BattleMusicEntryResources, session.State.NativeBattleMemory?.MusicProcess?.ReturnChannel);
        BattleDefinition definition = CreateEncounterBattle(encounter, seed, flags) with { NativeGlobalSoundProcess = prepared.Native.SoundProcess, NativeMusicProcess = prepared.Native.MusicProcess };
        BattleSession candidate = CreateLiveBattleSession(definition, session.State.GameplayPreferences with { AtbMode = mode });
        Events.CommitNativeBattleMusicEntry(prepared, _ => session.SetBattle(candidate.Capture()));
        Events.SynchronizeNativeBattleMusicProcess(candidate.NativeMusicProcess);
        return candidate;
    }

    private void PublishBattleSnapshot(BattleSession? active)
    {
        BattleSnapshot? snapshot = active?.Capture();
        session.SetBattle(snapshot);
        Events.SynchronizeNativeBattleMusicProcess(snapshot?.NativeMusicProcess);
    }

    private void ValidateBattleMusicHostState(BattleSession active)
    {
        if (active.NativeMusicProcess is { } music && !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(music.Buffers), JsonSerializer.SerializeToElement(Events.Scene.MusicProcess)))
        {
            throw new InvalidDataException("Saved battle music buffers disagree with the same live physical owner projected by its field/world host.");
        }
    }
}
