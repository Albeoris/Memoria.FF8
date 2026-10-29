using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect210Controller
{
    private static NativeBattleEffect210Frame RunProgramme(NativeBattleEffect210Frame frame, uint main, int length, NativeBattleVector position, uint selectors, short depth, short? phase, short? secondPhase, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = position.X, Y = position.Y, Z = position.Z };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, 0)).Write(0x20, BitConverter.GetBytes(selectors)).Write(0x24, BitConverter.GetBytes(depth)).Write(0x2C, BitConverter.GetBytes(frame.State.ReadDword(ArenaGlobal) + 0xAB10));
        caller = phase is short first ? caller.Write(0x26, BitConverter.GetBytes(first)) : caller.MarkUnknown(0x26, 2);
        caller = secondPhase is short second ? caller.Write(0x28, BitConverter.GetBytes(second)) : caller.MarkUnknown(0x28, 2);
        return NativeBattleEffect210Programme.Advance(frame, main + 0x168, length, caller, resources, inputs, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(210, Read(frame, main))).Frame;
    }

    private static NativeBattleEffect210Frame DrawEarlyMesh(NativeBattleEffect210Frame frame, uint main, int caster, int phase, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 128);
        NativeBattleEffect210PointStep point = inputs.Point502170(frame, caster, 0x25, 0, Context(frame, temporary + 0x78, 8));
        frame = point.Frame;
        frame = PublishContext(frame, temporary + 0x78, point.Output8, 8);
        NativeBattleVector vector = Vector(point.Output8);
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(phase << 8);
        local = local with { Rotation = local.Rotation.SetItem(4, unchecked((short)-vector.Y)), X = vector.X, Y = 0, Z = vector.Z };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = PublishContext(frame, temporary + 0x58, PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, 0)), 32);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, 0));
        NativeBattleEffectArena context = Context(frame, temporary, 128).Write(0, BitConverter.GetBytes(0x145D6ACu)).Write(8, BitConverter.GetBytes(0u)).Write(12, BitConverter.GetBytes(phase << 9)).Write(28, BitConverter.GetBytes(0xF0u));
        frame = PublishContext(frame, temporary, context, 128);
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(0x145D6AC), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(210, Read(frame, main), 0xD6AC) with { ResourceGeneration = 0x145D6AC });
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, 128);
        frame = inputs.Execute(frame, new NativeBattleEffect210DrawBatch(drawn.Batch, CursorGlobal));
        return Release(frame, 128);
    }
}
