using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect80Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect80Sprite
{
    public static NativeBattleEffect80Frame DrawTask(NativeBattleEffect80Frame frame, uint taskAddress, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        const int length = 0xD8;
        (frame, uint address) = Allocate(frame, length);
        NativeBattleEffectArena temporary = Context(frame, address, length);
        NativeBattleProjectedSpriteHead prepared = NativeBattleProjectedSpriteD8.PrepareHead(frame.Memory, task, inputs.ReadView(frame), temporary);
        frame = PublishContext(frame with { Memory = prepared.Memory }, address, prepared.TemporaryD8, length);
        NativeBattleProjectedSpriteDraw drawn = NativeBattleProjectedSpriteD8.Draw(prepared, source =>
        {
            if (!resources.Sprites.TryGetValue(source, out ImmutableArray<byte> bytes))
            {
                throw new NotSupportedException("Effect80 particle requires its selected original projected sprite source.");
            }
            return new(0, source, 0, 0, bytes);
        }, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17);
        frame = PublishContext(frame with { Memory = drawn.Memory }, address, drawn.TemporaryD8, length);
        frame = inputs.Execute(frame, new NativeBattleEffect80DeliverBatch(drawn.Batch, 0x25D493C));
        return Release(frame, length);
    }

}
