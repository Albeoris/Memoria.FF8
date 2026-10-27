namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModuleSpriteDraw(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary180, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleModuleSpriteDrawing
{
    public const int TemporaryLength = 180;

    public static NativeBattleModuleSpriteDraw Draw571C80(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary180, Func<uint, NativeBattleModuleResource> readResource, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket = 17, byte depthShift = 2, bool allowCallerScale = false, bool allowFirstPaletteOverride = false)
    {
        temporary180.Validate();
        if (depthShift > 31)
        {
            throw new InvalidDataException("Native571C80 requires its actual bounded depth shift.");
        }
        temporary180.ValidateExtent(TemporaryLength);
        NativeBattleProjectedSpriteDraw drawn = NativeBattleProjectedSpriteD8.DrawUnbiased571C80(memory, temporary180, readResource, projection, cameraRoll, tableBaseBucket, depthShift, allowCallerScale, allowFirstPaletteOverride);
        return new(drawn.Memory, drawn.TemporaryD8, drawn.Batch);
    }
}
