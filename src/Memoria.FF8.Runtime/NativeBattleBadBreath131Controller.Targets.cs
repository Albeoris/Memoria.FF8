using static Memoria.FF8.Runtime.NativeBattleBadBreath131Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBadBreath131Controller
{
    public static (NativeBattleBadBreath131Frame Frame, int Result) AdvanceTarget(NativeBattleBadBreath131Frame frame, uint address, NativeBattleBadBreath131Resources resources, NativeBattleBadBreath131Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        byte actor = task.ReadByte(15);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, task.ReadRawPayloadSpan().Slice(4, 8));
        if (task.ReadUnknownPayloadRanges() is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                int first = Math.Max(16, range.Offset);
                int end = Math.Min(24, range.Offset + range.Length);
                if (first < end)
                {
                    context = context.MarkUnknown(first - 16, end - first);
                }
            }
        }
        context = context.Write(8, BitConverter.GetBytes(Working(frame) + ScratchOffset));
        NativeBattleBadBreath131ActStep advanced = inputs.AdvanceAct(frame, address, resources.Target, 0x1C, 0x128, context);
        frame = inputs.Execute(advanced.Frame, new NativeBattleBadBreath131ActorFlags(actor, 0, 0x800));
        task = Read(frame, address);
        int age = Signed(task, 12);
        uint color;
        if (unchecked((uint)age) < 8 || unchecked((uint)(age - 30)) <= 8)
        {
            int factor = unchecked((uint)age) < 8 ? age << 9 : (age - 30) << 9;
            short first = unchecked((short)(unchecked((uint)age) < 8 ? factor : 4096 - factor));
            short second = unchecked((short)(4096 - first));
            uint destination = inputs.ReadModel(actor).Color40 ?? throw new NotSupportedException("Bad Breath requires its actual retained target color.");
            NativeBattleWeightedColorStep blended = NativeBattleWeightedColor56CC00.Blend(frame.Memory, resources.ActorColor, task.ReadDword(0x18), first, second, destination, inputs.Colors);
            frame = frame with { Memory = blended.Memory };
            color = blended.Color;
        }
        else
        {
            color = age is >= 0 and <= 30 ? resources.ActorColor : task.ReadDword(0x18);
        }
        frame = inputs.Execute(frame, new NativeBattleBadBreath131ActorColor(actor, color));
        if (age <= 16)
        {
            (frame, uint childAddress) = Append(frame, 3, ParticleCallback);
            if (childAddress != 0)
            {
                frame = DrawRandom(frame, out ushort selector);
                int bones = inputs.ReadBoneCount(actor);
                if (bones is < 1 or > 255)
                {
                    throw new NotSupportedException("Bad Breath requires its original BYTE skeleton count.");
                }
                NativeBattleBadBreath131PointStep point = inputs.ReadBone(frame, actor, unchecked((short)(selector * bones >> 15)), 0);
                frame = point.Frame;
                frame = DrawRandom(frame, out ushort random);
                NativeBattleModulePoolTask child = Vector(Read(frame, childAddress), 0x10, point.Position).WriteWord(12, 0).WriteWord(0x16, unchecked((ushort)((random & 511) + 1024)));
                frame = Write(frame, childAddress, child);
            }
        }
        if (age == 40)
        {
            frame = inputs.Execute(frame, new NativeBattleBadBreath131Contact(Read(frame, address).ReadByte(14)));
        }
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        if (unchecked((short)next) >= 47)
        {
            frame = inputs.Execute(frame, new NativeBattleBadBreath131ActorFlags(actor, 0x800, 0));
            return (frame, 2);
        }
        return (frame, 0);
    }

    public static (NativeBattleBadBreath131Frame Frame, int Result) AdvanceParticle(NativeBattleBadBreath131Frame frame, uint address, NativeBattleBadBreath131Resources resources, NativeBattleBadBreath131Inputs inputs)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Bad Breath requires its physical sprite temporary."), 180);
        frame = frame with { Staging = allocation.Staging };
        NativeBattleModulePoolTask task = Read(frame, address);
        short scale = Signed(task, 0x16);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadCamera(frame), Vector(task, 0x10), scale, scale, unchecked((short)-(scale >> 2))) };
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(frame.Staging ?? throw new NotSupportedException("Bad Breath lost its physical sprite temporary."), allocation.Address, resources.ParticleSprite.Address, 0).Write(allocation.Address + 4, BitConverter.GetBytes(task.ReadWord(12)));
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleBadBreath131DrawSprite180(allocation.Address, resources.ParticleSprite.Address));
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging ?? throw new NotSupportedException("Bad Breath lost its physical sprite temporary."), 180) };
        task = Read(frame, address);
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) - 16))).WriteWord(12, next);
        return (Write(frame, address, task), unchecked((short)next) < 18 ? 0 : 2);
    }
}
