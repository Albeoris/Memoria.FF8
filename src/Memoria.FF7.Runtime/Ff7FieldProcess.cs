using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7Actor
{
    public required int Slot { get; init; }
    public required int Entity { get; init; }
    public bool EntityBound { get; init; } = true;
    public required int Model { get; init; }
    public int PartyCharacter { get; init; } = -1;
    public int X4096 { get; init; }
    public int Y4096 { get; init; }
    public int Z4096 { get; init; }
    public int Triangle { get; init; }
    public int Direction { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public int OffsetZ { get; init; }
    public Ff7OffsetProcess? Offset { get; init; }
    public Ff7NativeModelEffectState? ModelEffect { get; init; }
    public bool AutomaticBlinkDisabled { get; init; }
    public byte NativeEyeLeft { get; init; }
    public byte NativeEyeRight { get; init; }
    public ushort BlinkCountdown { get; init; }
    public Ff7LadderProcess? Ladder { get; init; }
    public Ff7JumpProcess? Jump { get; init; }
    public int TurnStart { get; init; }
    public int TurnEnd { get; init; }
    public int TurnMode { get; init; }
    public int TurnStep { get; init; }
    public int TurnSteps { get; init; }
    public bool Visible { get; init; }
    public bool TalkEnabled { get; init; } = true;
    public bool CollisionEnabled { get; init; } = true;
    public int TalkRadius { get; init; }
    public int CollisionRadius { get; init; }
    public int MoveSpeed { get; init; }
    public int AnimationSpeed { get; init; } = 16;
    public int BaseAnimationSpeed { get; init; } = 16;
    public int AnimationStatus { get; init; }
    public int DefaultAnimationSpeed { get; init; } = 16;
    public int DefaultAnimation { get; init; }
    public int FirstAnimationFrame { get; init; }
    public int LastAnimationFrame { get; init; } = -1;
    public bool HoldAnimation { get; init; }
    public int Animation { get; init; }
    public int? RequestedAnimation { get; init; }
    public int AnimationFrame16 { get; init; }
    public bool AnimationLoop { get; init; } = true;
    public bool AnimationStopped { get; init; }
    public bool AnimationFinished { get; init; }
    public bool PlayerControlled { get; init; }
}

public sealed record Ff7Window(int Id, int X, int Y, int Width, int Height, int Style = 0, int Flags = 0, int Rows = 0)
{
    public byte SpecialType { get; init; }
    public byte SpecialX { get; init; }
    public byte SpecialY { get; init; }
}
public sealed record Ff7Dialogue(long RequestId, int Window, int Text, int? FirstChoice, int? LastChoice, int Selected, int Page = 0, bool Completed = false, int TokenPosition = 0, int WaitTicks = 0, bool AwaitingInput = false, Ff7DialoguePresentation? Presentation = null)
{
    public bool TextVisible => !Completed && (Presentation?.NativeState ?? 2) >= 2;
    public bool ChoiceReady => !Completed && AwaitingInput && FirstChoice is not null && (Presentation is null || Presentation.NativeState == 6);
}
public sealed record Ff7DialoguePresentation(int NativeState, int AnimatedWidth, int AnimatedHeight, short RevealBudget = 0, short FastCounter = 1, int ExpansionOffset = 0, bool ConfirmPending = false, int Row = 0);
public readonly record struct Ff7DialogueBounds(float X, float Y, int Width, int Height);
public sealed record Ff7Line(int Slot, int Entity, short X1, short Y1, short Z1, short X2, short Y2, short Z2, byte Enabled = 1, byte SlipDisabled = 0, bool Touch = false, bool Across = false, bool Push = false, bool Talk = false, bool Enter = false, bool Leave = false, byte ProximityAngle = 0, bool OnLine = false);
public sealed record Ff7JumpProcess(long RequestId, int TargetX, int TargetY, int TargetTriangle, int Duration, int StartX = 0, int StartY = 0, int StartZ = 0, int TargetZ = 0, int VerticalVelocity = 0, int Step = 0, bool Initialized = false, bool Completed = false);
public sealed record Ff7LadderProcess(long RequestId, int TargetX, int TargetY, int TargetZ, int TargetTriangle, byte Mode, int StartX = 0, int StartY = 0, int StartZ = 0, int Steps = 0, int Step = 0, bool Initialized = false, bool Completed = false);
public sealed record Ff7OffsetProcess(int StartX, int StartY, int StartZ, int TargetX, int TargetY, int TargetZ, int Mode, int Steps, int Step = 0, bool Completed = false);
public sealed record Ff7Menu(long RequestId, int Mode, int Parameter, bool Completed = false);
public sealed record Ff7Movie(int PreparedId, bool Playing, int Frame, bool Completed, long? RequestId, bool CameraDisabled = false, Ff7MovieCameraConsumption? CameraConsumption = null);
public sealed record Ff7MovieCameraConsumption(int Frame, long WordsBeforeRead, long WordsAfterRead);
public sealed record Ff7HostReceipt(FieldHostRequest Request, FieldHostReply Reply);
public sealed record Ff7CameraScroll(int Mode, int StartX, int StartY, int TargetX, int TargetY, int Steps, int Step = 0, bool Completed = false);
public sealed record Ff7CameraViewOffset(byte Mode, byte Steps, short Start, short Target, byte Step = 0);
public sealed record Ff7CameraTracking(int Mode, int Slot, int Steps, int StartX, int StartY, int Step = 0, bool Completed = false);
public sealed record Ff7PartyJoin(long RequestId, int LeaderEntity, Ff7JoinFollower[] Followers, bool Split = false);
public sealed record Ff7JoinFollower(int Entity, int Stage = 0, int PreviousSpeed = 0, short TargetX = 0, short TargetY = 0, bool Arrived = false);

public sealed record Ff7Motion(long RequestId, int Entity, int TargetX, int TargetY, bool Turn, bool Animated, Ff7EntityMotionTarget? EntityTarget = null, bool Completed = false);
public sealed record Ff7EntityMotionTarget(int Entity, int X4096, int Y4096, short Radius);
public sealed record Ff7AnimationRequest(long RequestId, int Entity);
public sealed record Ff7SoundVolumeFade(int Target, int RatePerSecond, int AccumulatedMilliseconds = 0, bool Started = false);
public sealed record Ff7MusicVolumeFade(int Target, int RatePerSecond, int AccumulatedMilliseconds = 0, int SubmillisecondTicks = 0);
public enum Ff7GlobalCommand
{
    None,
    Movie,
    Battle,
    Transition,
    Menu
}

public sealed record Ff7FieldProcess
{
    public byte BlinkRandomIndex { get; init; }
    public bool BlinkStateInitialized { get; init; }
    public long Tick { get; init; }
    public Ff7Line[] Lines { get; init; } = [];
    public bool LineBlocked { get; init; }
    public Ff7Actor[] Actors { get; init; } = [];
    public Ff7Window[] Windows { get; init; } = [];
    public Ff7Dialogue[] Dialogues { get; init; } = [];
    public Ff7HostReceipt[] Receipts { get; init; } = [];
    public Ff7Motion[] Motions { get; init; } = [];
    public Ff7AnimationRequest[] AnimationRequests { get; init; } = [];
    public ushort[] BackgroundStates { get; init; } = new ushort[256];
    public ushort[] PaletteBuffer { get; init; } = new ushort[1024];
    public ushort[][] PaletteRows { get; init; } = [];
    public bool PaletteRowsUseCachedIndices { get; init; }
    public long PaletteVersion { get; init; }
    public byte[] BlockedTriangles { get; init; } = [];
    public Ff7Menu? Menu { get; init; }
    public Ff7PartyJoin? Join { get; init; }
    public Ff7Movie? Movie { get; init; }
    public bool MovieBackgroundEnabled { get; init; }
    public bool MovieCameraDisabled { get; init; }
    public FieldFadeCommand? Fade { get; init; }
    public Ff7FadeProcess? FadeState { get; init; }
    public FieldMusicCommand? Music { get; init; }
    public byte MusicLock { get; init; }
    public long MusicTicks { get; init; }
    public int? NativeMusicTrack { get; init; }
    public int? SavedNativeMusicTrack { get; init; }
    public int MusicVolume { get; init; } = 127;
    public Ff7MusicVolumeFade? MusicVolumeFade { get; init; }
    public int[] SoundChannelVolumes { get; init; } = [127, 127, 127, 127, 127];
    public Ff7SoundVolumeFade?[] SoundChannelVolumeFades { get; init; } = new Ff7SoundVolumeFade?[5];
    public bool[] SoundChannelStoppedByFade { get; init; } = new bool[5];
    public int SoundVolumeClockSubmillisecondTicks { get; init; }
    public int SavedMusicTrack { get; init; } = -1;
    public int NextFieldMusicTrack { get; init; } = -1;
    public int NextBattleMusicTrack { get; init; } = -1;
    public FieldAudioCommand? Audio { get; init; }
    public bool PlayerLocked { get; init; }
    public bool BattlesDisabled { get; init; }
    public bool MapJumpDisabled { get; init; }
    public bool MenuDisabled { get; init; }
    public int BattleMode { get; init; }
    public int BattleMode2 { get; init; }
    public int BattleTable { get; init; }
    public int BattleResult { get; init; }
    public int CameraX { get; init; }
    public int CameraY { get; init; }
    public short? CameraFocusOffset { get; init; }
    public Ff7CameraViewOffset? CameraViewOffset { get; init; }
    public Ff7CameraTracking? CameraTracking { get; init; }
    public bool? CameraFollowPlayer { get; init; }
    public int CameraEntity { get; init; } = -1;
    public Ff7CameraScroll? CameraScroll { get; init; }
    public bool CameraLocked { get; init; }
    public Ff7CameraShakeAxis? CameraShakeX { get; init; }
    public Ff7CameraShakeAxis? CameraShakeY { get; init; }
    public bool CursorDisabled { get; init; }
    public int MapName { get; init; }
    public int MapDisplay { get; init; }
    public FieldHostRequest? EncounterRequest { get; init; }
    public byte EncounterInitiative { get; init; }
    public ushort? PreloadedMap { get; init; }
    public FieldTransitionCommand? Transition { get; init; }
    public long? GlobalRequestId { get; init; }
    public Ff7GlobalCommand GlobalCommand { get; init; }

    public void Validate()
    {
        CameraShakeX?.Validate();
        CameraShakeY?.Validate();
        FadeState?.Validate();
        if (SoundChannelVolumes is null || SoundChannelVolumes.Length != 5 || SoundChannelVolumes.Any(volume => volume is < 0 or > 127))
        {
            throw new InvalidDataException("FFVII native sound channel volumes are corrupt.");
        }
        if (SoundChannelVolumeFades is not { Length: 5 } || SoundChannelStoppedByFade is not { Length: 5 } || SoundVolumeClockSubmillisecondTicks is < 0 or >= (int)TimeSpan.TicksPerMillisecond || SoundChannelVolumeFades.Any(fade => fade is not null && (fade.Target is < 0 or > 127 || fade.RatePerSecond is < -7620 or > 7620)))
        {
            throw new InvalidDataException("FFVII native sound volume ramp continuation is corrupt.");
        }
        if (MusicVolumeFade is { } musicFade && (musicFade.Target is < 0 or > 127 || musicFade.RatePerSecond is < -7620 or > 7620 || musicFade.SubmillisecondTicks is < 0 or >= (int)TimeSpan.TicksPerMillisecond))
        {
            throw new InvalidDataException("FFVII music volume fade continuation is corrupt.");
        }
        if (MusicTicks < 0 || NativeMusicTrack is < 0 or > 98 || SavedNativeMusicTrack is < 0 or > 98 || MusicVolume is < 0 or > 127)
        {
            throw new InvalidDataException("FFVII music continuation has invalid native track, clock or volume state.");
        }
        if (Tick < 0 || Actors is null || Windows is null || Dialogues is null || Receipts is null || Motions is null || AnimationRequests is null || BackgroundStates is not { Length: 256 })
        {
            throw new InvalidDataException("FFVII field process is missing required owners.");
        }
        if (Actors.Any(actor => actor is null || actor.Slot < 0 || actor.Entity < 0 || actor.Model < 0 || actor.Direction is < 0 or > 255 || actor.Animation < 0 || actor.RequestedAnimation is < 0 or > 127 || actor.AnimationFrame16 < 0) || Actors.Where(actor => actor.EntityBound).Select(actor => actor.Entity).Distinct().Count() != Actors.Count(actor => actor.EntityBound) || Actors.Select(actor => actor.Slot).Distinct().Count() != Actors.Length)
        {
            throw new InvalidDataException("FFVII field actor ownership is invalid.");
        }
        if (Actors.Count(actor => actor.PlayerControlled) > 1)
        {
            throw new InvalidDataException("FFVII controlled-actor selection must identify a unique physical slot.");
        }
        if (CameraTracking is Ff7CameraTracking tracking && (tracking.Mode is < 1 or > 3 || tracking.Slot < 0 || !Actors.Any(actor => actor.Slot == tracking.Slot) || tracking.Steps is < 0 or > short.MaxValue || tracking.Step < 0 || tracking.Step > tracking.Steps))
        {
            throw new InvalidDataException("FFVII camera tracking has invalid native target ownership or timing.");
        }
        foreach (Ff7Actor actor in Actors)
        {
            actor.ModelEffect?.Validate();
            if (actor.NativeEyeLeft > 2 || actor.NativeEyeRight > 2)
            {
                throw new InvalidDataException("FFVII native eye state has an unsupported applied mode.");
            }
            if (actor.Jump is Ff7JumpProcess jump && (actor.Ladder is not null || jump.RequestId < 1 || jump.TargetTriangle < 0 || jump.Duration is < 1 or > short.MaxValue || jump.Step < 0 || jump.Step > jump.Duration))
            {
                throw new InvalidDataException("FFVII jump continuation has invalid native owners or steps.");
            }
            if (actor.Ladder is Ff7LadderProcess ladder && (ladder.RequestId < 1 || ladder.Mode > 3 || ladder.TargetTriangle < 0 || ladder.Steps < 0 || ladder.Step < 0 || ladder.Step > ladder.Steps || ladder.Initialized && ladder.Steps == 0))
            {
                throw new InvalidDataException("FFVII ladder continuation has invalid native owners or steps.");
            }
        }
        if (Join is not null && (Join.RequestId < 1 || Join.Followers is null || Join.Followers.Length > 2 || !Actors.Any(actor => actor.EntityBound && actor.Entity == Join.LeaderEntity) || Join.Followers.Any(follower => follower is null || follower.Stage is < 0 or > 3 || follower.PreviousSpeed is < 0 or > 65535 || !Actors.Any(actor => actor.EntityBound && actor.Entity == follower.Entity)) || Join.Followers.Select(follower => follower.Entity).Distinct().Count() != Join.Followers.Length))
        {
            throw new InvalidDataException("FFVII JOIN continuation has invalid native owners or stages.");
        }
        if (Windows.Any(window => window is null || window.Id is < 0 or > 255 || window.Width < 0 || window.Height < 0) || Windows.Select(window => window.Id).Distinct().Count() != Windows.Length || Receipts.Any(receipt => receipt is null || receipt.Request is null || receipt.Request.Id < 1 || receipt.Reply is null) || Receipts.Select(receipt => receipt.Request.Id).Distinct().Count() != Receipts.Length)
        {
            throw new InvalidDataException("FFVII field window or request ownership is invalid.");
        }
        if (Dialogues.Any(dialogue => dialogue is null || dialogue.RequestId < 1 || dialogue.Text < 0 || Windows.All(window => window.Id != dialogue.Window)) || Movie is { Frame: < 0 })
        {
            throw new InvalidDataException("FFVII field dialogue or movie continuation is invalid.");
        }
        if (Movie?.CameraConsumption is Ff7MovieCameraConsumption consumption && (consumption.Frame < -1 || consumption.WordsBeforeRead < 0 || consumption.WordsAfterRead < consumption.WordsBeforeRead || consumption.WordsAfterRead - consumption.WordsBeforeRead is not (0 or 10)))
        {
            throw new InvalidDataException("FFVII movie camera consumption is invalid.");
        }
    }
}
