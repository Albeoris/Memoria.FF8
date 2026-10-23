using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGatlingGun150Controller
{
    public static (NativeBattleGatlingGun150Frame Frame, int Result) AdvanceDirector(NativeBattleGatlingGun150Frame frame, uint address, NativeBattleGatlingGun150Resources resources, NativeBattleGatlingGun150Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int caster = checked((int)frame.State.ReadDword(CasterGlobal));
        NativeBattleGatlingGun150PointStep point = inputs.ReadBone502170(frame, caster, 0xF0, 0);
        frame = point.Frame with { State = point.Frame.State.WriteView(CasterPointGlobal, point.Point8, 8) };
        int age = Signed(Read(frame, address), 12);
        if (age is >= 2 and <= 25 && (age & 1) != 0)
        {
            (frame, uint child) = RequireAppend(frame, 0x60E6D0);
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 16, frame.State.ReadView(CasterPointGlobal, 8), 8).WriteWord(12, 0);
            short yaw = inputs.ReadModel(frame, caster).Rotation14 ?? throw new NotSupportedException("Gatling Gun requires its caster yaw.");
            task = task.WriteWord(24, unchecked((ushort)yaw));
            frame = DrawRandom(frame, out ushort angle);
            task = task.WriteWord(26, (ushort)(angle & 0x7FF));
            frame = DrawRandom(frame, out ushort size);
            frame = Write(frame, child, task.WriteWord(28, (ushort)(size % 1280 + 2560)));
        }
        if (age == 2)
        {
            (frame, uint child) = RequireAppend(frame, 0x60E820);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age == 1)
        {
            int targetActor = checked((int)frame.State.ReadDword(FirstTargetGlobal));
            NativeBattleGatlingGun150PointStep target = inputs.ReadAnchor571400(frame, targetActor);
            frame = target.Frame;
            int x = Word(frame, CasterPointGlobal);
            int z = Word(frame, CasterPointGlobal + 4);
            short stepX = unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(target.Point8.Read(0, 2)) - x) / 6));
            short stepZ = unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(target.Point8.Read(4, 2)) - z) / 6));
            for (int index = 1; index <= 5; index++)
            {
                (frame, uint child) = RequireAppend(frame, 0x60EAE0);
                NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)(index * 2)).WriteWord(16, unchecked((ushort)(x + index * stepX))).WriteWord(18, 0).WriteWord(20, unchecked((ushort)(z + index * stepZ))).WriteWord(24, (ushort)index);
                frame = Write(frame, child, task);
            }
        }
        if (age == 11)
        {
            int targetActor = checked((int)frame.State.ReadDword(FirstTargetGlobal));
            NativeBattleGatlingGun150PointStep target = inputs.ReadAnchor571400(frame, targetActor);
            frame = target.Frame;
            NativeBattleEffectLocalBounds bounds = inputs.ReadModel(frame, targetActor).GeometryBounds52 ?? throw new NotSupportedException("Gatling Gun requires its actual target bounds.");
            short stepY = unchecked((short)((bounds.Minimum.Y - bounds.Maximum.Y) / 8));
            short x = BinaryPrimitives.ReadInt16LittleEndian(target.Point8.Read(0, 2));
            short z = BinaryPrimitives.ReadInt16LittleEndian(target.Point8.Read(4, 2));
            for (int index = 1; index <= 7; index++)
            {
                (frame, uint child) = RequireAppend(frame, 0x60EFF0);
                NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)(index * 2)).WriteWord(16, unchecked((ushort)x)).WriteWord(20, unchecked((ushort)z)).WriteWord(24, (ushort)index);
                frame = DrawRandom(frame, out ushort random);
                frame = Write(frame, child, task.WriteWord(18, unchecked((ushort)(bounds.Maximum.Y + index * stepY - random % 100))));
            }
        }
        if (age == 30)
        {
            frame = inputs.Execute(frame, new NativeBattleGatlingGun150ContactsAll(Parameters(frame).HeaderIndex7C));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleGatlingGun150Sound(resources.Sound, 0, 128));
        }
        NativeBattleModulePoolTask director = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, director), Signed(director, 12) > 36 ? 2 : 0);
    }
}
