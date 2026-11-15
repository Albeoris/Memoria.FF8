using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278Vibration(NativeBattleCarbuncle278Frame Frame, int Handle);
public sealed record NativeBattleCarbuncle278Inputs(NativeBattleActionPacketsState Packets, Func<NativeBattleCarbuncle278Frame, NativeBattleVector, NativeBattleEffectLocalBounds, NativeBattleCarbuncle278Frame> Mirror, Func<NativeBattleCarbuncle278Frame, NativeBattleCarbuncle278Vibration> StartVibration)
{
    public Func<NativeBattleCarbuncle278Frame, NativeBattleModulePoolTask, NativeBattleCarbuncle278ModelStep>? PresentReturn { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleCarbuncle278Frame>? BeginFinalTexture { get; init; }
    public Func<NativeBattleCarbuncle278Frame, int, NativeBattleCarbuncle278Frame>? StopVibration { get; init; }
    public Func<NativeBattleCarbuncle278Frame, NativeBattleCarbuncle278Frame>? RestoreActors { get; init; }
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
        NativeBattleActionHeaderSlot? expandedHeader = null;
        if (frame.ExpandedStorage)
        {
            NativeBattleEffectParameters expandedParameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Carbuncle requires its current action header.");
            expandedHeader = inputs.Packets.ReadHeader(expandedParameters.Header);
        }
        frame = NativeBattleCarbuncle278Banks.Initialize(frame, inputs.Packets.Roster, expandedHeader?.ReadContactCount() ?? 0);
        NativeBattleVector plane = new(0, 0, unchecked((short)(state.Base81CC - 200)));
        NativeBattleEffectLocalBounds bounds = NativeBattleStaticMeshBounds.Read6D6070(resources.MirrorBoundsMesh.Bytes.AsSpan());
        state = state with { MirrorPlane81F0 = plane, Bounds81E0 = bounds };
        frame = frame with { Memory = frame.Memory with { Carbuncle278 = state } };
        frame = inputs.Mirror(frame, plane, bounds);
        NativeBattleEffectTaskPool creature = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2508188);
        (creature, int slot) = creature.Append(0x681630);
        NativeBattleModulePoolTask task = creature.Slots[slot].ClearPayload(0x5E0 - 12);
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
        NativeBattleActionHeaderSlot header = expandedHeader ?? inputs.Packets.ReadHeader(parameters.Header);
        int count = header.ReadContactCount();
        task = task.WriteContactCount(0xE, count);
        var recipients = ImmutableArray.CreateBuilder<(BattleModelOwner Owner, BattleContactOccurrence Occurrence)>(count);
        ImmutableArray<byte>? originalFirst = [0, 0, 0, 0];
        for (int index = 0; index < count; index++)
        {
            NativeBattleContactPacket contact = inputs.Packets.ReadContact(header.FirstContact.Advance(index));
            BattleModelOwner actor = contact.Primary.Recipient;
            BattleContactOccurrence occurrence = contact.Occurrence ?? throw new InvalidDataException("Native Carbuncle requires its captured recipient occurrence.");
            recipients.Add((actor, occurrence));
            if (index == 0)
            {
                originalFirst = inputs.Packets.ModelAliases.EncodeAddress(actor);
            }
            else if (index < 3)
            {
                int offset = 0x14 + index * 4;
                task = inputs.Packets.ModelAliases.EncodeAddress(actor) is { } encoded ? task.WriteBytes(offset, encoded.AsSpan()) : task.MarkPayloadUnknown(offset, 4);
            }
        }
        task.WriteExecutionReference(0x14, 4, new BattleExecutionReference.OrderedContactRecipients(recipients.MoveToImmutable()), originalFirst);
        bounds = NativeBattleStaticMeshBounds.Read6D6070(secondBounds.Bytes.AsSpan());
        state = NativeBattleCarbuncle278Entry.RequireState(frame.Memory) with { Bounds81E0 = bounds };
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        short delta = unchecked((short)(bounds.Maximum.Z - bounds.Minimum.Z));
        byte[] dimensions = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(dimensions, bounds.Minimum.Z);
        BinaryPrimitives.WriteInt16LittleEndian(dimensions.AsSpan(2), unchecked((short)(delta + (delta >> 4))));
        (byte[] bytes, bool[] known) = task.BorrowPayloadArena(12, task.PayloadLength).ReadSnapshot(0, task.PayloadLength);
        arena = arena.Write(0, dimensions).WriteWithKnownness(0x28A4 + 12, bytes, known);
        creature = creature.WriteSlot(slot, task);
        frame = frame with { Memory = frame.Memory with { Arena = arena, Carbuncle278 = state, CarbuncleExecutableMemory = pose.Executable }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(pool => pool.DescriptorAddress == creature.DescriptorAddress ? creature : pool).ToImmutableArray() } };
        return frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x3E84) };

    }
}
