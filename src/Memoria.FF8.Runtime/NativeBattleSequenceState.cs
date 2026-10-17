using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Owned resource-header and pose-counter fields used by504290/50DB40.</summary>
public sealed record NativeBattleSequenceState
{
    public ushort Offset46 { get; init; }
    public byte Wait1 { get; init; }
    public byte Default2 { get; init; } = 1;
    public byte Pending3 { get; init; }
    public byte Callback42 { get; init; }
    public byte Alternate43 { get; init; }
    public short Header36 { get; init; }
    public short Header38 { get; init; }
    public short Header40 { get; init; }
    public ImmutableArray<short?> Locals { get; init; } = ImmutableArray.CreateRange(new short?[8]);
    public ImmutableArray<short?> Scratch { get; init; } = ImmutableArray.CreateRange(new short?[8]);
    public ImmutableArray<short?> AlternateScratch48 { get; init; } = ImmutableArray.CreateRange(new short?[8]);
    public byte? Pose { get; init; }
    public byte PoseFlags109 { get; init; }
    public byte? PoseFrame114 { get; init; }
    public byte? PoseLimit115 { get; init; }
    public NativeBattlePoseProgress? LinkedPose { get; init; }
    public NativeBattleDecodedPose? DecodedPose { get; init; }
    public NativeBattleDecodedPose? LinkedDecodedPose { get; init; }
    public byte? Geometry4 { get; init; }
    public byte? Geometry5 { get; init; }
    public uint MeshMask124 { get; init; } = uint.MaxValue;

    public void Validate()
    {
        if (Locals.IsDefault || Locals.Length != 8 || Scratch.IsDefault || Scratch.Length != 8 || AlternateScratch48.IsDefault || AlternateScratch48.Length != 8 || PoseFrame114 is byte frame && PoseLimit115 is byte limit && frame > limit || LinkedPose is { } linked && linked.Frame > linked.Limit || DecodedPose is { } decoded && (decoded.Bones.IsDefaultOrEmpty || decoded.Bones.Length > 128) || LinkedDecodedPose is { } linkedDecoded && (linkedDecoded.Bones.IsDefaultOrEmpty || linkedDecoded.Bones.Length > 128))
        {
            throw new InvalidDataException("Native sequence state has invalid local WORD owners or pose counter.");
        }
    }
}

public enum NativeBattleSequenceCueKind
{
    Text,
    ActorSound,
    SystemSound,
    Texture,
    CommandTitle,
    ActionCallback,
    TriggerWindow,
    SuspendParticipant,
    RestoreStanding
}

public sealed record NativeBattleSequenceCue(NativeBattleSequenceCueKind Kind, int First = 0, int Second = 0, int Third = 0)
{
    public BattleNativeRenzokukenBlock? TriggerWindow { get; init; }
    public bool OverrideSoundOnContact4 { get; init; }
}
public sealed record NativeBattleSequenceStep(NativeBattleGraphicsModel Model, ImmutableArray<NativeBattleSequenceCue> Cues);

public sealed record NativeBattleSequenceInputs(byte? EntryAdvantage, ushort? CameraTasks)
{
    public NativeBattleGraphicsResource? OriginalResource { get; init; }
    public Func<ushort>? GlobalRandom { get; init; }
    public Func<byte, int>? ReadExternal { get; init; }
    public Func<NativeBattleGraphicsModel, byte, int>? ReadModelExternal { get; init; }
    public Action<byte, int>? WriteExternal { get; init; }
    public Func<NativeBattleGraphicsModel, byte, int, NativeBattleGraphicsModel>? WriteModelExternal { get; init; }
    public Action<NativeBattleAuxiliaryProgram, bool, byte>? ScheduleSoundParticle { get; init; }
    public Func<NativeBattleGraphicsModel, NativeBattleGraphicsModel>? ApplyActionContacts { get; init; }
    public Func<NativeBattleGraphicsModel, NativeBattleGraphicsModel>? ApplyNextContact { get; init; }
    public Func<NativeBattleGraphicsModel, NativeBattleGraphicsModel>? ApplyCurrentContact { get; init; }
    public Func<NativeBattleGraphicsModel, byte, byte, byte, NativeBattleGraphicsModel>? SelectModelSequence { get; init; }
    public Func<NativeBattleGraphicsModel, byte, NativeBattleGraphicsModel>? BindModelRing90 { get; init; }
    public Func<NativeBattleGraphicsModel, byte, NativeBattleGraphicsModel>? RebindActionGeometry { get; init; }
    public Func<NativeBattleGraphicsModel, byte, byte, byte, short, byte, NativeBattleGraphicsModel>? ScheduleModelMotion { get; init; }
    public Action<byte, byte, byte>? ScheduleCameraShake { get; init; }
    public Action<byte>? ScheduleStageFade { get; init; }
    public Action<byte>? ScheduleModelGroupFade { get; init; }
    public Func<NativeBattleGraphicsModel, byte, NativeBattleVector, short, NativeBattleGraphicsModel>? ScheduleModelDeformation { get; init; }
    public Action<NativeBattleGraphicsModel, NativeBattleSequenceCue>? PlaySound { get; init; }
    public Action<NativeBattleAuxiliaryProgram, byte>? ScheduleTextureAnimation { get; init; }
    public Action<byte, int>? ApplyTexture { get; init; }
    public Action<NativeBattleGraphicsModel, byte>? AdvanceTexture { get; init; }
    public Action<byte>? ScheduleModelEffect { get; init; }
    public Func<NativeBattleGraphicsModel, NativeBattleAuxiliaryProgram, (NativeBattleGraphicsModel Model, int Offset)>? ScheduleContactEffect { get; init; }
    public Func<NativeBattleGraphicsModel, NativeBattleAuxiliaryProgram, (NativeBattleGraphicsModel Model, int Offset)>? ScheduleModelParticle { get; init; }
}
