namespace Memoria.FF8.Runtime;

public sealed record NativeBattleResonance49DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleResonance49Operation;
public sealed record NativeBattleResonance49StorePacketBatch(NativeBattleGfGeometryBatch Batch, uint DestinationAddress, ushort Flags = 0) : NativeBattleResonance49Operation;
public sealed record NativeBattleResonance49DeliverScreenPair(NativeBattleGfGeometryAttempt Quad, NativeBattleGfGeometryAttempt Mode) : NativeBattleResonance49Operation;
