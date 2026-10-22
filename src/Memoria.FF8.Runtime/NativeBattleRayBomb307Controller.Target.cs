using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb307Controller
{
    public static NativeBattleRayBomb307TaskStep AdvanceTarget(NativeBattleRayBomb307Frame frame, uint address, NativeBattleRayBomb307Resources resources, NativeBattleRayBomb307Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = Signed(task, 14);
        if (actor is < 0 or > 6)
        {
            throw new InvalidDataException("Ray-Bomb target leaves its actual saved actor bank.");
        }
        uint saved = checked(SavedActorsAddress + (uint)(actor * 20));
        int age = Signed(task, 12);
        if (age == 0)
        {
            NativeBattleRayBomb307AnchorStep anchor = inputs.ReadAnchor571400(frame, actor);
            frame = anchor.Frame;
            task = task.WriteWord(16, unchecked((ushort)anchor.Anchor.X)).WriteWord(18, unchecked((ushort)anchor.Anchor.Y)).WriteWord(20, unchecked((ushort)anchor.Anchor.Z)).WriteWord(22, unchecked((ushort)anchor.Anchor.W));
            NativeBattleRayBomb307State state = frame.State.WriteWord(saved, (ushort)(inputs.ReadModel(actor).Flags0 & 0x1020));
            state = state.WriteView(saved + 12, inputs.ReadPosition8(actor), 8);
            state = state.WriteView(saved + 4, inputs.ReadRotation8(actor), 8);
            frame = frame with { State = state };
        }
        if (age <= 24)
        {
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorFlags((byte)actor, 0, 0x1020));
            int lift = unchecked(NativeBattleCameraMath.Sine(unchecked(age << 11) / 24) * Signed(task, 32)) >> 12;
            NativeBattleEffectArena rotation = inputs.ReadRotation8(actor).Write(0, BitConverter.GetBytes(unchecked((short)(unchecked(Signed(task, 24) * age) / 24)))).Write(2, BitConverter.GetBytes(unchecked((short)(unchecked(age << 13) / 24))));
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorWrite8((byte)actor, rotation, null));
            NativeBattleEffectArena original = frame.State.ReadView(saved + 12, 8);
            NativeBattleVector delta = new(unchecked((short)(Signed(task, 16) - TargetWord(original, 0))), unchecked((short)(Signed(task, 18) - TargetWord(original, 2))), unchecked((short)(Signed(task, 20) - TargetWord(original, 4))));
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(TargetWord(rotation, 0), TargetWord(rotation, 2), TargetWord(rotation, 4)));
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(matrix, delta);
            NativeBattleEffectArena position = inputs.ReadPosition8(actor).Write(0, BitConverter.GetBytes(unchecked((short)(Signed(task, 16) - unchecked((short)x))))).Write(2, BitConverter.GetBytes(unchecked((short)(Signed(task, 18) - unchecked((short)y) + lift)))).Write(4, BitConverter.GetBytes(unchecked((short)(Signed(task, 20) - unchecked((short)z)))));
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorWrite8((byte)actor, null, position));
        }
        else if (age <= 32)
        {
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorFlags((byte)actor, 0, 0x1020));
            int lift = unchecked(NativeBattleCameraMath.Sine(unchecked((age - 24) << 11) / 8) * (Signed(task, 32) >> 3)) >> 12;
            NativeBattleEffectArena position = inputs.ReadPosition8(actor).Write(2, BitConverter.GetBytes(unchecked((short)lift)));
            frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorWrite8((byte)actor, null, position));
        }
        frame = Write(frame, address, task);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        if (age is 24 or 31)
        {
            int count = age == 24 ? 16 : 12;
            for (int particle = 0; particle < count; particle++)
            {
                int slot = FindParticle(frame, 100);
                if (slot is < 0 or >= 80)
                {
                    break;
                }
                int at = slot * 24;
                frame = SlotDword(frame, at, 1);
                frame = SlotWord(frame, at + 4, 0);
                frame = DrawRandom(frame, out ushort size);
                frame = SlotWord(frame, at + 6, unchecked((short)(age == 24 ? size % 1024 + 2560 : size % 768 + 2048)));
                frame = SlotWord(frame, at + 8, Signed(task, 16));
                frame = SlotWord(frame, at + 10, 0);
                frame = SlotWord(frame, at + 12, Signed(task, 20));
                frame = DrawRandom(frame, out ushort angleDraw);
                int angle = angleDraw % 4096;
                frame = DrawRandom(frame, out ushort speedDraw);
                int speed = age == 24 ? speedDraw % 90 + 120 : speedDraw % 60 + 100;
                frame = SlotWord(frame, at + 16, unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12)));
                frame = SlotWord(frame, at + 18, 0);
                frame = SlotWord(frame, at + 20, unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12)));
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(age + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) < 32)
        {
            return new(frame, 0);
        }
        frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorFlags((byte)actor, 0x1020, frame.State.ReadWord(saved)));
        frame = inputs.Execute(frame, new NativeBattleRayBomb307ActorWrite8((byte)actor, frame.State.ReadView(saved + 4, 8), frame.State.ReadView(saved + 12, 8)));
        frame = inputs.Execute(frame, new NativeBattleRayBomb307Contact(Signed(task, 26)));
        return new(frame, 2);
    }

    private static short TargetWord(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset, 2));
}
