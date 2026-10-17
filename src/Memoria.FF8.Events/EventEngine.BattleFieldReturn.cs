using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Completion authority supplied only by an actual471F70 kind3 resource reload. No live producer is implemented yet.</summary>
public sealed record NativeBattleFieldReloadCompletion([property: JsonRequired] int Version, [property: JsonRequired] string ExecutableHash, [property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] long BattleEntryFrame, [property: JsonRequired] byte NativeResult)
{
    public void Validate()
    {
        if (Version != 1 || ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || FieldId is < 72 or > ushort.MaxValue || ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || BattleEntryFrame < 1 || NativeResult is not (2 or 4))
        {
            throw new InvalidDataException("Native kind3 completion requires its actual original field/program and supported ordinary battle result2/4.");
        }
    }
}

public sealed record NativeBattleFieldReturnReceipt(NativeBattleFieldReloadCompletion Reload, NativeBattleMusicReturnReceipt Music);

public sealed partial class EventEngine
{
    public sealed class PreparedNativeBattleFieldReturn
    {
        internal PreparedNativeBattleFieldReturn()
        {
        }

        public required NativeBattleFieldReturnOwners Owners { get; init; }
        public required NativeBattleFieldReloadCompletion Reload { get; init; }
        internal SceneEventState SourceScene { get; init; } = null!;
        internal long SourceFrame { get; init; }
        internal NativeMusicProcessState SourceBuffers { get; init; } = null!;
        internal NativeGlobalSoundProcess SourceSound { get; init; } = null!;
        internal NativeFieldResumeTail Tail { get; init; } = null!;
        internal Action PublishAnimations { get; init; } = null!;
        internal NativeBattleCompletion Battle { get; init; } = null!;
        internal ushort PersistentFlags { get; init; }
        internal NativeMusicChannelState? SourceChannel { get; init; }
        internal byte? SourceGain { get; init; }
        internal NativeWorldControlState SourceControls { get; init; } = null!;
        internal NativeSeeDState SourceSeeD { get; init; } = null!;
        internal NativeExamTimerState SourceTimer { get; init; } = null!;
        internal NativeSalaryPresentation SourceSalary { get; init; } = null!;
        internal NativeVibrationProcessState SourceVibration { get; init; } = null!;
        internal NativeFieldFadeState SourceFade { get; init; } = null!;
        internal NativeCharacterVitals SourceVitals { get; init; } = null!;
        internal NativeEventMemory SourceMemory { get; init; } = null!;
    }

