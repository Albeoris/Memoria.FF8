using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMagmaBreath48DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal = 0x26481B8, ushort Flags = 0) : NativeBattleMagmaBreath48Operation;

public static partial class NativeBattleMagmaBreath48Controller
{
    private delegate NativeBattleGfGeometryBatch Mesh48Instance(NativeBattleGfSourceReader resource, NativeBattleActionMatrix matrix, NativeBattlePilotVisualIdentity? identity);

    private static Mesh48Instance PrepareMesh48(NativeBattleEffectNodeMemory memory, int node, byte[] source, Func<NativeBattleMagmaBreath48Frame> readFrame, Action<NativeBattleMagmaBreath48Frame> writeFrame, NativeBattleMagmaBreath48Inputs inputs)
    {
        NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
        NativeBattleMagmaBreath48Frame frame = readFrame() with { Memory = written.Memory, Effect = written.Effect };
        uint resource = memory.Dword(node + 0x170);
        if (resource is not (0x15AE4FC or 0x15B0514))
        {
            throw new NotSupportedException("Magma Breath mesh drawing requires its actual current mutable geometry.");
        }
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectNodeContextMemory.Capture(frame.Memory), 92, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
        uint flags = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A)) == 0 ? 0x30u : 0;
        short fade = unchecked((short)memory.Word(node + 0x1CE));
        context = NativeBattleStaticMeshContext88.Put(context, 0, resource);
        if (fade != 0)
        {
            flags |= 0xC0;
            context = NativeBattleStaticMeshContext88.Put(NativeBattleStaticMeshContext88.Put(context, 8, memory.Dword(node + 0x16C)), 12, unchecked((uint)(int)fade));
        }
        context = NativeBattleStaticMeshContext88.Put(context, 0x1C, flags);
        frame = PublishContext(frame, temporary, context, 88);
        int count = unchecked((sbyte)memory.Byte(node + 0x1D8));
        int instance = 0;
        if (count <= 0)
        {
            frame = Release(frame, 88);
        }
        memory.PublishProcessMemory(frame.Memory);
        writeFrame(frame);
        return (_, _, identity) =>
        {
            NativeBattleEffectNodeMemoryWrite current = memory.FlushStorage();
            frame = readFrame() with { Memory = current.Memory, Effect = current.Effect };
            NativeBattleEffectArena packedView = ReadBytes(frame, checked(memory.Pointer(node) + 0xAC), 32);
            (byte[] bytes, bool[] known) = packedView.ReadSnapshot(0, 32);
            frame = LoadMatrix(frame, new(bytes.ToImmutableArray(), known.ToImmutableArray()));
            frame = inputs.Execute(frame, new NativeBattleMagmaBreath48DrawMesh88(temporary, resource) { PilotIdentity = identity });
            instance++;
            if (instance == count)
            {
                frame = Release(frame, 88);
            }
            memory.PublishProcessMemory(frame.Memory);
            writeFrame(frame);
            return new([], 0);
        };
    }

}
