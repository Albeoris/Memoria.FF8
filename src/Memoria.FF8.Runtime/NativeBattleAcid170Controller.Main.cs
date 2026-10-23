namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid170Controller
{
    public static (NativeBattleAcid170Frame Frame, int Result) AdvanceMain(NativeBattleAcid170Frame frame, uint address, NativeBattleAcid170Resources resources, NativeBattleAcid170Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        if (age == 1)
        {
            ushort index = Read(frame, address).ReadWord(14);
            (frame, uint curve) = RequireAppend(frame, 0x5DCAF0);
            frame = Write(frame, curve, Read(frame, curve).WriteWord(12, 0).WriteWord(14, index));
            frame = Write(frame, address, Read(frame, address).WriteWord(14, unchecked((ushort)(index + 1))));
            int caster = checked((int)frame.State.ReadDword(0x23B10E8));
            NativeBattleAcid170PointStep bone = inputs.ReadBone502170(frame, caster, 40, 0);
            frame = WriteBytes(bone.Frame, 0x23B1340, bone.Point8, 6);
            caster = checked((int)frame.State.ReadDword(0x23B10E8));
            bone = inputs.ReadBone502170(frame, caster, 38, 0);
            frame = WriteBytes(bone.Frame, 0x23B0FC8, bone.Point8, 6);
            (frame, uint particles) = RequireAppend(frame, 0x5DEA10);
            uint mask = 1u << (Read(frame, address).ReadWord(14) & 31);
            frame = Write(frame, particles, Read(frame, particles).WriteWord(12, 0).WriteWord(14, unchecked((ushort)mask)));
        }
        if (age == 46)
        {
            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Acid requires its captured first physical contact pointer.");
            frame = inputs.Execute(frame, new NativeBattleAcid170Contact506690(first));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleAcid170Sound501330(resources.Sound, 0, 128));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        return (frame, Signed(Read(frame, address), 12) > 55 ? 2 : 0);
    }
}
