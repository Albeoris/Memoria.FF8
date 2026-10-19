using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeBattleAuxiliaryKind
{
    None,
    CameraBlend509930,
    SoundParticle50F830,
    TextureAnimation5057D0,
    MusicLoad501A70,
    CommonTextureLoad508660,
    OutcomeFade501D10,
    MoveToModel50F750,
    AttachModel50F500,
    CameraShake50F6C0,
    ModelSequence505C70,
    ModelStatus50A000,
    StageFade501F90,
    ModelGroupFade50F0E0,
    ModelDeformation50F2E0,
    ModelResourceFlags50B080,
    DeferredCompletion5086E0,
    ScreenBlend501E80
}

public sealed record NativeBattleAuxiliaryTask([property: JsonRequired] ushort Flags, [property: JsonRequired] ushort State, [property: JsonRequired] int? Next, [property: JsonRequired] NativeBattleAuxiliaryKind Kind, [property: JsonRequired] ImmutableArray<byte> Payload)
{
    [JsonRequired] public NativeBattleAuxiliaryProgram? Program { get; init; }
    [JsonRequired] public uint UnknownByteMask { get; init; }
}

public sealed record NativeBattleAuxiliaryStep(NativeBattleAuxiliaryState State, ushort? CameraBlend)
{
    public ImmutableArray<NativeBattleAuxiliaryEmission> Emissions { get; init; } = [];
}

/// <summary>Shared506BF0 auxiliary list: sixteen44-byte slots, distinct from the twenty32-byte contact effects.</summary>
public sealed partial record NativeBattleAuxiliaryState([property: JsonRequired] ImmutableArray<NativeBattleAuxiliaryTask> Slots, [property: JsonRequired] int? Head, [property: JsonRequired] int? Tail)
{
    public static NativeBattleAuxiliaryState Clear()
    {
        //506BF0 ->508300 memset-clears each complete44-byte slot.
        return new(Enumerable.Range(0, 16).Select(_ => new NativeBattleAuxiliaryTask(0, 0, null, NativeBattleAuxiliaryKind.None, ImmutableArray.CreateRange(new byte[32]))).ToImmutableArray(), null, null);
    }

