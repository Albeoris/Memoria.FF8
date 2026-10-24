using static Memoria.FF8.Runtime.NativeBattleEffect174Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect174Controller
{
    public static NativeBattleEffect174Step AdvanceMain5D4660(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs)
    {
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        int caster = checked((int)frame.State.ReadDword(CasterGlobal));
        if (age == 10)
        {
            (frame, uint child) = AppendRequired(frame, ChildDescriptor, 0x5D4CB0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect174SetResourceFlags44(caster, 0x1F));
        }
        if (age == 9)
        {
            (frame, uint child) = AppendRequired(frame, ChildDescriptor, 0x5D4ED0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
            for (int index = 0; index < 5; index++)
            {
                frame = WriteBytes(frame, HistoryAddress + (uint)(index * HistoryStride), NativeBattleEffectArena.Clear(), 4);
            }
        }
        if (age == 68)
        {
            NativeBattleEffect174PointStep point = inputs.Point502170(frame, caster, 0x19, 0x2C00, TaskView(Read(frame, address), 16, 8));
            frame = WriteBytes(point.Frame, address + 16, point.Output8, 8);
            frame = Write(frame, address, Read(frame, address).WriteWord(18, unchecked((ushort)(Signed(Read(frame, address), 18) - 80))));
            (frame, uint child) = AppendPointChild(frame, address, 0x5D51C0);
            frame = Write(frame, child, Read(frame, child).WriteWord(28, 0x700).WriteWord(30, 30).WriteWord(24, unchecked((ushort)CasterRotation(frame, inputs))));
        }
        if (age >= 68 && age <= 98 && age % 3 == 1)
        {
            (frame, uint child) = AppendPointChild(frame, address, 0x5D52F0);
            frame = Write(frame, child, Read(frame, child).WriteWord(24, unchecked((ushort)CasterRotation(frame, inputs))).WriteWord(28, 0x1C0).WriteWord(30, 0x1C0));
        }
        if (age == 68)
        {
            (frame, _) = AppendPointChild(frame, address, 0x5D5430);
        }
        if (age == 90)
        {
            (frame, uint first) = AppendRequired(frame, ChildDescriptor, 0x5D5560);
            int firstAngle = RollMain(ref frame) % 4096;
            frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(24, (ushort)firstAngle).WriteWord(26, 16).WriteWord(28, 0x2E0));
            (frame, uint second) = AppendRequired(frame, ChildDescriptor, 0x5D5560);
            int secondAngle = RollMain(ref frame) % 4096;
            frame = Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(24, (ushort)secondAngle).WriteWord(26, unchecked((ushort)-32)).WriteWord(28, 0x360));
        }
        int target = checked((int)frame.State.ReadDword(TargetGlobal));
        if (age == 100)
        {
            NativeBattleEffectArena retained = TaskView(Read(frame, address), 16, 8);
            NativeBattleEffect174PointStep point = target < 3 ? inputs.Point502170(frame, target, 0xF1, 0, retained) : inputs.Anchor571400(frame, target, retained);
            frame = WriteBytes(point.Frame, address + 16, point.Output8, 8);
            for (int index = 0; index < 2; index++)
            {
                (frame, uint child) = AppendPointChild(frame, address, 0x5D7090);
                int angle = RollMain(ref frame) % 4096;
                short radius = unchecked((short)(TargetScale(frame, inputs) >> 1));
                frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)angle).WriteWord(26, unchecked((ushort)(index == 0 ? 20 : -20))).WriteWord(28, unchecked((ushort)radius)).WriteWord(30, unchecked((ushort)(radius / 18))));
            }
        }
        if (age == 120)
        {
            for (int index = 0; index < 2; index++)
            {
                (frame, uint child) = AppendPointChild(frame, address, 0x5D5690);
                int angle = RollMain(ref frame) % 4096;
                short radius = unchecked((short)(TargetScale(frame, inputs) * 2));
                frame = Write(frame, child, Read(frame, child).WriteWord(24, (ushort)angle).WriteWord(26, (ushort)(index == 0 ? 40 : 60)).WriteWord(32, (ushort)(index == 0 ? 0 : 2048)).WriteWord(28, unchecked((ushort)radius)).WriteWord(30, unchecked((ushort)(radius / 40))));
            }
        }
        if (age == 130 && (FirstContact(frame, inputs).Packet.Primary.HitFlags3 & 4) == 0)
        {
            (frame, _) = AppendPointChild(frame, address, 0x5D64E0);
            (frame, uint child) = AppendPointChild(frame, address, 0x5D7220);
            frame = Write(frame, child, Read(frame, child).WriteWord(14, unchecked((ushort)target)));
            int angle = (RollMain(ref frame) % 2 + 1) << 12;
            frame = Write(frame, child, Read(frame, child).WriteWord(24, unchecked((ushort)angle)));
        }
        if (age == 159 && (FirstContact(frame, inputs).Packet.Primary.HitFlags3 & 4) == 0)
        {
            (frame, _) = AppendPointChild(frame, address, 0x5D6AA0);
        }
        if (age == 175)
        {
            (int contactIndex, NativeBattleContactPacket packet) = FirstContact(frame, inputs);
            if ((packet.Primary.HitFlags3 & 4) == 0)
            {
                ushort flags = inputs.ReadModel(frame, target).Flags0;
                frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(target, 0, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((ushort)(flags | 4)))), 2));
            }
            frame = inputs.Execute(frame, new NativeBattleEffect174FirstContact506690(contactIndex));
        }
        if (age == 129)
        {
            (frame, uint cleanup) = AppendRequired(frame, RootDescriptor, CleanupCallback);
            frame = Write(frame, cleanup, Read(frame, cleanup).WriteWord(12, 0));
        }
        if (age == 0)
        {
            frame = NativeBattleEffect174Lower.InitializeActorFade6EE0(frame, 32, inputs);
        }
        if (age <= 32)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect174Stage5713E0(unchecked(age << 6), 0));
        }
        if (age >= 68 && age < 83)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect174Stage5713E0(unchecked(age * 136 - 7200), 0));
        }
        if (age == 83)
        {
            frame = NativeBattleEffect174Lower.SetStageFlags5540(frame, inputs);
        }
        if (age == 172)
        {
            frame = NativeBattleEffect174Lower.SetStageFlags5520(frame, inputs);
            frame = NativeBattleEffect174Lower.InitializeActorFade6D30(frame, 8, inputs);
        }
        if (age >= 172)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect174Stage5713E0(unchecked((180 - age) << 9), 0));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect174Sound501330(SoundAddress, 0, 128));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        if (next > 180)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect174Stage5713E0(0, 0));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    private static (NativeBattleEffect174Frame Frame, uint Address) AppendRequired(NativeBattleEffect174Frame frame, uint descriptor, uint callback)
    {
        (frame, uint address) = Append(frame, descriptor, callback);
        if (address == 0)
        {
            throw new NotSupportedException("Effect174 mandatory allocation exhausted its current physical task bank.");
        }
        return (frame, address);
    }

    private static (NativeBattleEffect174Frame Frame, uint Address) AppendPointChild(NativeBattleEffect174Frame frame, uint parent, uint callback)
    {
        (frame, uint address) = AppendRequired(frame, ChildDescriptor, callback);
        return (Write(frame, address, CopyTask(Read(frame, address), 16, TaskView(Read(frame, parent), 16, 8), 8).WriteWord(12, 0)), address);
    }

    private static short CasterRotation(NativeBattleEffect174Frame frame, NativeBattleEffect174Inputs inputs) => inputs.ReadModel(frame, checked((int)frame.State.ReadDword(CasterGlobal))).Rotation14 ?? throw new NotSupportedException("Effect174 requires current caster rotation14.");
    private static short TargetScale(NativeBattleEffect174Frame frame, NativeBattleEffect174Inputs inputs) => inputs.ReadModel(frame, checked((int)frame.State.ReadDword(TargetGlobal))).Transform?.Scale38 ?? throw new NotSupportedException("Effect174 requires current target scale38.");

    private static int RollMain(ref NativeBattleEffect174Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    private static (int Index, NativeBattleContactPacket Packet) FirstContact(NativeBattleEffect174Frame frame, NativeBattleEffect174Inputs inputs)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect174 requires its current header.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int index = packets.Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect174 requires the actual first contact pointer.");
        return (index, packets.ReadContact(index));
    }
}
