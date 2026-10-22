using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBadBreath300Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBadBreath300Controller
{
    private static (NativeBattleBadBreath300Frame Frame, int Result) AdvanceTarget(NativeBattleBadBreath300Frame frame, uint address, NativeBattleBadBreath300Resources resources, NativeBattleBadBreath300Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = task.ReadByte(15);
        int scale = (Transform(inputs.ReadModel(actor)).Scale38 >> 2) + 512;
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 28).Write(0, task.ReadBytes(0x10, 8));
        context = WriteDword(context, 8, unchecked((uint)scale));
        context = WriteDword(context, 12, unchecked((uint)scale));
        context = WriteDword(context, 16, unchecked((uint)scale));
        NativeBattleStackBytes stack = frame.TargetScaleStack ?? throw new NotSupportedException("Bad Breath target requires its current execution stack owner.");
        if (stack.Known.All(known => known))
        {
            context = WriteDword(context, 20, stack.ReadUInt32(-0x4C));
        }
        context = WriteDword(context, 24, frame.State.ReadDword(WorkingGlobal) + ScratchOffset);
        NativeBattleGfAnimationWorkspace workspace = new(task.ReadBytes(0x18, 0x128).ToImmutableArray());
        NativeBattleGfAnimationAdvance advance = workspace.Advance701970(resources.Target, 0, (current, node) =>
        {
            frame = Write(frame, address, Read(frame, address).WriteBytes(0x18, current.Data.AsSpan()));
            frame = inputs.EmitNode(frame, resources.Target, context, node);
            return new(Read(frame, address).ReadBytes(0x18, 0x128).ToImmutableArray());
        });
        frame = Write(frame, address, Read(frame, address).WriteBytes(0x18, advance.Workspace.Data.AsSpan()));
        int age = unchecked((short)Read(frame, address).ReadWord(12));
        if (age <= 8)
        {
            (frame, uint childAddress) = Append(frame, 3, ParticleCallback);
            if (childAddress != 0)
            {
                frame = DrawRandom(frame, out ushort selector);
                int count = inputs.ReadPoseSource(actor).BoneCount;
                int selected = selector * count >> 15;
                if ((uint)selected > byte.MaxValue)
                {
                    throw new InvalidDataException("Bad Breath selected a bone outside its byte skeleton directory.");
                }
                NativeBattleBadBreath300PointStep point = inputs.ReadBone502170(frame, actor, (byte)selected, 0);
                frame = point.Frame;
                frame = DrawRandom(frame, out ushort random);
                NativeBattleModulePoolTask child = Read(frame, childAddress).WriteWord(12, 0).WriteWord(0x10, unchecked((ushort)point.Position.X)).WriteWord(0x12, unchecked((ushort)point.Position.Y)).WriteWord(0x14, unchecked((ushort)point.Position.Z)).WriteWord(0x16, (ushort)((random & 511) + 1024));
                frame = Write(frame, childAddress, child);
            }
        }
        if (age == 30)
        {
            frame = inputs.Execute(frame, new NativeBattleBadBreath300Contact(FirstContact(frame, inputs) + Read(frame, address).ReadByte(14)));
        }
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        return (frame, unchecked((short)next) < 37 ? 0 : 2);
    }

    private static (NativeBattleBadBreath300Frame Frame, int Result) AdvanceParticle(NativeBattleBadBreath300Frame frame, uint address, NativeBattleBadBreath300Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleBadBreath300DrawParticle(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) - 16))).WriteWord(12, next);
        return (Write(frame, address, task), unchecked((short)next) < 18 ? 0 : 2);
    }
}
