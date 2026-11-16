using System.Collections.Immutable;
using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Controller
{
    private const int OriginalTargetRows = 3;

    public static NativeBattleEffect65Step AdvanceRoot6E03F0(NativeBattleEffect65Frame frame, uint address, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool prior = root.ReadDword(0x10) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(frame.State.ReadDword(ArenaGlobal) + (uint)(prior ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteDword(0x10, prior ? 0u : 1u));
        root = Read(frame, address);
        if (root.ReadWord(12) == 1 && frame.State.ReadDword(ControlGlobal) == 0 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, root.WriteByte(15, 1));
            frame = SaveActorFlags6E07A0(frame, inputs);
            frame = InitializeMainBank(frame);
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect65 exhausted its mandatory main bank.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x36B);
            frame = inputs.Execute(frame, new NativeBattleEffect65Vibration4A29A0(VibrationAddress, 1, 128, main + 0x10));
            frame = CaptureActors(frame, main, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect65IoInitialize534110(checked(frame.State.ReadDword(ArenaGlobal) + IoOffset)));
            frame = InitializeOrbitRows(frame, resources);
        }
        int survivors = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            (frame, survivors) = AdvancePool(frame, 1, resources, inputs);
            if (frame.Effect.Pools.Length == 3)
            {
                (frame, _) = AdvancePool(frame, 2, resources, inputs);
            }
            else
            {
                NativeBattleEffectArena queue = frame.State.ReadChildQueue();
                if (BinaryPrimitives.ReadUInt32LittleEndian(queue.Read(0, 4)) != 0)
                {
                    throw new NotSupportedException("Effect65 requires an observed empty inactive child queue before initialization.");
                }
                frame = frame with { State = frame.State.WriteDword(ChildDescriptor + 4, 0) };
            }
        }
        if (frame.State.ReadDword(ControlGlobal) == 0)
        {
            if (Read(frame, address).ReadByte(15) != 0 && survivors == 0)
            {
                return new(frame, 2);
            }
            frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect65Frame SaveActorFlags6E07A0(NativeBattleEffect65Frame frame, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        ImmutableDictionary<BattleModelOwner, ushort>.Builder saved = ImmutableDictionary.CreateBuilder<BattleModelOwner, ushort>();
        foreach (BattleModelOwner actor in packets.Roster.Owners)
        {
            ushort flags = inputs.ReadModel(frame, actor).Flags0;
            saved.Add(actor, flags);
            if (packets.ModelAliases.TryEncodeExact(actor, out byte originalSlot))
            {
                frame = frame with { State = frame.State.WriteDword(SavedFlagsAddress + (uint)originalSlot * 4, flags) };
            }
        }
        return frame with { State = frame.State with { SavedFlags = saved.ToImmutable() } };
    }

    public static NativeBattleEffect65Frame RestoreActorFlags6E2BD0(NativeBattleEffect65Frame frame, NativeBattleEffect65Inputs inputs)
    {
        foreach (BattleModelOwner model in inputs.ReadPackets(frame).Roster.Owners)
        {
            if ((inputs.ReadModel(frame, model).Flags0 & 2) != 0)
            {
                ushort saved = frame.State.SavedFlags.TryGetValue(model, out ushort flags) ? flags : throw new InvalidDataException("The current model has no retained effect flags.");
                frame = inputs.Execute(frame, new NativeBattleEffect65ModelFlags(model, 0xFFFB, (ushort)(saved & 4)));
            }
        }
        return frame;
    }

    internal static NativeBattleEffect65Frame CaptureActors(NativeBattleEffect65Frame frame, uint main, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        BattleActionHeaderReference header = (frame.Effect.Parameters ?? throw new NotSupportedException("Effect65 requires its actual header pointer.")).Header;
        ImmutableArray<NativeBattleContactPacket> contacts = packets.ReadHeaderContacts(header);
        int count = contacts.Length;
        if (count > OriginalTargetRows && inputs.StorageContract != BattleContactTaskStorageContract.ExpandableArena)
        {
            throw new NotSupportedException("Effect65 original target rows would overlap the main programme workspace.");
        }
        ImmutableArray<NativeBattleEffect65TargetRow>.Builder overflow = ImmutableArray.CreateBuilder<NativeBattleEffect65TargetRow>(Math.Max(0, count - OriginalTargetRows));
        frame = Write(frame, main, Read(frame, main).WriteWord(14, (ushort)count));
        int[] minimum = [32767, 32767, 32767];
        int[] maximum = [-32767, -32767, -32767];
        for (int index = 0; index < count; index++)
        {
            BattleModelOwner model = contacts[index].Primary.Recipient;
            NativeBattleEffectArena position = inputs.ReadPosition8(frame, model);
            if (index < OriginalTargetRows)
            {
                int offset = 0x24 + index * 12;
                NativeBattleModulePoolTask recipientTask = Read(frame, main);
                recipientTask.WriteExecutionReference(offset, 4, new BattleExecutionReference.Model(model), packets.ModelAliases.EncodeAddress(model));
                frame = Write(frame, main, recipientTask);
                frame = WriteBytes(frame, checked(main + (uint)offset + 4), position, 8);
            }
            else
            {
                // The fourth physical row begins at the live ACT workspace.
                NativeBattleEffectArena captured = CopyBytes(position, 0, NativeBattleEffectArena.CreateUnknown(8), 0, 8);
                overflow.Add(new(model, captured));
            }
            NativeBattleEffect65PointStep anchor = inputs.ReadAnchor571400(frame, model);
            frame = anchor.Frame;
            NativeBattleVector point = Vector(anchor.Point8);
            int[] coordinates = [point.X, point.Y, point.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                minimum[axis] = Math.Min(minimum[axis], coordinates[axis]);
                maximum[axis] = Math.Max(maximum[axis], coordinates[axis]);
            }
        }
        NativeBattleVector center = new(unchecked((short)((minimum[0] + maximum[0]) / 2)), unchecked((short)((minimum[1] + maximum[1]) / 2)), unchecked((short)((minimum[2] + maximum[2]) / 2)));
        frame = WriteBytes(frame, main + 0x1C, PointView(center), 6);
        frame = WriteBytes(frame, CenterAddress, PointView(center), 6);
        NativeBattleEffectArena caster = inputs.ReadPosition8(frame, frame.State.ReadModelAddress(CasterGlobal));
        frame = WriteBytes(frame, main + 0x14, caster, 8);
        frame = WriteBytes(frame, CasterPointAddress, caster, 8);
        return frame with { State = frame.State with { HostedTargetOverflow = overflow.MoveToImmutable() } };
    }

    private static int ReadCapturedTargetCount(NativeBattleEffect65Frame frame, uint main)
    {
        int count = Signed(Read(frame, main), 14);
        if (count < 0 || frame.State.HostedTargetOverflow.Length != Math.Max(0, count - OriginalTargetRows))
        {
            throw new InvalidDataException("Effect65 target count differs from its captured hosted rows.");
        }
        return count;
    }

    private static NativeBattleEffect65TargetRow ReadCapturedTarget(NativeBattleEffect65Frame frame, uint main, int index)
    {
        if (index >= OriginalTargetRows)
        {
            return frame.State.HostedTargetOverflow[index - OriginalTargetRows];
        }
        NativeBattleModulePoolTask task = Read(frame, main);
        int offset = 0x24 + index * 12;
        return new(task.ReadExecutionReference<BattleExecutionReference.Model>(offset, 4).Owner, TaskView(task, offset + 4, 8));
    }

    public static NativeBattleEffect65Frame InitializeOrbitRows(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources)
    {
        uint row = checked(frame.State.ReadDword(ArenaGlobal) + 0xDB8);
        for (uint bank = 0; bank < 8; bank++)
        {
            uint record = 0x133ABC8 + bank * 16;
            frame = frame with { State = frame.State.WriteWord(record, 0).WriteWord(record + 2, 0) };
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(record - 8, 4).AsSpan());
            uint alpha = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(record - 4, 4).AsSpan());
            for (int index = 0; index < 128; index++)
            {
                NativeCrtRandomState random = frame.Random.Draw(out ushort first);
                int polar = (first * 1600 >> 15) - 800;
                random = random.Draw(out ushort second);
                int angle = (second & 0xFFF) - 0x800;
                NativeBattleVector direction = Direction6A6020(polar, angle);
                int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(polar * polar + angle * angle));
                random = random.Draw(out ushort speed);
                int factor = speed / 2 + 12000;
                NativeBattleEffectArena data = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)(direction.X * factor >> 12)))).Write(2, BitConverter.GetBytes(unchecked((short)(direction.Y * factor >> 12)))).Write(4, BitConverter.GetBytes(unchecked((short)(direction.Z * factor >> 12)))).Write(6, BitConverter.GetBytes(unchecked((short)length))).Write(8, BitConverter.GetBytes(color)).Write(12, BitConverter.GetBytes(alpha));
                random = random.Draw(out ushort code);
                data = data.Write(11, [(byte)code]);
                byte initialAlpha = (byte)(alpha >> 24);
                random = random.Draw(out ushort variation);
                data = data.Write(15, [unchecked((byte)(initialAlpha + ((variation * initialAlpha) >> 17)))]);
                frame = frame with { Random = random };
                frame = WriteBytes(frame, row, data, 16);
                row += 16;
            }
        }
        return frame;
    }

    public static NativeBattleVector Direction6A6020(int polar, int angle)
    {
        int cosine = NativeBattleCameraMath.Cosine(polar);
        return new(unchecked((short)(NativeBattleCameraMath.Sine(angle) * cosine >> 12)), unchecked((short)-NativeBattleCameraMath.Sine(polar)), unchecked((short)(unchecked(-NativeBattleCameraMath.Cosine(angle) * cosine) >> 12)));
    }
}
