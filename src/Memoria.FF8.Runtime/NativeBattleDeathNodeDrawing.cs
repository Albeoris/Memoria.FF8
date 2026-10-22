using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDeathNodeMeshStep(NativeBattleDeathFrame Frame, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleDeathNodeDrawing
{
    private const uint ContextPointer = 0x2742864;
    public const uint CopiedViewAddress = 0x2793E58;

    public static NativeBattleDeathFrame PublishCamera(NativeBattleDeathFrame frame, NativeBattlePackedMatrix32 camera)
    {
        camera.Validate();
        frame = frame with { Memory = frame.Memory with { SharedEffectView2793E58 = camera } };
        frame = frame with { Memory = WritePointer(frame, CopiedViewAddress) };
        NativeBattleDeathFamilyProfile profile = Profile(frame);
        NativeBattleDeathState state = profile.RequireState(frame.Memory);
        state = state with { Module = state.Module.Write(profile.ModuleOffset(0x273AFE8), BitConverter.GetBytes(CopiedViewAddress)) };
        return frame with { Memory = profile.WithState(frame.Memory, state) };
    }

    private static NativeBattleDeathFamilyProfile Profile(NativeBattleDeathFrame frame) => NativeBattleDeathFamilyProfile.ForEffect(frame.Effect.Binding.Effect);

    private static uint ReadPointer(NativeBattleDeathFrame frame)
    {
        NativeBattleDeathFamilyProfile profile = Profile(frame);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(profile.RequireState(frame.Memory).Module.Read(profile.ModuleOffset(ContextPointer), 4));
    }

    private static NativeBattleEffectMemory WritePointer(NativeBattleDeathFrame frame, uint value)
    {
        NativeBattleDeathFamilyProfile profile = Profile(frame);
        NativeBattleDeathState state = profile.RequireState(frame.Memory);
        return profile.WithState(frame.Memory, state with { Module = state.Module.Write(profile.ModuleOffset(ContextPointer), BitConverter.GetBytes(value)) });
    }

    public static NativeBattleDeathFrame Begin(NativeBattleDeathFrame frame, uint meshAddress, short sourceWord3A, short fade, uint? farColor)
    {
        if (ReadPointer(frame) != CopiedViewAddress)
        {
            throw new NotSupportedException("Death mesh requires its actual copied-view scratch pointer before reserving104 bytes.");
        }
        frame = frame with { Memory = WritePointer(frame, NativeBattleActorWaveMesh.ContextAddress) };
        NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(frame.Memory);
        NativeBattleEffectArena context = ReadContext(view);
        context = NativeBattleActorWaveMesh.PrepareContext(context, meshAddress, sourceWord3A, fade, farColor);
        view = NativeBattleEffectNodeContextMemory.Copy(context, 0, view, NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleActorWaveMesh.ContextLength);
        return frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, view) };
    }

    public static NativeBattleDeathNodeMeshStep Emit(NativeBattleDeathFrame frame, ImmutableArray<byte> mesh, uint meshAddress, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, short? matrixPadding18)
    {
        RequireReserved(frame);
        NativeBattleEffectMemory memory = frame.Memory;
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Death mesh requires its actual COMMON renderer process.");
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Death mesh requires its current projection controls.");
        NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(memory);
        NativeBattleActorWaveMeshStep emitted = NativeBattleActorWaveMesh.EmitBatch(mesh, meshAddress, matrix, projection, ReadContext(view), new(memory.GeometryRegisters ?? throw new NotSupportedException("Death mesh requires its canonical geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Death mesh requires its canonical color registers.")), process.ModelRendererRegisters ?? throw new NotSupportedException("Death mesh requires its retained FAR controls."), controls.Dqa, controls.Dqb, controls.Zsf4, constants, matrixPadding18, controls.Zsf3, offset => ReadVertexW(mesh, offset));
        view = NativeBattleEffectNodeContextMemory.Copy(emitted.Context104, 0, view, NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleActorWaveMesh.ContextLength);
        memory = NativeBattleEffectNodeContextMemory.Publish(memory, view);
        memory = memory with { GeometryRegisters = emitted.Registers.Geometry, ScriptedColorRegisters = emitted.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = emitted.FarControls } };
        return new(frame with { Memory = memory }, emitted.Batch);
    }

    public static NativeBattleDeathFrame End(NativeBattleDeathFrame frame)
    {
        RequireReserved(frame);
        return frame with { Memory = WritePointer(frame, CopiedViewAddress) };
    }

    private static NativeBattleEffectArena ReadContext(NativeBattleEffectArena view)
    {
        return NativeBattleEffectNodeContextMemory.Copy(view, NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleActorWaveMesh.ContextLength), 0, NativeBattleActorWaveMesh.ContextLength);
    }

    private static short ReadVertexW(ImmutableArray<byte> mesh, int offset)
    {
        if (offset < 8 || offset > mesh.Length - 8)
        {
            throw new InvalidDataException("Death mesh vertex padding leaves its actual selected resource.");
        }
        return System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(mesh.AsSpan().Slice(offset + 6, 2));
    }

    private static void RequireReserved(NativeBattleDeathFrame frame)
    {
        if (ReadPointer(frame) != NativeBattleActorWaveMesh.ContextAddress)
        {
            throw new InvalidDataException("Death mesh lost its actual104-byte shared scratch reservation.");
        }
    }
}
