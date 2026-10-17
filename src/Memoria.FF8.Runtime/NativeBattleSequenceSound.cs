using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The reached505AB0 operands; spatial pan is produced separately from actor bone241.</summary>
public sealed record NativeBattleSequenceSound(byte SoundId, bool ActorSound, [property: JsonPropertyName("Attribute")] byte Flags, [property: JsonPropertyName("Volume")] ushort Attributes);
