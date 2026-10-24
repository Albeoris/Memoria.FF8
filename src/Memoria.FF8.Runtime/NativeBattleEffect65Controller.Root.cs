using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Controller
{
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
        for (int actor = 0; actor < 7; actor++)
        {
            frame = frame with { State = frame.State.WriteDword(SavedFlagsAddress + (uint)(actor * 4), inputs.ReadModel(frame, ModelAddress(actor)).Flags0) };
        }
        return frame;
    }

    public static NativeBattleEffect65Frame RestoreActorFlags6E2BD0(NativeBattleEffect65Frame frame, NativeBattleEffect65Inputs inputs)
    {
        for (int actor = 0; actor < 7; actor++)
        {
            uint model = ModelAddress(actor);
            if ((inputs.ReadModel(frame, model).Flags0 & 2) != 0)
            {
                byte saved = frame.State.Read(SavedFlagsAddress + (uint)(actor * 4), 1)[0];
                frame = inputs.Execute(frame, new NativeBattleEffect65ModelFlags(model, 0xFFFB, (ushort)(saved & 4)));
            }
        }
        return frame;
    }

    private static NativeBattleEffect65Frame CaptureActors(NativeBattleEffect65Frame frame, uint main, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = Header(frame, packets);
        int count = header.ReadByte(16);
        frame = Write(frame, main, Read(frame, main).WriteWord(14, (ushort)count));
        int[] minimum = [32767, 32767, 32767];
        int[] maximum = [-32767, -32767, -32767];
        for (int index = 0; index < count; index++)
        {
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect65 requires its actual header contact pointer.");
            NativeBattleContactPacket contact = packets.Contacts[first + index] ?? throw new NotSupportedException("Effect65 requires its current contact row.");
            uint model = ModelAddress(contact.Primary.ActorIndex);
            uint row = checked(main + 0x24 + (uint)(index * 12));
            frame = WriteBytes(frame, row, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(model)), 4);
            frame = WriteBytes(frame, row + 4, inputs.ReadPosition8(frame, model), 8);
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
        NativeBattleEffectArena caster = inputs.ReadPosition8(frame, frame.State.ReadDword(CasterGlobal));
        frame = WriteBytes(frame, main + 0x14, caster, 8);
        return WriteBytes(frame, CasterPointAddress, caster, 8);
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

    private static NativeBattleActionHeaderSlot Header(NativeBattleEffect65Frame frame, NativeBattleActionPacketsState packets) => packets.Headers[(frame.Effect.Parameters ?? throw new NotSupportedException("Effect65 requires its actual header pointer.")).HeaderIndex7C];
}
