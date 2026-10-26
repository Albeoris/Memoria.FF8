using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Frame InitializeActorOverlay648820(NativeBattleEffect187Frame frame, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect187 actor overlay requires its actual parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect187 actor overlay requires its actual contacts.");
        for (int index = 0; index < header.ReadByte(0x10); index++)
        {
            NativeBattleContactPacket contact = packets.ReadContact(first + index);
            if ((contact.Primary.HitFlags3 & 4) != 0)
            {
                continue;
            }
            int actor = contact.Primary.ActorIndex;
            NativeBattleGraphicsModel model = inputs.ReadModel(frame, actor);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect187 actor overlay requires its current model transform.");
            (frame, uint address) = AppendRequired(frame, 0x648AF0);
            frame = Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, checked((ushort)actor)));
            NativeBattleEffect187PointStep firstBone = inputs.ReadBone502170(frame, actor, 0xF0, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = firstBone.Frame;
            NativeBattleEffect187PointStep secondBone = inputs.ReadBone502170(frame, actor, 0xF1, 0, TaskView(Read(frame, address), 0x10, 8));
            frame = secondBone.Frame;
            NativeBattleEffectArena point = secondBone.Point8;
            for (int axis = 0; axis < 3; axis++)
            {
                int sum = BinaryPrimitives.ReadInt16LittleEndian(firstBone.Point8.Read(axis * 2, 2)) + BinaryPrimitives.ReadInt16LittleEndian(point.Read(axis * 2, 2));
                point = point.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(sum / 2))));
            }
            point = point.Write(6, BitConverter.GetBytes(transform.Angle36));
            frame = Write(frame, address, CopyTask(Read(frame, address), 0x10, point, 8));
            NativeCrtRandomState random = frame.Random.Draw(out ushort randomValue);
            int radius = unchecked(((randomValue % 256) + 128) * transform.Scale38) >> 12;
            int span = unchecked(radius * 2);
            for (int axis = 0; axis < 3; axis++)
            {
                random = random.Draw(out randomValue);
                int delta = randomValue % span - radius;
                NativeBattleModulePoolTask task = Read(frame, address);
                frame = Write(frame, address, task.WriteWord(0x10 + axis * 2, unchecked((ushort)(task.ReadWord(0x10 + axis * 2) + delta))));
            }
            random = random.Draw(out randomValue);
            frame = Write(frame, address, Read(frame, address).WriteWord(0x18, unchecked((ushort)(randomValue % 0x500 - 0x280))).WriteWord(0x1C, 0));
            random = random.Draw(out randomValue);
            int velocity = unchecked((randomValue % 48 + 48) * Math.Min(transform.Scale38, (short)0xA00)) >> 12;
            frame = Write(frame, address, Read(frame, address).WriteWord(0x1E, unchecked((ushort)velocity)));
            frame = frame with { Random = random };
        }
        return frame;
    }
}