    public NativeBattleFieldReloadCompletion RequireNativeBattleFieldReload(byte nativeResult)
    {
        NativeBattleCompletion battle = PendingInteractiveBattle ?? throw new NotSupportedException("Native field return requires its suspended interactive BATTLE owner.");
        if (nativeResult is not (2 or 4))
        {
            throw new NotSupportedException("Native battle result1/3/5 requires its actual4706B0 saved183/fresh-field/game-over return branch.");
        }
        NativeBattleFieldReloadCompletion reload = Scene.NativeBattleFieldReload ?? throw new NotSupportedException("Native kind3 field return requires actual471F70/471010/52BC00 resource-reload completion; the paused field VM or kind6 menu path cannot establish it.");
        reload.Validate();
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } proof || !proof.IsFrom(NativeRuntimeProfile, FieldId, programHash) || reload.FieldId != FieldId || reload.ProgramHash != programHash || reload.BattleEntryFrame != battle.Frame || reload.NativeResult != nativeResult)
        {
            throw new InvalidDataException("Native kind3 reload completion disagrees with the actual suspended field and battle return.");
        }
        return reload;
    }

    public PreparedNativeBattleFieldReturn PrepareNativeBattleFieldReturn(NativeBattleMusicProcessState process, ushort flags, byte nativeResult, NativeBattleMusicEntryResources resources)
    {
        NativeBattleFieldReloadCompletion reload = RequireNativeBattleFieldReload(nativeResult);
        if (!services.CanStoreNativeBattleFieldReturn || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(process.Buffers), JsonSerializer.SerializeToElement(Scene.MusicProcess)))
        {
            throw new NotSupportedException("Native field return requires atomic battle/global/tail transport and the same live physical music buffers.");
        }
        NativeBattleCompletion battle = PendingInteractiveBattle!;
        NativeGlobalSoundProcess sound = RequireMusicSoundProcess();
        bool keep = (flags & 0x10) != 0;
        NativeBattleMusicReturnPreparation music = NativeBattleMusicReturn.Prepare52B4F0(process, sound, flags, keep ? null : Scene.MusicChannel108?.MusicId, keep ? null : Scene.SoundMusicVolume197, resources, ReadMusicCurrentField);
        ushort persistent = Scene.NativeBattleFlagWrites is { Count: > 0 } writes ? writes[^1].PersistentFlags : throw new NotSupportedException("Native52B4F0(0) requires actual saved WORD182 before restoring active battle flags.");
        NativeFieldFadeState fade = services.FieldFade ?? throw new NotSupportedException("Native field return requires the retained physical and saved fade banks.");
        NativeVibrationProcessState vibration = services.VibrationProcess ?? throw new NotSupportedException("Native field return requires its actual controller queue.");
        NativeFieldResumeTail tail = PrepareNativeFieldResumeTail(fade.ResumeKind6().SetMapFadeDisabled(false), vibration.MarkControllerPending(0), music.MusicProcess.Buffers);
        tail = tail with { Timer = tail.Timer with { SaveGlobalMask4 = (persistent & 4) != 0 } };
        NativeFieldActorAnimation[] animations = PrepareNativeFieldAnimations(Scene.NativeActorObjectIds.Where(objectId => (actors[objectId].NativePartyFlags ?? throw new NotSupportedException("Native52B9F0 animation restore requires actual actor DWORD352.")).AutoAnimation4).ToArray());
        return new()
        {
            Owners = new(music, tail.Controls, tail.SeeD, tail.Timer, tail.Salary, tail.Vibration, tail.Fade, tail.Memory), Reload = reload,
            SourceScene = Scene, SourceFrame = Frame, SourceBuffers = Scene.MusicProcess!, SourceSound = sound, Tail = tail,
            Battle = battle, PersistentFlags = persistent, SourceChannel = Scene.MusicChannel108, SourceGain = Scene.SoundMusicVolume197,
            SourceControls = services.WorldControls!, SourceSeeD = services.SeeD!, SourceTimer = services.ExamTimer!, SourceSalary = services.SalaryPresentation!, SourceVibration = vibration, SourceFade = fade, SourceVitals = services.CharacterVitals!, SourceMemory = EventMemory,
            PublishAnimations = () =>
            {
                foreach (int objectId in Scene.NativeActorObjectIds)
                {
                    ActorState actor = actors[objectId];
                    if (actor.NativePartyFlags is not { AutoAnimation4: true })
                    {
                        PublishNativeAnimation(actor, true);
                    }
                }
                foreach (NativeFieldActorAnimation animation in animations)
                {
                    SelectFullNativeAnimation(actors[animation.ObjectId], animation.Idle, true);
                }
            }
        };
    }

    public void CommitNativeBattleFieldReturn(PreparedNativeBattleFieldReturn prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        bool keep = prepared.Owners.Music.Receipt.KeptMusic;
        if (!ReferenceEquals(Scene, prepared.SourceScene) || Frame != prepared.SourceFrame || !ReferenceEquals(Scene.MusicProcess, prepared.SourceBuffers) || !ReferenceEquals(services.GlobalSoundProcess, prepared.SourceSound) || !ReferenceEquals(services.WorldControls, prepared.SourceControls) || !ReferenceEquals(services.SeeD, prepared.SourceSeeD) || !ReferenceEquals(services.ExamTimer, prepared.SourceTimer) || !ReferenceEquals(services.SalaryPresentation, prepared.SourceSalary) || !ReferenceEquals(services.VibrationProcess, prepared.SourceVibration) || !ReferenceEquals(services.FieldFade, prepared.SourceFade) || !ReferenceEquals(services.CharacterVitals, prepared.SourceVitals) || !ReferenceEquals(EventMemory, prepared.SourceMemory) || !keep && (Scene.MusicChannel108 != prepared.SourceChannel || Scene.SoundMusicVolume197 != prepared.SourceGain) || Scene.NativeBattleFieldReload != prepared.Reload)
        {
            throw new InvalidOperationException("Prepared native field return no longer matches its current physical owners.");
        }
        services.SetNativeBattleFieldReturn(prepared.Owners);
        PublishNativeFieldResumeScene(prepared.Tail);
        if (prepared.Owners.Music.Receipt.Playback is not null)
        {
            Scene.MusicChannel108 = Scene.MusicChannel108! with { ChannelId = 0 };
        }
        NativeBattleCompletion source = prepared.Battle;
        Scene.NativeBattleFlagWrites!.Add(new(1, NativeBattleFlagWriter.FieldBattleReturn, FieldId, programHash, Frame, source.ObjectId, source.ScriptId, source.Operation, null, prepared.PersistentFlags, prepared.PersistentFlags));
        prepared.PublishAnimations();
        Scene.NativeBattleFieldReturn = new(prepared.Reload, prepared.Owners.Music.Receipt);
        Scene.NativeBattleFieldReload = null;
        AudioCommandExecuted?.Invoke(prepared.Owners.Music.Receipt);
    }
}
