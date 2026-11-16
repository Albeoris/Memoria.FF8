using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect70Controller
{
    public static NativeBattleEffect70Step AdvanceRoot(NativeBattleEffect70Frame frame, uint address, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool alternate = task.ReadByte(14) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        bool starting = task.ReadWord(12) == 1 && task.ReadByte(15) == 0;
        if (starting)
        {
            frame = PrepareRecipientBank(frame, inputs.ReadPackets(frame));
        }
        uint packetLength = checked((uint)PacketBankLength(frame.State));
        uint packetStart = checked(arena + FirstPacketOffset + (alternate ? 0 : packetLength));
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, packetStart).WriteDword(SecondaryCursorGlobal, checked(packetStart + packetLength)).WriteDword(DepthGlobal, 0) };
        frame = Write(frame, address, task.WriteByte(14, alternate ? (byte)0 : (byte)1));
        if (starting)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = CaptureFlags(frame, inputs);
            frame = NativeBattleEffect70Particles.InitializeSparks(frame);
            frame = NativeBattleEffect70Particles.InitializeRings(frame);
            frame = inputs.Execute(frame, new NativeBattleEffect70IoInitialize534110(arena + 0x678));
            frame = InitializeCommonPool(frame, 1);
            frame = InitializeCommonPool(frame, 2);
            (frame, uint main) = Append(frame, 1, 0x6DDD50);
            frame = ClearBytes(frame, main + 12, 0x1150);
            frame = inputs.Execute(frame, new NativeBattleEffect70StartVibration(resources.Vibration, main, 16));
            frame = CaptureTargets(frame, main, inputs);
        }
        int survivors = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            NativeBattlePackedMatrix32 packed = inputs.ReadView(frame);
            NativeBattleEffectArena view = frame.State.ReadView(ViewAddress, 32);
            view = NativeBattleEffectNodeContextMemory.Copy(PackedArena(packed), 20, view, 20, 12);
            NativeBattleActionMatrix normalized = NativeBattleGfMatrices.Normalize67AAE0(packed.ReadMatrix());
            for (int index = 0; index < 9; index++)
            {
                view = view.Write(index * 2, BitConverter.GetBytes(normalized.Rotation[index]));
            }
            frame = frame with { State = frame.State.WriteView(ViewAddress, view, 32) };
            (frame, survivors) = Scan(frame, 1, resources, inputs);
            (frame, _) = Scan(frame, 2, resources, inputs);
        }
        int depth = unchecked((int)frame.State.ReadDword(DepthGlobal));
        int previous = unchecked((int)frame.State.ReadDword(PreviousDepthGlobal));
        if (depth != previous)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect70StageIntensity5713E0(depth, 0));
            if (depth >= 4096 || previous >= 4096)
            {
                for (int group = 0; group < 4; group++)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect70StageFlags(group, depth >= 4096 ? (byte)0xFD : (byte)0xFF, depth >= 4096 ? (byte)0 : (byte)2));
                }
            }
            frame = frame with { State = frame.State.WriteDword(PreviousDepthGlobal, unchecked((uint)depth)) };
        }
        if (Read(frame, address).ReadByte(15) != 0 && survivors == 0)
        {
            return new(inputs.Execute(frame, new NativeBattleEffect70StageIntensity5713E0(0, 0)), 2);
        }
        task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)))), 0);
    }

    public static NativeBattleEffect70Frame CaptureFlags(NativeBattleEffect70Frame frame, NativeBattleEffect70Inputs inputs)
    {
        (BattleActorModels models, NativeBattleModelBoundary bindings) = inputs.RequireModelBank(frame);
        var saved = models.Entries.ToImmutableDictionary(entry => entry.Owner, entry => entry.State.Flags0);
        //6DDCC0 captures the seven current cells, including unbound retained cells.
        for (byte index = 0; index < NativeBattleModelBoundary.CellCount; index++)
        {
            NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(index);
            ushort flags = bindings.ReadOriginalCell(slot, models).Flags0;
            frame = frame with { State = frame.State.WriteDword(SavedFlagsAddress + (uint)index * 4, flags) };
        }
        return frame with { State = frame.State with { SavedFlags = saved } };
    }

    public static NativeBattleEffect70Frame HideEnabled(NativeBattleEffect70Frame frame, NativeBattleEffect70Inputs inputs)
    {
        return UpdateEnabledFlags(frame, inputs, restore: false);
    }

    public static NativeBattleEffect70Frame RestoreFlags(NativeBattleEffect70Frame frame, NativeBattleEffect70Inputs inputs)
    {
        return UpdateEnabledFlags(frame, inputs, restore: true);
    }

    private static NativeBattleEffect70Frame UpdateEnabledFlags(NativeBattleEffect70Frame frame, NativeBattleEffect70Inputs inputs, bool restore)
    {
        (BattleActorModels models, NativeBattleModelBoundary bindings) = inputs.RequireModelBank(frame);
        NativeBattleModelAliases aliases = bindings.CaptureAliases(models);
        ushort andMask = restore ? (ushort)0xFFFB : ushort.MaxValue;
        for (byte index = 0; index < NativeBattleModelBoundary.CellCount; index++)
        {
            NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(index);
            if ((bindings.ReadOriginalCell(slot, models).Flags0 & 2) != 0)
            {
                ushort orMask = restore ? (ushort)(frame.State.ReadDword(SavedFlagsAddress + (uint)index * 4) & 4) : (ushort)4;
                NativeBattleEffect70Operation operation = bindings.FindOriginalOwner(slot, models) is { } actor
                    ? new NativeBattleEffect70ModelFlags(actor, andMask, orMask, true)
                    : new NativeBattleEffect70UnboundModelFlags(slot, andMask, orMask);
                frame = inputs.Execute(frame, operation);
            }
        }
        foreach (BattleModelEntry<BattleModelOwner> entry in models.Entries)
        {
            if ((entry.State.Flags0 & 2) != 0 && !aliases.TryEncodeExact(entry.Owner, out _))
            {
                ushort saved = restore ? frame.State.SavedFlags[entry.Owner] : (ushort)4;
                frame = inputs.Execute(frame, new NativeBattleEffect70ModelFlags(entry.Owner, andMask, (ushort)(saved & 4), true));
            }
        }
        return frame;
    }

    private static NativeBattleEffect70Frame CaptureTargets(NativeBattleEffect70Frame frame, uint main, NativeBattleEffect70Inputs inputs)
    {
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        BattleActionHeaderReference headerIndex = Parameters(frame).Header;
        NativeBattleActionHeaderSlot header = packets.ReadHeader(headerIndex);
        int count = header.ReadContactCount();
        frame = Write(frame, main, Read(frame, main).WriteWord(14, (ushort)count));
        short[] minimum = [32767, 32767, 32767];
        short[] maximum = [-32767, -32767, -32767];
        for (int index = 0; index < count; index++)
        {
            BattleContactCursor first = header.FirstContact;
            BattleModelOwner actor = packets.ReadContact(first.Advance(index)).Primary.Recipient;
            uint record = RecipientRecord(frame, main, index);
            if (index < NativeBattleEffect70RecipientBank.OriginalCapacity)
            {
                NativeBattleModulePoolTask recipientTask = Read(frame, main);
                recipientTask.WriteExecutionReference(checked((int)(record - main)), 4, new BattleExecutionReference.Model(actor), packets.ModelAliases.EncodeAddress(actor));
                frame = Write(frame, main, recipientTask);
            }
            else
            {
                NativeBattleEffect70RecipientBank bank = frame.State.ExtendedRecipients!;
                NativeBattleEffectArena records = bank.Records.WriteExecutionReference(checked((int)(record - bank.Address)), 4, new BattleExecutionReference.Model(actor), packets.ModelAliases.EncodeAddress(actor));
                frame = frame with { State = frame.State with { ExtendedRecipients = bank with { Records = records } } };
            }
            frame = WriteBytes(frame, record + 4, (inputs.ReadPosition8 ?? throw new NotSupportedException("Effect70 requires actual packed model position bytes."))(frame, actor), 8);
            frame = WriteBytes(frame, record + 12, frame.State.ReadView(RetainedFirstGlobal, 8), 8);
            NativeBattleEffect70PointStep step = (inputs.ReadAnchor571400 ?? throw new NotSupportedException("Effect70 requires its current target point producer."))(frame, actor);
            frame = step.Frame;
            NativeBattleVector anchor = Vector(step.Point8);
            short[] xyz = [anchor.X, anchor.Y, anchor.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                minimum[axis] = Math.Min(minimum[axis], xyz[axis]);
                maximum[axis] = Math.Max(maximum[axis], xyz[axis]);
            }
            frame = frame with { State = frame.State.WriteDword(SideGlobal, actor.Side == BattleSide.Enemies ? uint.MaxValue : 1) };
        }
        for (int axis = 0; axis < 3; axis++)
        {
            frame = frame with { State = frame.State.WriteWord(CenterAddress + (uint)axis * 2, unchecked((ushort)((minimum[axis] + maximum[axis]) / 2))) };
        }
        return frame;
    }

    private static NativeBattleEffect70Frame PrepareRecipientBank(NativeBattleEffect70Frame frame, NativeBattleActionPacketsState packets)
    {
        int count = packets.ReadHeader(Parameters(frame).Header).ReadContactCount();
        if (count <= NativeBattleEffect70RecipientBank.OriginalCapacity)
        {
            return frame;
        }
        int length = (count - NativeBattleEffect70RecipientBank.OriginalCapacity) * NativeBattleEffect70RecipientBank.RecordSize;
        (NativeBattleEffectMemory memory, uint address) = NativeBattlePresentationTaskStorage.ReserveByteRange(frame.Memory, frame.Effect, packets.Roster, length);
        int secondaryCapacity = checked(count * 2);
        (memory, uint secondary) = NativeBattlePresentationTaskStorage.Reserve(memory, frame.Effect, packets.Roster, Pools[2].Stride, secondaryCapacity);
        NativeBattleEffect70RecipientBank bank = new(address, checked((ushort)count), secondary, checked((ushort)secondaryCapacity), NativeBattleEffectArena.Clear().MarkUnknown(0, length));
        return frame with { Memory = memory, State = frame.State with { ExtendedRecipients = bank } };
    }

    public static NativeBattleEffect70Frame PutDword(NativeBattleEffect70Frame frame, uint address, uint value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
    public static NativeBattleEffect70Frame PutWord(NativeBattleEffect70Frame frame, uint address, short value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
    public static NativeBattleEffect70Frame PutByte(NativeBattleEffect70Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
}
