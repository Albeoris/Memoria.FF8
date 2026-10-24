using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect319Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect319Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect319Sprite
{
    public static NativeBattleEffect319Step Advance(NativeBattleEffect319Frame frame, uint address, NativeBattleEffect319Resources resources, NativeBattleEffect319Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena point = TaskView(task, 16, 8);
        (byte[] wBytes, bool[] wKnown) = point.ReadSnapshot(6, 2);
        short? w = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
        NativeBattleVector vector = new(Word319(point, 0), Word319(point, 2), Word319(point, 4));
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), vector, w, 4096, -1024) };
        NativeBattleEffectArena context = PutWord319(PutWord319(Put319(Context(frame, temporary, 180), 0, 0xFF12E0), 4, task.ReadWord(12)), 0x24, 0);
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect319DrawBatch(draw.Batch, 0x25053A4));
        frame = Release(frame, 180);
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        return new(frame, next < 10 ? 0 : 2);
    }
}
