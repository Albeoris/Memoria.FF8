namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect88MeshPreparation
{
    internal static NativeBattleEffectNodeMeshInstance Prepare(NativeBattleEffectNodeMemory memory, int node, byte[] source, NativeBattleCameraProjection projection, NativeBattleSummonColors? colors)
    {
        NativeBattleEffectNodePilotMeshInstance prepared = PreparePilot(memory, node, source, projection, colors, false);
        return (bytes, matrix) => prepared(bytes, matrix, default);
    }

    internal static NativeBattleEffectNodePilotMeshInstance PreparePilot(NativeBattleEffectNodeMemory memory, int node, byte[] source, NativeBattleCameraProjection projection, NativeBattleSummonColors? colors, bool captureVisual = true)
    {
        uint meshAddress = memory.Dword(node + 0x170);
        short curagaFade = unchecked((short)memory.Word(node + 0x1CE));
        NativeBattleEffectMemory process = memory.CaptureProcessMemory();
        NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(process);
        NativeBattleEffectArena curagaContext = NativeBattleEffectNodeContextMemory.Copy(view, NativeBattleCuraga88Mesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleCuraga88Mesh.ContextLength), 0, NativeBattleCuraga88Mesh.ContextLength);
        curagaContext = NativeBattleCuraga88Mesh.PrepareContext(curagaContext, meshAddress, System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A)), curagaFade, curagaFade != 0 ? memory.Dword(node + 0x16C) : null);
        view = NativeBattleEffectNodeContextMemory.Copy(curagaContext, 0, view, NativeBattleCuraga88Mesh.SharedViewOffset, NativeBattleCuraga88Mesh.ContextLength);
        memory.PublishProcessMemory(NativeBattleEffectNodeContextMemory.Publish(process, view));
        return (NativeBattleGfSourceReader bytes, NativeBattleActionMatrix matrix, NativeBattlePilotVisualIdentity identity) =>
        {
            NativeBattleEffectMemory current = memory.CaptureProcessMemory();
            NativeBattleEffectArena currentView = NativeBattleEffectNodeContextMemory.Capture(current);
            NativeBattleEffectArena currentContext = NativeBattleEffectNodeContextMemory.Copy(currentView, NativeBattleCuraga88Mesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleCuraga88Mesh.ContextLength), 0, NativeBattleCuraga88Mesh.ContextLength);
            NativeBattlePrimitiveProcess primitive = current.PrimitiveProcess ?? throw new NotSupportedException("Drink Magic mesh requires its retained renderer process.");
            NativeBattleScriptedGraphicsMemory controls = current.ScriptedGraphics ?? throw new NotSupportedException("Drink Magic mesh requires its retained projection controls.");
            NativeBattleCuraga88MeshStep emitted = NativeBattleCuraga88Mesh.EmitBatch(bytes, meshAddress, matrix, projection, currentContext, new(current.GeometryRegisters ?? throw new NotSupportedException("Drink Magic mesh requires its retained geometry registers."), current.ScriptedColorRegisters ?? throw new NotSupportedException("Drink Magic mesh requires its retained color registers.")), primitive.ModelRendererRegisters ?? throw new NotSupportedException("Drink Magic mesh requires its retained far controls."), controls.Dqa, controls.Dqb, controls.Zsf4, colors ?? throw new NotSupportedException("Drink Magic mesh requires its original geometry constants."), unchecked((short)memory.Word(node + 0xAC + 18)), zsf3: controls.Zsf3, pilotIdentity: captureVisual ? identity : null);
            currentView = NativeBattleEffectNodeContextMemory.Copy(emitted.Context88, 0, currentView, NativeBattleCuraga88Mesh.SharedViewOffset, NativeBattleCuraga88Mesh.ContextLength);
            current = NativeBattleEffectNodeContextMemory.Publish(current, currentView);
            memory.PublishProcessMemory(current with { GeometryRegisters = emitted.Registers.Geometry, ScriptedColorRegisters = emitted.Registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = emitted.FarControls } });
            return emitted.Batch;
        };
    }
}
