using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8NodeMeshStep(NativeBattleDoom8Frame Frame, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleDoom8NodeDrawing
{
    public const uint ContextPointer = 0x275F92C;
    public const uint CopiedViewAddress = 0x2793E58;

    public static NativeBattleDoom8Frame Begin(NativeBattleDoom8Frame frame, uint meshAddress, short sourceWord3A, short fade, uint? farColor)
    {
        if (frame.State.ReadDword(ContextPointer) != CopiedViewAddress)
        {
            throw new NotSupportedException("Doom mesh requires its actual copied-view scratch pointer before reserving104 bytes.");
        }
        NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(frame.Memory);
        NativeBattleEffectArena context = ReadContext(view);
        context = NativeBattleActorWaveMesh.PrepareContext(context, meshAddress, sourceWord3A, fade, farColor);
        view = NativeBattleEffectNodeContextMemory.Copy(context, 0, view, NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleActorWaveMesh.ContextLength);
        return frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, view), State = frame.State.WriteDword(ContextPointer, NativeBattleActorWaveMesh.ContextAddress) };
    }

    public static NativeBattleDoom8NodeMeshStep Emit(NativeBattleDoom8Frame frame, ImmutableArray<byte> mesh, uint meshAddress, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, short? matrixPadding18)
    {
        using NativeBattleGfSourceReader reader = new(mesh);
        return Emit(frame, reader, meshAddress, matrix, projection, constants, matrixPadding18);
    }

    internal static NativeBattleDoom8NodeMeshStep Emit(NativeBattleDoom8Frame frame, NativeBattleGfSourceReader mesh, uint meshAddress, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, short? matrixPadding18)
    {
        RequireReserved(frame);
        NativeBattleEffectMemory memory = frame.Memory;
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Doom mesh requires its actual COMMON renderer process.");
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Doom mesh requires its current projection controls.");
        NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(memory);
        NativeBattleActorWaveMeshStep emitted = NativeBattleActorWaveMesh.EmitBatch(mesh, meshAddress, matrix, projection, ReadContext(view), new(memory.GeometryRegisters ?? throw new NotSupportedException("Doom mesh requires its canonical geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Doom mesh requires its canonical color registers.")), process.ModelRendererRegisters ?? throw new NotSupportedException("Doom mesh requires its retained FAR controls."), controls.Dqa, controls.Dqb, controls.Zsf4, constants, matrixPadding18, controls.Zsf3, offset => ReadVertexW(mesh, offset));
        view = NativeBattleEffectNodeContextMemory.Copy(emitted.Context104, 0, view, NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleActorWaveMesh.ContextLength);
        memory = NativeBattleEffectNodeContextMemory.Publish(memory, view);
        memory = memory with { GeometryRegisters = emitted.Registers.Geometry, ScriptedColorRegisters = emitted.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = emitted.FarControls } };
        return new(frame with { Memory = memory }, emitted.Batch);
    }

    public static NativeBattleDoom8Frame End(NativeBattleDoom8Frame frame)
    {
        RequireReserved(frame);
        return frame with { State = frame.State.WriteDword(ContextPointer, CopiedViewAddress) };
    }

    private static NativeBattleEffectArena ReadContext(NativeBattleEffectArena view)
    {
        return NativeBattleEffectNodeContextMemory.Copy(view, NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleActorWaveMesh.ContextLength), 0, NativeBattleActorWaveMesh.ContextLength);
    }

    private static short ReadVertexW(NativeBattleGfSourceReader mesh, int offset)
    {
        if (offset < 8 || offset > mesh.Length - 8)
        {
            throw new InvalidDataException("Doom mesh vertex padding leaves its actual selected resource.");
        }
        return mesh.Short(offset + 6);
    }

    private static void RequireReserved(NativeBattleDoom8Frame frame)
    {
        if (frame.State.ReadDword(ContextPointer) != NativeBattleActorWaveMesh.ContextAddress)
        {
            throw new InvalidDataException("Doom mesh lost its actual104-byte shared scratch reservation.");
        }
    }
}
