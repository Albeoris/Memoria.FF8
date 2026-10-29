using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect173Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect173Lower
{
    private const uint L173Cursor = 0x239A8CC;
    private const uint L173Records = 0x23972E0;

    public static NativeBattleEffect173Step Advance(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs) => Read(frame, address).Callback switch
    {
        0x5D7BA0 => AdvanceOrbit(frame, address, resources, inputs),
        0x5D7E70 or 0x5D7FE0 => AdvanceStatic(frame, address, resources, inputs),
        0x5D80C0 => AdvanceCloud(frame, address, resources, inputs),
        0x5D83E0 => AdvanceShards(frame, address, resources, inputs),
        0x5D8B40 => AdvanceRays(frame, address, resources, inputs),
        0x5D8E20 => AdvanceEmitter(frame, address, resources, inputs),
        0x5D90B0 => NativeBattleEffect173Programme.Advance90B0(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect173 requires its selected lower callback.")
    };

    private static NativeBattleEffect173Step AdvanceShards(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int index = Signed(task, 14);
        if ((uint)index >= (uint)resources.MeshSources.Length)
        {
            throw new InvalidDataException("Shard drawing leaves its selected current bank directory.");
        }
        int age = Signed(task, 12);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)), Signed(task, 0x1C), Signed(task, 0x1C), Signed(task, 0x1C)) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null);
        (frame, uint temporary) = Allocate(frame, 172);
        uint source = resources.MeshSources[index];
        NativeBattleStaticMeshResource mesh = resources.ReadMesh(source);
        int polygon = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(polygon + 24, 4));
        uint recordAddress = resources.FaceRecordAddresses[index];
        NativeBattleEffectArena context = Context(frame, temporary, 172);
        context = L173Put(L173Put(L173Put(L173Put(context, 0, source), 8, 0), 0x1C, 0x30), 0x20, unchecked((uint)(age >= 10 ? age - 10 : age)));
        context = L173Put(context, 0x24, unchecked((uint)index));
        context = CopyBytes(PackedArena(composed), 0, context, 0x6C, 32);
        NativeBattleEffect173Mesh172Step drawn = NativeBattleEffect173Mesh172.DrawLoaded(frame.Memory, mesh, context, frame.State.ReadView(recordAddress, count * 32), recordAddress, inputs.ReadProjection(frame), inputs.Colors, age >= 10, L173Paused(frame, inputs), pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(173, task) with { ResourceGeneration = source });
        context = L173Put(drawn.Context172, 0x24, drawn.Batch.AllocationBytes != 0 ? 1u : 0u);
        frame = frame with { Memory = drawn.Memory, State = frame.State.WriteView(recordAddress, drawn.Records, count * 32) };
        frame = PublishContext(frame, temporary, context, 172);
        frame = inputs.Execute(frame, new NativeBattleEffect173DrawBatch(drawn.Batch, L173Cursor));
        frame = Release(frame, 172);
        if (L173Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next > 10 && L173U(context, 0x24) == 0 ? 2 : 0);
    }

    private static NativeBattleEffect173Step AdvanceStatic(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool first = task.Callback == 0x5D7E70;
        if (first && Signed(task, 14) > 0)
        {
            if (!L173Paused(frame, inputs))
            {
                frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(Signed(task, 14) - 1))));
            }
            return new(frame, 0);
        }
        int age = Signed(task, 12);
        int scale = Signed(task, 0x1C);
        if (first)
        {
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), Signed(task, 0x1A))) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        }
        else
        {
            frame = L173Setup(frame, frame.State.ReadView(0x2398AA0, 8), unchecked((short)scale), unchecked((short)-(scale >> 3)), inputs);
        }
        (frame, uint temporary) = Allocate(frame, 88);
        uint source = first ? 0xD5ED20u : 0xD5F710u;
        int threshold = first ? 12 : 32;
        NativeBattleEffectArena context = L173Put(L173Put(L173Put(Context(frame, temporary, 88), 0, source), 8, 0), 0x1C, age < threshold ? 0x33u : 0xF3u);
        if (age >= threshold)
        {
            context = L173Put(context, 12, unchecked((uint)((age - threshold) << 10)));
        }
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(173, task) with { ResourceGeneration = source });
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect173DrawBatch(drawn.Batch, L173Cursor));
        frame = Release(frame, 88);
        if (L173Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        int velocity = Signed(task, 0x1E);
        task = task.WriteWord(0x1C, unchecked((ushort)(scale + (first ? -velocity : velocity))));
        if (first)
        {
            task = task.WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 20)));
        }
        else if (age >= 24)
        {
            int value = unchecked((short)(velocity + 32));
            task = task.WriteWord(0x1E, unchecked((ushort)(value + value / 3)));
        }
        short next = unchecked((short)(age + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next >= (first ? 16 : 36) ? 2 : 0);
    }

    private static NativeBattleEffect173Frame L173Setup(NativeBattleEffect173Frame frame, NativeBattleEffectArena point, short scale, short depth, NativeBattleEffect173Inputs inputs)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), L173Vector(point, 0), w, scale, depth) };
    }
    private static NativeBattleEffect173Frame L173Sprite(NativeBattleEffect173Frame frame, uint temporary, NativeBattleEffectArena context, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: pilotIdentity);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Temporary180, 180);
        return inputs.Execute(frame, new NativeBattleEffect173DrawBatch(drawn.Batch, L173Cursor));
    }
    private static bool L173Paused(NativeBattleEffect173Frame frame, NativeBattleEffect173Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    private static int L173Random(ref NativeBattleEffect173Frame frame)
    {
        NativeCrtRandomState next = frame.Random.Draw(out ushort value);
        frame = frame with { Random = next };
        return value;
    }
    private static NativeBattleEffectArena L173Word(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleEffectArena L173Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static short L173S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint L173U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleVector L173Vector(NativeBattleEffectArena arena, int offset) => new(L173S(arena, offset), L173S(arena, offset + 2), L173S(arena, offset + 4));
    private static NativeBattleEffectArena L173Store(NativeBattleEffectArena arena, int offset, NativeBattleVector vector) => arena.Write(offset, BitConverter.GetBytes(vector.X)).Write(offset + 2, BitConverter.GetBytes(vector.Y)).Write(offset + 4, BitConverter.GetBytes(vector.Z));
}
