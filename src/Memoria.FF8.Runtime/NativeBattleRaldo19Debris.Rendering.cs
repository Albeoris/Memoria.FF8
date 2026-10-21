using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRaldo19DebrisHead(NativeBattleRaldo19Frame Frame, NativeBattleEffectArena TemporaryD8);

public sealed record NativeBattleRaldo19DebrisDraw(NativeBattleRaldo19Frame Frame, NativeBattleEffectArena TemporaryD8, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleRaldo19DebrisRendering
{
    public const int TemporaryLength = 0xD8;

    public static NativeBattleRaldo19DebrisHead PrepareHead(NativeBattleRaldo19Frame frame, NativeBattleModulePoolTask task, NativeBattlePackedMatrix32 packedCamera, NativeBattleEffectArena temporaryD8)
    {
        if (task.Callback is not (0x8AA1D0 or 0x8AAA30) || (task.ReadByte(0x26) & 4) != 0)
        {
            throw new InvalidDataException("Raldo debris preparation requires its reached visible sprite callback.");
        }
        frame.ToGeneric();
        NativeBattleProjectedSpriteHead prepared = NativeBattleProjectedSpriteD8.PrepareHead(frame.Memory, task, packedCamera, temporaryD8);
        return new(frame with { Memory = prepared.Memory }, prepared.TemporaryD8);
    }

    public static NativeBattleRaldo19DebrisDraw Draw(NativeBattleRaldo19DebrisHead head, NativeBattleRaldo19Resources resources, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket)
    {
        NativeBattleProjectedSpriteDraw drawn = NativeBattleProjectedSpriteD8.Draw(new(head.Frame.Memory, head.TemporaryD8), resources.Debris, projection, cameraRoll, tableBaseBucket);
        return new(head.Frame with { Memory = drawn.Memory }, drawn.TemporaryD8, drawn.Batch);
    }
}
