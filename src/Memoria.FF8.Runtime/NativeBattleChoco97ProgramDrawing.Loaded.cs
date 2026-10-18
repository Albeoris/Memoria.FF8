using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleChoco97LoadedProgram
{
    File445,
    File449,
    File450
}

public sealed record NativeBattleChoco97LoadedProgramResources(NativeBattleChoco97LoadedProgram Profile, NativeBattleGfAnimationSource ProgramSource);
public sealed record NativeBattleChoco97LoadedProgramDraw(NativeBattleModulePoolTask Task, NativeBattleGfAnimationSource Source, ImmutableArray<byte> MorphScratch, NativeBattleGfGeometryBatch Packets, int? Remaining, bool ReadyToComplete);

public static partial class NativeBattleChoco97ProgramDrawing
{
    public static NativeBattleChoco97LoadedProgramDraw AdvanceLoaded(NativeBattleChoco97LoadedProgram profile, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> targetRaw8, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozen)
    {
        (uint address, int length, uint callback, int clear, int delta, int[] geometries) = LoadedProfile(profile);
        RequireTask(task, source);
        RequireScratch(morphScratch);
        if (source.Address != address || source.Data.Length != length || task.Callback != callback || task.ReadDword(0x78) != clear)
        {
            throw new InvalidDataException("Native Choco loaded program requires its actual callback, file owner and clear operand.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        int? remaining = null;
        NativeBattleGfGeometryBatch packets = new([], 0);
        bool draw = false;
        switch (phase)
        {
            case 0:
                task = InitializeWorkspace72A6C0(task, source, profile == NativeBattleChoco97LoadedProgram.File450 ? targetRaw8 : actorRaw8);
                if (profile == NativeBattleChoco97LoadedProgram.File445)
                {
                    task = task.WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) - 384)));
                }
                else if (profile == NativeBattleChoco97LoadedProgram.File449)
                {
                    task = task.WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) - 1024))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) - 384)));
                }
                draw = profile != NativeBattleChoco97LoadedProgram.File449;
                break;
            case 1:
                draw = true;
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Choco loaded program phase leaves its three physical callbacks.");
        }
        if (draw)
        {
            NativeBattleChoco97ProgramDraw step = Advance72A720(task, source, morphScratch, view, projection, colors, frozen);
            task = step.Task;
            packets = step.Packets;
            morphScratch = step.MorphScratch;
            remaining = step.Remaining;
            if (phase == 1 && step.Remaining == 0)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
            }
        }
        if (phase == 0)
        {
            task = task.WriteByte(0x29, 1);
        }
        foreach (int geometry in geometries)
        {
            int offset = ReadInt(source.Data, checked(8 + geometry * 4));
            source = source with { Data = NativeBattleChoco97Uv.Scroll7311C0(source.Data, offset / 4 * 4, delta) };
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, source, morphScratch, packets, remaining, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }

    public static (uint Address, int Length, uint Callback, int Clear, int Delta, int[] Geometries) LoadedProfile(NativeBattleChoco97LoadedProgram profile)
    {
        return profile switch
        {
            NativeBattleChoco97LoadedProgram.File445 => (0x19DB018, 0x4EE0, 0x731840, 0x44C, 12, [0, 3]),
            NativeBattleChoco97LoadedProgram.File449 => (0x19E4958, 0x3D08, 0x731950, 0x170, 18, [2]),
            NativeBattleChoco97LoadedProgram.File450 => (0x19E8660, 0x6420, 0x731100, 0x290, 6, [1, 2, 5, 6]),
            _ => throw new NotSupportedException("Native Choco loaded program requires an admitted original caller profile.")
        };
    }
}
