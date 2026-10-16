using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Logical provenance of48D200's DWORD12; this is not the executable's transient text pointer value.</summary>
public sealed record NativeBattleActionDataReference([property: JsonRequired] uint ProducerAddress, [property: JsonRequired] ushort Argument, string? FormattedText = null);

/// <summary>Only the fields actually written by48D200 before48E830 publishes a20-byte physical header.</summary>
public sealed record NativeBattleActionHeaderPreparation([property: JsonRequired] byte Actor, [property: JsonRequired] byte Command, [property: JsonRequired] byte Sequence, [property: JsonRequired] sbyte CameraChoice, [property: JsonRequired] ushort Action, [property: JsonRequired] ushort Effect, NativeBattleActionDataReference? Data);

/// <summary>Caller-owned1D28DF0/E0C inputs; headerBYTE17 is written separately by485610 after the dispatch group.</summary>
public sealed record NativeBattleActionProductionContext([property: JsonRequired] byte RepeatMode, [property: JsonRequired] byte MultiCastMode, [property: JsonRequired] bool DrawFollowUp, [property: JsonRequired] uint CompletionCallback);

public enum NativeBattleDeferredContactKind
{
    None,
    OrdinaryGunblade,
    RenzokukenInitial,
    RenzokukenFinisher,
    DuelControl,
    ShotControl,
    GuardianForceInitial
}

/// <summary>Already-applied factual contacts for the single canonical header/contact bank; presentation cannot apply them again.</summary>
public sealed record NativeBattleActionProductionResult(NativeBattleActionHeaderPreparation Header, ImmutableArray<NativeBattleContactPacket> Contacts, byte PublishedTargetCount, NativeBattleDeferredContactKind DeferredContact, byte? ReflectorIndex);
