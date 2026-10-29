namespace Memoria.FF8.Runtime;

public static partial class NativeBattleHoly175Drawing
{
    public static NativeBattleHoly175DrawingResult DrawSprite31(NativeBattleModulePoolTask task, NativeBattleHoly175Sprite31Row row, NativeBattleHoly175State state, NativeBattleHoly175Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleHoly175SpriteResource source = resources.Sprite(0xD524C4);
        NativeBattleModuleResource resource = new(0, source.Address, 0, 0, source.Bytes);
        NativeBattleModuleSpriteFrame frame = resource.ProjectedFrame571C80(row.Frame);
        short x = unchecked((short)(unchecked((short)task.ReadWord(0x10)) + (unchecked(NativeBattleCameraMath.Cosine(row.Angle) * row.Radius) >> 12)));
        short z = unchecked((short)(unchecked((short)task.ReadWord(0x14)) + (unchecked(NativeBattleCameraMath.Sine(row.Angle) * row.Radius) >> 12)));
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(175, state.Sprite31Rows, 0x5D34D0, row.Index),
            Resource = resource,
            SpriteFrame = frame,
            Phase = row.Frame,
            Anchor = new(x, row.Height, z, 0),
            Scale = row.Scale,
            DepthOffset = -(row.Scale >> 4),
            SpriteFlags = 0
        };
        NativeBattleModulePacketBatch packets = NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2);
        return new(state, null, packets, false, frame.NextOffset);
    }

    public static NativeBattleHoly175DrawingResult DrawSprite4(NativeBattleHoly175Sprite4Draw row, NativeBattleHoly175State state, NativeBattleHoly175Resources resources, NativeBattleCameraProjection projection, short cameraRoll)
    {
        short focal = unchecked((short)(projection.FocalLength ?? throw new NotSupportedException("Native Holy screen sprites require their actual focal-length WORD.")));
        NativeBattleActionMatrix screen = NativeBattleGfMatrices.Euler56CD50(new(0, 0, 450)) with { Z = focal };
        NativeBattleActionMatrix local = new([row.Scale, 0, 0, 0, row.Scale, 0, 0, 0, row.Scale], row.Position.X, row.Position.Y, row.Position.Z, 4096);
        NativeBattleActionMatrix loaded = NativeBattlePoseMatrices.Compose56C2F0(screen, local);
        uint brightness = unchecked((uint)Math.Min(128, 21 * row.Age));
        uint color = unchecked(brightness * 0x10101u);
        NativeBattleModulePacketBatch packets = EmitLoadedSprite(resources.Sprite(0xD52374), row.Frame, loaded, projection, cameraRoll, 0xC, color, 4121, 14, NativeBattlePilotVisualLifetimes.Identity(175, state.Sprite4Rows, 0x5D44D0, row.Index));
        return new(state, null, packets, true, null);
    }
}
