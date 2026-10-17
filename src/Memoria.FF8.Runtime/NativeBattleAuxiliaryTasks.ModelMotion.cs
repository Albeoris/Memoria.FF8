using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    public (NativeBattleAuxiliaryState State, bool Allocated) AppendModelMotion(byte opcode, int source, int target, byte first, byte second, short fraction, byte duration)
    {
        Validate();
        if (opcode is not (0x9E or 0xAD or 0xAE) || (uint)source >= 7 || (uint)target >= 7)
        {
            throw new ArgumentOutOfRangeException(nameof(opcode), "Native motion requires its actual model bank and producer opcode.");
        }
        int slot = Enumerable.Range(0, 16).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            return (this, false);
        }
        byte[] payload = Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 0x1D972C0u + 156u * (uint)source);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), 0x1D972C0u + 156u * (uint)target);
        uint written;
        if (opcode == 0x9E)
        {
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(8), duration);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10), 0);
            written = 0xFFF;
        }
        else
        {
            payload[8] = first;
            payload[9] = opcode == 0xAD ? (byte)255 : second;
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10), fraction);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), duration);
            written = 0xFFFF;
        }
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = opcode == 0x9E ? NativeBattleAuxiliaryKind.MoveToModel50F750 : NativeBattleAuxiliaryKind.AttachModel50F500, Payload = payload.ToImmutableArray(), UnknownByteMask = Slots[slot].UnknownByteMask & ~written };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return (this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot }, true);
    }

    private static (NativeBattleAuxiliaryTask Task, bool Release) AdvanceModelMotion(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        uint written = task.Kind == NativeBattleAuxiliaryKind.MoveToModel50F750 ? 0xFFFu : 0xFFFFu;
        if ((task.UnknownByteMask & written) != 0)
        {
            throw new InvalidDataException("Native model motion lacks its written physical task operands.");
        }
        Func<int, NativeBattleGraphicsModel> read = inputs?.ReadModel ?? throw new NotSupportedException("Native model motion requires its current model bank.");
        Action<int, NativeBattleGraphicsModel> write = inputs?.WriteModel ?? throw new NotSupportedException("Native model motion requires its model publication owner.");
        byte[] payload = task.Payload.ToArray();
        int sourceIndex = MotionModel(payload, 0);
        int targetIndex = MotionModel(payload, 4);
        NativeBattleGraphicsModel source = read(sourceIndex);
        NativeBattleGraphicsModel target = read(targetIndex);
        bool release;
        if (task.Kind == NativeBattleAuxiliaryKind.MoveToModel50F750)
        {
            short elapsed = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(10)) + 1));
            short duration = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(8));
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10), elapsed);
            int ratio = (elapsed << 12) / duration;
            NativeBattleModelTransform transform = source.Transform ?? throw new NotSupportedException("Native50F750 requires its source transform.");
            NativeBattleVector destination = target.Transform?.Position28 ?? throw new NotSupportedException("Native50F750 requires its target position.");
            NativeBattleVector position = transform.Position28 with { X = unchecked((short)(transform.Initial20.X + (unchecked(ratio * (destination.X - transform.Initial20.X)) >> 12))), Z = unchecked((short)(transform.Initial20.Z + (unchecked(ratio * (destination.Z - transform.Initial20.Z)) >> 12))) };
            release = elapsed >= duration;
            write(sourceIndex, source with { Transform = transform with { Position28 = position, Initial20 = release ? transform.Initial20 with { X = position.X, Z = position.Z } : transform.Initial20 } });
        }
        else
        {
            Func<int, byte, short, NativeBattleVector> bone = inputs?.ReadBone ?? throw new NotSupportedException("Native50F500 requires its current bone matrices.");
            short fraction = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(10));
            byte first = payload[8];
            NativeBattleVector desired;
            if (first == 255)
            {
                desired = target.Transform?.Position28 ?? throw new NotSupportedException("Native50F500 requires its target position.");
            }
            else
            {
                desired = bone(sourceIndex, (byte)(first & 127), fraction);
                if (payload[9] != 255)
                {
                    NativeBattleVector second = bone(sourceIndex, payload[9], fraction);
                    desired = new(unchecked((short)((desired.X + second.X) / 2)), unchecked((short)((desired.Y + second.Y) / 2)), unchecked((short)((desired.Z + second.Z) / 2)));
                }
            }
            NativeBattleVector anchor = bone(targetIndex, first >= 128 ? (byte)242 : (byte)241, 2048);
            NativeBattleActionMatrix matrix = target.GeometryWorldMatrix ?? throw new NotSupportedException("Native50F500 requires its retained model matrix.");
            uint remaining = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(12)) - 1);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), remaining);
            release = remaining == 0;
            ushort flags = target.Flags2 ?? throw new NotSupportedException("Native50F500 requires its target flagsWORD2.");
            write(targetIndex, target with { GeometryWorldMatrix = matrix with { X = unchecked(matrix.X + desired.X - anchor.X), Y = unchecked(matrix.Y + desired.Y - anchor.Y), Z = unchecked(matrix.Z + desired.Z - anchor.Z) }, Flags2 = release ? (ushort)(flags & ~0x20) : flags });
        }
        return (task with { Payload = payload.ToImmutableArray() }, release);
    }

    private static int MotionModel(byte[] payload, int offset)
    {
        uint relative = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset)) - 0x1D972C0u);
        if (relative % 156 != 0 || relative / 156 >= 7)
        {
            throw new NotSupportedException("Native model motion pointer lies outside its seven-model bank.");
        }
        return (int)(relative / 156);
    }
}