    public void Validate()
    {
        if (Slots.IsDefault || Slots.Length != 16 || Head is < 0 or >= 16 || Tail is < 0 or >= 16 || Slots.Any(task => task is null || task.Next is < 0 or >= 16 || !Enum.IsDefined(task.Kind) || task.Payload.IsDefault || task.Payload.Length != 32 || (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.None))
        {
            throw new InvalidDataException("Native auxiliary tasks require sixteen physical44-byte slots and their retained payloads.");
        }
        if (Slots.Count(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.ScreenBlend501E80) > 1)
        {
            throw new InvalidDataException("Native501EBC identifies only one live screen blend task.");
        }
        HashSet<int> visited = [];
        for (int? next = Head; next is int index; next = Slots[index].Next)
        {
            if (!visited.Add(index) || (Slots[index].Flags & 1) == 0)
            {
                throw new InvalidDataException("Native auxiliary task links contain a cycle or a free physical slot.");
            }
            if (Slots[index].Kind == NativeBattleAuxiliaryKind.SoundParticle50F830)
            {
                (Slots[index].Program ?? throw new InvalidDataException("Native50F830 lacks its original operand-program pointer.")).Validate();
                if ((Slots[index].UnknownByteMask & 0x1F0FFFu) != 0)
                {
                    throw new InvalidDataException("Native50F830 lacks its written counter/model/operand/size fields.");
                }
            }
            else if (Slots[index].Kind is NativeBattleAuxiliaryKind.ModelSequence505C70 or NativeBattleAuxiliaryKind.ModelStatus50A000 or NativeBattleAuxiliaryKind.ModelResourceFlags50B080)
            {
                uint mask = Slots[index].Kind switch { NativeBattleAuxiliaryKind.ModelSequence505C70 => 0x20Fu, NativeBattleAuxiliaryKind.ModelResourceFlags50B080 => 0x3Fu, _ => 0xFFu };
                if ((Slots[index].UnknownByteMask & mask) != 0)
                {
                    throw new InvalidDataException("Native linked model callback lacks its written source pointer and operand fields.");
                }
            }
            else if (Slots[index].Kind == NativeBattleAuxiliaryKind.DeferredCompletion5086E0 && (Slots[index].UnknownByteMask & 0xF01) != 0)
            {
                throw new InvalidDataException("Native5086E0 lacks its written phase BYTE and caller completion pointer.");
            }
            else if ((Slots[index].Kind is NativeBattleAuxiliaryKind.OutcomeFade501D10 or NativeBattleAuxiliaryKind.ScreenBlend501E80) && (Slots[index].UnknownByteMask & 0x3F) != 0)
            {
                throw new InvalidDataException("Native501D10 lacks its written counter/duration/start/target fields.");
            }
            else if (Slots[index].Kind == NativeBattleAuxiliaryKind.CameraBlend509930 && (Slots[index].UnknownByteMask & 3) != 0)
            {
                throw new InvalidDataException("Native509930 lacks its written frame/duration BYTEs.");
            }
            else if (Slots[index].Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0)
            {
                (Slots[index].Program ?? throw new InvalidDataException("Native5057D0 lacks its original deadline pointer.")).Validate();
                if ((Slots[index].UnknownByteMask & 0xF0F) != 0)
                {
                    throw new InvalidDataException("Native5057D0 lacks its model pointer, toggle, sequence and elapsed WORD.");
                }
            }
            else if (Slots[index].Kind == NativeBattleAuxiliaryKind.MusicLoad501A70 && (Slots[index].UnknownByteMask & 0xF1) != 0)
            {
                throw new InvalidDataException("Native501A70 lacks its written phase BYTE and caller completion pointer.");
            }
            else if (Slots[index].Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660 && (Slots[index].UnknownByteMask & 0xFF1) != 0)
            {
                throw new InvalidDataException("Native508660 lacks its written phase BYTE, destination and completion pointers.");
            }
        }
    }

    public NativeBattleAuxiliaryState AppendCameraBlend(byte duration)
    {
        Validate();
        int slot = -1;
        for (int index = 0; index < Slots.Length; index++)
        {
            if ((Slots[index].Flags & 1) == 0)
            {
                slot = index;
                break;
            }
        }
        if (slot < 0)
        {
            //509810 dereferences506C10's result; the original has no successful allocation at exhaustion.
            throw new InvalidOperationException("Native camera blend exhausted the shared sixteen-slot auxiliary task pool.");
        }
        NativeBattleAuxiliaryTask task = Slots[slot];
        task = task with { Flags = (ushort)(task.Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.CameraBlend509930, Payload = task.Payload.SetItem(0, 0).SetItem(1, duration), UnknownByteMask = task.UnknownByteMask & ~3u };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot };
    }

    public NativeBattleAuxiliaryStep Advance(NativeBattleAuxiliaryInputs? inputs = null)
    {
        Validate();
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots;
        int? head = Head;
        int? previous = null;
        ushort? blend = null;
        ImmutableArray<NativeBattleAuxiliaryEmission>.Builder emissions = ImmutableArray.CreateBuilder<NativeBattleAuxiliaryEmission>();
        for (int? next = Head; next is int index;)
        {
            NativeBattleAuxiliaryTask task = slots[index];
            bool release;
            if (task.Kind is NativeBattleAuxiliaryKind.ModelSequence505C70 or NativeBattleAuxiliaryKind.ModelStatus50A000 or NativeBattleAuxiliaryKind.ModelResourceFlags50B080)
            {
                AdvanceModelRing(task, inputs);
                release = true;
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.ModelDeformation50F2E0)
            {
                task = (inputs?.AdvanceModelDeformation50F2E0 ?? throw new NotSupportedException("Native50F2E0 requires its shared matrix and group renderer."))(task);
                release = task.Payload[7] >= 15;
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.ModelGroupFade50F0E0)
            {
                (task, release) = AdvanceModelGroupFade50F0E0(task, inputs);
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.StageFade501F90)
            {
                (task, release) = AdvanceStageFade501F90(task, inputs);
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.ScreenBlend501E80)
            {
                (task, release) = AdvanceScreenBlend501E80(task, inputs);
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.DeferredCompletion5086E0)
            {
                (inputs?.WriteCompletionByte ?? throw new NotSupportedException("Native5086E0 requires its actual caller completion BYTE."))(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(8)), 255);
                release = true;
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.CameraShake50F6C0)
            {
                (task, release) = AdvanceCameraShake50F6C0(task, inputs);
            }
            else if (task.Kind is NativeBattleAuxiliaryKind.MoveToModel50F750 or NativeBattleAuxiliaryKind.AttachModel50F500)
            {
                (task, release) = AdvanceModelMotion(task, inputs);
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.OutcomeFade501D10)
            {
                (task, release) = AdvanceOutcomeFade501D10(task, inputs);
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660)
            {
                (task, release) = AdvanceCommonTextureLoad508660(task, inputs);
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.MusicLoad501A70)
            {
                (task, release) = AdvanceMusicLoad(task, inputs);
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0)
            {
                (task, release) = AdvanceTextureAnimation(task, inputs);
            }
            else if (task.Kind == NativeBattleAuxiliaryKind.SoundParticle50F830)
            {
                (NativeBattleAuxiliaryTask updated, bool completed, NativeBattleAuxiliaryEmission? emission) = AdvanceSoundParticle(task, inputs);
                task = updated;
                release = completed;
                if (emission is not null)
                {
                    emissions.Add(emission);
                }
            }
            else
            {
                byte frame = unchecked((byte)(task.Payload[0] + 1));
                byte duration = task.Payload[1];
                task = task with { Payload = task.Payload.SetItem(0, frame) };
                //509930 increments itsBYTE before signed IDIV; duration0 faults in the original too.
                int angle = (frame << 10) / duration;
                blend = unchecked((ushort)NativeBattleCameraMath.Sine(angle));
                release = frame >= duration;
            }
            next = task.Next;
            if (release)
            {
                slots = slots.SetItem(index, task with { Flags = 0 });
                if (previous is int retained)
                {
                    slots = slots.SetItem(retained, slots[retained] with { Next = next });
                }
                else
                {
                    head = next;
                }
            }
            else
            {
                slots = slots.SetItem(index, task);
                previous = index;
            }
        }
        return new(this with { Slots = slots, Head = head, Tail = previous }, blend) { Emissions = emissions.ToImmutable() };
    }
}
