using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleChoco99LoadedProgram
{
    File471,
    File474,
    File475,
    File482,
    File483,
    File484
}

public sealed record NativeBattleChoco99ProgramFile(short FileId, NativeBattleGfAnimationSource ProgramSource);
public sealed record NativeBattleChoco99ProgramResources(NativeBattleGfAnimationSource ProgramSource, ImmutableArray<NativeBattleChoco99ProgramFile> Loaded);
/// <summary>The caller submits Draw.Packets before emitting the optional phase-zero sound.</summary>
public sealed record NativeBattleChoco99ProgramDraw(NativeBattleChoco97LoadedProgramDraw Draw, uint? SoundDescriptor);

public static partial class NativeBattleChoco99ProgramDrawing
{
    public static NativeBattleModulePoolTask InitializeWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8) => NativeBattleChoco97ProgramDrawing.InitializeProfiled(task, source, actorRaw8, NativeBattleChocoProgramProfile.Choco99);

    public static NativeBattleChoco97ProgramDraw Advance718A70(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary) => NativeBattleChoco97ProgramDrawing.AdvanceProfiled(task, source, morphScratch, view, projection, colors, frozenSecondary, NativeBattleChocoProgramProfile.Choco99);

    public static NativeBattleChoco97NodeDraw Node718B90(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors) => NativeBattleChoco97ProgramDrawing.NodeProfiled(source, node, morphScratch, context, projection, colors, NativeBattleChocoProgramProfile.Choco99);

    public static NativeBattleChoco99ProgramDraw AdvanceInitial(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary)
    {
        NativeBattleChocoLoadedProgramProfile profile = new(NativeBattleChocoProgramProfile.Choco99, 0x166DC58, 0x2884, 0x7189B0, 0x4AC, false, 0, 0, true, [], 0x1529FA4);
        var result = NativeBattleChoco97ProgramDrawing.AdvanceLoadedProfiled(profile, task, source, actorRaw8, actorRaw8, morphScratch, view, projection, colors, frozenSecondary);
        return new(result.Draw, result.SoundDescriptor);
    }

    public static NativeBattleChoco99ProgramDraw AdvanceLoaded(NativeBattleChoco99LoadedProgram profile, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary)
    {
        var result = NativeBattleChoco97ProgramDrawing.AdvanceLoadedProfiled(Profile(profile), task, source, actorRaw8, actorRaw8, morphScratch, view, projection, colors, frozenSecondary);
        return new(result.Draw, result.SoundDescriptor);
    }

    public static NativeBattleChoco99ProgramDraw AdvanceHeldOrStatic(NativeBattleChoco99LoadedProgram profile, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, short contextMode40, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary)
    {
        var definition = CustomProfile(profile);
        if (task.PayloadLength != 0x548 - 12 || task.Callback != definition.Callback || source.Address != definition.Address || source.IsDefault || source.Length != definition.Length || task.ReadDword(0x74) != source.Address || task.ReadDword(0x78) != definition.Clear || morphScratch.IsDefault || morphScratch.Length != 0x760)
        {
            throw new InvalidDataException("Native Choco program requires its exact task, source, clear and morph owners.");
        }
        source = NativeBattleOwnedGfAnimationSource.Own(source);
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        bool draw;
        switch (phase)
        {
            case 0:
                task = NativeBattleChoco97ProgramDrawing.InitializeWorkspaceProfiled(task, source, NativeBattleChocoProgramProfile.Choco99).WriteWord(0x1C, 0).WriteWord(0x1E, profile is NativeBattleChoco99LoadedProgram.File483 or NativeBattleChoco99LoadedProgram.File484 ? (ushort)1536 : (ushort)0).WriteWord(0x20, profile == NativeBattleChoco99LoadedProgram.File482 ? (ushort)864 : (ushort)0);
                if (profile != NativeBattleChoco99LoadedProgram.File482)
                {
                    task = task.WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
                }
                draw = true;
                task = task.WriteByte(0x29, 1);
                break;
            case 1:
                draw = definition.TerminalMode is null || contextMode40 < definition.TerminalMode.Value;
                if (!draw)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
                }
                break;
            case 2:
                draw = false;
                break;
            default:
                throw new NotSupportedException("Native Choco program phase leaves its physical caller.");
        }
        int? remaining = null;
        NativeBattleGfGeometryBatch packets = new([], 0);
        if (draw)
        {
            NativeBattleChoco97ProgramDraw step = profile == NativeBattleChoco99LoadedProgram.File482 ? Advance720290(task, source, morphScratch, projection, colors, frozenSecondary) : Advance718A70(task, source, morphScratch, view, projection, colors, frozenSecondary);
            task = step.Task;
            morphScratch = step.MorphScratch;
            remaining = step.Remaining;
            packets = step.Packets;
            if (phase == 1 && definition.TerminalMode is null && remaining == 0)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
            }
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(new(task, source, morphScratch, packets, remaining, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0), null);
    }

    public static NativeBattleChoco97ProgramDraw Advance720290(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> morphScratch, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary)
    {
        NativeBattleActionMatrix context = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)), unchecked((short)task.ReadWord(0x20)), 4096);
        return NativeBattleChoco97ProgramDrawing.AdvanceWithExplicitContext(task, source, morphScratch, context, projection, colors, frozenSecondary, NativeBattleChocoProgramProfile.Choco99);
    }

    public static (uint Address, int Length, uint Callback, int Clear) LoadedProfile(NativeBattleChoco99LoadedProgram profile)
    {
        if (profile is NativeBattleChoco99LoadedProgram.File475 or NativeBattleChoco99LoadedProgram.File482 or NativeBattleChoco99LoadedProgram.File483 or NativeBattleChoco99LoadedProgram.File484)
        {
            var custom = CustomProfile(profile);
            return (custom.Address, custom.Length, custom.Callback, custom.Clear);
        }
        NativeBattleChocoLoadedProgramProfile selected = Profile(profile);
        return (selected.Address, selected.Length, selected.Callback, selected.Clear);
    }

    private static (uint Address, int Length, uint Callback, int Clear, short? TerminalMode) CustomProfile(NativeBattleChoco99LoadedProgram profile)
    {
        return profile switch
        {
            NativeBattleChoco99LoadedProgram.File475 => (0x19EED20, 0x2220, 0x71FEC0, 0xB0, 8),
            NativeBattleChoco99LoadedProgram.File482 => (0x19DF380, 0x2CD4, 0x7201F0, 0x194, null),
            NativeBattleChoco99LoadedProgram.File483 => (0x19E2054, 0x2724, 0x720320, 0x30, 10),
            NativeBattleChoco99LoadedProgram.File484 => (0x19E4778, 0x1034, 0x720410, 0x1E8, 10),
            _ => throw new NotSupportedException("Native Choco program requires an admitted held or static caller.")
        };
    }

    private static NativeBattleChocoLoadedProgramProfile Profile(NativeBattleChoco99LoadedProgram profile)
    {
        return profile switch
        {
            NativeBattleChoco99LoadedProgram.File471 => new(NativeBattleChocoProgramProfile.Choco99, 0x19E3018, 0x4EE0, 0x71F950, 0x44C, false, -384, 0, true, [new(0, 12), new(3, 12)], null),
            NativeBattleChoco99LoadedProgram.File474 => new(NativeBattleChocoProgramProfile.Choco99, 0x19EB018, 0x3D08, 0x71FDC0, 0x170, false, -1024, -384, false, [new(2, 18)], null),
            _ => throw new NotSupportedException("Native Choco loaded program requires an admitted original caller profile.")
        };
    }
}
