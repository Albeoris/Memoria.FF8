namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleGraphicsModel Model) AdvanceNativeStatusIcons(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model)
    {
        NativeBattleStatusIconStep step = NativeBattleStatusIcons.Advance50A165(model, graphics.Flags, definition.NativeGraphics!.Rules.StatusIcons, ReadAnchor);
        foreach (NativeBattleModuleDraw draw in step.Draws)
        {
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native5710D0 status icons require the prior published camera projection.");
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera!.Roll, 17, 2);
            int drawIndex = graphics.ModuleDraws.Length;
            graphics = graphics with { ModuleDraws = graphics.ModuleDraws.Add(draw) };
            graphics = AppendNativeModulePacketBatch(graphics, batch, drawIndex, null);
        }
        return (graphics, step.Model);

        NativeBattleVector ReadAnchor(NativeBattleGraphicsModel current)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, current.Index).PoseSource ?? throw new NotSupportedException("Native5710D0 status icons require the current502170 skeleton/pose source.");
            return NativeBattlePoseMatrices.ReadBone(current, source, 0xF0, 0x1000);
        }
    }
}
