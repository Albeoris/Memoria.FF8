using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291Vibration(NativeBattlePandemona291Frame Frame, int Handle);
public sealed record NativeBattlePandemona291BoundStep(NativeBattlePandemona291Frame Frame, NativeBattlePandemona291Bounds Bounds);
public sealed record NativeBattlePandemona291ConstructionInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattlePandemona291Frame, int, NativeBattlePandemona291BoundStep> Bounds, Func<NativeBattlePandemona291Frame, NativeBattlePandemona291Vibration> StartVibration);

public static class NativeBattlePandemona291Construction
{
    public static NativeBattlePandemona291Frame Initialize6ED3B6(NativeBattlePandemona291Frame frame, NativeBattlePandemona291ConstructionInputs inputs)
    {
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona construction requires its retained module owner.");
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona construction requires its original model buffer.");
        if (buffer != NativeBattleEffectNodeMemory.ArenaAddress)
        {
            throw new NotSupportedException("Native Pandemona construction requires the actual current arena buffer.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona construction requires its arena.");
        ImmutableArray<NativeBattleEffectTaskPool> pools = frame.Effect.Pools;
        AddPool(0x25562A8, 0x21A4, 0x1030, 1);
        AddPool(0x2556298, 0x1AA4, 0x1C, 0x40);
        AddPool(0x2556288, 0xD90, 0x45C, 3);
        AddPool(0x2556278, 0x640, 0x9C, 12);
        AddPool(0x2556268, 0, 0x190, 4);
        int creatureIndex = Enumerable.Range(0, pools.Length).Single(index => pools[index].DescriptorAddress == 0x25562A8);
        NativeBattleEffectTaskPool creature = pools[creatureIndex];
        (creature, int slot) = creature.Append(0x6ED900);
        int child = checked((int)(creature.BankAddress - buffer) + slot * creature.Stride);
        NativeBattleModulePoolTask task = creature.Slots[slot] with { Payload = new byte[0x1024].ToImmutableArray() };
        arena = arena.Write(child + 12, task.Payload.AsSpan());
        frame = frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = pools.SetItem(creatureIndex, creature with { Slots = creature.Slots.SetItem(slot, task) }) } };
        NativeBattlePandemona291Vibration vibration = inputs.StartVibration(frame);
        frame = vibration.Frame;
        arena = frame.Memory.Arena!;
        WriteDword(child + 0x10, unchecked((uint)vibration.Handle));
        WriteDword(child + 0x102C, unchecked(buffer + (uint)child + 0x42C));
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Pandemona construction requires its action header.");
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
        int count = header.ReadByte(0x10);
        int first = count == 0 ? 0 : header.FirstContactIndex ?? throw new NotSupportedException("Native Pandemona construction requires its actual contact array.");
        for (int index = 0; index < count; index++)
        {
            int actorIndex = inputs.Packets.ReadContact(first + index).Primary.ActorIndex;
            NativeBattleGraphicsModel actor = inputs.ReadModel(actorIndex);
            NativeBattleModelTransform transform = actor.Transform ?? throw new NotSupportedException("Native Pandemona target requires its current position and scale.");
            int record = child + 0x14 + index * 32;
            WriteDword(record, unchecked(0x1D972C0u + (uint)actorIndex * 156));
            WriteWord(record + 0xC, unchecked((ushort)transform.Position28.X));
            WriteWord(record + 0xE, unchecked((ushort)transform.Position28.Y));
            WriteWord(record + 0x10, unchecked((ushort)transform.Position28.Z));
            WriteWord(record + 0x12, unchecked((ushort)transform.Vertical34));
            WriteWord(record + 0x14, unchecked((ushort)(actor.Rotation12 ?? throw new NotSupportedException("Native Pandemona target requires its current rotation X."))));
            WriteWord(record + 0x16, unchecked((ushort)((actor.Rotation14 ?? throw new NotSupportedException("Native Pandemona target requires its current rotation Y.")) + 0x800)));
            WriteWord(record + 0x18, unchecked((ushort)(actor.Rotation16 ?? throw new NotSupportedException("Native Pandemona target requires its current rotation Z."))));
            arena = arena.MarkUnknown(record + 0x1A, 2).Write(record + 0x1C, new byte[] { 0, 1 });
            WriteWord(record + 0xA, 0x1000);
            int factor = transform.Scale38 == 0 ? 16 : 65536 / (transform.Scale38 + 1024);
            WriteWord(record + 8, unchecked((ushort)factor));
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            NativeBattlePandemona291BoundStep bound = inputs.Bounds(frame, actorIndex);
            frame = bound.Frame;
            arena = frame.Memory.Arena!;
            WriteWord(record + 4, unchecked((ushort)bound.Bounds.Maximum));
            WriteWord(record + 6, unchecked((ushort)bound.Bounds.Minimum));
        }
        for (int index = 0; index < count - 1; index++)
        {
            for (int next = index + 1; next < count; next++)
            {
                int left = child + 0x14 + index * 32;
                int right = child + 0x14 + next * 32;
                if (ReadActor(left).Transform!.Position28.X < ReadActor(right).Transform!.Position28.X)
                {
                    var saved = arena.ReadSnapshot(left, 32);
                    CopySnapshot(right, left);
                    StoreSnapshot(right, saved);
                }
            }
        }
        for (int index = 0; index < count; index++)
        {
            int record = child + 0x14 + index * 32;
            int source = ActorIndex(record);
            NativeBattleGraphicsModel actor = inputs.ReadModel(source);
            int? linked = actor.ReadLinkedModelIndex140();
            bool onlyTarget = true;
            HashSet<int> visited = [];
            while (linked is int current && current != source)
            {
                if (!visited.Add(current))
                {
                    throw new InvalidDataException("Native Pandemona linked target ring does not close at its source.");
                }
                NativeBattleGraphicsModel other = inputs.ReadModel(current);
                if ((other.Flags0 & 2) != 0 && Enumerable.Range(0, count).Any(target => ActorIndex(child + 0x14 + target * 32) == current))
                {
                    onlyTarget = false;
                    break;
                }
                linked = other.ReadLinkedModelIndex140();
            }
            arena = arena.Write(record + 0x1E, new byte[] { onlyTarget ? (byte)1 : (byte)0 });
        }
        state = frame.Memory.Pandemona291!;
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona requires its original static animation sources.");
        ImmutableArray<NativeBattlePandemona291Animation> animations = state.Animations ?? (state.AnimationsKnownCold ? resources.AnimationSources : null) ?? throw new NotSupportedException("Native Pandemona requires its current retained static animations.");
        uint[] addresses = [0x13A0E58, 0x13A4F88, 0x13A6508, 0x13A8508, 0x13AB5C0, 0x13AC1B8, 0x13ACDB0];
        for (int index = 0; index < addresses.Length; index++)
        {
            int selected = Enumerable.Range(0, animations.Length).FirstOrDefault(candidate => animations[candidate].Address == addresses[index], -1);
            if (selected < 0)
            {
                throw new NotSupportedException("Native Pandemona requires every selected mutable static animation.");
            }
            NativeBattlePandemona291Animation rewritten = index < 3 ? NativeBattlePandemona291Animations.Rewrite6F2E40(animations[selected]) : NativeBattlePandemona291Animations.Rewrite6F2E60(animations[selected]);
            animations = animations.SetItem(selected, rewritten);
        }
        state = state with { Animations = animations, AnimationsKnownCold = false };
        task = task with { Payload = arena.ReadSnapshot(child + 12, 0x1024).Data.ToImmutableArray() };
        creature = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x25562A8);
        frame = frame with { Memory = frame.Memory with { Arena = arena, Pandemona291 = state }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(pool => pool.DescriptorAddress == creature.DescriptorAddress ? creature with { Slots = creature.Slots.SetItem(slot, task) } : pool).ToImmutableArray() } };
        return frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x231D4) };

        void AddPool(uint descriptor, int offset, ushort stride, ushort capacity)
        {
            pools = pools.Where(pool => pool.DescriptorAddress != descriptor).Append(NativeBattleEffectTaskPool.Initialize(descriptor, unchecked(buffer + (uint)offset), stride, capacity)).ToImmutableArray();
            arena = arena.Write(offset, new byte[stride * capacity]);
        }

        void WriteWord(int offset, ushort value) => arena = arena.Write(offset, BitConverter.GetBytes(value));
        void WriteDword(int offset, uint value) => arena = arena.Write(offset, BitConverter.GetBytes(value));
        int ActorIndex(int record) => checked((int)((BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(record, 4)) - 0x1D972C0) / 156));
        NativeBattleGraphicsModel ReadActor(int record) => inputs.ReadModel(ActorIndex(record));
        void CopySnapshot(int source, int destination) => StoreSnapshot(destination, arena.ReadSnapshot(source, 32));
        void StoreSnapshot(int destination, (byte[] Data, bool[] Known) snapshot)
        {
            arena = arena.Write(destination, snapshot.Data);
            for (int index = 0; index < snapshot.Known.Length; index++)
            {
                if (!snapshot.Known[index])
                {
                    arena = arena.MarkUnknown(destination + index, 1);
                }
            }
        }
    }
}
