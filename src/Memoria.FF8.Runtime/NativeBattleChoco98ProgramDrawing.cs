using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleChoco98LoadedProgram
{
    File457,
    File461,
    File462,
    File463
}

public sealed record NativeBattleChoco98ProgramResources(NativeBattleGfAnimationSource ProgramSource, ImmutableArray<NativeBattleChoco98LoadedProgramResources> Loaded);
public sealed record NativeBattleChoco98LoadedProgramResources(NativeBattleChoco98LoadedProgram Profile, NativeBattleGfAnimationSource ProgramSource);
/// <summary>The caller submits Draw.Packets before emitting the optional phase-zero sound.</summary>
public sealed record NativeBattleChoco98LoadedProgramDraw(NativeBattleChoco97LoadedProgramDraw Draw, uint? SoundDescriptor);

public static class NativeBattleChoco98ProgramDrawing
{
    public static NativeBattleModulePoolTask InitializeWorkspace7224C0(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8) => NativeBattleChoco97ProgramDrawing.InitializeProfiled(task, source, actorRaw8, NativeBattleChocoProgramProfile.Choco98);

    public static NativeBattleChoco97ProgramDraw Advance722520(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary, NativeBattlePilotVisualIdentity? pilotIdentity = null) => NativeBattleChoco97ProgramDrawing.AdvanceProfiled(task, source, morphScratch, view, projection, colors, frozenSecondary, NativeBattleChocoProgramProfile.Choco98, pilotIdentity: pilotIdentity);

    public static NativeBattleChoco97NodeDraw Node722640(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null) => NativeBattleChoco97ProgramDrawing.NodeProfiled(source, node, morphScratch, context, projection, colors, NativeBattleChocoProgramProfile.Choco98, pilotIdentity: pilotIdentity);

    public static NativeBattleChoco98LoadedProgramDraw AdvanceLoaded(NativeBattleChoco98LoadedProgram profile, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> targetRaw8, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        var result = NativeBattleChoco97ProgramDrawing.AdvanceLoadedProfiled(Profile98(profile), task, source, actorRaw8, targetRaw8, morphScratch, view, projection, colors, frozenSecondary, pilotIdentity: pilotIdentity);
        return new(result.Draw, result.SoundDescriptor);
    }

    public static (uint Address, int Length, uint Callback, int Clear) LoadedProfile(NativeBattleChoco98LoadedProgram profile)
    {
        NativeBattleChocoLoadedProgramProfile selected = Profile98(profile);
        return (selected.Address, selected.Length, selected.Callback, selected.Clear);
    }

    private static NativeBattleChocoLoadedProgramProfile Profile98(NativeBattleChoco98LoadedProgram profile)
    {
        return profile switch
        {
            NativeBattleChoco98LoadedProgram.File457 => new(NativeBattleChocoProgramProfile.Choco98, 0x19DB018, 0x4EE0, 0x7294C0, 0x44C, false, -384, 0, true, [new(0, 12), new(3, 12)], null),
            NativeBattleChoco98LoadedProgram.File461 => new(NativeBattleChocoProgramProfile.Choco98, 0x19E187C, 0x3D08, 0x7295D0, 0x170, false, -1024, -384, false, [new(2, 18)], null),
            NativeBattleChoco98LoadedProgram.File462 => new(NativeBattleChocoProgramProfile.Choco98, 0x19E5584, 0x38EC, 0x728C60, 0x494, true, -500, 0, true, [], 0x152AF3C),
            NativeBattleChoco98LoadedProgram.File463 => new(NativeBattleChocoProgramProfile.Choco98, 0x19E8E70, 0x532C, 0x728DA0, 0x7D0, true, 0, 0, true, [new(0, 6), new(1, 4), new(2, 6), new(5, 8)], null),
            _ => throw new NotSupportedException("Native Choco loaded program requires an admitted original caller profile.")
        };
    }
}
