using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect66Drawing
{
    public static NativeBattleEffect66Frame DrawStatic(NativeBattleEffect66Frame frame, uint temporaryAddress, uint source, byte depthShift, uint cursorGlobal, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporaryAddress, 88);
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, 17, depthShift);
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporaryAddress, drawn.Context, 88);
        return inputs.Execute(frame, new NativeBattleEffect66DeliverBatch(drawn.Batch, cursorGlobal));
    }

    public static NativeBattleEffect66Frame DrawSprite(NativeBattleEffect66Frame frame, uint temporaryAddress, byte depthShift, uint cursorGlobal, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporaryAddress, NativeBattleModuleSpriteDrawing.TemporaryLength);
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, depthShift);
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporaryAddress, drawn.Temporary180, NativeBattleModuleSpriteDrawing.TemporaryLength);
        return inputs.Execute(frame, new NativeBattleEffect66DeliverBatch(drawn.Batch, cursorGlobal));
    }
}
