using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Constructs a fresh normal-load event process from semantic facts without executing a field program.</summary>
    public void InitializeGameplaySave(NativeFieldRuntimeProfile profile, GameplaySaveSceneFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        facts.Validate();
        if (program is not null || profile is not { IsVerified: true } || services is not GameSession session || session.State.Scene is not (SceneKind.Field or SceneKind.WorldMap) || session.State.EventState is not null || session.State.DepartingFieldState is not null || session.State.WorldEventState is not null || session.State.Battle is not null || session.State.PendingBattleEncounter is not null || session.State.FieldId != facts.FieldId)
        {
            throw new InvalidOperationException("Gameplay save initialization requires a fresh event process matching its semantic destination.");
        }
        if (facts.FieldParty.Where(identity => identity != 255).Distinct().Count() != facts.FieldParty.Count(identity => identity != 255) || facts.PlaceId is < 0 or > ushort.MaxValue)
        {
            throw new InvalidDataException("Gameplay scene facts require unique party identities and a WORD place identifier.");
        }
        if (session.State.Scene == SceneKind.Field && (facts.Origin != GameplaySaveSceneFactsOrigin.Field || session.State.FieldLoadContext?.Origin != NativeFieldLoadOrigin.GameplaySave))
        {
            throw new InvalidOperationException("Gameplay field initialization requires its explicit normal-load context and current field facts.");
        }
        if (facts.PersistentBattleMode is not ushort persistent || facts.ActiveBattleMode is not ushort active)
        {
            throw new NotSupportedException("Semantic scene reconstruction requires the current saved battle-mode WORDs.");
        }
        NativeExamTimerState? timer = session.State.ExamTimer;
        if (timer is not null && (timer.Event182Mask4 != ((persistent & 4) != 0) || timer.Event183Mask1 != ((persistent & 0x100) != 0) || timer.SaveGlobalMask4 != ((active & 4) != 0)))
        {
            throw new InvalidDataException("Gameplay battle-mode WORDs require their canonical timer flag mirrors before scene initialization.");
        }
        if (session.State.Scene == SceneKind.WorldMap)
        {
            InitializeWorldSave(profile, facts.FieldParty);
        }
        else
        {
            InitializeColdMusicProcess(profile, NativeMusicBootstrap.GameplaySaveRule);
            Scene.MusicProcess = Scene.MusicProcess! with { Pending = false };
            Scene.NativeLayerZeroScrollCurrent = new NativeLayerZeroScrollCurrent(1, 0, 0);
            Scene.NativeBackgroundScrollCurrents = Enumerable.Range(1, 7).ToDictionary(layer => layer, _ => new NativeBackgroundScrollCurrent(1, 0, 0));
            Scene.NativeDrawPointDisplayMask = 0;
            Scene.NativeFieldParticleRandomPhase = 0;
            if (DrawPointParticleTables is not null)
            {
                Scene.NativeDrawPointParticles = NativeDrawPointParticles.Initialize(null, DrawPointParticlePhase(), DrawPointParticleTables);
            }
        }
        string identity = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(facts)));
        Scene.MusicProcess = Scene.MusicProcess! with { Bootstrap = new NativeMusicBootstrap(1, NativeMusicBootstrap.GameplaySaveRule, profile.ExecutableHash) };
        Scene.NativeBattleFlagWrites = [new NativeBattleFlagWrite(1, NativeBattleFlagWriter.GameplaySave, session.State.FieldId, identity, 0, -1, -1, -1, null, persistent, active)];
        Scene.Party.Clear();
        Scene.Party.AddRange(facts.FieldParty.Select(identity => (int)identity));
        Scene.Names.Clear();
        foreach ((int subject, string name) in facts.Names)
        {
            Scene.Names[subject] = name;
        }
        Scene.UnlockedTutorials.Clear();
        Scene.UnlockedTutorials.UnionWith(facts.UnlockedTutorials);
        Scene.PartyPower = facts.PartyPower;
        Scene.MenuEnabled = facts.MenuEnabled;
        Scene.RunEnabled = facts.RunEnabled;
        Scene.MapJumpDisabled = facts.MapJumpDisabled;
        Scene.PlaceId = facts.PlaceId;
        Scene.NativeSalaryMovementUnits = facts.PreviousSalaryMovement;
        Scene.WorldMusicSelection ??= NativeWorldMusicSelection.InitialValue();
        if (facts.Music is { } music)
        {
            Scene.NativeBattleMusic203 = music.BattleMusic ?? Scene.NativeBattleMusic203;
            SetSoundVolumeByte(197, music.MusicVolume ?? 127);
            SetSoundVolumeByte(198, music.SoundVolume ?? 127);
            if (music.WorldSelection is byte selected)
            {
                Scene.WorldMusicSelection = NativeWorldMusicSelection.FromRetainedPhysicalByte(selected, identity);
            }
        }
    }
}
