namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath157Controller
{
    public static (NativeBattleBreath157Frame Frame, int Result) AdvanceDirector(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs)
    {
        frame = PreparePose(frame, 0x23ECDB0, 0x23ECDD8, 0x23ECDF8, resources, inputs);
        frame = PreparePose(frame, 0x23EB5C0, 0x23EB5E0, 0x23EBF88, resources, inputs);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        int caster = checked((int)frame.State.ReadDword(0x23ECDF0));
        if (age == 0)
        {
            NativeBattleBreath157PointStep anchor = inputs.ReadAnchor571400(frame, checked((int)frame.State.ReadDword(0x23EB5F0)));
            frame = anchor.Frame with { State = anchor.Frame.State.WriteView(0x23ECDD0, anchor.Point8, 8) };
            frame = PrepareEmitter(frame, caster, 0x23ECDF8, 0x23ECDD8, 0x23ECDE8, new(0, 550, -640), inputs);
        }
        if (age == 1)
        {
            (frame, uint child) = RequireAppend(frame, 0x6043A0);
            frame = DrawRandom(frame, out ushort angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(24, (ushort)(angle & 4095)).WriteWord(28, 5120));
            frame = DrawRandom(frame, out ushort increment);
            frame = Write(frame, child, Read(frame, child).WriteWord(26, (ushort)(increment % 50 + 30)));
            (frame, child) = RequireAppend(frame, 0x6043A0);
            frame = DrawRandom(frame, out angle);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(24, (ushort)(angle & 4095)).WriteWord(28, 3840));
            frame = DrawRandom(frame, out increment);
            frame = Write(frame, child, Read(frame, child).WriteWord(26, unchecked((ushort)(-20 - increment % 60))));
        }
        if (age == 2)
        {
            (frame, uint child) = RequireAppend(frame, 0x6044D0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age == 19)
        {
            frame = PrepareEmitter(frame, caster, 0x23EBF88, 0x23EB5E0, 0x23EB5F4, new(0, 900, -1350), inputs);
        }
        if (age is 20 or 32 or 43)
        {
            (frame, uint child) = RequireAppend(frame, 0x604780);
            ushort index = Read(frame, address).ReadWord(14);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, index));
            frame = Write(frame, address, Read(frame, address).WriteWord(14, unchecked((ushort)(index + 1))));
        }
        if (age == 65)
        {
            frame = inputs.Execute(frame, new NativeBattleBreath157ContactFirst(Parameters(frame).HeaderIndex7C));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleBreath157Sound(resources.SoundA, 0, 128));
        }
        if (age == 20)
        {
            frame = inputs.Execute(frame, new NativeBattleBreath157Sound(resources.SoundB, 0, 128));
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) > 70 ? 2 : 0);
    }

    private static NativeBattleBreath157Frame PreparePose(NativeBattleBreath157Frame frame, uint destination, uint direction, uint position, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs)
    {
        NativeBattleVector reference = resources.ReferenceDirection;
        NativeBattleModuleAxisAngle axis = NativeBattleModuleAlignment.Between571480(reference.X, reference.Y, reference.Z, unchecked((int)frame.State.ReadDword(direction)), unchecked((int)frame.State.ReadDword(direction + 4)), unchecked((int)frame.State.ReadDword(direction + 8)));
        NativeBattleActionMatrix local = new(NativeBattleModuleAlignment.Rotation5714F0(axis.Angle, axis.Axis), Word(frame, position), Word(frame, position + 2), Word(frame, position + 4), 4096);
        frame = WriteMatrix(frame, destination, local);
        NativeBattleActionMatrix transformed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        return WriteMatrix(frame, destination, transformed);
    }

    private static NativeBattleBreath157Frame WriteMatrix(NativeBattleBreath157Frame frame, uint destination, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            frame = Word(frame, destination + (uint)(index * 2), matrix.Rotation[index]);
        }
        return Dword(Dword(Dword(frame, destination + 20, unchecked((uint)matrix.X)), destination + 24, unchecked((uint)matrix.Y)), destination + 28, unchecked((uint)matrix.Z));
    }

    private static NativeBattleBreath157Frame PrepareEmitter(NativeBattleBreath157Frame frame, int actor, uint destination, uint direction, uint length, NativeBattleVector offset, NativeBattleBreath157Inputs inputs)
    {
        NativeBattleBreath157PointStep bone = inputs.ReadBone502170(frame, actor, 0xF0, 4096);
        frame = bone.Frame with { State = bone.Frame.State.WriteView(destination, bone.Point8, 6) };
        var rotated = NativeBattleGfMatrices.RotateLong56C600(inputs.ReadModelWorld(frame, actor), offset.X, offset.Y, offset.Z);
        frame = Word(Word(Word(frame, destination, Word(frame, destination) + rotated.X), destination + 2, Word(frame, destination + 2) + rotated.Y), destination + 4, Word(frame, destination + 4) + rotated.Z);
        int x = Word(frame, 0x23ECDD0) - Word(frame, destination);
        int y = Word(frame, 0x23ECDD2) - Word(frame, destination + 2);
        int z = Word(frame, 0x23ECDD4) - Word(frame, destination + 4);
        int squared = NativeBattleActionGeometryRules.SquaredMagnitude56BC50(x, y, z);
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        frame = Dword(Dword(Dword(frame, direction, unchecked((uint)normalized.X)), direction + 4, unchecked((uint)normalized.Y)), direction + 8, unchecked((uint)normalized.Z));
        return Dword(frame, length, unchecked((uint)NativeBattleRayBomb10Geometry.Sqrt56BEC0(squared)));
    }
}
