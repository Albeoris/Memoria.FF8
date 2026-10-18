namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState SubmitNativeShadowPackets5088A0(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model, NativeBattleCameraProjection projection, int tableBaseBucket, bool ordinaryModel)
    {
        if (this.definition.NativeGraphics?.PrimitiveTables is null || (model.Flags0 & 0x20) != 0)
        {
            return graphics;
        }
        if (ordinaryModel)
        {
            ushort flags2 = model.Flags2 ?? throw new NotSupportedException("Native502D40 shadow requires modelWORD2.");
            uint color = model.Color44 ?? throw new NotSupportedException("Native502D40 shadow requires modelDWORD44.");
            if ((flags2 & 0x20) != 0 || color == 0x32000000)
            {
                return graphics;
            }
            NativeBattlePrimitiveProcess process = NativeEffectMemory?.PrimitiveProcess ?? throw new NotSupportedException("Native502D40 shadow requires its actual packet bank/cursor.");
            if (process.NativeRemaining500820() <= 0xF0)
            {
                return graphics;
            }
        }
        NativeBattleGraphicsDefinition definition = this.definition.NativeGraphics ?? throw new NotSupportedException("Native5088A0 requires its actual graphics definition.");
        bool gouraud = (definition.PrimitiveCapabilities ?? throw new NotSupportedException("Native5088A0 requires its selected primitive-device capability profile; explicitly migrate the named snapshot.")).GouraudAlpha;
        NativeBattleEffectLocalBounds bounds = model.GeometryBounds52 ?? throw new NotSupportedException("Native5088A0 requires its retained5086F0 model bounds.");
        NativeBattleShadowSource source = definition.Rules.ShadowSource ?? throw new NotSupportedException("Native5088A0 requires its authenticated matrix/vector seeds.");
        NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(model, bounds, projection.View ?? throw new NotSupportedException("Native5088A0 requires its prior published camera matrix."), source, gouraud);
        NativeBattleModuleProjectedVertex[] vertices = shape.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shape.CameraMatrix, vertex, projection)).ToArray();
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native5088A0 requires its retained packet memory.");
        NativeBattlePrimitiveProcess packets = memory.PrimitiveProcess ?? throw new NotSupportedException("Native5088A0 requires its actual common packet cursor.");
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native5088A0 requires its initialized OT.");
        NativeBattleGfGeometryBatch batch = NativeBattleShadowPackets.EmitBatch5088A0(vertices, model.Color44 ?? throw new NotSupportedException("Native5088A0 requires modelDWORD44."), gouraud, tableBaseBucket, 16);
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(packets, frame.Pending, batch);
        NativeEffectMemory = memory with { PrimitiveProcess = written.Process };
        return graphics with { PrimitiveFrame = frame with { Pending = written.Ordering } };
    }
}
