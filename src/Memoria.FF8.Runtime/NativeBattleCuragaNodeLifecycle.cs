using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCuragaNodeKinematics
{
    internal static NativeBattleEffectNodeBank CurrentBank(NativeBattleCuragaExecutableState? source)
    {
        NativeBattleCuragaExecutableState retained = source ?? throw new NotSupportedException("Native Curaga node requires its retained current executable bank.");
        retained.Validate();
        return new(retained.ResourceAddress, retained.Data ?? throw new NotSupportedException("Native Curaga node requires its materialized executable bytes."));
    }

    internal static NativeBattleEffectNodeLifecycle CreateLifecycle(NativeBattleCuragaNodeMemory memory, NativeBattleEffectNodeBank bank, Func<int, NativeBattleGraphicsModel>? readModel = null)
    {
        return new(memory, bank, new(memory.State.CopiedCamera, readModel ?? (_ => throw new NotSupportedException("Native node orientation requires its actual model reader.")), (parent, source, target) => NativeBattleCuragaNodes.AllocateEmitter(memory, parent, source, target), (emitter, source) => NativeBattleCuragaNodes.AllocateGeometry(memory, emitter, source)));
    }

    internal static NativeBattleCuragaFrame FlushCurrent(NativeBattleCuragaNodeMemory memory, NativeBattleEffectNodeBank bank)
    {
        NativeBattleCuragaFrame result = memory.Flush();
        NativeBattleCuragaFamilyProfile profile = memory.Profile;
        NativeBattleCuragaExecutableState retained = profile.RequireExecutable(result.Memory);
        return result with { Memory = profile.WithExecutable(result.Memory, retained with { Data = bank.Data }) };
    }

    private static NativeBattleEffectArena WriteWaveWord(NativeBattleEffectArena context, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return context.Write(offset, bytes);
    }

    private static NativeBattleEffectNodePrepareMesh PrepareMeshes(NativeBattleCuragaNodeMemory memory, NativeBattleCameraProjection projection, NativeBattleSummonColors? colors)
    {
        return (node, source) =>
        {
            if (memory.Profile.Effect is 29 or 37)
            {
                uint meshAddress = memory.Dword(node + 0x170);
                short curagaFade = unchecked((short)memory.Word(node + 0x1CE));
                NativeBattleEffectMemory process = memory.CaptureProcessMemory();
                NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(process);
                NativeBattleEffectArena curagaContext = NativeBattleEffectNodeContextMemory.Copy(view, NativeBattleCuraga88Mesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleCuraga88Mesh.ContextLength), 0, NativeBattleCuraga88Mesh.ContextLength);
                curagaContext = NativeBattleCuraga88Mesh.PrepareContext(curagaContext, meshAddress, System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A)), curagaFade, curagaFade != 0 ? memory.Dword(node + 0x16C) : null);
                view = NativeBattleEffectNodeContextMemory.Copy(curagaContext, 0, view, NativeBattleCuraga88Mesh.SharedViewOffset, NativeBattleCuraga88Mesh.ContextLength);
                memory.PublishProcessMemory(NativeBattleEffectNodeContextMemory.Publish(process, view));
                return (ImmutableArray<byte> bytes, NativeBattleActionMatrix matrix) =>
                {
                    NativeBattleEffectMemory current = memory.CaptureProcessMemory();
                    NativeBattleEffectArena currentView = NativeBattleEffectNodeContextMemory.Capture(current);
                    NativeBattleEffectArena currentContext = NativeBattleEffectNodeContextMemory.Copy(currentView, NativeBattleCuraga88Mesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleCuraga88Mesh.ContextLength), 0, NativeBattleCuraga88Mesh.ContextLength);
                    NativeBattlePrimitiveProcess primitive = current.PrimitiveProcess ?? throw new NotSupportedException("Drink Magic mesh requires its retained renderer process.");
                    NativeBattleScriptedGraphicsMemory controls = current.ScriptedGraphics ?? throw new NotSupportedException("Drink Magic mesh requires its retained projection controls.");
                    NativeBattleCuraga88MeshStep emitted = NativeBattleCuraga88Mesh.EmitBatch(bytes, meshAddress, matrix, projection, currentContext, new(current.GeometryRegisters ?? throw new NotSupportedException("Drink Magic mesh requires its retained geometry registers."), current.ScriptedColorRegisters ?? throw new NotSupportedException("Drink Magic mesh requires its retained color registers.")), primitive.ModelRendererRegisters ?? throw new NotSupportedException("Drink Magic mesh requires its retained far controls."), controls.Dqa, controls.Dqb, controls.Zsf4, colors ?? throw new NotSupportedException("Drink Magic mesh requires its original geometry constants."), unchecked((short)memory.Word(node + 0xAC + 18)), zsf3: controls.Zsf3);
                    currentView = NativeBattleEffectNodeContextMemory.Copy(emitted.Context88, 0, currentView, NativeBattleCuraga88Mesh.SharedViewOffset, NativeBattleCuraga88Mesh.ContextLength);
                    current = NativeBattleEffectNodeContextMemory.Publish(current, currentView);
                    memory.PublishProcessMemory(current with { GeometryRegisters = emitted.Registers.Geometry, ScriptedColorRegisters = emitted.Registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = emitted.FarControls } });
                    return emitted.Batch;
                };
            }
            if (memory.Profile.Effect is 19 or 23 or 60 or 73 or 78 or 81 or 82)
            {
                uint meshAddress = memory.Dword(node + 0x170);
                short waveFade = unchecked((short)memory.Word(node + 0x1CE));
                NativeBattleEffectMemory process = memory.CaptureProcessMemory();
                NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(process);
                NativeBattleEffectArena waveContext = NativeBattleEffectNodeContextMemory.Copy(view, NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleActorWaveMesh.ContextLength), 0, NativeBattleActorWaveMesh.ContextLength);
                waveContext = NativeBattleActorWaveMesh.PrepareContext(waveContext, meshAddress, System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A)), waveFade, waveFade != 0 ? memory.Dword(node + 0x16C) : null);
                if (memory.Profile.Effect is 73 or 78)
                {
                    sbyte sourceIndex = unchecked((sbyte)memory.Byte(node + 0x1D6));
                    NativeBattleEsunaMeshOperands prepared = NativeBattleMightyGuardMeshPackets.Prepare(memory.ContextWord(0x28), sourceIndex, System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A)), waveFade, waveFade != 0 ? memory.Dword(node + 0x16C) : null);
                    if (sourceIndex is 4 or 5)
                    {
                        memory.ContextWord(0x28, prepared.NextUvCounter);
                        waveContext = WriteWaveWord(waveContext, 0x18, prepared.Context.UOffset);
                        waveContext = WriteWaveWord(waveContext, 0x28, 128);
                    }
                }
                view = NativeBattleEffectNodeContextMemory.Copy(waveContext, 0, view, NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleActorWaveMesh.ContextLength);
                memory.PublishProcessMemory(NativeBattleEffectNodeContextMemory.Publish(process, view));
                return (ImmutableArray<byte> bytes, NativeBattleActionMatrix matrix) =>
                {
                    NativeBattleEffectMemory current = memory.CaptureProcessMemory();
                    NativeBattleEffectArena currentView = NativeBattleEffectNodeContextMemory.Capture(current);
                    NativeBattleEffectArena currentContext = NativeBattleEffectNodeContextMemory.Copy(currentView, NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleActorWaveMesh.ContextLength), 0, NativeBattleActorWaveMesh.ContextLength);
                    NativeBattlePrimitiveProcess primitive = current.PrimitiveProcess ?? throw new NotSupportedException("The actor wave mesh requires its retained renderer process.");
                    NativeBattleScriptedGraphicsMemory controls = current.ScriptedGraphics ?? throw new NotSupportedException("The actor wave mesh requires its retained projection controls.");
                    NativeBattleActorWaveMeshStep emitted = NativeBattleActorWaveMesh.EmitBatch(bytes, meshAddress, matrix, projection, currentContext, new(current.GeometryRegisters ?? throw new NotSupportedException("The actor wave mesh requires its retained geometry registers."), current.ScriptedColorRegisters ?? throw new NotSupportedException("The actor wave mesh requires its retained color registers.")), primitive.ModelRendererRegisters ?? throw new NotSupportedException("The actor wave mesh requires its retained far controls."), controls.Dqa, controls.Dqb, controls.Zsf4, colors ?? throw new NotSupportedException("The actor wave mesh requires its original geometry constants."), unchecked((short)memory.Word(node + 0xAC + 18)), zsf3: controls.Zsf3);
                    currentView = NativeBattleEffectNodeContextMemory.Copy(emitted.Context104, 0, currentView, NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleActorWaveMesh.ContextLength);
                    current = NativeBattleEffectNodeContextMemory.Publish(current, currentView);
                    memory.PublishProcessMemory(current with { GeometryRegisters = emitted.Registers.Geometry, ScriptedColorRegisters = emitted.Registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = emitted.FarControls } });
                    return emitted.Batch;
                };
            }
            uint flags = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(0x3A)) == 0 ? 0x30u : 0;
            short fade = unchecked((short)memory.Word(node + 0x1CE));
            uint? farColor = null;
            if (fade != 0)
            {
                flags |= 0xC0;
                farColor = memory.Dword(node + 0x16C);
            }
            NativeBattleCuragaMeshContext context = new(flags, farColor, fade == 0 ? null : fade, null, null, null, 17, 2);
            return (ImmutableArray<byte> bytes, NativeBattleActionMatrix matrix) => NativeBattleCuragaMeshPackets.EmitBatch87EEB0(bytes, matrix, projection, context, colors: colors, observeFirstThreeFlags: memory.WriteProjectionFlags);
        };
    }
}
