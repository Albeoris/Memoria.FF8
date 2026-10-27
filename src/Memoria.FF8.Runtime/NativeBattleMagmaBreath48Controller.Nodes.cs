using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMagmaBreath48Controller
{
    public static (NativeBattleMagmaBreath48Frame Frame, int Result) AdvanceEngine825E90(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        frame = Dword(frame, 0x26495E8, checked(address + 0x34));
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 0x58) == 2)
        {
            throw new NotSupportedException("Magma Breath placement mode2 requires its observed prior stack vector.");
        }
        int actor = Signed(task, 0x52);
        NativeBattleMagmaBreath48PointStep first = inputs.ReadBone502170(frame, actor, 0x15, 0);
        frame = first.Frame;
        frame = WriteMouth(frame, address, 0x1F4, first.Point8, false);
        NativeBattleMagmaBreath48PointStep second = inputs.ReadBone502170(frame, actor, 0x15, 0);
        frame = WriteMouth(second.Frame, address, 0x204, second.Point8, true);
        var result = NativeBattleEffectNodeLifecycle.AdvanceContext(() => Read(frame, address), value => frame = Write(frame, address, value), AdvanceNodes);
        frame = Word(Word(frame, 0x26495D2, unchecked(frame.State.ReadWord(0x26495D2) + result.EmitterCount)), 0x26495C2, unchecked(frame.State.ReadWord(0x26495C2) + result.GeometryCount));
        return (frame, result.Complete ? 1 : 0);

        void AdvanceNodes()
        {
            int emitters = checked((int)(frame.State.ReadDword(EmitterPointerGlobal) - NativeBattleEffectNodeMemory.ArenaAddress));
            int geometry = checked((int)(frame.State.ReadDword(GeometryPointerGlobal) - NativeBattleEffectNodeMemory.ArenaAddress));
            NativeBattleTheEnd85SpawnScratchObserver spawnScratch = new(NativeBattleEffectNodeContextMemory.Capture(frame.Memory));
            using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, emitters, EmitterStorageLength, geometry, GeometryStorageLength, useSharedScratch: true) { SpawnScratchObserver = spawnScratch };
            NativeBattleEffectNodeBank bank = new(SourceAddress, frame.State.Read(SourceAddress, SourceLength).ToImmutableArray(), ReadExternal, ReadResource);
            ushort emitterCursor = frame.State.ReadWord(EmitterScanCursorGlobal);
            ushort geometryCursor = frame.State.ReadWord(GeometryScanCursorGlobal);
            NativeBattleEffectNodeInputs nodeInputs = new(inputs.ReadView(frame).ReadMatrix(), actorIndex => inputs.ReadModel(frame, actorIndex), (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, emitters, 20, ref emitterCursor, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, geometry, 100, ref geometryCursor, emitter, source))
            {
                MorphResource = (mesh, firstShape, secondShape, ratio) => frame = MorphResource(frame, resources, mesh, firstShape, secondShape, ratio),
                ResourceUpdate = (currentBank, resource, kind, sourceIndex) =>
                {
                    if (sourceIndex == 0 && kind is 4 or 5)
                    {
                        frame = ScrollResource(frame, resources, resource);
                    }
                    return currentBank.Data;
                }
            };
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
            NativeCrtRandomState random = frame.Random;
            lifecycle.Birth();
            lifecycle.AdvanceList(ref random);
            NativeBattleEffectNodeMemoryWrite spawned = memory.FlushStorage();
            frame = frame with { Memory = spawned.Memory, Effect = spawned.Effect };
            NativeBattleEffectArena currentScratch = NativeBattleEffectNodeContextMemory.Copy(spawnScratch.View, 100, NativeBattleEffectNodeContextMemory.Capture(spawned.Memory), 100, 80);
            memory.PublishProcessMemory(NativeBattleEffectNodeContextMemory.Publish(spawned.Memory, currentScratch));
            _ = lifecycle.DrawNodes(inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), (node, source) => PrepareMesh48(memory, node, source, () => frame, value => frame = value, inputs), (meshes, batch) =>
            {
                if (!meshes)
                {
                    NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
                    frame = frame with { Memory = written.Memory, Effect = written.Effect };
                    (frame, _) = Allocate(frame, 180);
                    frame = inputs.Execute(frame, new NativeBattleMagmaBreath48DeliverBatch(batch));
                    frame = Release(frame, 180);
                    memory.PublishProcessMemory(frame.Memory);
                }
            });
            NativeBattleEffectNodeMemoryWrite final = memory.FlushStorage();
            frame = frame with { Memory = final.Memory, Effect = final.Effect, Random = random, State = frame.State.WriteWord(EmitterScanCursorGlobal, emitterCursor).WriteWord(GeometryScanCursorGlobal, geometryCursor).Write(SourceAddress, bank.Data.AsSpan()) };

            byte[] ReadExternal(uint pointer, int length)
            {
                if (pointer >= ChannelBase || pointer >= MeshAAddress && (ulong)pointer + (uint)length <= (ulong)MeshAAddress + MutablePeLength)
                {
                    return frame.State.Read(pointer, length);
                }
                return resources.ReadStatic(pointer, length).ToArray();
            }
            ImmutableArray<byte> ReadResource(uint pointer)
            {
                if (pointer is MeshAAddress or MeshBAddress)
                {
                    return resources.ReadMesh(pointer).Bytes;
                }
                return resources.ReadResource(pointer);
            }
        }
    }

    private static NativeBattleMagmaBreath48Frame WriteMouth(NativeBattleMagmaBreath48Frame frame, uint address, int offset, NativeBattleEffectArena point, bool flattenY)
    {
        byte[] bytes = point.Read(0, 6);
        short x = BinaryPrimitives.ReadInt16LittleEndian(bytes);
        short y = flattenY ? (short)0 : BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2));
        short z = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)) + 0x100));
        NativeBattleModulePoolTask task = Read(frame, address).WriteDword(0x34 + offset, unchecked((uint)(x << 16))).WriteDword(0x38 + offset, unchecked((uint)(y << 16))).WriteDword(0x3C + offset, unchecked((uint)(z << 16)));
        return Write(frame, address, task);
    }

    internal static NativeBattleMagmaBreath48Frame MorphResource(NativeBattleMagmaBreath48Frame frame, NativeBattleMagmaBreath48Resources resources, uint mesh, uint firstShape, uint secondShape, short ratio)
    {
        if (firstShape == 0 || secondShape == 0)
        {
            return frame;
        }
        _ = resources.ReadMesh(mesh);
        int count = unchecked((int)frame.State.ReadDword(checked(mesh + 4)));
        if (count <= 0)
        {
            return frame;
        }
        ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices45E9D0(resources.ReadResource(firstShape), 8, resources.ReadResource(secondShape), 8, count, ratio);
        for (int index = 0; index < vertices.Length; index++)
        {
            frame = frame with { State = frame.State.WriteView(checked(mesh + 8 + (uint)index * 8), PointView(vertices[index]), 6) };
        }
        return frame;
    }

    internal static NativeBattleMagmaBreath48Frame ScrollResource(NativeBattleMagmaBreath48Frame frame, NativeBattleMagmaBreath48Resources resources, uint mesh)
    {
        NativeBattleStaticMeshResource seed = resources.ReadMesh(mesh);
        NativeBattleEffectArena current = frame.State.ReadView(mesh, seed.Bytes.Length);
        (byte[] bytes, _) = current.ReadSnapshot(0, seed.Bytes.Length);
        ImmutableArray<byte> scrolled = NativeBattleChoco97Uv.Scroll7311C0(bytes.ToImmutableArray(), -8);
        int directory = BinaryPrimitives.ReadInt32LittleEndian(current.Read(0, 4));
        int cursor = directory / 4 * 4;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(current.Read(cursor, 4));
            cursor = checked(cursor + 4);
            int[] corners = group is 2 or 6 ? [11, 13, 17] : group is 3 or 7 ? [13, 17, 21, 23] : [];
            for (int face = 0; face < count; face++)
            {
                foreach (int lane in corners)
                {
                    _ = current.Read(cursor + lane, 1);
                    frame = frame with { State = frame.State.Write(checked(mesh + (uint)(cursor + lane)), scrolled.AsSpan().Slice(cursor + lane, 1)) };
                }
                cursor = checked(cursor + strides[group]);
            }
        }
        return frame;
    }
}
