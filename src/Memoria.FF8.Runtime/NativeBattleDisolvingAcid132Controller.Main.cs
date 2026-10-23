using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDisolvingAcid132Controller
{
    public static (NativeBattleDisolvingAcid132Frame Frame, int Result) AdvanceMain(NativeBattleDisolvingAcid132Frame frame, uint address, NativeBattleDisolvingAcid132Resources resources, NativeBattleDisolvingAcid132Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age == 10)
        {
            frame = inputs.Execute(frame, new NativeBattleDisolvingAcid132Sound(resources.Sound, 0, 128));
        }
        int phase = age - 20;
        if ((uint)phase < 10 && (phase & 1) == 0)
        {
            NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, resources.EmissionVectors.AsSpan().Slice((phase >> 1) * 16, 8));
            NativeBattleDisolvingAcid132PointStep emitter = inputs.ReadEmitter6B2B70(frame, row);
            frame = emitter.Frame;
            uint target = Read(frame, address).ReadDword(16);
            frame = DrawRandom(frame, out ushort random);
            byte selector = checked((byte)((random * inputs.ReadModelBoneCount(target)) >> 15));
            NativeBattleDisolvingAcid132PointStep recipient = inputs.ReadBone502170(frame, target, selector, 0);
            frame = recipient.Frame;
            NativeBattlePackedMatrix32 world = inputs.ReadModelMatrix(frame, frame.State.ReadDword(CasterGlobal));
            NativeBattleEffectArena sourceVector = NativeBattleEffectArena.Clear().Write(0, resources.EmissionVectors.AsSpan().Slice((phase >> 1) * 16 + 8, 8));
            byte[] vector = sourceVector.Read(0, 6);
            NativeBattleVector direction = new(BinaryPrimitives.ReadInt16LittleEndian(vector), BinaryPrimitives.ReadInt16LittleEndian(vector.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(vector.AsSpan(4)));
            direction = NativeBattleGfMatrices.RotateShort56C4F0(world.ReadMatrix(), direction);
            (frame, _) = NativeBattleDisolvingAcid132Trail.Create6B1EC0(frame, PointView(emitter.Point), PointView(recipient.Point), unchecked((short)Actor(target)), 150, PointView(direction), sourceVector, world.ReadMatrix());
        }
        if (age >= 45)
        {
            return (inputs.Execute(frame, new NativeBattleDisolvingAcid132Contact(0)), 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static NativeBattleDisolvingAcid132Frame CreatePrimaryAct(NativeBattleDisolvingAcid132Frame frame, NativeBattleEffectArena point, NativeBattleDisolvingAcid132Resources resources)
    {
        (frame, uint address) = Append(frame, 2, PrimaryActCallback);
        if (address == 0)
        {
            throw new NotSupportedException("Disolving Acid exhausted its actual ACT task bank.");
        }
        NativeBattleModulePoolTask task = CopyTask(Read(frame, address).WriteWord(12, 0), 16, point, 8);
        frame = DrawRandom(frame, out ushort random);
        task = InitializeWorkspace(task, 0x18, 0xB0, (random & 1) != 0 ? resources.PrimaryAct : resources.SecondaryAct);
        return Write(frame, address, task);
    }

    public static NativeBattleDisolvingAcid132Frame CreateImpactAct(NativeBattleDisolvingAcid132Frame frame, NativeBattleEffectArena point, NativeBattleDisolvingAcid132Resources resources)
    {
        (frame, uint address) = Append(frame, 2, ImpactActCallback);
        if (address == 0)
        {
            throw new NotSupportedException("Disolving Acid exhausted its actual ACT task bank.");
        }
        NativeBattleModulePoolTask task = CopyTask(Read(frame, address).WriteWord(12, 0), 16, point, 8);
        return Write(frame, address, InitializeWorkspace(task, 0x18, 0x78, resources.ImpactAct));
    }

    public static NativeBattleModulePoolTask CopyTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }
}
