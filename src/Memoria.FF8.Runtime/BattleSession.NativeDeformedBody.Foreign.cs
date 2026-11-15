namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeDeformedBodyBridge DrawNativeForeignDeformedBody(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleFileStaging staging, NativeBattleGraphicsState graphics, BattleModelOwner actor, short fraction, uint cacheAddress, int cacheLength, NativeBattleForeignModelGeometry geometry, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        NativeBattleForeignModelDrawing.RequireCoefficientCache(geometry, cacheAddress, cacheAddress, cacheLength);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Host body deformation requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Host body deformation requires its published projection.");
        NativeDeformedBodyBridge current = AllocateNativeDeformedBody(memory, effect, random, staging, graphics, actor, 132);
        current = current with { Temporary = current.Temporary.Write(0x80, BitConverter.GetBytes(fraction)).Write(0x2C, BitConverter.GetBytes(cacheAddress)) };
        current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, current.TemporaryAddress, current.Temporary, 132) };
        NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Host body shadow requires its current GTE matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Host body shadow requires its current arithmetic registers.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Host body shadow requires its current depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Host body shadow requires its original template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Host body shadow requires its primitive capabilities."), 4112, 16, pilotIdentity is { } shadowIdentity ? shadowIdentity with { ResourceGeneration = -1 } : null);
        current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
        current = AppendNativeDeformedBody(current, shadow.Batch);
        current = current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 0x78) };
        current = current with { Temporary = current.Temporary.Write(0x40, new byte[4]).Write(0x44, BitConverter.GetBytes((short)320)).Write(0x46, BitConverter.GetBytes((short)216)).Write(0x48, BitConverter.GetBytes(current.Model.Color40 ?? throw new NotSupportedException("Host body deformation requires its retained color."))) };
        NativeBattleEffectArena scratch = NativeBattleEffectArena.CreateUnknown(checked(geometry.Vertices.Length * 8));
        NativeBattleDeformedBodyStep drawn = NativeBattleDeformedBodyDrawing.DrawForeign(current.Memory, current.Temporary, scratch, geometry, camera.CapturePackedView1D97778(), projection, current.Memory.ScriptedGraphics!, cacheAddress, cacheLength, pilotIdentity ?? new(effect.Binding.Effect, 0x5099D0, actor, 0, 0));
        current = AppendNativeDeformedBody(current with { Memory = drawn.Memory, Temporary = drawn.Temporary132 }, drawn.Batch);
        return FinishNativeDeformedBody(current, actor, 132);
    }
}
