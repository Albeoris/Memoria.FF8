using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleChoco100LoadedProgram
{
    File492,
    File496,
    File502
}

public sealed record NativeBattleChoco100ProgramFile(NativeBattleChoco100LoadedProgram Profile, NativeBattleGfAnimationSource ProgramSource);
public sealed record NativeBattleChoco100ProgramResources(NativeBattleGfAnimationSource ProgramSource, ImmutableArray<NativeBattleChoco100ProgramFile> Loaded);

public static class NativeBattleChoco100ProgramDrawing
{
    public static NativeBattleModulePoolTask InitializeWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> originRaw8) => NativeBattleChoco97ProgramDrawing.InitializeProfiled(task, source, originRaw8, NativeBattleChocoProgramProfile.Choco100);

    public static NativeBattleChoco97ProgramDraw Advance70F6B0(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary) => NativeBattleChoco97ProgramDrawing.AdvanceProfiled(task, source, morphScratch, view, projection, colors, frozenSecondary, NativeBattleChocoProgramProfile.Choco100);

    public static NativeBattleChoco97NodeDraw Node70F7D0(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short depthBias) => NativeBattleChoco97ProgramDrawing.NodeProfiled(source, node, morphScratch, context, projection, colors, NativeBattleChocoProgramProfile.Choco100, depthBias);

    public static NativeBattleActionMatrix ComposeNodeMatrix(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context) => NativeBattleChoco97ProgramDrawing.ComposeNodeMatrix(node, context, true);

    public static NativeBattleChoco99ProgramDraw AdvanceInitial(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary) => Advance(null, task, source, actorRaw8, actorRaw8, 0, morphScratch, view, projection, colors, frozenSecondary);

    public static NativeBattleChoco99ProgramDraw AdvanceLoaded(NativeBattleChoco100LoadedProgram profile, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> targetRaw8, short contextMode40, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary) => Advance(profile, task, source, actorRaw8, targetRaw8, contextMode40, morphScratch, view, projection, colors, frozenSecondary);

    private static NativeBattleChoco99ProgramDraw Advance(NativeBattleChoco100LoadedProgram? profile, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, ImmutableArray<byte> actorRaw8, ImmutableArray<byte> targetRaw8, short contextMode40, ImmutableArray<byte> morphScratch, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint frozenSecondary)
    {
        var definition = profile is { } loaded ? LoadedProfile(loaded) : (Address: 0x1663154u, Length: 0x2884, Callback: 0x70F5F0u, Clear: 0x4AC);
        if (task.Payload.IsDefault || task.Payload.Length != 0x540 - 12 || task.Callback != definition.Callback || source.Address != definition.Address || source.Data.IsDefault || source.Data.Length != definition.Length || task.ReadDword(0x74) != source.Address || task.ReadDword(0x78) != definition.Clear || morphScratch.IsDefault || morphScratch.Length != 0x120)
        {
            throw new InvalidDataException("Native Choco program requires its actual task, source, clear and morph owners.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        short? terminalMode = profile == NativeBattleChoco100LoadedProgram.File496 ? (short)5 : profile == NativeBattleChoco100LoadedProgram.File502 ? (short)9 : null;
        bool draw;
        switch (phase)
        {
            case 0:
                task = InitializeWorkspace(task, source, profile == NativeBattleChoco100LoadedProgram.File502 ? targetRaw8 : actorRaw8);
                if (profile == NativeBattleChoco100LoadedProgram.File492)
                {
                    task = task.WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) - 384)));
                }
                else if (profile == NativeBattleChoco100LoadedProgram.File496)
                {
                    task = task.WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) - 1024))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) - 384)));
                }
                else if (profile == NativeBattleChoco100LoadedProgram.File502)
                {
                    task = task.WriteWord(0x60, 0x480).WriteDword(0x58, 0x2800).WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + 0xA800))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + 0x400))).WriteWord(0x86, 0xF000);
                }
                draw = profile != NativeBattleChoco100LoadedProgram.File496;
                task = task.WriteByte(0x29, 1);
                break;
            case 1:
                draw = terminalMode is null || contextMode40 < terminalMode.Value;
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
        NativeBattleGfGeometryBatch packets = new([], 0);
        int? remaining = null;
        if (draw)
        {
            NativeBattleChoco97ProgramDraw step = Advance70F6B0(task, source, morphScratch, view, projection, colors, frozenSecondary);
            task = step.Task;
            morphScratch = step.MorphScratch;
            packets = step.Packets;
            remaining = step.Remaining;
            if (phase == 1 && terminalMode is null && remaining == 0)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
            }
        }
        foreach ((int geometry, int delta) in profile == NativeBattleChoco100LoadedProgram.File492 ? new[] { (0, 12), (3, 12) } : profile == NativeBattleChoco100LoadedProgram.File496 ? new[] { (2, 18) } : Array.Empty<(int, int)>())
        {
            int offset = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + geometry * 4));
            source = source with { Data = NativeBattleChoco97Uv.Scroll7311C0(source.Data, offset / 4 * 4, delta) };
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(new(task, source, morphScratch, packets, remaining, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0), profile is null && phase == 0 ? 0x15297F4u : null);
    }

    public static (uint Address, int Length, uint Callback, int Clear) LoadedProfile(NativeBattleChoco100LoadedProgram profile) => profile switch
    {
        NativeBattleChoco100LoadedProgram.File492 => (0x19EF018, 0x4EE0, 0x7163D0, 0x44C),
        NativeBattleChoco100LoadedProgram.File496 => (0x19E4970, 0x3D08, 0x716840, 0x170),
        NativeBattleChoco100LoadedProgram.File502 => (0x19DC678, 0x638C, 0x716950, 0x32C),
        _ => throw new NotSupportedException("Native Choco program requires an admitted original loaded caller.")
    };
}
