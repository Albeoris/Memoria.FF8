using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlaster186Controller
{
    public static (NativeBattleBlaster186Frame Frame, int Result) AdvanceMain(NativeBattleBlaster186Frame frame, uint address, NativeBattleBlaster186Resources resources, NativeBattleBlaster186Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        frame = PrepareMainGeometry(frame, age, resources, inputs);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        if (age == 0)
        {
            frame = frame with { State = NativeBattleBlaster186Morph.Initialize(frame.State, resources) };
        }
        if (age == 7)
        {
            (frame, uint child) = RequireAppend(frame, 0x5BFCF0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, 1));
        }
        if (age == 39)
        {
            (frame, uint child) = RequireAppend(frame, 0x5BFC50);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, 0xE10).WriteWord(28, 255));
            (frame, child) = RequireAppend(frame, 0x5C0410);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
            (frame, child) = RequireAppend(frame, 0x5C0690);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(28, 0xE00));
        }
        if (age == 40)
        {
            (frame, uint child) = RequireAppend(frame, 0x5C0190);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, 1));
            (frame, child) = RequireAppend(frame, 0x5C0750);
            int target = checked((int)frame.State.ReadDword(0x22B7600));
            NativeBattleModelTransform transform = inputs.ReadModel(frame, target).Transform ?? throw new NotSupportedException("Blaster requires its current recipient transform.");
            int caster = checked((int)frame.State.ReadDword(0x22BC0FC));
            short yaw = inputs.ReadModel(frame, caster).Rotation14 ?? throw new NotSupportedException("Blaster requires its current caster yaw.");
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child).WriteWord(12, 0).WriteWord(28, 0x1600).WriteWord(24, unchecked((ushort)(yaw + 2048))), 16, PointView(transform.Position28, transform.Vertical34), 8);
            frame = Write(frame, child, task);
            (frame, child) = RequireAppend(frame, 0x5C0900);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age == 60)
        {
            frame = inputs.Execute(frame, new NativeBattleBlaster186ContactsAll(Parameters(frame).HeaderIndex7C));
        }
        if (age <= 16)
        {
            frame = inputs.Execute(frame, new NativeBattleBlaster186Stage5713E0(unchecked(age << 7), 0));
        }
        else if (age >= 57)
        {
            frame = inputs.Execute(frame, new NativeBattleBlaster186Stage5713E0(unchecked((65 - age) << 8), 0));
        }
        if (age == 7)
        {
            frame = inputs.Execute(frame, new NativeBattleBlaster186Sound(resources.Sound, 0, 128));
        }
        NativeBattleModulePoolTask main = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        frame = Write(frame, address, main);
        if (Signed(main, 12) > 65)
        {
            return (inputs.Execute(frame, new NativeBattleBlaster186Stage5713E0(0, 0)), 2);
        }
        return (frame, 0);
    }

    public static NativeBattleBlaster186Frame PrepareMainGeometry(NativeBattleBlaster186Frame frame, int age, NativeBattleBlaster186Resources resources, NativeBattleBlaster186Inputs inputs)
    {
        int caster = checked((int)frame.State.ReadDword(0x22BC0FC));
        NativeBattleBlaster186PointStep bone = inputs.ReadBone502170(frame, caster, 0x21, 4096);
        frame = bone.Frame with { State = bone.Frame.State.WriteView(0x22BC108, bone.Point8, 6) };
        if (age == 0)
        {
            int actor = checked((int)frame.State.ReadDword(0x22B7600));
            NativeBattleBlaster186PointStep target = inputs.ReadAnchor571400(frame, actor);
            frame = target.Frame with { State = target.Frame.State.WriteView(0x22BC0F0, target.Point8, 6) };
        }
        int x = Word(frame, 0x22BC0F0) - Word(frame, 0x22BC108);
        int y = Word(frame, 0x22BC0F2) - Word(frame, 0x22BC10A);
        int z = Word(frame, 0x22BC0F4) - Word(frame, 0x22BC10C);
        var horizontal = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, 0, z);
        NativeBattleModuleAxisAngle horizontalAlignment = NativeBattleModuleAlignment.Between571480(resources.Forward[0], resources.Forward[1], resources.Forward[2], horizontal.X, horizontal.Y, horizontal.Z);
        NativeBattleActionMatrix horizontalMatrix = new(NativeBattleModuleAlignment.Rotation5714F0(horizontalAlignment.Angle, horizontalAlignment.Axis), 0, 0, 0, 4096);
        int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(NativeBattleSleepingGas168.NormalizeSquare(x, y, z));
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        NativeBattleModuleAxisAngle pitchAlignment = NativeBattleModuleAlignment.Between571480(horizontal.X, horizontal.Y, horizontal.Z, direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix pitch = new(NativeBattleModuleAlignment.Rotation5714F0(pitchAlignment.Angle, pitchAlignment.Axis), 0, 0, 0, 4096);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(pitch, horizontalMatrix);
        for (int index = 0; index < 9; index++)
        {
            frame = Word(frame, 0x22BBA70u + (uint)(index * 2), rotation.Rotation[index]);
        }
        frame = Dword(Dword(Dword(frame, 0x22BC110, unchecked((uint)direction.X)), 0x22BC114, unchecked((uint)direction.Y)), 0x22BC118, unchecked((uint)direction.Z));
        frame = Word(frame, 0x22BC10E, length - 300);
        int[] axes = [direction.X, direction.Y, direction.Z];
        for (int axis = 0; axis < 3; axis++)
        {
            uint current = 0x22BC108u + (uint)(axis * 2);
            frame = Word(frame, current, Word(frame, current) + (unchecked(axes[axis] * 300) >> 12));
            if (age == 0)
            {
                uint target = 0x22BC0F0u + (uint)(axis * 2);
                frame = Word(frame, target, Word(frame, target) + (unchecked(axes[axis] * -400) >> 12));
            }
        }
        return frame;
    }
}
