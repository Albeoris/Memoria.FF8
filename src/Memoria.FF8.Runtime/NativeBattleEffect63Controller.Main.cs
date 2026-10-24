using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect63Controller
{
    public static NativeBattleEffect63Step AdvanceMain6E6E10(NativeBattleEffect63Frame frame, uint address, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        frame = inputs.Execute(frame, new NativeBattleEffect63Stage5713E0(StageStrength(age), 0));
        uint caster = frame.State.ReadDword(CasterGlobal);
        NativeBattleEffect63PointStep first = inputs.Point6E7710(frame, caster, NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(FirstPointAddress, 8)), UnknownPoint());
        NativeBattleEffect63PointStep second = inputs.Point6E7710(first.Frame, caster, NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(SecondPointAddress, 8)), UnknownPoint());
        frame = second.Frame;
        NativeBattleVector a = Vector(first.Output8);
        NativeBattleVector b = Vector(second.Output8);
        NativeBattleVector midpoint = new(unchecked((short)((a.X + b.X) / 2)), unchecked((short)((a.Y + b.Y) / 2)), unchecked((short)((a.Z + b.Z) / 2)));
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(0, 52);
        if ((ushort)age < 45)
        {
            (frame, caller) = AdvanceAtPoint(frame, address + 0x1E8, 0x6A0, midpoint, caller, resources, inputs);
        }
        if ((ushort)age < 48)
        {
            (frame, caller) = AdvanceAtPoint(frame, address + 0x18, 0xE8, a, caller, resources, inputs);
            (frame, caller) = AdvanceAtPoint(frame, address + 0x100, 0xE8, b, caller, resources, inputs);
        }
        NativeBattleEffectArena anchor = TaskView(Read(frame, address), 0x10, 8);
        if ((uint)(age - 16) < 20)
        {
            int relative = age - 16;
            int fraction = relative * 4096 / 20;
            _ = NativeBattleCameraMath.Cosine(fraction >> 2);
            int cosine = NativeBattleCameraMath.Cosine(fraction >> 2);
            int product = unchecked(NativeBattleCameraMath.Sine(fraction) * cosine);
            int pitch = product >> 17;
            int yaw = product >> 15;
            int length = unchecked(fraction + 4 * NativeBattleCameraMath.Sine(fraction / 2));
            int fade = relative < 8 ? 4096 - (relative << 9) : 0;
            frame = NativeBattleEffect63Lower.DrawBeam6E7790(frame, first.Output8, anchor, relative << 5, -pitch, yaw, length, fade, resources, inputs);
            frame = NativeBattleEffect63Lower.DrawBeam6E7790(frame, second.Output8, anchor, relative << 5, pitch, -yaw, length, fade, resources, inputs);
        }
        int late = age - 36;
        if ((uint)late < 28)
        {
            if ((uint)(late - 1) < 4)
            {
                frame = NativeBattleEffect63Lower.Envelope6E7A30(frame, late - 1, 255, 255, 255, 0, 1, 3, inputs);
            }
            if (late < 8)
            {
                frame = NativeBattleEffect63Lower.DrawBeam6E7790(frame, first.Output8, anchor, late << 5, 0, 0, 4096, late << 9, resources, inputs);
                frame = NativeBattleEffect63Lower.DrawBeam6E7790(frame, second.Output8, anchor, late << 5, 0, 0, 4096, late << 9, resources, inputs);
            }
            if (late < 3)
            {
                frame = BirthSprites(frame, anchor);
            }
            NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, frame.State.ReadDword(TargetGlobal));
            int x = midpoint.X - BinaryPrimitives.ReadInt16LittleEndian(model.Read(0x1C, 2));
            int z = BinaryPrimitives.ReadInt16LittleEndian(model.Read(0x20, 2)) - midpoint.Z;
            (NativeBattleActionMatrix matrix, int length) = NativeBattleFunguarLaser277.Basis50CBA0(x, 0, z);
            NativeBattleVector origin = Vector(anchor);
            matrix = matrix with { X = unchecked(origin.X + unchecked(x * 500) / length), Y = origin.Y, Z = unchecked(origin.Z - unchecked(z * 500) / length) };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), matrix);
            ushort? padding = age < 48 ? (ushort)0 : null;
            caller = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(composed, padding)), 0, caller, 0, 32).Write(0x20, BitConverter.GetBytes(SecondSelectorsAddress)).Write(0x24, BitConverter.GetBytes(unchecked((short)-128))).Write(0x26, BitConverter.GetBytes(unchecked((ushort)(late << 5)))).Write(0x30, BitConverter.GetBytes(checked(frame.State.ReadDword(WorkGlobal) + MorphOffset)));
            frame = NativeBattleEffect63Programme.Advance(frame, address + 0x888, 0x580, caller, resources, inputs).Frame;
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect63Sound501330(SoundAddress, 0, 128));
        }
        if (age == 54)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect63 requires its current action header.");
            int firstContact = inputs.ReadPackets(frame).Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect63 requires its current first contact.");
            frame = inputs.Execute(frame, new NativeBattleEffect63Contact506690(firstContact));
        }
        if (age >= 64)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect63Stage5713E0(0, 0));
            return new(frame, 2);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static (NativeBattleEffect63Frame Frame, NativeBattleEffectArena Caller) AdvanceAtPoint(NativeBattleEffect63Frame frame, uint workspace, int length, NativeBattleVector point, NativeBattleEffectArena caller, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs)
    {
        NativeBattleActionMatrix local = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], point.X, point.Y, point.Z, 4096);
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        caller = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(composed, 0)), 0, caller, 0, 32).Write(0x20, BitConverter.GetBytes(FirstSelectorsAddress)).Write(0x24, BitConverter.GetBytes(unchecked((short)-128))).Write(0x30, BitConverter.GetBytes(checked(frame.State.ReadDword(WorkGlobal) + MorphOffset)));
        return (NativeBattleEffect63Programme.Advance(frame, workspace, length, caller, resources, inputs).Frame, caller);
    }

    public static int StageStrength(int age)
    {
        if ((uint)(age - 16) < 4)
        {
            return unchecked((age - 16) * 575);
        }
        if (age >= 52)
        {
            return unchecked((64 - age) * 2300) / 12;
        }
        return age >= 16 ? 2300 : 0;
    }

    private static NativeBattleEffectArena UnknownPoint() => NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
}
