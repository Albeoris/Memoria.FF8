using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Lower
{
    public static NativeBattleEffect66Step Advance(NativeBattleEffect66Frame frame, uint callback, uint task, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs) => callback switch
    {
        0x622EB0 or 0x624DF0 or 0x625110 or 0x625470 or 0x626690 or 0x626F80 or 0x627210 or 0x627890 or 0x627BD0 => NativeBattleEffect66Clouds.Advance(frame, callback, task, resources, inputs),
        0x624AE0 => AdvanceBirths624AE0(frame, task, resources, inputs),
        0x624CB0 or 0x6269D0 or 0x626C50 or 0x626E20 or 0x627510 or 0x627730 => AdvanceStatic(frame, callback, task, resources, inputs),
        0x6257F0 => AdvanceBody6257F0(frame, task, inputs),
        0x627CF0 => AdvanceLinked627CF0(frame, task, inputs),
        _ => throw new NotSupportedException("Effect66 reached an unsupported lower callback.")
    };

    public static NativeBattleEffect66Frame Invoke(NativeBattleEffect66Frame frame, uint address, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs) => address switch
    {
        0x625790 => InitializeBody625790(frame),
        0x627B90 => InitializeSparks627B90(frame),
        0x626970 => BirthCenter626970(frame),
        0x626B00 => BirthRings626B00(frame),
        0x626DC0 => BirthFlare626DC0(frame),
        0x6274D0 => BirthOverlay6274D0(frame),
        0x6276F0 => BirthOverlay6276F0(frame),
        0x627840 => InitializeBurst627840(frame),
        _ => throw new NotSupportedException("Effect66 reached an unsupported lower factory.")
    };

    private static bool Paused(NativeBattleEffect66Frame frame, NativeBattleEffect66Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    private static (NativeBattleEffect66Frame Frame, uint Task) Mandatory(NativeBattleEffect66Frame frame, uint callback)
    {
        (frame, uint task) = Append(frame, ChildDescriptor, callback);
        if (task == 0)
        {
            throw new InvalidDataException("Effect66 exhausted its mandatory lower task bank.");
        }
        return (frame, task);
    }

    private static NativeBattleEffect66Frame Random(NativeBattleEffect66Frame frame, out int value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort output);
        value = output;
        return frame with { Random = random };
    }

    private static NativeBattleEffect66Frame CopyCenter(NativeBattleEffect66Frame frame, uint task) => WriteBytes(frame, task + 0x10, ReadBytes(frame, CenterAddress, 8), 8);

    public static NativeBattleEffect66Frame InitializeBody625790(NativeBattleEffect66Frame frame)
    {
        (frame, uint task) = Mandatory(frame, 0x6257F0);
        frame = Write(frame, task, Read(frame, task).WriteWord(12, 0));
        uint arena = frame.State.ReadDword(ArenaGlobal);
        byte[] flags = new byte[0x1184];
        for (int offset = 0; offset < flags.Length; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(flags.AsSpan(offset), 0x40004000);
        }
        frame = WriteBytes(frame, arena + 0x28000, NativeBattleEffectArena.Clear().Write(0, flags), flags.Length);
        frame = frame with { State = frame.State.WriteDword(0x24C9190, arena + 0x28000).WriteDword(0x24C917C, arena + 0x29184).WriteDword(0x24C9180, 0) };
        for (int index = 0; index < 1000; index++)
        {
            frame = WriteBytes(frame, arena + 0x29184 + (uint)(index * 68), NativeBattleEffectArena.Clear(), 2);
        }
        return frame;
    }

    public static NativeBattleEffect66Frame InitializeSparks627B90(NativeBattleEffect66Frame frame)
    {
        (frame, uint task) = Mandatory(frame, 0x627BD0);
        frame = Write(frame, task, Read(frame, task).WriteWord(12, 0));
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(InitializedBase + 4, arena).WriteDword(BaseAddress, 0) };
        for (int index = 0; index < 800; index++)
        {
            frame = WriteBytes(frame, arena + (uint)(index * 24), NativeBattleEffectArena.Clear(), 4);
        }
        return frame;
    }

    public static NativeBattleEffect66Frame BirthCenter626970(NativeBattleEffect66Frame frame)
    {
        frame = frame with { State = frame.State.WriteWord(CenterAddress + 2, unchecked((ushort)(frame.State.ReadWord(CenterAddress + 2) + 2000))) };
        (frame, uint task) = Mandatory(frame, 0x6269D0);
        frame = CopyCenter(frame, task);
        return Write(frame, task, Read(frame, task).WriteWord(12, 0).WriteWord(0x18, 0).WriteWord(0x1A, 30).WriteWord(0x1C, 0x4000).WriteWord(0x1E, 215).WriteWord(0x20, 0x2000));
    }

    public static NativeBattleEffect66Frame BirthRings626B00(NativeBattleEffect66Frame frame)
    {
        (int Duration, int Rise, int Y, int Speed, int Scale, int Vertical, int Fall)[] rows = [(34, 8, -1000, 30, 0x1800, 0x1000, 12), (36, 12, 0, 55, 0x1800, 0xE00, 12), (32, 10, 1200, 40, 0x1000, 0xA00, 10)];
        foreach (var row in rows)
        {
            (frame, uint task) = Mandatory(frame, 0x626C50);
            frame = CopyCenter(frame, task);
            frame = Random(frame, out int angle);
            NativeBattleModulePoolTask record = Read(frame, task);
            record = record.WriteWord(0x12, unchecked((ushort)(Signed(record, 0x12) + row.Y))).WriteWord(12, 0).WriteWord(14, (ushort)row.Duration).WriteWord(0x16, (ushort)row.Rise).WriteWord(0x18, (ushort)(angle & 2047)).WriteWord(0x1A, (ushort)row.Speed).WriteWord(0x1C, (ushort)row.Scale).WriteWord(0x1E, 1).WriteWord(0x20, (ushort)row.Vertical).WriteWord(0x22, (ushort)row.Fall);
            frame = Write(frame, task, record);
        }
        return frame;
    }

    public static NativeBattleEffect66Frame BirthFlare626DC0(NativeBattleEffect66Frame frame)
    {
        (frame, uint task) = Mandatory(frame, 0x626E20);
        frame = CopyCenter(frame, task);
        frame = Random(frame, out int angle);
        NativeBattleModulePoolTask record = Read(frame, task);
        return Write(frame, task, record.WriteWord(0x12, unchecked((ushort)(Signed(record, 0x12) + 280))).WriteWord(12, 0).WriteWord(0x18, (ushort)(angle & 4095)).WriteWord(0x1A, 80).WriteWord(0x1C, 8192).WriteWord(0x1E, 512));
    }

    public static NativeBattleEffect66Frame BirthOverlay6274D0(NativeBattleEffect66Frame frame)
    {
        (frame, uint task) = Mandatory(frame, 0x627510);
        frame = CopyCenter(frame, task);
        return Write(frame, task, Read(frame, task).WriteWord(12, 0).WriteWord(0x1C, 1536).WriteWord(0x1E, 1536).WriteWord(0x20, 32));
    }

    public static NativeBattleEffect66Frame BirthOverlay6276F0(NativeBattleEffect66Frame frame)
    {
        (frame, uint task) = Mandatory(frame, 0x627730);
        frame = CopyCenter(frame, task);
        return Write(frame, task, Read(frame, task).WriteWord(12, 0).WriteWord(0x1C, 469).WriteWord(0x1E, 469));
    }

    public static NativeBattleEffect66Frame InitializeBurst627840(NativeBattleEffect66Frame frame)
    {
        (frame, uint task) = Mandatory(frame, 0x627890);
        frame = Write(frame, task, Read(frame, task).WriteWord(12, 0).WriteWord(0x10, frame.State.ReadWord(CenterAddress)).WriteWord(0x12, 0).WriteWord(0x14, unchecked((ushort)(frame.State.ReadDword(CenterAddress + 4) + 7600))));
        uint arena = frame.State.ReadDword(0x24C917C);
        frame = frame with { State = frame.State.WriteDword(InitializedBase, arena) };
        for (int index = 0; index < 400; index++)
        {
            frame = WriteBytes(frame, arena + (uint)(index * 24), NativeBattleEffectArena.Clear(), 4);
        }
        return frame;
    }

    public static NativeBattleEffect66Step AdvanceBody6257F0(NativeBattleEffect66Frame frame, uint task, NativeBattleEffect66Inputs inputs)
    {
        int age = Signed(Read(frame, task), 12);
        if (age < 160)
        {
            (frame, uint temporary) = Allocate(frame, 176);
            NativeBattleEffectArena context = Context(frame, temporary, 176).Write(0x2C, BitConverter.GetBytes(frame.State.ReadDword(0x24C9190))).Write(0x44, BitConverter.GetBytes(age));
            frame = PublishContext(frame, temporary, context, 176);
            if (age != 0)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect66ModelFlags((int)frame.State.ReadDword(CasterGlobal), 0xFFFF, 4));
            }
            frame = inputs.Execute(frame, new NativeBattleEffect66DrawBody176(temporary));
            frame = Release(frame, 176);
        }
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        ushort next = unchecked((ushort)(Read(frame, task).ReadWord(12) + 1));
        frame = Write(frame, task, Read(frame, task).WriteWord(12, next));
        return new(frame, unchecked((short)next) > 160 ? 2 : 0);
    }

    public static NativeBattleEffect66Step AdvanceLinked627CF0(NativeBattleEffect66Frame frame, uint task, NativeBattleEffect66Inputs inputs)
    {
        if (Signed(Read(frame, task), 12) < 120)
        {
            (frame, uint temporary) = Allocate(frame, 44);
            NativeBattleEffectArena context = Context(frame, temporary, 44).Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes((short)320)).Write(0x1A, BitConverter.GetBytes((short)216)).Write(0x20, BitConverter.GetBytes(uint.MaxValue));
            frame = PublishContext(frame, temporary, context, 44);
            frame = inputs.Execute(frame, new NativeBattleEffect66DrawStageBody44(temporary));
            frame = Release(frame, 44);
        }
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        ushort next = unchecked((ushort)(Read(frame, task).ReadWord(12) + 1));
        frame = Write(frame, task, Read(frame, task).WriteWord(12, next));
        if (unchecked((short)next) <= 120)
        {
            return new(frame, 0);
        }
        return new(inputs.Execute(frame, new NativeBattleEffect66StageGroupFlags(0xFF, 2)), 2);
    }
}
