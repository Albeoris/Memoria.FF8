using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticTextureTask(int Slot, ushort Flags, ushort State, int? Next, NativeBattleAuxiliaryKind Kind, uint UnknownByteMask, int PayloadLength, uint? ModelPointer, byte? CapturedSequence, int? ProgramOffset, int? ProgramLength, byte? Deadline, int? ModelIndex, ushort? ModelFlags0, ushort? ResourceFlags44, byte? ModelSequence);

public sealed record BattleDiagnosticTextureQuiescence(int Capacity, int PhysicalActiveCount, int? Head, int? Tail, bool Complete, ImmutableArray<BattleDiagnosticTextureTask> Tasks)
{
    public bool IsTerminalQuiescent
    {
        get
        {
            if (!Complete || Capacity != 16 || Tasks.IsDefaultOrEmpty || Tasks.Length > Capacity || PhysicalActiveCount != Tasks.Length || Head is not (>= 0 and < 16) || Tail is not (>= 0 and < 16))
            {
                return false;
            }
            HashSet<int> visited = [];
            int? next = Head;
            foreach (BattleDiagnosticTextureTask row in Tasks)
            {
                if (row is null || row.Slot != next || row.Slot is < 0 or >= 16 || !visited.Add(row.Slot) || (row.Flags & 1) == 0 || row.Kind != NativeBattleAuxiliaryKind.TextureAnimation5057D0 || row.PayloadLength != 32 || (row.UnknownByteMask & 0x20F) != 0 || row.ModelIndex is not (>= 0 and < 7) || row.ModelPointer != 0x1D972C0u + 156u * (uint)row.ModelIndex.Value || row.CapturedSequence is null || row.ProgramOffset is not >= 0 || row.ProgramLength is not > 0 || row.ProgramOffset >= row.ProgramLength || row.Deadline != 255 || row.ModelFlags0 is not ushort flags || (flags & 2) == 0 || row.ResourceFlags44 is not ushort resource || (resource & 0x40) != 0 || row.ModelSequence != row.CapturedSequence)
                {
                    return false;
                }
                next = row.Next;
            }
            return next is null && Tasks[^1].Slot == Tail;
        }
    }
}

public sealed partial class BattleSession
{
    private static BattleDiagnosticTextureQuiescence CaptureDiagnosticTextureQuiescence(NativeBattleGraphicsState graphics)
    {
        NativeBattleAuxiliaryState auxiliary = graphics.Auxiliary;
        ImmutableArray<NativeBattleAuxiliaryTask> slots = auxiliary.Slots;
        if (slots.IsDefault)
        {
            return new(0, 0, auxiliary.Head, auxiliary.Tail, false, []);
        }
        bool complete = slots.Length == 16;
        int active = 0;
        foreach (NativeBattleAuxiliaryTask task in slots)
        {
            if (task is null)
            {
                complete = false;
            }
            else if ((task.Flags & 1) != 0)
            {
                active++;
            }
        }
        ImmutableArray<BattleDiagnosticTextureTask>.Builder rows = ImmutableArray.CreateBuilder<BattleDiagnosticTextureTask>();
        HashSet<int> visited = [];
        int? next = auxiliary.Head;
        while (next is int slot)
        {
            if ((uint)slot >= (uint)slots.Length || rows.Count >= 16 || !visited.Add(slot) || slots[slot] is not { } task)
            {
                complete = false;
                break;
            }
            bool known = task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0 && !task.Payload.IsDefault && task.Payload.Length == 32 && (task.UnknownByteMask & 0x20F) == 0;
            uint? pointer = known ? BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan()) : null;
            byte? sequence = known ? task.Payload[9] : null;
            uint relative = pointer is uint actual ? unchecked(actual - 0x1D972C0u) : uint.MaxValue;
            int? index = relative % 156 == 0 && relative / 156 < 7 ? (int)(relative / 156) : null;
            NativeBattleGraphicsModel? model = index is int selected && !graphics.Models.IsDefault && selected < graphics.Models.Length ? graphics.Models[selected] : null;
            NativeBattleAuxiliaryProgram? program = task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0 ? task.Program : null;
            int? length = program is not null && !program.Source.IsDefault ? program.Source.Length : null;
            byte? deadline = program is not null && !program.Source.IsDefault && (uint)program.Offset < program.Source.Length ? program.Source[program.Offset] : null;
            rows.Add(new(slot, task.Flags, task.State, task.Next, task.Kind, task.UnknownByteMask, task.Payload.IsDefault ? -1 : task.Payload.Length, pointer, sequence, program?.Offset, length, deadline, model?.Index, model?.Flags0, model?.ResourceFlags44, model?.Sequence));
            complete &= (task.Flags & 1) != 0;
            next = task.Next;
        }
        complete &= next is null && rows.Count == active && (rows.Count == 0 ? auxiliary.Head is null && auxiliary.Tail is null : rows[rows.Count - 1].Slot == auxiliary.Tail);
        return new(slots.Length, active, auxiliary.Head, auxiliary.Tail, complete, rows.ToImmutable());
    }
}