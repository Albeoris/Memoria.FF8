namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleDevour62Frame Frame) DrawNativeDevour62Quads(NativeBattleGraphicsState graphics, NativeBattleDevour62Frame current, uint address)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
        NativeBattleGfGeometryBatch batch = NativeBattleDevour62Quads.Emit(task, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(62, task) with { ResourceGeneration = task.Callback });
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Devour screen drawing requires its current ordering table.");
        int cursor = current.State.PrimaryDrawCursor ?? throw new NotSupportedException("Devour screen drawing requires its selected private cursor.");
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch, flags20: 1);
        return (graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } }, current with { Memory = written.Memory, State = current.State with { PrimaryDrawCursor = written.Cursor } });
    }
}
