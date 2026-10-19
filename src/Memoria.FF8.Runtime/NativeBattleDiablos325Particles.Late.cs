using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDiablos325Particles
{
    public static NativeBattleDiablos325ProgrammeStep Spawn6560B1(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask parent)
    {
        int counter = unchecked((short)parent.ReadWord(0xC));
        if (counter is < 470 or > 477)
        {
            throw new ArgumentOutOfRangeException(nameof(parent));
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        if (counter == 470)
        {
            uint bank = checked(state.ModelBuffer5208 + 0x3505C);
            NativeBattleEffectTaskPool initialized = NativeBattleEffectTaskPool.Initialize(Descriptor, bank, Stride, Capacity);
            NativeBattleEffectArena arena = (frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos late particle initialization requires its current arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(bank, Stride * Capacity), new byte[Stride * Capacity]);
            frame = Replace(frame with { Memory = frame.Memory with { Arena = arena } }, initialized);
        }
        for (int index = 0; index < 2; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(item => item.DescriptorAddress == Descriptor);
            if (pool.Slots.All(task => (task.Flags & 1) != 0))
            {
                continue;
            }
            (pool, int slot) = pool.Append(0x659860);
            NativeCrtRandomState random = frame.Random ?? throw new NotSupportedException("Native Diablos late particle birth requires its current CRT random owner.");
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos late particle birth requires its current skeleton.");
            int count = arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(parent.ReadDword(0x244), 1), 1)[0];
            random = random.Draw(out ushort boneRandom);
            byte[] vector = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(vector.AsSpan(6), unchecked((short)(boneRandom * count >> 15)));
            NativeBattleVector point = NativeBattleDiablos325Point.Sample6F0CC0(frame.Memory, parent, vector);
            random = random.Draw(out ushort scaleRandom);
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, unchecked((ushort)point.X)).WriteWord(0xE, unchecked((ushort)point.Y)).WriteWord(0x10, unchecked((ushort)point.Z)).WriteWord(0x12, unchecked((ushort)-256)).WriteDword(0x14, (uint)((scaleRandom & 2047) + 2048));
            NativeBattleGfAnimationSource source = NativeBattleDiablos325LateProgramme.ReadSource(frame, 735, 0x42620);
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(task.Payload.Slice(0x18 - 12, 0xB8)).Initialize7016B0(source, 0xB8);
            byte[] payload = task.Payload.ToArray();
            workspace.Data.AsSpan().CopyTo(payload.AsSpan(0x18 - 12));
            pool = pool with { Slots = pool.Slots.SetItem(slot, task with { Payload = payload.ToImmutableArray() }) };
            frame = Synchronize(Replace(frame with { Random = random }, pool), pool);
        }
        byte color = unchecked((byte)(112 - ((counter - 470) << 4)));
        return new(frame, parent.WriteByte(0x134, color).WriteByte(0x135, color).WriteByte(0x136, color));
    }

    private static NativeBattleEffectTaskCallback Advance659860(NativeBattleDiablos325Frame frame, NativeBattleEffectTaskPool pool, int index, NativeBattleCameraProjection projection, NativeBattleActionMatrix billboard, NativeBattleSummonColors colors, Func<NativeBattleDiablos325Frame, NativeBattleGfGeometryBatch, NativeBattleDiablos325Frame> submit, Action<NativeBattleDiablos325Frame> publish)
    {
        NativeBattleModulePoolTask task = pool.Slots[index];
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleDiablos325LateResources tables = frame.Effect.Binding.Diablos325Resources?.LateResources ?? throw new NotSupportedException("Native Diablos late particles require their original selector table.");
        ImmutableArray<byte> selector = tables.Read(0xFE0FB4, 1).ToArray().ToImmutableArray();
        NativeBattleGfAnimationSource source = NativeBattleDiablos325LateProgramme.ReadSource(frame, 735, 0x42620);
        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x18 - 12, 0xB8));
        short scale = unchecked((short)task.ReadDword(0x14));
        NativeBattleVector position = new(unchecked((short)task.ReadWord(0xC)), unchecked((short)task.ReadWord(0xE)), unchecked((short)task.ReadWord(0x10)));
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, unchecked((int)state.Pause5180), (current, node) =>
        {
            NativeBattleDiablos325NodeDraw draw = NativeBattleDiablos325Programme.Emit656B70(frame.Memory, source, node, projection, colors, position, unchecked((short)task.ReadWord(0x12)), 0, selector, selector, effectCamera: billboard, contextScale: new(scale, scale, scale));
            frame = submit(frame with { Memory = draw.Memory }, draw.Packets);
            return current;
        });
        byte[] payload = task.Payload.ToArray();
        advanced.Workspace.Data.AsSpan().CopyTo(payload.AsSpan(0x18 - 12));
        task = task with { Payload = payload.ToImmutableArray() };
        publish(frame);
        return new(pool with { Slots = pool.Slots.SetItem(index, task) }, advanced.Remaining == 0 ? 2 : 0);
    }
}
