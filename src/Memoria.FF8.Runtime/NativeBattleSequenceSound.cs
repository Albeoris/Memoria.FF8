namespace Memoria.FF8.Runtime;

/// <summary>The reached505AB0 operands; spatial pan is produced separately from actor bone241.</summary>
public sealed record NativeBattleSequenceSound(byte SoundId, bool ActorSound, byte Attribute, ushort Volume);
