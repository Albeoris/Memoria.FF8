namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleEffect218Frame DrawWindowed218(NativeBattleEffect218Frame frame, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleEffectArena, NativeBattleEffectArena> prepare, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver, byte depthShift = 2)
    {
        (frame, uint temporary) = Allocate(frame, 144);
        NativeBattleEffectArena context = prepare(Context(frame, temporary, 144));
        NativeBattleWindowedMesh144Step drawn = NativeBattleWindowedMesh144.DrawLoaded(frame.Memory, mesh, projection, context, colors, depthShift: depthShift, groupMask: (1 << 2) | (1 << 3) | (1 << 6) | (1 << 7), pilotIdentity: NativeBattleEffect218Presentation.Resource(frame, mesh.Address));
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context144, 144);
        frame = deliver(frame, drawn.Batch);
        return Release(frame, 144);
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceTargetWindow633720(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleStaticMeshResource mesh, NativeBattlePackedMatrix32 currentView, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        if (mesh.Address != 0xE22570)
        {
            throw new InvalidDataException("Effect218 target window requires its actual selected mesh.");
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, 2048, 0));
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(rotation, 3072, 7680, 6144) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(currentView.ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 144);
        short age = Signed(task, 12);
        NativeBattleEffectArena context = Context(frame, temporary, 144).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[4]).Write(0x10, new byte[4]).Write(0x14, BitConverter.GetBytes(age * 24)).Write(0x18, BitConverter.GetBytes((short)64)).Write(0x1A, BitConverter.GetBytes((short)128)).Write(0x1C, BitConverter.GetBytes((short)64)).Write(0x1E, BitConverter.GetBytes((short)128)).Write(0x20, BitConverter.GetBytes(0x33u));
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect218 target window requires its current FAR registers.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Effect218 target window requires its actual renderer controls.")).PublishFarColor45DDA0(16, 16, 144);
        uint cue = unchecked((uint)(age * 4096 / 18));
        NativeBattleScriptedDpcsResult shade = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown(), 0x00808080, cue, far, colors);
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = far }, ScriptedColorRegisters = shade.Registers } };
        context = context.Write(0x24, BitConverter.GetBytes(shade.PackedRgb));
        NativeBattleWindowedMesh144Step drawn = NativeBattleWindowedMesh144.DrawLoaded(frame.Memory, mesh, projection, context, colors, groupMask: (1 << 2) | (1 << 3) | (1 << 6) | (1 << 7), pilotIdentity: NativeBattleEffect218Presentation.Resource(frame, mesh.Address));
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context144, 144);
        frame = deliver(frame, drawn.Batch);
        frame = Release(frame, 144);
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 18 ? 2 : 0);
    }
}
