using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect285Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect285Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect285Sprite
{
    public static NativeBattleEffect285Step Advance(NativeBattleEffect285Frame frame, uint address, NativeBattleEffect285Resources resources, NativeBattleEffect285Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena point = TaskView(task, 12, 8);
        (byte[] wBytes, bool[] wKnown) = point.ReadSnapshot(6, 2);
        short? w = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Word285(point, 0), Word285(point, 2), Word285(point, 4)), w, 2048, -512) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord285(PutWord285(Put285(Context(frame, temporary, 180), 0, 0x1084AF8), 4, task.ReadWord(18)), 0x24, 0);
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCamera(frame).Roll, 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect285DrawBatch(draw.Batch, 0x2507D60));
        frame = Release(frame, 180);
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 18) + 1));
        frame = Write(frame, address, task.WriteWord(18, unchecked((ushort)next)));
        return new(frame, next < 7 ? 0 : 2);
    }
}
