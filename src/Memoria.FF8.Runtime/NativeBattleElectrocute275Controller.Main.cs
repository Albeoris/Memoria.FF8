using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectrocute275Controller
{
    private static (NativeBattleElectrocute275Frame Frame, int Result) Main(NativeBattleElectrocute275Frame frame, uint address, NativeBattleElectrocute275Resources resources, NativeBattleElectrocute275Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != MainCallback)
        {
            throw new NotSupportedException("Electrocute reached an unsupported main callback.");
        }
        int age = Signed(task, 12);
        int fade = age < 4 ? age << 9 : age > 60 ? (64 - age) << 9 : 2048;
        if (fade > unchecked((int)frame.State.ReadDword(FadeGlobal)))
        {
            frame = frame with { State = frame.State.WriteDword(FadeGlobal, unchecked((uint)fade)) };
        }
        int caster = Actor(frame.State.ReadDword(CasterGlobal));
        if ((uint)age < 8 && (age & 1) == 0)
        {
            int count = resources.ReadPathWord(BonePathsAddress);
            (frame, _) = CreateBoneTrail(frame, inputs.ReadBindingAddress(caster), BonePathsAddress, age % count, 3, 4096, resources);
        }
        if (age == 0)
        {
            NativeBattleElectrocute275AnchorStep anchor = inputs.ReadAnchor571400(frame, caster);
            frame = inputs.Execute(anchor.Frame, new NativeBattleElectrocute275SpatialSound(resources.Sound, 256, anchor.Anchor));
        }
        if ((uint)(age - 8) < 64 && ((age - 8) & 1) == 0)
        {
            frame = DrawRandom(frame, out ushort random);
            int count = resources.ReadPathWord(BonePathsAddress);
            (frame, _) = CreateBoneTrail(frame, inputs.ReadBindingAddress(caster), BonePathsAddress, random * count >> 15, 3, 4096, resources);
        }
        if ((uint)(age - 8) < 12)
        {
            NativeBattleElectrocute275AnchorStep anchor = inputs.ReadAnchor571400(frame, caster);
            frame = anchor.Frame;
            frame = DrawRandom(frame, out ushort pitch);
            NativeBattleVector direction = NativeBattleElectrocute275Direction.Compute(unchecked((short)((pitch & 1023) + 256)), unchecked((short)(((age - 8) << 10) / 12 + 1536)));
            NativeBattleVector source = new(unchecked((short)(anchor.Anchor.X + (direction.X >> 8))), unchecked((short)(anchor.Anchor.Y + (direction.Y >> 8))), unchecked((short)(anchor.Anchor.Z + (direction.Z >> 8))));
            NativeBattleVector destination = new(unchecked((short)(source.X + direction.X)), unchecked((short)(source.Y + direction.Y)), unchecked((short)(source.Z + direction.Z)));
            frame = DrawRandom(frame, out ushort vx);
            frame = DrawRandom(frame, out ushort vy);
            frame = DrawRandom(frame, out ushort vz);
            frame = DrawRandom(frame, out ushort texture);
            (frame, uint head) = StartWorld(frame, new((short)((vx & 127) - 64), (short)((vy & 127) - 64), (short)((vz & 127) - 64)), source, destination, null, -1, 0, 256, 250, (short)(texture & 1), 3);
            frame = ClearHeadVelocity(frame, head);
        }
        int targetAge = age - 14;
        if ((uint)targetAge < 30)
        {
            if ((targetAge & 1) == 0)
            {
                frame = DrawRandom(frame, out ushort selectedBone);
                short selector = resources.ColorIndices[selectedBone * 6 >> 15];
                NativeBattleElectrocute275PointStep sourceStep = inputs.ReadBone502170(frame, caster, selector, 0);
                frame = sourceStep.Frame;
                NativeBattleElectrocute275AnchorStep casterStep = inputs.ReadAnchor571400(frame, caster);
                frame = casterStep.Frame;
                int targetCount = Signed(Read(frame, address), 14);
                if (targetCount <= 0)
                {
                    throw new InvalidDataException("Electrocute target cycle requires a captured target.");
                }
                int actor = -1;
                for (int attempt = 0; attempt < targetCount; attempt++)
                {
                    task = Read(frame, address);
                    int index = Signed(task, 16);
                    if ((uint)index >= targetCount)
                    {
                        throw new InvalidDataException("Electrocute target cycle leaves its captured contacts.");
                    }
                    actor = inputs.Packets.ReadContact(FirstContact(frame, inputs) + index).Primary.ActorIndex;
                    frame = Write(frame, address, task.WriteWord(16, unchecked((ushort)(index + 1 >= targetCount ? 0 : index + 1))));
                    if ((inputs.ReadModel(actor).Flags0 & 2) != 0)
                    {
                        break;
                    }
                    actor = -1;
                }
                if (actor < 0)
                {
                    throw new NotSupportedException("Electrocute target cycle has no original enabled actor.");
                }
                frame = DrawRandom(frame, out ushort selectedTargetBone);
                int bones = inputs.ReadBoneCount(actor);
                if (bones is < 1 or > 255)
                {
                    throw new InvalidDataException("Electrocute requires its current BYTE-sized target skeleton count.");
                }
                NativeBattleElectrocute275PointStep targetStep = inputs.ReadBone502170(frame, actor, (short)(selectedTargetBone * bones >> 15), 0);
                frame = targetStep.Frame;
                short dy = unchecked((short)(sourceStep.Position.Y - casterStep.Anchor.Y));
                NativeBattleVector velocity = new(unchecked((short)(sourceStep.Position.X - casterStep.Anchor.X)), unchecked((short)(dy > 0 ? dy >> 3 : dy >> 1)), unchecked((short)(unchecked((short)(sourceStep.Position.Z - casterStep.Anchor.Z)) >> 2)));
                frame = DrawRandom(frame, out ushort texture);
                (frame, uint head) = StartWorld(frame, velocity, sourceStep.Position, targetStep.Position, null, (short)actor, 0, 256, 250, (short)(texture & 1), 3);
                frame = ClearHeadVelocity(frame, head);
            }
            if ((targetAge & 3) == 0)
            {
                (frame, uint child) = Append(frame, 5, ActCallback);
                if (child == 0)
                {
                    throw new NotSupportedException("Electrocute reached a full original ACT task bank.");
                }
                NativeBattleModulePoolTask act = Read(frame, child);
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(act, 12, act.PayloadLength).Initialize7016B0(resources.Act, 200);
                frame = Write(frame, child, workspace.CopyTo(act, 12, retainUnknown: false));
            }
        }
        if (age == 44)
        {
            frame = inputs.Execute(frame, new NativeBattleElectrocute275ContactAll());
        }
        if (age >= 64)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }
}
