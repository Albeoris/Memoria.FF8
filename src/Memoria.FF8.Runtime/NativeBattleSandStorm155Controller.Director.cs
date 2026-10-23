namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSandStorm155Controller
{
    public static (NativeBattleSandStorm155Frame Frame, int Result) AdvanceDirector(NativeBattleSandStorm155Frame frame, uint address, NativeBattleSandStorm155Resources resources, NativeBattleSandStorm155Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        if (age == 0)
        {
            NativeBattleSandStorm155AnchorStep anchor = inputs.ReadAnchor571400(frame, checked((int)frame.State.ReadDword(0x23F2950)));
            frame = anchor.Frame;
            frame = Word(Word(Word(Word(frame, 0x23F2958, anchor.Anchor.X), 0x23F295A, anchor.Anchor.W), 0x23F295C, anchor.Anchor.Z), 0x23F295E, anchor.Anchor.Y);
            NativeBattleSandStorm155PointStep center = inputs.ReadTargetCenter(frame, Parameters(frame).TargetMask7A);
            frame = center.Frame;
            frame = Word(Word(Word(frame, 0x23F2938, center.Point.X), 0x23F293A, center.Point.Y), 0x23F293C, center.Point.Z);
            frame = NativeBattleSandStorm155Geometry.Sort607090(frame, resources);
        }
        if (age == 1)
        {
            (frame, uint displace) = RequireAppend(frame, 0x607130);
            NativeBattleModulePoolTask task = Read(frame, displace).WriteWord(12, 0).WriteWord(28, 0);
            frame = DrawRandom(frame, out ushort angle);
            frame = DrawRandom(frame, out ushort increment);
            frame = Write(frame, displace, task.WriteWord(24, (ushort)(angle & 4095)).WriteWord(26, unchecked((ushort)(-70 - increment % 70))));
            (frame, uint mesh) = RequireAppend(frame, 0x607210);
            task = Read(frame, mesh).WriteWord(12, 0).WriteWord(18, 1024).WriteWord(22, 1024).WriteWord(32, 0);
            frame = DrawRandom(frame, out angle);
            frame = DrawRandom(frame, out ushort radius);
            frame = DrawRandom(frame, out ushort sway);
            frame = DrawRandom(frame, out ushort turn);
            int velocity = turn % 40 + 20;
            int horizontal = sway % 100 - 50;
            if (horizontal < 0)
            {
                velocity = -velocity;
            }
            task = task.WriteWord(24, (ushort)(angle & 4095)).WriteWord(26, (ushort)(radius % 70 * 2 + 140)).WriteWord(28, unchecked((ushort)horizontal)).WriteWord(30, unchecked((ushort)velocity));
            frame = DrawRandom(frame, out ushort speed);
            frame = Write(frame, mesh, task.WriteWord(34, (ushort)(speed % 150 + 190)));
            frame = Copy(Copy(frame, 0x23F2938, 0x23F07F8, 8), 0x23F2938, 0x23F07F0, 8);
            frame = DrawRandom(frame, out ushort left);
            frame = Word(frame, 0x23F07F0, Word(frame, 0x23F07F0) - 4000 - left % 2600);
            frame = DrawRandom(frame, out ushort leftZ);
            frame = Word(frame, 0x23F07F4, Word(frame, 0x23F07F4) + leftZ % 1300 - 650);
            frame = DrawRandom(frame, out ushort right);
            frame = Word(frame, 0x23F07F8, Word(frame, 0x23F07F8) + 4000 + right % 2600);
            frame = DrawRandom(frame, out ushort rightZ);
            frame = Word(frame, 0x23F07FC, Word(frame, 0x23F07FC) + rightZ % 1300 - 650);
            frame = DrawRandom(frame, out ushort controlX);
            frame = Word(Word(frame, 0x23F2920, Word(frame, 0x23F2938) + controlX % 2600 - 1300), 0x23F2922, 0);
            frame = DrawRandom(frame, out ushort controlZ);
            frame = Word(frame, 0x23F2924, Word(frame, 0x23F293C) + controlZ % 1300 + 600);
        }
        if (age == 3 || age == 4)
        {
            (frame, uint child) = RequireAppend(frame, age == 3 ? 0x6076E0u : 0x607920u);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age == 50)
        {
            frame = inputs.Execute(frame, new NativeBattleSandStorm155ContactsAll(Parameters(frame).HeaderIndex7C));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleSandStorm155Sound(resources.Sound, 0, 128));
        }
        NativeBattleModulePoolTask director = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, director), Signed(director, 12) > 60 ? 2 : 0);
    }

    private static (NativeBattleSandStorm155Frame Frame, uint Address) RequireAppend(NativeBattleSandStorm155Frame frame, uint callback)
    {
        var appended = Append(frame, callback);
        if (appended.Address == 0)
        {
            throw new NotSupportedException("Sand Storm exhausted its actual child task bank.");
        }
        return appended;
    }
}
