using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleChoco97LoadedProgram
{
    File445,
    File449,
    File450
}

public sealed record NativeBattleChoco97LoadedProgramResources(NativeBattleChoco97LoadedProgram Profile, NativeBattleGfAnimationSource ProgramSource);
public sealed record NativeBattleChoco97LoadedProgramDraw(NativeBattleModulePoolTask Task, [property: System.Text.Json.Serialization.JsonConverter(typeof(NativeBattleOwnedGfAnimationSource.Converter))] NativeBattleGfAnimationSource Source, ImmutableArray<byte> MorphScratch, NativeBattleGfGeometryBatch Packets, int? Remaining, bool ReadyToComplete);
internal sealed record NativeBattleChocoProgramVScroll(int Geometry, int Delta);
internal sealed record NativeBattleChocoLoadedProgramProfile(NativeBattleChocoProgramProfile Core, uint Address, int Length, uint Callback, int Clear, bool TargetOrigin, short OriginY, short OriginZ, bool ImmediateDraw, ImmutableArray<NativeBattleChocoProgramVScroll> Scrolls, uint? PhaseZeroSound);

public static partial class NativeBattleChoco97ProgramDrawing
{
    public static NativeBattleChoco97LoadedProgramDraw AdvanceLoaded(NativeBattleChoco97LoadedProgram profile, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> targetRaw8, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozen)
    {
        return AdvanceLoadedProfiled(Profile97(profile), task, source, actorRaw8, targetRaw8, morphScratch, view, projection, colors, frozen).Draw;
    }

    internal static (NativeBattleChoco97LoadedProgramDraw Draw, uint? SoundDescriptor) AdvanceLoadedProfiled(NativeBattleChocoLoadedProgramProfile profile, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> targetRaw8, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozen)
    {
        source = NativeBattleOwnedGfAnimationSource.Own(source);
        RequireTask(task, source, profile.Core);
        RequireScratch(morphScratch, profile.Core);
        if (source.Address != profile.Address || source.Length != profile.Length || task.Callback != profile.Callback || task.ReadDword(0x78) != profile.Clear)
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
                task = InitializeProfiled(task, source, profile.TargetOrigin ? targetRaw8 : actorRaw8, profile.Core);
                if (profile.OriginY != 0)
                {
                    task = task.WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + profile.OriginY)));
                }
                if (profile.OriginZ != 0)
                {
                    task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + profile.OriginZ)));
                }
                draw = profile.ImmediateDraw;
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
            NativeBattleChoco97ProgramDraw step = AdvanceProfiled(task, source, morphScratch, view, projection, colors, frozen, profile.Core);
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
        foreach (NativeBattleChocoProgramVScroll scroll in profile.Scrolls)
        {
            using NativeBattleGfSourceReader reader = source.OpenReader();
            int offset = ReadInt(reader, checked(8 + scroll.Geometry * 4));
            source = NativeBattleChoco97Uv.Scroll7311C0(source, offset / 4 * 4, scroll.Delta);
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return (new(task, source, morphScratch, packets, remaining, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0), phase == 0 ? profile.PhaseZeroSound : null);
    }

    public static (uint Address, int Length, uint Callback, int Clear, int Delta, int[] Geometries) LoadedProfile(NativeBattleChoco97LoadedProgram profile)
    {
        NativeBattleChocoLoadedProgramProfile selected = Profile97(profile);
        return (selected.Address, selected.Length, selected.Callback, selected.Clear, selected.Scrolls[0].Delta, selected.Scrolls.Select(scroll => scroll.Geometry).ToArray());
    }

    private static NativeBattleChocoLoadedProgramProfile Profile97(NativeBattleChoco97LoadedProgram profile)
    {
        return profile switch
        {
            NativeBattleChoco97LoadedProgram.File445 => new(NativeBattleChocoProgramProfile.Choco97, 0x19DB018, 0x4EE0, 0x731840, 0x44C, false, -384, 0, true, [new(0, 12), new(3, 12)], null),
            NativeBattleChoco97LoadedProgram.File449 => new(NativeBattleChocoProgramProfile.Choco97, 0x19E4958, 0x3D08, 0x731950, 0x170, false, -1024, -384, false, [new(2, 18)], null),
            NativeBattleChoco97LoadedProgram.File450 => new(NativeBattleChocoProgramProfile.Choco97, 0x19E8660, 0x6420, 0x731100, 0x290, true, 0, 0, true, [new(1, 6), new(2, 6), new(5, 6), new(6, 6)], null),
            _ => throw new NotSupportedException("Native Choco loaded program requires an admitted original caller profile.")
        };
    }
}
