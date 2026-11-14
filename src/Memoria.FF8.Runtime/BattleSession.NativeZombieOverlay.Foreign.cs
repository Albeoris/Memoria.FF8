namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleOriginalEffectState Effect) DrawNativeForeignZombieOverlay(NativeBattleGraphicsState graphics, int index, int age, int reach, NativeBattleZombieResources resources, NativeBattleCameraProjection projection, NativeBattleOriginalEffectState effect, NativeBattlePilotVisualIdentity identity, NativeBattleForeignModelGeometry geometry)
    {
        var parameters = NativeBattleZombieDrawing.OverlayParameters(age, reach);
        geometry = geometry with { World = geometry.World with { Y = unchecked(geometry.World.Y + parameters.Lift) } };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Host body waves require the current camera."), geometry.World);
        NativeBattleModuleProjectedVertex origin = NativeBattleModuleProjection.Project45FE10(matrix, resources.Zero, projection);
        if (origin.ReadDepth45E250() == 0)
        {
            return (graphics, effect);
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Host body waves require their retained effect memory.");
        NativeBattleScriptedGraphicsMemory registers = memory.ScriptedGraphics ?? throw new NotSupportedException("Host body waves require the current depth controls.");
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
        uint tint = NativeBattleDuelSharedPackets.InterpolateColor56CC00(resources.Black, model.Color40 ?? throw new NotSupportedException("Host body waves require their actual target tint."), parameters.Weight, 4096 - parameters.Weight, 0);
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Host body waves require their temporary workspace."), 24);
        NativeBattleZombieOverlayDraw drawn = NativeBattleZombieOverlay.DrawForeign(geometry, projection, tint, temporary.Staging, temporary.Address, resources.Wave, unchecked((short)origin.Screen), parameters.Phase, unchecked((short)(parameters.Amplitude >> 2)), registers.Dqa, registers.Dqb, identity);
        NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 24);
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Host body waves require the current ordering table.");
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, effect.DrawArenaOffset ?? throw new NotSupportedException("Host body waves require their selected packet cursor."), primitive.Pending, drawn.Packets);
        NativeEffectMemory = written.Memory;
        return (graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } }, effect with { DrawArenaOffset = written.Cursor });
    }
}
