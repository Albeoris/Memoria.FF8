using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect320Controller
{
    public static NativeBattleEffect320Step AdvanceMain65BE90(NativeBattleEffect320Frame frame, uint address, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        int phase = Signed(Read(frame, address), 12);
        if ((uint)phase < 36)
        {
            NativeBattleEffectArena originInput = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(0xFF12A0, 8));
            NativeBattleEffect320PointStep origin = inputs.Origin65DA30(frame, originInput, TaskView(Read(frame, address), 0x30, 8));
            frame = WriteBytes(origin.Frame, address + 0x30, origin.Output8, 6);
            NativeBattleVector point = Vector(origin.Output8);
            NativeBattleVector center = Vector(TaskView(Read(frame, address), 0x38, 8));
            NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(center.X - point.X, point.Y - center.Y, point.Z - center.Z).Matrix with { X = point.X, Y = point.Y, Z = point.Z };
            frame = StoreMainMatrix(frame, address, basis);
            frame = AdvanceProgramme(frame, address, 0x40, 0xFE0FAC, 0xFF12D4, -16, null, resources, inputs);
            if ((uint)phase >= 15)
            {
                frame = BirthEarlyParticles(frame, address, phase, resources, inputs);
            }
        }
        int elapsed = phase - 36;
        if ((uint)elapsed <= 10)
        {
            int factor = InterpolationFactor(elapsed);
            if (elapsed == 0)
            {
                frame = NativeBattleEffect320Lower.Flash65D900(frame, 0x808080, inputs);
            }
            (frame, NativeBattleVector blended) = Blend(frame, address, factor, 4096 - factor, inputs);
            frame = SetMainTranslation(frame, address, blended);
            frame = AdvanceProgramme(frame, address, 0x40, 0xFE0FAC, 0xFF12D4, -16, null, resources, inputs);
        }
        if ((uint)elapsed < 16)
        {
            frame = SetMainTranslation(frame, address, Vector(TaskView(Read(frame, address), 0x30, 8)));
            frame = AdvanceProgramme(frame, address, 0x230, 0xFE0FB4, 0xFE0FC8, -16, null, resources, inputs);
        }
        int finalElapsed = phase - 45;
        if ((uint)finalElapsed < 53)
        {
            if (finalElapsed == 1)
            {
                frame = NativeBattleEffect320Lower.Flash65D970(frame, 0x808080, inputs);
            }
            else if (finalElapsed >= 2 && finalElapsed <= 4)
            {
                uint color = finalElapsed == 2 ? 0xF0F0F0u : finalElapsed == 3 ? 0xA0A0A0u : 0x505050u;
                frame = NativeBattleEffect320Lower.Flash65D900(frame, color, inputs);
            }
            NativeBattleVector center = Vector(TaskView(Read(frame, address), 0x38, 8));
            NativeBattleActionMatrix centered = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = center.X, Y = center.Y, Z = center.Z };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), centered);
            NativeBattleEffectArena caller = BuildCaller(frame, composed, 0xFF12C8, 0xFF12BC, -512, unchecked((ushort)(finalElapsed << 4)));
            frame = NativeBattleEffect320Programme.Advance(frame, address + 0x340, caller, resources, inputs).Frame;
        }
        if (phase == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect320Sound501330(SoundAddress, 0, 128));
        }
        if ((uint)elapsed <= 10)
        {
            frame = BirthLateParticles(frame, address, InterpolationFactor(elapsed), inputs);
        }
        if (phase == 83)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect320 requires its current contact parameters.");
            NativeBattleActionHeaderSlot header = inputs.ReadPackets(frame).Headers[parameters.HeaderIndex7C];
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect320 requires its current contact pointer.");
            frame = inputs.Execute(frame, new NativeBattleEffect320AllContacts506BA0(first, header.ReadByte(16)));
        }
        short next = unchecked((short)(phase + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next >= 98 ? 2 : 0);
    }

    public static int InterpolationFactor(int elapsed)
    {
        uint shifted = unchecked((uint)(elapsed << 12));
        return unchecked((int)(((0xCCCCCCCDUL * shifted) >> 32) >> 3));
    }

    private static NativeBattleEffect320Frame StoreMainMatrix(NativeBattleEffect320Frame frame, uint address, NativeBattleActionMatrix matrix)
    {
        NativeBattleEffectArena prior = TaskView(Read(frame, address), 0x10, 32);
        NativeBattleEffectArena packed = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        packed = CopyBytes(prior, 18, packed, 18, 2);
        return WriteBytes(frame, address + 0x10, packed, 32);
    }

    private static NativeBattleEffect320Frame SetMainTranslation(NativeBattleEffect320Frame frame, uint address, NativeBattleVector point)
    {
        return Write(frame, address, Read(frame, address).WriteDword(0x24, unchecked((uint)(int)point.X)).WriteDword(0x28, unchecked((uint)(int)point.Y)).WriteDword(0x2C, unchecked((uint)(int)point.Z)));
    }

    private static NativeBattleEffect320Frame AdvanceProgramme(NativeBattleEffect320Frame frame, uint address, int cacheOffset, uint material, uint shader, short depth, ushort? phase, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        NativeBattleActionMatrix current = PackedMatrix(TaskView(Read(frame, address), 0x10, 32)).ReadMatrix();
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), current);
        NativeBattleEffectArena caller = BuildCaller(frame, composed, material, shader, depth, phase);
        return NativeBattleEffect320Programme.Advance(frame, checked(address + (uint)cacheOffset), caller, resources, inputs).Frame;
    }

    public static NativeBattleEffectArena BuildCaller(NativeBattleEffect320Frame frame, NativeBattleActionMatrix matrix, uint material, uint shader, short depth, ushort? phase)
    {
        NativeBattleEffectArena caller = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 48), 0, 32);
        caller = caller.Write(0x20, BitConverter.GetBytes(material)).Write(0x24, BitConverter.GetBytes(shader)).Write(0x28, BitConverter.GetBytes(depth)).Write(0x2C, BitConverter.GetBytes(checked(frame.State.ReadDword(ArenaGlobal) + (uint)MorphOffset)));
        if (phase is ushort value)
        {
            caller = caller.Write(0x2A, BitConverter.GetBytes(value));
        }
        return caller;
    }

    private static (NativeBattleEffect320Frame Frame, NativeBattleVector Point) Blend(NativeBattleEffect320Frame frame, uint main, int factor, int complement, NativeBattleEffect320Inputs inputs)
    {
        NativeBattleVector first = Vector(TaskView(Read(frame, main), 0x38, 8));
        NativeBattleVector second = Vector(TaskView(Read(frame, main), 0x30, 8));
        NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, first, second, unchecked((short)factor), unchecked((short)complement), inputs.Colors);
        return (frame with { Memory = blended.Memory }, blended.Position);
    }

    public static NativeBattlePackedMatrix32 PackedMatrix(NativeBattleEffectArena source)
    {
        (byte[] data, bool[] known) = source.ReadSnapshot(0, 32);
        return new(data.ToImmutableArray(), known.ToImmutableArray());
    }
}
