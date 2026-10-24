using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect173Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect173Controller
{
    public static NativeBattleEffect173Step AdvanceMain5D7570(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        int actor = checked((int)frame.State.ReadDword(CasterGlobal));
        NativeBattleEffect173PointStep point = inputs.Point502170(frame, actor, 0xF0, 4096, ReadBytes(frame, AnchorAddress, 8));
        frame = WriteBytes(point.Frame, AnchorAddress, point.Output8, 8);
        var rotated = NativeBattleGfMatrices.RotateLong56C600(inputs.ReadWorld(frame, actor).ReadMatrix(), 0, 0, -150);
        NativeBattleVector anchor = Vector(ReadBytes(frame, AnchorAddress, 8));
        anchor = new(unchecked((short)(anchor.X + rotated.X)), unchecked((short)(anchor.Y + rotated.Y)), unchecked((short)(anchor.Z + rotated.Z)));
        frame = WriteBytes(frame, AnchorAddress, PutPoint(ReadBytes(frame, AnchorAddress, 8), 0, anchor), 8);
        NativeBattleVector target = Vector(ReadBytes(frame, TargetPointAddress, 8));
        short centerX = unchecked((short)((target.X + anchor.X) / 2));
        short centerZ = unchecked((short)((target.Z + anchor.Z) / 2));
        frame = WriteBytes(frame, CenterAddress, PutPoint(ReadBytes(frame, CenterAddress, 8), 0, new(centerX, 0, centerZ)), 8);
        int dx = centerX - anchor.X;
        int dz = centerZ - anchor.Z;
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, 0, dz);
        frame = frame with { State = frame.State.WriteDword(DirectionAddress, unchecked((uint)normalized.X)).WriteDword(DirectionAddress + 4, unchecked((uint)normalized.Y)).WriteDword(DirectionAddress + 8, unchecked((uint)normalized.Z)).WriteDword(LengthGlobal, unchecked((uint)NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dz * dz)))) };
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        if (age == 1)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect173 requires its current target selector.");
            frame = inputs.Execute(frame, new NativeBattleEffect173TargetCenter5020A0(parameters.TargetMask7A, TargetPointAddress));
            frame = InitializeCommonBank(frame);
        }
        if (age == 2)
        {
            (frame, uint child) = AppendRequired(frame, 0x5D7BA0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
        }
        if (age == 14)
        {
            for (int index = 0; index < 4; index++)
            {
                (frame, uint child) = AppendRequired(frame, 0x5D7E70);
                NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 16, ReadBytes(frame, AnchorAddress, 8), 8).WriteWord(12, 0).WriteWord(14, BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(BirthRowsAddress + (uint)(index * 4), 2))).WriteWord(28, 0x1400).WriteWord(30, 0x16D).WriteWord(24, unchecked((ushort)CasterRotation(frame, inputs))).WriteWord(26, BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(0xD61DA0 + (uint)(index * 4), 2)));
                frame = Write(frame, child, task);
            }
        }
        if (age == 8)
        {
            (frame, uint child) = AppendRequired(frame, 0x5D7FE0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(28, 0x280).WriteWord(30, 0xA0));
        }
        if (age == 37)
        {
            (frame, uint child) = AppendRequired(frame, 0x5D80C0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(24, 0x200).WriteWord(26, unchecked((ushort)-34)));
        }
        if (age == 38)
        {
            (frame, uint child) = AppendRequired(frame, 0x5D8E20);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(24, 0x1A0).WriteWord(26, unchecked((ushort)-16)));
        }
        if (age == 70)
        {
            (frame, uint temporary) = Allocate(frame, 172);
            for (int index = 0; index < 5; index++)
            {
                (frame, uint child) = AppendRequired(frame, 0x5D83E0);
                NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 16, ReadBytes(frame, AnchorAddress, 8), 8).WriteWord(12, 0).WriteWord(14, (ushort)index).WriteWord(18, 0).WriteWord(28, 4096).WriteWord(24, unchecked((ushort)((CasterRotation(frame, inputs) - 2048) & 4095)));
                frame = Write(frame, child, task);
                frame = WriteBytes(frame, temporary, NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(MeshSourcesAddress + (uint)(index * 4), 4)), 4);
                frame = WriteBytes(frame, temporary + 36, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(index)), 4);
                frame = NativeBattleEffect173Lower.InitializeCentroids7980(frame, temporary, resources);
            }
            frame = Release(frame, 172);
        }
        if (age == 74)
        {
            (frame, uint child) = AppendRequired(frame, 0x5D8B40);
            frame = Write(frame, child, CopyTask(Read(frame, child), 16, ReadBytes(frame, AnchorAddress, 8), 8).WriteWord(12, 0));
        }
        if (age == 85)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect173 requires current contacts.");
            NativeBattleActionHeaderSlot header = inputs.ReadPackets(frame).Headers[parameters.HeaderIndex7C];
            frame = inputs.Execute(frame, new NativeBattleEffect173AllContacts506BA0(header.FirstContactIndex ?? throw new NotSupportedException("Effect173 requires the actual contact pointer."), header.ReadByte(16)));
        }
        if (age <= 16)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect173Stage5713E0(unchecked(age * 3 << 6), 0x502020));
        }
        else if (age >= 87)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect173Stage5713E0(unchecked((95 - age) * 3 << 7), 0x502020));
        }
        if (age is 1 or 36)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect173Sound501330(age == 1 ? 0xD61D84u : 0xD61D88u, 0, 128));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        if (next > 95)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect173Stage5713E0(0, 0));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    private static short CasterRotation(NativeBattleEffect173Frame frame, NativeBattleEffect173Inputs inputs) => inputs.ReadModel(frame, checked((int)frame.State.ReadDword(CasterGlobal))).Rotation14 ?? throw new NotSupportedException("Effect173 requires current caster rotation14.");

    private static (NativeBattleEffect173Frame Frame, uint Address) AppendRequired(NativeBattleEffect173Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, callback);
        if (address == 0)
        {
            throw new NotSupportedException("Effect173 mandatory child allocation exhausted its current pool.");
        }
        return (frame, address);
    }
}
