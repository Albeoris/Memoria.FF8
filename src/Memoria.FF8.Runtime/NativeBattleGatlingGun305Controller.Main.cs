using static Memoria.FF8.Runtime.NativeBattleGatlingGun305Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGatlingGun305Controller
{
    private static (NativeBattleGatlingGun305Frame Frame, int Result) Main(NativeBattleGatlingGun305Frame frame, uint address, NativeBattleGatlingGun305Resources resources, NativeBattleGatlingGun305Inputs inputs)
    {
        int caster = checked((int)frame.State.ReadDword(CasterGlobal));
        NativeBattleGatlingGun305PointStep anchor = inputs.ReadBone502170(frame, caster, 0xF0, 0xC00);
        frame = anchor.Frame;
        short yaw = inputs.ReadModel(caster).Rotation14 ?? throw new NotSupportedException("Gatling Gun requires its caster yaw.");
        short x = unchecked((short)(anchor.Position.X + (unchecked(-NativeBattleCameraMath.Sine(yaw) * 100) >> 12)));
        short z = unchecked((short)(anchor.Position.Z + (unchecked(-NativeBattleCameraMath.Cosine(yaw) * 100) >> 12)));
        frame = frame with { State = frame.State.WriteWord(AnchorGlobal, unchecked((ushort)x)).WriteWord(AnchorGlobal + 2, unchecked((ushort)anchor.Position.Y)).WriteWord(AnchorGlobal + 4, unchecked((ushort)z)) };
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleGatlingGun305BindCaster((byte)caster, BindingSave, 0xCEB5E8));
            frame = inputs.Execute(frame, new NativeBattleGatlingGun305RingSequence((byte)caster, 1));
        }
        if (age >= 9 && age <= 32 && (age & 1) != 0)
        {
            (frame, uint child) = Append(frame, 0x5A5C50);
            frame = CopyAnchor(frame, child);
            NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(0x18, unchecked((ushort)yaw));
            frame = DrawRandom(frame, out ushort random);
            task = task.WriteWord(0x1A, (ushort)(random & 0x7FF));
            frame = DrawRandom(frame, out random);
            frame = Write(frame, child, task.WriteWord(0x1C, (ushort)(random % 640 + 1536)));
        }
        if (age == 9)
        {
            (frame, uint child) = Append(frame, 0x5A5DA0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age == 8)
        {
            NativeBattleGatlingGun305AnchorStep target = inputs.ReadAnchor571400(frame, checked((int)frame.State.ReadDword(TargetGlobal)));
            frame = target.Frame;
            short stepX = unchecked((short)((target.Anchor.X - x) / 6));
            short stepZ = unchecked((short)((target.Anchor.Z - z) / 6));
            for (int index = 1; index <= 5; index++)
            {
                (frame, uint child) = Append(frame, 0x5A6070);
                frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)(index * 2)).WriteWord(0x10, unchecked((ushort)(x + index * stepX))).WriteWord(0x12, 0).WriteWord(0x14, unchecked((ushort)(z + index * stepZ))).WriteWord(0x18, (ushort)index));
            }
        }
        if (age == 18)
        {
            int targetActor = checked((int)frame.State.ReadDword(TargetGlobal));
            NativeBattleGatlingGun305AnchorStep target = inputs.ReadAnchor571400(frame, targetActor);
            frame = target.Frame;
            NativeBattleEffectLocalBounds bounds = inputs.ReadModel(targetActor).GeometryBounds52 ?? throw new NotSupportedException("Gatling Gun requires its target bounds.");
            short stepY = unchecked((short)((bounds.Minimum.Y - bounds.Maximum.Y) / 8));
            for (int index = 1; index <= 7; index++)
            {
                (frame, uint child) = Append(frame, 0x5A6580);
                NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)(index * 2)).WriteWord(0x10, unchecked((ushort)target.Anchor.X)).WriteWord(0x14, unchecked((ushort)target.Anchor.Z)).WriteWord(0x18, (ushort)index);
                frame = DrawRandom(frame, out ushort random);
                frame = Write(frame, child, task.WriteWord(0x12, unchecked((ushort)(bounds.Maximum.Y + index * stepY - random % 100))));
            }
        }
        if (age <= 8 || age >= 28)
        {
            frame = inputs.Execute(frame, new NativeBattleGatlingGun305Stage(unchecked((short)((age <= 8 ? age : 36 - age) * 192)), 0));
        }
        if (age == 30)
        {
            frame = inputs.Execute(frame, new NativeBattleGatlingGun305Contact(FirstContact(frame, inputs)));
        }
        if (age == 8)
        {
            frame = inputs.Execute(frame, new NativeBattleGatlingGun305Sound(resources.Sound, 128));
        }
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        if (unchecked((short)next) > 36)
        {
            frame = inputs.Execute(frame, new NativeBattleGatlingGun305Stage(0, 0));
            return (frame, 2);
        }
        return (frame, 0);
    }

    private static NativeBattleGatlingGun305Frame CopyAnchor(NativeBattleGatlingGun305Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = CopyPacked(Read(frame, address), 0x10, frame.State.ReadView(AnchorGlobal, 8));
        return Write(frame, address, task);
    }

    private static NativeBattleModulePoolTask CopyPacked(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, 8);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < bytes.Length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
