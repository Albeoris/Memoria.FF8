using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleChoco99EmissionKind
{
    Trail,
    Radial
}

public static partial class NativeBattleChoco99ProgramDrawing
{
    public static NativeBattleChoco99ProgramDraw AdvanceMoving71FFB0(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, short contextMode40, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary, Func<NativeBattleModulePoolTask, NativeBattleChoco99EmissionKind, short, NativeBattleModulePoolTask> emit)
    {
        return AdvanceMoving(task, source, contextMode40, morphScratch, view, projection, colors, frozenSecondary, emit, false);
    }

    public static NativeBattleChoco99ProgramDraw AdvanceSmall720610(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, short contextMode40, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary, Func<NativeBattleModulePoolTask, NativeBattleChoco99EmissionKind, short, NativeBattleModulePoolTask> emit)
    {
        return AdvanceMoving(task, source, contextMode40, morphScratch, view, projection, colors, frozenSecondary, emit, true);
    }

    private static NativeBattleChoco99ProgramDraw AdvanceMoving(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, short contextMode40, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary, Func<NativeBattleModulePoolTask, NativeBattleChoco99EmissionKind, short, NativeBattleModulePoolTask> emit, bool small)
    {
        ArgumentNullException.ThrowIfNull(emit);
        if (task.PayloadLength != 0x548 - 12 || task.Callback != (small ? 0x720610u : 0x71FFB0u) || source.Address != 0x19F0F40 || source.Data.IsDefault || source.Data.Length != 0x1638 || task.ReadDword(0x74) != source.Address || task.ReadDword(0x78) != 0x2C || morphScratch.IsDefault || morphScratch.Length != 0x760)
        {
            throw new InvalidDataException("Native Choco moving program requires its actual task, file, clear and morph owners.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        int? remaining = null;
        NativeBattleGfGeometryBatch packets = new([], 0);
        if (phase == 0)
        {
            task = NativeBattleChoco97ProgramDrawing.InitializeWorkspaceProfiled(task, source, NativeBattleChocoProgramProfile.Choco99).WriteWord(0x1C, small ? (ushort)0 : (ushort)4096).WriteWord(0x1E, unchecked((ushort)(small ? -8192 : -4096))).WriteWord(0x20, 0).WriteWord(0x68, unchecked((ushort)-384)).WriteWord(0x6A, 512).WriteDword(0x50, small ? 256u : 4096u).WriteDword(0x54, small ? 256u : 4096u).WriteDword(0x58, small ? 256u : 4096u);
            Draw();
            task = task.WriteByte(0x29, 1);
        }
        else if (small && phase == 1)
        {
            if (unchecked((int)task.ReadDword(0x50)) < 4096)
            {
                int scale = unchecked((int)task.ReadDword(0x50) + 768);
                if (scale >= 4096)
                {
                    scale = 4096;
                }
                task = task.WriteDword(0x50, unchecked((uint)scale)).WriteDword(0x54, unchecked((uint)scale)).WriteDword(0x58, unchecked((uint)scale));
            }
            Motion();
            if (contextMode40 >= 8)
            {
                Complete(2);
            }
            else
            {
                Draw();
                Emit(NativeBattleChoco99EmissionKind.Trail, 8);
            }
        }
        else if (!small && phase == 1)
        {
            Motion();
            if (Word(0x1E) >= 0)
            {
                task = task.WriteWord(0x84, 10).WriteByte(0x29, 2);
            }
            else
            {
                Draw();
                Emit(NativeBattleChoco99EmissionKind.Trail, 10);
            }
        }
        else if (!small && phase == 2)
        {
            task = task.WriteWord(0x84, unchecked((ushort)(task.ReadWord(0x84) - 1)));
            if (Word(0x84) <= 0)
            {
                task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x68) * -5))).WriteWord(0x1E, unchecked((ushort)(1536 - unchecked((ushort)(task.ReadWord(0x6A) * 5))))).WriteWord(0x84, 0).WriteByte(0x29, 3);
            }
        }
        else if (!small && phase == 3)
        {
            task = task.WriteWord(0x84, unchecked((ushort)(task.ReadWord(0x84) + 1)));
            if (Word(0x84) >= 4 && Word(0x84) <= 8)
            {
                Emit(NativeBattleChoco99EmissionKind.Radial, 10);
            }
            Motion();
            if (contextMode40 >= 10)
            {
                Complete(4);
            }
            else
            {
                Draw();
                Emit(NativeBattleChoco99EmissionKind.Trail, 10);
            }
        }
        else if (phase != (small ? 2 : 4))
        {
            throw new NotSupportedException("Native Choco moving program phase leaves its physical caller.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(new(task, source, morphScratch, packets, remaining, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0), null);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));

        void Motion()
        {
            task = task.WriteDword(0x540, task.ReadDword(0x1C)).WriteDword(0x544, task.ReadDword(0x20)).WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x68)))).WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + task.ReadWord(0x6A)))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + task.ReadWord(0x6C)))).WriteWord(0x64, unchecked((ushort)((task.ReadWord(0x64) - 64) & 4095)));
        }

        void Draw()
        {
            NativeBattleChoco97ProgramDraw step = Advance718A70(task, source, morphScratch, view, projection, colors, frozenSecondary);
            task = step.Task;
            morphScratch = step.MorphScratch;
            remaining = step.Remaining;
            packets = step.Packets;
        }

        void Emit(NativeBattleChoco99EmissionKind kind, short mode)
        {
            byte expectedChildren = unchecked((byte)(task.ReadByte(0x28) + 1));
            task = emit(task, kind, mode);
            if (task.ReadByte(0x28) != expectedChildren)
            {
                throw new InvalidDataException("Native Choco emission must publish its actual appended parent child count.");
            }
        }

        void Complete(byte nextPhase)
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, nextPhase);
        }
    }
}
