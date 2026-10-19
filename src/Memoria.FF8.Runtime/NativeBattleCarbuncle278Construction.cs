using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278Vibration(NativeBattleCarbuncle278Frame Frame, int Handle);
public sealed record NativeBattleCarbuncle278Inputs(NativeBattleActionPacketsState Packets, Func<NativeBattleCarbuncle278Frame, NativeBattleVector, NativeBattleEffectLocalBounds, NativeBattleCarbuncle278Frame> Mirror, Func<NativeBattleCarbuncle278Frame, NativeBattleCarbuncle278Vibration> StartVibration)
{
    public Func<NativeBattleCarbuncle278Frame, NativeBattleModulePoolTask, uint, NativeBattleCarbuncle278ModelStep>? DrawRecipientRange { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleModulePoolTask, NativeBattleCarbuncle278Frame>? ApplyRecipientContact { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleModulePoolTask, NativeBattleCarbuncle278ModelStep>? DrawRecipientChild { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleModulePoolTask, NativeBattleCarbuncle278ModelStep>? AdvanceRecipient { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleEffectFileRequest, NativeBattleCarbuncle278Frame>? QueueFile { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleModulePoolTask, NativeBattleCarbuncle278ModelStep>? AdvanceProgramme { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleModulePoolTask, NativeBattleCarbuncle278ModelStep>? PresentProgramme { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleModulePoolTask, NativeBattleCarbuncle278Frame>? PresentSparkleWindow { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleModulePoolTask, NativeBattleCarbuncle278Frame>? DrawSparkle { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleModulePoolTask, NativeBattleCarbuncle278ModelStep>? ModelCreature { get; init; }
    public Func<NativeBattleCarbuncle278Frame, ushort, NativeBattleCarbuncle278Frame>? DrawCreature { get; init; }
    public Func<NativeBattleCarbuncle278Frame, ushort, NativeBattleCarbuncle278Frame>? PresentCreature { get; init; }
}

/// <summary>The root callback's ordered physical pool and creature construction.</summary>
public static class NativeBattleCarbuncle278Construction
{
    public static NativeBattleCarbuncle278Frame Initialize680E36(NativeBattleCarbuncle278Frame frame, NativeBattleCarbuncle278Inputs inputs)
    {
        NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(frame.Memory);
        NativeBattleCarbuncle278Resources resources = frame.Effect.Binding.Carbuncle278Resources ?? throw new NotSupportedException("Native Carbuncle6D6070 requires its original model resources.");
        NativeBattleEffectOwnedModelResource model = resources.CreatureModel ?? throw new NotSupportedException("Native Carbuncle requires its current original creature model.");
        NativeBattleStaticMeshResource secondBounds = resources.CreatureBoundsMesh ?? throw new NotSupportedException("Native Carbuncle requires its second original bounds model.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Carbuncle requires its current effect arena.");
        ImmutableArray<NativeBattleEffectTaskPool> pools = frame.Effect.Pools;
        AddPool(0x2508128, 4, 0x1C, 0x100);
        AddPool(0x2508138, 0x1C04, 0x1C, 0x18);
        AddPool(0x2508148, 0x1EA4, 0x1C, 9);
        AddPool(0x2508158, 0x1FA0, 0x10C, 3);
        AddPool(0x2508168, 0x22C4, 0x1C, 8);
        AddPool(0x2508178, 0x23A4, 0x14, 0x40);
        AddPool(0x2508188, 0x28A4, 0x5E0, 1);
        NativeBattleVector plane = new(0, 0, unchecked((short)(state.Base81CC - 200)));
        NativeBattleEffectLocalBounds bounds = NativeBattleStaticMeshBounds.Read6D6070(resources.MirrorBoundsMesh.Bytes.AsSpan());
        state = state with { MirrorPlane81F0 = plane, Bounds81E0 = bounds };
        frame = frame with { Memory = frame.Memory with { Arena = arena, Carbuncle278 = state }, Effect = frame.Effect with { Pools = pools } };
        frame = inputs.Mirror(frame, plane, bounds);
        NativeBattleEffectTaskPool creature = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2508188);
        (creature, int slot) = creature.Append(0x681630);
        NativeBattleModulePoolTask task = creature.Slots[slot] with { Payload = new byte[0x5E0 - 12].ToImmutableArray() };
        NativeBattleCarbuncle278Vibration vibration = inputs.StartVibration(frame);
        frame = vibration.Frame;
        task = task.WriteDword(0x10, unchecked((uint)vibration.Handle));
        task = NativeBattleCarbuncle278Creature.Initialize6EC060(task, creature.BankAddress, model);
        NativeBattleCarbuncleExecutableMemory executable = frame.Memory.CarbuncleExecutableMemory ?? NativeBattleCarbuncleExecutableMemory.FromOriginal(model);
        NativeBattleCarbuncle278CreaturePose pose = NativeBattleCarbuncle278Creature.Select6574D0(task, executable, model, 0);
        task = pose.Task;
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(-2048));
        short sine = unchecked((short)NativeBattleCameraMath.Sine(-2048));
        NativeBattleActionMatrix matrix = new([cosine, 0, unchecked((short)-sine), 0, 4096, 0, sine, 0, cosine], 0, 0, 0, 4096);
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x60 + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        task = task.WriteWord(0x72, 0).WriteDword(0x74, 0).WriteWord(0x3C, 0).WriteDword(0x78, unchecked((uint)-20)).WriteWord(0x3E, unchecked((ushort)-20)).WriteDword(0x7C, unchecked((uint)state.Base81CC)).WriteWord(0x40, unchecked((ushort)state.Base81CC));
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Carbuncle requires its current action header.");
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
        int count = header.ReadByte(0x10);
        task = task.WriteWord(0xE, (ushort)count);
        for (int index = 0; index < count; index++)
        {
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Carbuncle creature requires its current contact pointer.");
            byte actor = inputs.Packets.ReadContact(first + index).Primary.ActorIndex;
            task = task.WriteDword(0x14 + index * 4, unchecked(0x1D972C0u + actor * 0x9Cu));
        }
        bounds = NativeBattleStaticMeshBounds.Read6D6070(secondBounds.Bytes.AsSpan());
        state = NativeBattleCarbuncle278Entry.RequireState(frame.Memory) with { Bounds81E0 = bounds };
        arena = frame.Memory.Arena!;
        short delta = unchecked((short)(bounds.Maximum.Z - bounds.Minimum.Z));
        byte[] dimensions = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(dimensions, bounds.Minimum.Z);
        BinaryPrimitives.WriteInt16LittleEndian(dimensions.AsSpan(2), unchecked((short)(delta + (delta >> 4))));
        arena = arena.Write(0, dimensions).Write(0x28A4 + 12, task.Payload.AsSpan());
        creature = creature with { Slots = creature.Slots.SetItem(slot, task) };
        frame = frame with { Memory = frame.Memory with { Arena = arena, Carbuncle278 = state, CarbuncleExecutableMemory = pose.Executable }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(pool => pool.DescriptorAddress == creature.DescriptorAddress ? creature : pool).ToImmutableArray() } };
        return frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x3E84) };

        void AddPool(uint descriptor, int offset, ushort stride, ushort count)
        {
            pools = pools.Where(pool => pool.DescriptorAddress != descriptor).Append(NativeBattleEffectTaskPool.Initialize(descriptor, unchecked(state.ModelBuffer81F8 + (uint)offset), stride, count)).ToImmutableArray();
            arena = arena.Write(offset, new byte[stride * count]);
        }
    }
}
