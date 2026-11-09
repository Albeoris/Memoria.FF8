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
    public int AnimationFrame16 { get; init; }
    public bool AnimationLoop { get; init; } = true;
    public bool AnimationStopped { get; init; }
    public bool AnimationFinished { get; init; }
    public bool PlayerControlled { get; init; }
}

public sealed record Ff7Window(int Id, int X, int Y, int Width, int Height, int Style = 0, int Flags = 0, int Rows = 0);
public sealed record Ff7Dialogue(long RequestId, int Window, int Text, int? FirstChoice, int? LastChoice, int Selected, int Page = 0, bool Completed = false, int TokenPosition = 0, int WaitTicks = 0, bool AwaitingInput = false);
public sealed record Ff7Movie(int PreparedId, bool Playing, int Frame, bool Completed, long? RequestId, bool CameraDisabled = false);
public sealed record Ff7HostReceipt(FieldHostRequest Request, FieldHostReply Reply);
public sealed record Ff7CameraScroll(int Mode, int StartX, int StartY, int TargetX, int TargetY, int Steps, int Step = 0, bool Completed = false);
public sealed record Ff7Motion(long RequestId, int Entity, int TargetX, int TargetY, bool Turn, bool Animated);
public sealed record Ff7AnimationRequest(long RequestId, int Entity);
public enum Ff7GlobalCommand
{
    None,
    Movie,
    Battle,
    Transition
}

public sealed record Ff7FieldProcess
{
    public long Tick { get; init; }
    public Ff7Actor[] Actors { get; init; } = [];
    public Ff7Window[] Windows { get; init; } = [];
    public Ff7Dialogue[] Dialogues { get; init; } = [];
    public Ff7HostReceipt[] Receipts { get; init; } = [];
    public Ff7Motion[] Motions { get; init; } = [];
    public Ff7AnimationRequest[] AnimationRequests { get; init; } = [];
    public ushort[] BackgroundStates { get; init; } = new ushort[256];
    public byte[] BlockedTriangles { get; init; } = [];
    public Ff7Movie? Movie { get; init; }
    public bool MovieBackgroundEnabled { get; init; }
    public bool MovieCameraDisabled { get; init; }
    public FieldFadeCommand? Fade { get; init; }
    public FieldMusicCommand? Music { get; init; }
    public byte MusicLock { get; init; }
    public long MusicTicks { get; init; }
    public int? NativeMusicTrack { get; init; }
    public int? SavedNativeMusicTrack { get; init; }
    public int MusicVolume { get; init; } = 127;
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
    public int CameraEntity { get; init; } = -1;
    public Ff7CameraScroll? CameraScroll { get; init; }
    public bool CameraLocked { get; init; }
    public bool CursorDisabled { get; init; }
    public int MapName { get; init; }
    public int MapDisplay { get; init; }
    public FieldTransitionCommand? Transition { get; init; }
    public long? GlobalRequestId { get; init; }
    public Ff7GlobalCommand GlobalCommand { get; init; }

    public void Validate()
    {
        if (Tick < 0 || Actors is null || Windows is null || Dialogues is null || Receipts is null || Motions is null || AnimationRequests is null || BackgroundStates is not { Length: 256 })
        {
            throw new InvalidDataException("FFVII field process is missing required owners.");
        }
        if (Actors.Any(actor => actor is null || actor.Slot < 0 || actor.Entity < 0 || actor.Model < 0 || actor.Direction is < 0 or > 255 || actor.Animation < 0 || actor.AnimationFrame16 < 0) || Actors.Where(actor => actor.EntityBound).Select(actor => actor.Entity).Distinct().Count() != Actors.Count(actor => actor.EntityBound) || Actors.Select(actor => actor.Slot).Distinct().Count() != Actors.Length)
        {
            throw new InvalidDataException("FFVII field actor ownership is invalid.");
        }
        if (Windows.Any(window => window is null || window.Id is < 0 or > 255 || window.Width < 0 || window.Height < 0) || Windows.Select(window => window.Id).Distinct().Count() != Windows.Length || Receipts.Any(receipt => receipt is null || receipt.Request is null || receipt.Request.Id < 1 || receipt.Reply is null) || Receipts.Select(receipt => receipt.Request.Id).Distinct().Count() != Receipts.Length)
        {
            throw new InvalidDataException("FFVII field window or request ownership is invalid.");
        }
        if (Dialogues.Any(dialogue => dialogue is null || dialogue.RequestId < 1 || dialogue.Text < 0 || Windows.All(window => window.Id != dialogue.Window)) || Movie is { Frame: < 0 })
        {
            throw new InvalidDataException("FFVII field dialogue or movie continuation is invalid.");
        }
    }
}
