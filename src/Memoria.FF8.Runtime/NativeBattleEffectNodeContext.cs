using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Normalized87E230/88D550/8836F0 task/header and actor/target anchor initialization; bank ownership stays with each caller.</summary>
public sealed record NativeBattleEffectNodeDogOrigins(NativeBattleVector ActorOrigin, NativeBattleVector TargetOrigin);

public static class NativeBattleEffectNodeContext
{
    private const int Context = 0x34;
    private const int OriginalRecipientCount = 4;
    internal const int ReferenceOffset = Context + 4;

    internal static BattleExecutionReference.NodeContext Read(NativeBattleModulePoolTask task) => task.ReadExecutionReference<BattleExecutionReference.NodeContext>(ReferenceOffset, 4);

    public static NativeBattleModulePoolTask Initialize(NativeBattleModulePoolTask task, NativeBattleModulePoolTask root, NativeBattleEffectParameters parameters, NativeBattleActionPacketsState packets, Func<BattleModelOwner, NativeBattleGraphicsModel> readModel, Func<BattleModelOwner, NativeBattlePoseSource>? readPoseSource, uint executableAddress, bool singleSelectedTarget = false, NativeBattleEffectNodeDogOrigins? dogOrigins = null, Action<BattleModelOwner, byte>? observeBone = null, Func<BattleModelOwner, byte, NativeBattleVector?>? readForeignBone = null, Func<NativeBattleModulePoolTask, BattleModelOwner, byte, (NativeBattleModulePoolTask Task, NativeBattleVector Point)>? readBone = null)
    {
        WriteWord(0x22, task.ReadWord(0x29A));
        WriteWord(0x24, task.ReadWord(0x29C));
        Write(0x224, executableAddress + 4);
        Write(0x228, executableAddress + 0x44);
        Write(0x22C, executableAddress + 0x84);
        Write(0x230, executableAddress + 0xC4);
        int count = singleSelectedTarget ? 1 : root.ReadContactCount(0x5A);
        Project(0x1C, 2, new BattleExecutionReference.ContactCount(count).EncodeOriginalWord());
        BattleModelOwner actor = task.ReadExecutionReference<BattleExecutionReference.Model>(0x2C, 1).Owner;
        Project(0x1E, 2, packets.ModelAliases.EncodeOperand(actor, 2));
        BattleActionHeaderReference headerIndex = parameters.Header.Advance(unchecked((sbyte)task.ReadByte(0x2A)));
        BattleContactCursor firstContact = packets.ReadHeader(headerIndex).FirstContact;
        var contacts = ImmutableArray.CreateBuilder<NativeBattleContactPacket>();
        for (int index = 0; index < count; index++)
        {
            NativeBattleContactPacket packet = packets.ReadContact(firstContact.Advance(singleSelectedTarget ? task.ReadTraversalPosition(0x2B) : index));
            contacts.Add(packet);
            if (index < OriginalRecipientCount)
            {
                Project(4 + 4 * index, 4, packets.ModelAliases.EncodeOperand(packet.Primary.Recipient, 4));
            }
        }
        WriteWord(0x1A, task.ReadWord(0x29E));
        short mode = unchecked((short)ReadWord(0x24));
        if (mode == 2 && dogOrigins is null)
        {
            //87E4C0/88D7E0/883980 and their target variants skip502170 in this branch and reuse their original uninitialized stack VECTOR.
            throw new NotSupportedException("Native shared node context mode2 requires the observed prior stack VECTOR at87E4C0/88D7E0/883980 and their target variants.");
        }
        NativeBattleGraphicsModel actorModel = readModel(actor);
        NativeBattleModelTransform actorTransform = actorModel.Transform ?? throw new NotSupportedException("Native87E4C0 requires actorWORD36 and its model transform.");
        NativeBattleVector actorFirst = Bone(actor, 241);
        SetVector(0x1C4, actorFirst with { Y = actorTransform.Angle36 });
        SetVector(0x1D4, actorFirst with { Y = 0 });
        SetVector(0x1E4, Bone(actor, 241));
        SetVector(0x1F4, Bone(actor, 240));
        short actorBound60 = (actorModel.GeometryBounds52 ?? throw new NotSupportedException("Native87E4C0 requires the actual geometry bounds WORD60.")).Maximum.Y;
        SetVector(0x204, actorFirst with { Y = actorBound60 });
        if (mode == 4)
        {
            SetVector(0x1B4, new(Word(task, 0x290), Word(task, 0x292), Word(task, 0x294)));
            foreach (int destination in new[] { 0x204, 0x1F4, 0x1E4, 0x1D4, 0x1C4 })
            {
                CopyVector(0x1B4, destination);
            }
        }
        var recipients = ImmutableArray.CreateBuilder<NativeBattleEffectNodeRecipient>(count);
        for (int index = 0; index < contacts.Count; index++)
        {
            NativeBattleContactPacket contact = contacts[index];
            BattleModelOwner target = contact.Primary.Recipient;
            NativeBattleGraphicsModel model = readModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native87E810 requires targetWORD36 and its model transform.");
            NativeBattleVector first = Bone(target, 241, true);
            NativeBattleEffectNodeVector heading = TargetVector(index, 0x54, first with { Y = transform.Angle36 });
            NativeBattleEffectNodeVector ground = TargetVector(index, 0x94, first with { Y = 0 });
            NativeBattleEffectNodeVector boneF1 = TargetVector(index, 0xD4, Bone(target, 241, true));
            NativeBattleEffectNodeVector boneF0 = TargetVector(index, 0x114, Bone(target, 240, true));
            short bound60 = (model.GeometryBounds52 ?? throw new NotSupportedException("Native87E810 requires target geometry bounds WORD60.")).Maximum.Y;
            NativeBattleEffectNodeVector upperBound = TargetVector(index, 0x154, first with { Y = bound60 });
            BattleContactOccurrence occurrence = contact.Occurrence ?? throw new InvalidDataException("Effect node contact has no occurrence reference.");
            recipients.Add(new(target, occurrence, heading, ground, boneF1, boneF0, upperBound));
        }
        int minimumX = 0;
        int maximumX = 0;
        int minimumZ = 0;
        int maximumZ = 0;
        for (int index = 0; index < count; index++)
        {
            int x = unchecked((int)recipients[index].Heading.X);
            int z = unchecked((int)recipients[index].Heading.Z);
            if (index == 0)
            {
                minimumX = maximumX = x;
                minimumZ = maximumZ = z;
            }
            else
            {
                minimumX = Math.Min(minimumX, x);
                maximumX = Math.Max(maximumX, x);
                minimumZ = Math.Min(minimumZ, z);
                maximumZ = Math.Max(maximumZ, z);
            }
        }
        Write(0x194, unchecked((uint)(unchecked(minimumX + maximumX) / 2)));
        Write(0x198, 0);
        Write(0x19C, unchecked((uint)(unchecked(minimumZ + maximumZ) / 2)));
        if (mode is 1 or 3)
        {
            int source = 0x194;
            if (mode == 3)
            {
                source = 0x1A4;
                SetVector(source, new(Word(task, 0x288), Word(task, 0x28A), Word(task, 0x28C)));
            }
            for (int index = 0; index < count; index++)
            {
                NativeBattleEffectNodeVector vector = ReadVector(source);
                recipients[index] = recipients[index] with { Heading = vector, Ground = vector, BoneF1 = vector, BoneF0 = vector, UpperBound = vector };
                if (index < OriginalRecipientCount)
                {
                    foreach (int destination in new[] { 0x54, 0x94, 0xD4, 0x114, 0x154 })
                    {
                        CopyVector(source, destination + 16 * index);
                    }
                }
            }
        }
        NativeBattleVector actorPosition = actorTransform.Position28;
        if (recipients.Count == 0)
        {
            throw new NotSupportedException("Native87E230 distance requires an observed first target owner.");
        }
        BattleModelOwner firstTargetOwner = recipients[0].Owner;
        NativeBattleModelTransform firstTarget = readModel(firstTargetOwner).Transform ?? throw new NotSupportedException("Native87E230 requires its first target's position WORDs.");
        short dx = unchecked((short)(actorPosition.X - firstTarget.Position28.X));
        short dy = unchecked((short)(actorPosition.Y - firstTarget.Position28.Y));
        short dz = unchecked((short)(actorPosition.Z - firstTarget.Position28.Z));
        int square = unchecked(dx * dx + dy * dy + dz * dz);
        //56BEC0 loads the signed DWORD, FSQRTs and returns the low DWORD of _ftol's signed64-bit truncation.
        uint distance = unchecked((uint)(long)Math.Sqrt(square));
        Write(0, distance);
        task.WriteExecutionReference(ReferenceOffset, 4, new BattleExecutionReference.NodeContext(actor, recipients.MoveToImmutable()), packets.ModelAliases.EncodeOperand(firstTargetOwner, 4));
        return task;

        NativeBattleVector Bone(BattleModelOwner owner, byte selector, bool target = false)
        {
            if (mode == 2)
            {
                return target ? dogOrigins!.TargetOrigin : dogOrigins!.ActorOrigin;
            }
            if (readBone is not null)
            {
                (task, NativeBattleVector point) = readBone(task, owner, selector);
                return point;
            }
            observeBone?.Invoke(owner, selector);
            return readForeignBone?.Invoke(owner, selector) ?? NativeBattlePoseMatrices.ReadBone(readModel(owner), (readPoseSource ?? throw new NotSupportedException("Effect node context requires its reached model pose source."))(owner), selector, 0);
        }
        uint Read(int offset) => task.ReadDword(Context + offset);
        ushort ReadWord(int offset) => task.ReadWord(Context + offset);
        void Write(int offset, uint value) => task = task.WriteDword(Context + offset, value);
        void WriteWord(int offset, ushort value) => task = task.WriteWord(Context + offset, value);
        void Project(int offset, int width, ImmutableArray<byte>? nativeBytes)
        {
            task = nativeBytes is { } bytes ? task.WriteRetainedBytes(Context + offset, bytes.AsSpan()) : task.MarkPayloadUnknown(Context + offset, width);
        }
        NativeBattleEffectNodeVector TargetVector(int index, int lane, NativeBattleVector value)
        {
            if (index < OriginalRecipientCount)
            {
                int offset = lane + 16 * index;
                SetVector(offset, value);
                return ReadVector(offset);
            }
            return new(unchecked((uint)(value.X << 16)), unchecked((uint)(value.Y << 16)), unchecked((uint)(value.Z << 16)), 0);
        }
        NativeBattleEffectNodeVector ReadVector(int offset)
        {
            uint x = Read(offset);
            uint y = Read(offset + 4);
            uint z = Read(offset + 8);
            Span<byte> bytes = stackalloc byte[4];
            Span<bool> known = stackalloc bool[4];
            task.ReadWithKnownness(Context + offset + 12, bytes, known);
            return new(x, y, z, BinaryPrimitives.ReadUInt32LittleEndian(bytes), NativeBattleEffectNodeMemory.KnownBytes(known));
        }
        void SetVector(int offset, NativeBattleVector value)
        {
            Write(offset, unchecked((uint)(value.X << 16)));
            Write(offset + 4, unchecked((uint)(value.Y << 16)));
            Write(offset + 8, unchecked((uint)(value.Z << 16)));
        }
        void CopyVector(int source, int destination)
        {
            NativeBattleEffectNodeVector vector = ReadVector(source);
            Write(destination, vector.X);
            Write(destination + 4, vector.Y);
            Write(destination + 8, vector.Z);
            Write(destination + 12, vector.Padding);
            for (int index = 0; index < 4; index++)
            {
                if (!vector.IsPaddingByteKnown(index))
                {
                    task = task.MarkPayloadUnknown(Context + destination + 12 + index, 1);
                }
            }
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
