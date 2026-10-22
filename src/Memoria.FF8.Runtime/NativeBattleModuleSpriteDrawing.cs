namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModuleSpriteDraw(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary180, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleModuleSpriteDrawing
{
    public const int TemporaryLength = 180;

    public static NativeBattleModuleSpriteDraw Draw571C80(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary180, Func<uint, NativeBattleModuleResource> readResource, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket = 17, byte depthShift = 2)
    {
        temporary180.Validate();
        if (depthShift > 31)
        {
            throw new InvalidDataException("Native571C80 requires its actual bounded depth shift.");
        }
        if (temporary180.Regions.Any(region => region.Offset < 0 || region.Offset > TemporaryLength - region.Data.Length) || temporary180.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > TemporaryLength - region.Length))
        {
            throw new InvalidDataException("Native571C80 requires its actual180-byte physical temporary.");
        }
        NativeBattleProjectedSpriteDraw drawn = NativeBattleProjectedSpriteD8.DrawUnbiased571C80(memory, temporary180, readResource, projection, cameraRoll, tableBaseBucket, depthShift);
        return new(drawn.Memory, drawn.TemporaryD8, drawn.Batch);
    }
}
