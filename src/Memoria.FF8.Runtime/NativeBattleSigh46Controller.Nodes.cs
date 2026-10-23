using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSigh46Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSigh46Controller
{
    public static (NativeBattleSigh46Frame Frame, int Result) AdvanceEngine832BE0(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        frame = Dword(frame, 0x26527B0, checked(address + 0x34));
        var result = NativeBattleEffectNodeLifecycle.AdvanceContext(() => Read(frame, address), value => frame = Write(frame, address, value), AdvanceNodes);
        frame = Word(Word(frame, 0x265279A, unchecked(frame.State.ReadWord(0x265279A) + result.EmitterCount)), 0x265278A, unchecked(frame.State.ReadWord(0x265278A) + result.GeometryCount));
        return (frame, result.Complete ? 1 : 0);

        void AdvanceNodes()
        {
            int emitters = checked((int)(frame.State.ReadDword(EmitterPointerGlobal) - NativeBattleEffectNodeMemory.ArenaAddress));
            int geometry = checked((int)(frame.State.ReadDword(GeometryPointerGlobal) - NativeBattleEffectNodeMemory.ArenaAddress));
            NativeBattleTheEnd85SpawnScratchObserver spawnScratch = new(NativeBattleEffectNodeContextMemory.Capture(frame.Memory));
            NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, emitters, EmitterStorageLength, geometry, GeometryStorageLength, useSharedScratch: true) { SpawnScratchObserver = spawnScratch };
            NativeBattleEffectNodeBank bank = new(SourceAddress, frame.State.Read(SourceAddress, SourceLength).ToImmutableArray(), ReadExternal, ReadResource);
            ushort emitterCursor = frame.State.ReadWord(EmitterScanCursorGlobal);
            ushort geometryCursor = frame.State.ReadWord(GeometryScanCursorGlobal);
            NativeBattleEffectNodeInputs nodeInputs = new(inputs.ReadView(frame).ReadMatrix(), actorIndex => inputs.ReadModel(frame, actorIndex), (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, emitters, 20, ref emitterCursor, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, geometry, 100, ref geometryCursor, emitter, source));
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
            NativeCrtRandomState random = frame.Random;
            lifecycle.Birth();
            lifecycle.AdvanceList(ref random);
            NativeBattleEffectNodeMemoryWrite spawned = memory.FlushStorage();
            frame = frame with { Memory = spawned.Memory, Effect = spawned.Effect };
            NativeBattleEffectArena currentScratch = NativeBattleEffectNodeContextMemory.Copy(spawnScratch.View, 100, NativeBattleEffectNodeContextMemory.Capture(spawned.Memory), 100, 80);
            memory.PublishProcessMemory(NativeBattleEffectNodeContextMemory.Publish(spawned.Memory, currentScratch));
            _ = lifecycle.DrawNodes(inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), (_, _) => throw new NotSupportedException("Sigh generic nodes have no original geometry directory."), (meshes, batch) =>
            {
                if (!meshes)
                {
                    NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
                    frame = frame with { Memory = written.Memory, Effect = written.Effect };
                    (frame, _) = AllocateNodeScratch(frame, 180);
                    frame = inputs.Execute(frame, new NativeBattleSigh46DeliverBatch(batch));
                    frame = ReleaseNodeScratch(frame, 180);
                    memory.PublishProcessMemory(frame.Memory);
                }
            });
            NativeBattleEffectNodeMemoryWrite final = memory.FlushStorage();
            frame = frame with { Memory = final.Memory, Effect = final.Effect, Random = random, State = frame.State.WriteWord(EmitterScanCursorGlobal, emitterCursor).WriteWord(GeometryScanCursorGlobal, geometryCursor).Write(SourceAddress, bank.Data.AsSpan()) };

            byte[] ReadExternal(uint pointer, int length)
            {
                if (pointer >= ChannelBase)
                {
                    return frame.State.Read(pointer, length);
                }
                return resources.ReadStatic(pointer, length).ToArray();
            }
            ImmutableArray<byte> ReadResource(uint pointer)
            {
                return resources.ReadResource(pointer);
            }
        }
    }

}
