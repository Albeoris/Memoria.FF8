using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDegenerator306Controller
{
    private static (NativeBattleDegenerator306Frame Frame, int Result) Main(NativeBattleDegenerator306Frame frame, uint address, NativeBattleDegenerator306Resources resources, NativeBattleDegenerator306Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        int caster = checked((int)frame.State.ReadDword(CasterGlobal));
        int target = checked((int)frame.State.ReadDword(TargetGlobal));
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306BindCaster((byte)caster, BindingSave, 0xCE454C));
            frame = inputs.Execute(frame, new NativeBattleDegenerator306RingSequence((byte)caster, 2));
        }
        if (age == 10)
        {
            NativeBattleDegenerator306PointStep first = inputs.ReadBone502170(frame, caster, 25, 4096);
            NativeBattleDegenerator306PointStep second = inputs.ReadBone502170(first.Frame, caster, 26, 4096);
            frame = second.Frame;
            NativeBattleVector middle = new(unchecked((short)((first.Position.X + second.Position.X) >> 1)), unchecked((short)((first.Position.Y + second.Position.Y) >> 1)), unchecked((short)((first.Position.Z + second.Position.Z) >> 1)));
            NativeBattleActionMatrix world = inputs.ReadModel(caster).GeometryWorldMatrix ?? throw new NotSupportedException("Degenerator requires its caster world matrix.");
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(world, new(0, 0, -50));
            frame = StorePoint(frame, AnchorGlobal, new(unchecked((short)(middle.X + offset.X)), unchecked((short)(middle.Y + offset.Y)), unchecked((short)(middle.Z + offset.Z))));
            (frame, uint child) = Spawn(frame, 0x5A3490, AnchorGlobal);
            frame = Write(frame, child, Read(frame, child).WriteWord(0x18, unchecked((ushort)Yaw(inputs.ReadModel(caster)))).WriteWord(0x1C, 0x700).WriteWord(0x1E, 15));
        }
        if (age is >= 10 and <= 100 && age % 3 == 1)
        {
            (frame, uint child) = Spawn(frame, 0x5A35D0, AnchorGlobal);
            frame = Write(frame, child, Read(frame, child).WriteWord(0x18, unchecked((ushort)Yaw(inputs.ReadModel(caster)))).WriteWord(0x1C, 0x1C0).WriteWord(0x1E, 0x1C0));
        }
        if (age == 10)
        {
            (frame, _) = Spawn(frame, 0x5A3710, AnchorGlobal);
        }
        if (age == 32)
        {
            frame = SpawnCamera(frame, 16, 0x2E0);
            frame = SpawnCamera(frame, -32, 0x360);
        }
        if (age == 42)
        {
            if (target < 3)
            {
                NativeBattleDegenerator306PointStep point = inputs.ReadBone502170(frame, target, 0xF1, 0);
                frame = StorePoint(point.Frame, 0x2244210, point.Position);
            }
            else
            {
                NativeBattleDegenerator306AnchorStep point = inputs.ReadAnchor571400(frame, target);
                frame = point.Frame with { State = point.Frame.State.WriteView(0x2244210, Packed(point.Anchor), 8) };
            }
            short scale = unchecked((short)(Transform(inputs.ReadModel(target)).Scale38 >> 1));
            frame = SpawnTargetMesh(frame, 0x5A53B0, 20, scale, unchecked((short)(scale / 18)), 0);
            frame = SpawnTargetMesh(frame, 0x5A53B0, -20, scale, unchecked((short)(scale / 18)), 0);
        }
        if (age == 62)
        {
            short scale = unchecked((short)(Transform(inputs.ReadModel(target)).Scale38 * 2));
            frame = SpawnTargetMesh(frame, 0x5A3970, 40, scale, unchecked((short)(scale / 40)), 0);
            frame = SpawnTargetMesh(frame, 0x5A3970, 60, scale, unchecked((short)(scale / 40)), 2048);
        }
        bool miss = (inputs.Packets.ReadContact(FirstContact(frame, inputs)).Primary.HitFlags3 & 4) != 0;
        if (age == 72 && !miss)
        {
            (frame, _) = Spawn(frame, 0x5A47C0, 0x2244210);
            (frame, uint child) = Spawn(frame, 0x5A5540, 0x2244210);
            frame = DrawRandom(frame, out ushort random);
            frame = Write(frame, child, Read(frame, child).WriteWord(14, (ushort)target).WriteWord(0x18, (ushort)(((random & 1) + 1) << 12)));
        }
        if (age == 101 && !miss)
        {
            (frame, _) = Spawn(frame, 0x5A4D80, 0x2244210);
        }
        if (age == 117)
        {
            if (!miss)
            {
                frame = inputs.Execute(frame, new NativeBattleDegenerator306ActorFlags((byte)target, 0, 4));
            }
            frame = inputs.Execute(frame, new NativeBattleDegenerator306Contact(FirstContact(frame, inputs)));
        }
        if (age == 71)
        {
            (frame, uint child) = AppendPool(frame, 0, CompletionCallback);
            if (child == 0)
            {
                throw new NotSupportedException("Degenerator requires its actual second root task slot.");
            }
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age == 0)
        {
            frame = NativeBattleDegenerator306Actors.Schedule5A51B0(frame, 8, inputs);
        }
        if (age <= 8)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306Stage(unchecked((short)(age << 8)), 0));
        }
        if (age is >= 10 and < 26)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306Stage(unchecked((short)(78 * age + 1268)), 0));
        }
        if (age == 26)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306StageFlags(2, 0));
        }
        if (age == 114)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306StageFlags(0, 2));
            frame = NativeBattleDegenerator306Actors.Schedule5A5010(frame, 8, inputs);
        }
        if (age >= 114)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306Stage(unchecked((short)((122 - age) << 9)), 0));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306Sound(resources.Sound, 128));
        }
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        if (unchecked((short)next) > 122)
        {
            frame = inputs.Execute(frame, new NativeBattleDegenerator306Stage(0, 0));
            return (frame, 2);
        }
        return (frame, 0);
    }

    private static NativeBattleDegenerator306Frame SpawnCamera(NativeBattleDegenerator306Frame frame, short speed, ushort scale)
    {
        (frame, uint child) = Append(frame, 0x5A3840);
        frame = DrawRandom(frame, out ushort random);
        return Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(0x18, (ushort)(random & 4095)).WriteWord(0x1A, unchecked((ushort)speed)).WriteWord(0x1C, scale));
    }
    private static NativeBattleDegenerator306Frame SpawnTargetMesh(NativeBattleDegenerator306Frame frame, uint callback, short speed, short scale, short decrement, ushort tilt)
    {
        (frame, uint child) = Spawn(frame, callback, 0x2244210);
        frame = DrawRandom(frame, out ushort random);
        NativeBattleModulePoolTask task = Read(frame, child).WriteWord(0x18, (ushort)(random & 4095)).WriteWord(0x1A, unchecked((ushort)speed)).WriteWord(0x1C, unchecked((ushort)scale)).WriteWord(0x1E, unchecked((ushort)decrement));
        if (callback == 0x5A3970)
        {
            task = task.WriteWord(0x20, tilt);
        }
        return Write(frame, child, task);
    }
    private static (NativeBattleDegenerator306Frame Frame, uint Address) Spawn(NativeBattleDegenerator306Frame frame, uint callback, uint source)
    {
        (frame, uint address) = Append(frame, callback);
        (byte[] bytes, bool[] known) = frame.State.ReadView(source, 8).ReadSnapshot(0, 8);
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, 0).WriteBytes(0x10, bytes);
        for (int index = 0; index < 8; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(0x10 + index, 1);
            }
        }
        return (Write(frame, address, task), address);
    }
    private static NativeBattleDegenerator306Frame StorePoint(NativeBattleDegenerator306Frame frame, uint address, NativeBattleVector point) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)point.X)).WriteWord(address + 2, unchecked((ushort)point.Y)).WriteWord(address + 4, unchecked((ushort)point.Z)) };
    private static NativeBattleEffectArena Packed(NativeBattleModuleAnchor point) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)).Write(6, BitConverter.GetBytes(point.W));
    private static short Signed(NativeBattleModulePoolTask task, int field) => unchecked((short)task.ReadWord(field));
    private static NativeBattleModelTransform Transform(NativeBattleGraphicsModel model) => model.Transform ?? throw new NotSupportedException("Degenerator requires its current actor transform.");
    private static short Yaw(NativeBattleGraphicsModel model) => model.Rotation14 ?? throw new NotSupportedException("Degenerator requires its current actor yaw.");
}
