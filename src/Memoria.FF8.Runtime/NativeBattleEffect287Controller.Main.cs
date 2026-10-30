using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect287Controller
{
    public static NativeBattleEffect287Step AdvanceMain6F31A0(NativeBattleEffect287Frame frame, uint address, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        uint caster = frame.State.ReadDword(CasterGlobal);
        frame = inputs.Execute(frame, new NativeBattleEffect287ModelFlags(caster, 2, 0xFFF7, 0));
        int age = Signed(Read(frame, address), 12);
        if ((uint)age < 75)
        {
            if ((uint)age < 36)
            {
                NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(SourcePointAddress, 8));
                NativeBattleEffect287MacStep point = inputs.PointMac6F3A80(frame, source, NativeBattleEffectArena.Clear().MarkUnknown(0, 16));
                frame = Write(point.Frame, address, CopyTask(Read(point.Frame, address), 16, point.Output16, 16));
            }
            NativeBattleEffectArena point16 = TaskView(Read(frame, address), 16, 16);
            if ((uint)age < 30)
            {
                uint directory = BinaryPrimitives.ReadUInt32LittleEndian(inputs.ReadModelArena9C(frame, caster).Read(100, 4));
                NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(SourcePointAddress, 8));
                frame = NativeBattleEffect287Attachments.Birth6F3D80(frame, directory, TriangleDescriptorsAddress + (uint)(TriangleDescriptorStride * (age % TriangleDescriptorCount)), source, 0, 12, 7, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect287ModelFlags(caster, 1, 255, 8));
            }
            else if ((uint)age < 60)
            {
                if (age is >= 32 and < 38)
                {
                    frame = BirthBoneSprites(frame, inputs);
                }
                if (age == 30)
                {
                    frame = ClearMeshQueue(frame);
                }
                if ((age & 1) == 0)
                {
                    frame = BirthSmallDebris(frame, inputs);
                }
            }
            frame = frame with { State = frame.State.WriteDword(IntensityGlobal, unchecked((uint)StageIntensity(age))) };
            short yaw = BinaryPrimitives.ReadInt16LittleEndian(inputs.ReadModelArena9C(frame, caster).Read(14, 2));
            NativeBattleActionMatrix world = NativeBattlePandemona291Restoration.Rotation701270(yaw) with { X = BinaryPrimitives.ReadInt32LittleEndian(point16.Read(0, 4)), Y = BinaryPrimitives.ReadInt32LittleEndian(point16.Read(4, 4)), Z = BinaryPrimitives.ReadInt32LittleEndian(point16.Read(8, 4)) };
            if ((uint)age < 8)
            {
                frame = BirthEarlySprites(frame, age, world);
            }
            frame = DrawProgrammes(frame, address, age, world, resources, inputs);
        }
        int relative = age - 40;
        if ((uint)relative < 12 && (relative & 3) == 0)
        {
            frame = BirthTarget(frame, address, relative >> 2, resources, inputs);
        }
        return AdvanceTail(frame, address, age, inputs);
    }

    public static NativeBattleEffect287Step AdvanceTail(NativeBattleEffect287Frame frame, uint address, int age, NativeBattleEffect287Inputs inputs)
    {
        uint arena = frame.State.ReadDword(ArenaGlobal);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect287Sound501330(SoundAddress, 0, 128));
        }
        if (age == 55)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect287Queue5341D0(360, arena + File360Offset, 0));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect287Advance534210());
        if (age == 65)
        {
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            NativeBattleActionHeaderSlot header = CurrentHeader(frame, packets);
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect287 requires its current contact pointer.");
            frame = inputs.Execute(frame, new NativeBattleEffect287Contacts506BA0(first, header.ReadByte(16)));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        if (next >= 75)
        {
            return new(frame with { State = frame.State.WriteDword(IntensityGlobal, 0) }, 2);
        }
        return new(frame, 0);
    }

    public static int StageIntensity(int age)
    {
        if (age < 33)
        {
            return 0;
        }
        if (age < 41)
        {
            return (age - 33) << 8;
        }
        if (age < 59)
        {
            return 2048;
        }
        return age < 75 ? (75 - age) << 7 : 0;
    }

    public static NativeBattleEffect287Frame DrawProgrammes(NativeBattleEffect287Frame frame, uint main, int age, NativeBattleActionMatrix world, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        uint morph = frame.State.ReadDword(ArenaGlobal) + MorphOffset;
        NativeBattleEffectArena caller = ProgrammeCaller(inputs.ReadView(frame), world, 0, unchecked((byte)((age & 3) << 5)), morph);
        frame = NativeBattleEffect287Programme.Advance(frame, main + 0x2C, 0x8C4, caller, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(287, Read(frame, main)) : null).Frame;
        uint caster = frame.State.ReadDword(CasterGlobal);
        short secondY = BinaryPrimitives.ReadInt16LittleEndian(inputs.ReadModelArena9C(frame, caster).Read(36, 2));
        caller = ProgrammeCaller(inputs.ReadView(frame), world with { Y = secondY }, 1, unchecked((byte)((-age & 3) << 5)), morph);
        return NativeBattleEffect287Programme.Advance(frame, main + 0x8F0, 0xB8, caller, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(287, Read(frame, main)) : null).Frame;
    }

    public static NativeBattleEffectArena ProgrammeCaller(NativeBattlePackedMatrix32 view, NativeBattleActionMatrix world, byte selector, byte phase, uint morph)
    {
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(view.ReadMatrix(), world);
        return PackedArena(NativeBattlePackedMatrix32.FromMatrix(composed, null)).Write(32, BitConverter.GetBytes((short)-128)).Write(34, new byte[] { selector, phase }).Write(36, BitConverter.GetBytes(morph));
    }

    public static NativeBattleEffect287Frame BirthTarget(NativeBattleEffect287Frame frame, uint main, int order, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        int target = resources.ReadStatic(TargetOrderAddress + (uint)order, 1)[0];
        if (target >= Signed(Read(frame, main), 14))
        {
            return frame;
        }
        (frame, uint child) = Append(frame, TargetDescriptor, 0x6F3D00);
        if (child == 0)
        {
            return frame;
        }
        uint model = Read(frame, main).ReadDword(32 + target * 4);
        NativeBattleEffect287PointStep point = inputs.Anchor571400(frame, model, TaskView(Read(frame, child), 12, 8));
        frame = Write(point.Frame, child, CopyTask(Read(point.Frame, child), 12, point.Output8, 8));
        frame = Write(frame, child, Read(frame, child).WriteWord(16, unchecked((ushort)(Signed(Read(frame, child), 16) - 500))));
        return NativeBattleEffect287Programme.Initialize(frame, child + 20, TargetProgrammeAddress, 0x1BC, resources);
    }
}
