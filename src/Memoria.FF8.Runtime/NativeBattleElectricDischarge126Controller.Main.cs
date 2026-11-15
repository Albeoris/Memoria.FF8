using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectricDischarge126Controller
{
    public static (NativeBattleElectricDischarge126Frame Frame, int Result) AdvanceMain(NativeBattleElectricDischarge126Frame frame, uint address, NativeBattleElectricDischarge126Resources resources, NativeBattleElectricDischarge126Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        int fade = age < 4 ? age << 9 : age > 48 ? (52 - age) << 9 : 2048;
        if (fade > unchecked((int)frame.State.ReadDword(FadeGlobal)))
        {
            frame = frame with { State = frame.State.WriteDword(FadeGlobal, unchecked((uint)fade)) };
        }
        BattleModelOwner caster = frame.State.ReadModel(CasterGlobal);
        if (unchecked((uint)age) < 8)
        {
            int paths = resources.ReadPathWord(BonePathsAddress);
            for (int index = 0; index < 3; index++)
            {
                (frame, _) = CreateBoneTrail(frame, inputs.ReadBindingReference(caster), BonePathsAddress, (age * 3 + index) % paths, 3, 4096, resources);
            }
        }
        if (age == 0)
        {
            NativeBattleElectricDischarge126AnchorStep anchor = inputs.ReadAnchor571400(frame, caster);
            frame = inputs.Execute(anchor.Frame, new NativeBattleElectricDischarge126SpatialSound(resources.Sound, 256, anchor.Anchor));
        }
        int emissionAge = age - 8;
        if (unchecked((uint)emissionAge) < 52)
        {
            frame = DrawRandom(frame, out ushort selected);
            int paths = resources.ReadPathWord(BonePathsAddress);
            (frame, _) = CreateBoneTrail(frame, inputs.ReadBindingReference(caster), BonePathsAddress, selected * paths >> 15, 3, 4096, resources);
        }
        if (unchecked((uint)emissionAge) < 12)
        {
            NativeBattleElectricDischarge126AnchorStep anchor = inputs.ReadAnchor571400(frame, caster);
            frame = anchor.Frame;
            frame = DrawRandom(frame, out ushort pitch);
            NativeBattleVector direction = NativeBattleElectrocute275Direction.Compute(unchecked((short)((pitch & 1023) - 256)), unchecked((short)(((emissionAge << 10) / 12) + ((emissionAge & 3) << 10))));
            NativeBattleVector source = new(unchecked((short)(anchor.Anchor.X + (direction.X >> 8))), unchecked((short)(anchor.Anchor.Y + (direction.Y >> 8))), unchecked((short)(anchor.Anchor.Z + (direction.Z >> 8))));
            NativeBattleVector target = new(unchecked((short)(source.X + direction.X)), unchecked((short)(source.Y + direction.Y)), unchecked((short)(source.Z + direction.Z)));
            frame = DrawRandom(frame, out ushort vx);
            frame = DrawRandom(frame, out ushort vy);
            frame = DrawRandom(frame, out ushort vz);
            frame = DrawRandom(frame, out ushort texture);
            (frame, uint head) = StartWorld(frame, new((short)((vx & 127) - 64), (short)((vy & 127) - 64), (short)((vz & 127) - 64)), source, target, null, null, inputs.Packets.ModelAliases, 0, 256, 250, (short)(texture & 1), 3);
            frame = ClearHeadVelocity(frame, head);
        }
        int targetAge = age - 14;
        if (unchecked((uint)targetAge) < 30)
        {
            if ((targetAge & 1) == 0)
            {
                frame = DrawRandom(frame, out ushort selected);
                NativeBattleElectricDischarge126PointStep source = inputs.ReadBone502170(frame, caster, resources.ColorIndices[selected * 5 >> 15], 0);
                frame = source.Frame;
                NativeBattleElectricDischarge126AnchorStep casterAnchor = inputs.ReadAnchor571400(frame, caster);
                frame = casterAnchor.Frame;
                BattlePartyModelOwner[] party = inputs.ReadPartyOwners().ToArray();
                BattlePartyModelOwner? actor = null;
                for (int attempt = 0; attempt < party.Length; attempt++)
                {
                    NativeBattleModulePoolTask task = Read(frame, address);
                    int index = task.ReadExecutionReference<BattleExecutionReference.TraversalPosition>(16, 2).Value;
                    if ((uint)index >= (uint)party.Length)
                    {
                        throw new InvalidDataException("Electric Discharge party cycle leaves its current owners.");
                    }
                    BattlePartyModelOwner selectedActor = party[index];
                    int next = index + 1 < party.Length ? index + 1 : 0;
                    ImmutableArray<byte>? original = inputs.Packets.ModelAliases.TryEncodeExact(selectedActor, out byte slot) ? [(byte)((slot + 1) % 3), 0] : null;
                    task.WriteExecutionReference(16, 2, new BattleExecutionReference.TraversalPosition(next), original);
                    frame = Write(frame, address, task);
                    if ((inputs.ReadModel(selectedActor).Flags0 & 2) != 0)
                    {
                        actor = selectedActor;
                        break;
                    }
                }
                if (actor is null)
                {
                    throw new NotSupportedException("Electric Discharge requires an enabled actor in its party cycle.");
                }
                frame = DrawRandom(frame, out ushort selectedTargetBone);
                int bones = inputs.ReadBoneCount(actor);
                if (bones is < 1 or > 255)
                {
                    throw new NotSupportedException("Electric Discharge requires its actual BYTE-sized target skeleton count.");
                }
                NativeBattleElectricDischarge126PointStep target = inputs.ReadBone502170(frame, actor, unchecked((short)(selectedTargetBone * bones >> 15)), 0);
                frame = target.Frame;
                short dy = unchecked((short)(source.Position.Y - casterAnchor.Anchor.Y));
                NativeBattleVector velocity = new(unchecked((short)(source.Position.X - casterAnchor.Anchor.X)), unchecked((short)(dy > 0 ? dy >> 3 : dy >> 1)), unchecked((short)(unchecked((short)(source.Position.Z - casterAnchor.Anchor.Z)) >> 2)));
                frame = DrawRandom(frame, out ushort texture);
                (frame, uint head) = StartWorld(frame, velocity, source.Position, target.Position, null, actor, inputs.Packets.ModelAliases, 0, 256, 250, (short)(texture & 1), 3);
                frame = ClearHeadVelocity(frame, head);
            }
            if ((targetAge & 3) == 0)
            {
                (frame, uint actAddress) = Append(frame, 5, ActCallback);
                if (actAddress == 0)
                {
                    throw new NotSupportedException("Electric Discharge exhausted its actual ACT task bank.");
                }
                NativeBattleModulePoolTask task = Read(frame, actAddress);
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12, task.PayloadLength).Initialize7016B0(resources.Act, 200);
                frame = Write(frame, actAddress, workspace.CopyTo(task, 12, retainUnknown: false));
            }
        }
        if (age >= 52)
        {
            NativeBattleActionHeaderSlot header = Header(frame, inputs);
            frame = inputs.Execute(frame, new NativeBattleElectricDischarge126ContactAll(header.FirstContact, header.ReadContactCount()));
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }
}
