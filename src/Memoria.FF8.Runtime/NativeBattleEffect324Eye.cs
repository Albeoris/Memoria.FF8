using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect324Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect324Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect324Eye
{
    public static NativeBattleEffect324Step Advance(NativeBattleEffect324Frame frame, uint address, NativeBattleEffect324Resources resources, NativeBattleEffect324Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int side = Signed(task, 14);
        short scale = Signed(task, 28);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, unchecked((short)(side << 11)), 0));
        rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale);
        NativeBattleEffectArena anchor = frame.State.ReadView(0x2203C58, 8);
        (byte[] wBytes, bool[] wKnown) = anchor.ReadSnapshot(6, 2);
        short? w = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Word324(anchor, 0), Word324(anchor, 2), Word324(anchor, 4)), w, scale, unchecked((short)-(scale >> 4))) };
        NativeBattleActionMatrix billboard = frame.Memory.ModuleSpriteBillboard21DFED0!.ReadMatrix();
        rotation = rotation with { X = unchecked(billboard.X + (side == 0 ? -200 : 200)), Y = unchecked(billboard.Y - 60), Z = billboard.Z };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = PutWord324(PutWord324(Put324(Context(frame, temporary, 180), 0, 0xCD0B2C), 4, task.ReadWord(12)), 0x24, 8);
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattlePilotVisualIdentity? parent = frame.PilotIdentity;
        frame = frame with { PilotIdentity = NativeBattleActor33Presentation.Resource(parent, 0xCD0B2C) };
        frame = DrawSprite324(frame, temporary, resources, inputs);
        frame = frame with { PilotIdentity = parent };
        short nextDirectory = Word324(Context(frame, temporary, 180), 0x28);
        frame = Release(frame, 180);
        if (Paused324(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1))));
        return new(frame, nextDirectory < 0 ? 2 : 0);
    }
}
