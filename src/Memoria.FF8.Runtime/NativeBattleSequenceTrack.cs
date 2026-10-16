using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSequenceMessage(int Frame, byte Text, byte Duration);

/// <summary>Fully resolved local/control50DB40 program; frames count504290 callbacks, not an inferred pose duration.</summary>
public sealed record NativeBattleSequenceTrack(byte Sequence, ImmutableArray<byte> Program, ImmutableArray<NativeBattleSequenceMessage> Messages, int CompletionFrame);
