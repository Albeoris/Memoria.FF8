namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleAcid170Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeForeignAcid170Body(NativeBattleAcid170Frame frame, NativeBattleGraphicsState graphics, BattleModelOwner actor, uint temporaryAddress, NativeBattleForeignModelGeometry geometry, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        NativeBattleForeignModelDrawing.RequireIndexedTopology(geometry);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Host body deformation requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Host body deformation requires its published projection.");
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Host body deformation requires its addressed temporary owner.");
        NativeBattleAcid170ModelFrame body = new(frame.Memory, staging, graphics.ActorModels.Get(actor), NativeAcid170View(staging, temporaryAddress, 172));
        if ((body.Model.Flags0 & 0x20) == 0)
        {
            body = Shadow(body);
        }
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(camera.CapturePackedView1D97778().ReadMatrix(), geometry.World), null);
        NativeBattleEffectArena temporary = body.Temporary172.Write(0x8C, composed.Bytes.AsSpan()).MarkUnknown(0x9E, 2);
        temporary = temporary.Write(0x1C, BitConverter.GetBytes(body.Model.Color40 ?? throw new NotSupportedException("Host body deformation requires its retained model color."))).Write(0x14, new byte[4]);
        byte alpha = body.Model.Alpha7 ?? throw new NotSupportedException("Host body deformation requires its retained model alpha.");
        temporary = temporary.Write(0x28, [alpha, alpha, alpha]);
        temporary = NativeBattleEffectNodeContextMemory.Copy(NativeAcid170View(body.Staging, 0x1D969A8, 4), 0, temporary, 0x10, 4);
        temporary = temporary.Write(0x18, BitConverter.GetBytes((ushort)320)).Write(0x1A, BitConverter.GetBytes((ushort)216)).Write(0x8A, BitConverter.GetBytes((ushort)1));
        body = DrawBody(body with { Temporary172 = temporary });
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor, body.Model) };
        return (frame with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.PublishView(body.Staging, temporaryAddress, body.Temporary172, 172) }, graphics);

        NativeBattleAcid170ModelFrame Shadow(NativeBattleAcid170ModelFrame current)
        {
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Host body shadow requires its current GTE matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Host body shadow requires its current GTE data.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Host body shadow requires its depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Host body shadow requires its original template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Host body shadow requires its primitive capabilities."), 4121, 16, pilotIdentity is { } identity ? identity with { Primitive = 0x5088A0 } : null);
            current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
            current = Append(current, shadow.Batch);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 0x78) };
        }

        NativeBattleAcid170ModelFrame DrawBody(NativeBattleAcid170ModelFrame current)
        {
            NativeBattleEffectArena scratch = NativeBattleEffectArena.CreateUnknown(checked(geometry.Vertices.Length * 8));
            NativeBattleAcid170ModelStep drawn = NativeBattleAcid170ModelDrawing.DrawForeign(current.Memory, current.Temporary172, scratch, geometry, camera.CapturePackedView1D97778(), projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Host body deformation requires its depth controls."), pilotIdentity ?? NativeBattlePilotVisualIdentity.ForModel(frame.Effect.Binding.Effect, 0x5099D0, actor));
            return Append(current with { Memory = drawn.Memory, Temporary172 = drawn.Temporary172 }, drawn.Batch);
        }

        NativeBattleAcid170ModelFrame Append(NativeBattleAcid170ModelFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Host body deformation requires its current COMMON ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Host body deformation requires its current COMMON packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
    }
}
