using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeBattleModuleKind
{
    None,
    CriticalFlash571300,
    Placement570CC0,
    Defensive5705A0,
    DefensiveDraw570650,
    Sprite570BC0,
    CastingSeed574C80,
    CastingRing5753A0,
    CastingGround575250,
    CastingParticle574F50,
    CastingTrail5751A0,
    GuardianCastingSeed5742A0,
    GuardianCastingRing574AE0,
    GuardianCastingGround574990,
    GuardianCastingParticle574670,
    GuardianCastingTrail574580,
    BoneSpriteSeed573560,
    BoneSprite573640,
    BoneSpriteLargeSeed5736E0,
    SpectralCastingSeed5737C0,
    SpectralCastingSeed574260,
    SpectralCasting573800,
    SpectralCastingParticle573C40,
    SpectralCastingGround573DF0,
    SpectralCastingColumn573F40,
    SpectralCastingRing5740D0,
    ImpactSeed5707A0,
    Impact570860,
    PaletteSprite570AC0
}

public enum NativeBattleModuleDrawKind
{
    Flash,
    Defensive,
    Sprite,
    Sound,
    Mesh,
    Impact570860
}

public sealed partial record NativeBattleModuleDraw(NativeBattleModuleDrawKind Kind, byte Intensity);
public sealed partial record NativeBattleModuleInputs(uint GraphicsFlags);
public sealed partial record NativeBattleModuleTask([property: JsonRequired] ushort Flags, [property: JsonRequired] ushort State, [property: JsonRequired] int? Next, [property: JsonRequired] NativeBattleModuleKind Kind, [property: JsonRequired] ImmutableArray<byte> Payload);
public sealed partial record NativeBattleModuleStep(NativeBattleModuleState State, ImmutableArray<NativeBattleModuleDraw> Draws);
public sealed partial record NativeBattleModuleCallback(NativeBattleModuleState State, bool Release, ImmutableArray<NativeBattleModuleDraw> Draws);

/// <summary>56F9A0 initializes the shared300-slot36-byte effect-module list polled through500900's AA0 descriptor.</summary>
public sealed partial record NativeBattleModuleState([property: JsonRequired] ImmutableArray<NativeBattleModuleTask> Slots, [property: JsonRequired] int? Head, [property: JsonRequired] int? Tail)
{
    [JsonRequired]
    public byte PublishedCount { get; init; }
    public const uint DescriptorAddress = 0x209FAA8;
    public const uint BankAddress = 0x209D078;
    public const ushort TaskStride = 36;
    public const ushort TaskCapacity = 300;

    public static NativeBattleModuleState Clear()
    {
        return new(Enumerable.Range(0, 300).Select(_ => new NativeBattleModuleTask(0, 0, null, NativeBattleModuleKind.None, NativeBattleTelemetry.Immutable(1143, new byte[24]))).ToImmutableArray(), null, null);
    }

    public void Validate()
    {
        if (Slots.IsDefault || Slots.Length != 300 || Head is < 0 or >= 300 || Tail is < 0 or >= 300 || Slots.Any(task => task is null || task.Next is < 0 or >= 300 || !Enum.IsDefined(task.Kind) || task.Payload.IsDefault || task.Payload.Length != 24 || task.Callback == 0 || task.Callback is not null && task.Kind != NativeBattleModuleKind.None || (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.None && task.Callback is null))
        {
            throw new InvalidDataException("Native effect module requires300 physical36-byte task slots and retained payloads.");
        }
        HashSet<int> visited = [];
        for (int? next = Head; next is int index; next = Slots[index].Next)
        {
            if (!visited.Add(index) || (Slots[index].Flags & 1) == 0)
            {
                throw new InvalidDataException("Native effect-module task links contain a cycle or a free physical slot.");
            }
        }
    }

    public (NativeBattleModuleState State, int Slot) Append(NativeBattleModuleKind kind)
    {
        Validate();
        if (kind == NativeBattleModuleKind.None || !Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        return AppendCore(kind, null);
    }

    public (NativeBattleModuleState State, int Slot) AppendCallback(uint callback)
    {
        Validate();
        if (callback == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(callback));
        }
        return AppendCore(NativeBattleModuleKind.None, callback);
    }

    private (NativeBattleModuleState State, int Slot) AppendCore(NativeBattleModuleKind kind, uint? callback)
    {
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
            throw new InvalidOperationException("Native effect module exhausted its300 physical task slots.");
        }
        NativeBattleModuleTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = kind, Callback = callback };
        ImmutableArray<NativeBattleModuleTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return (this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot }, slot);
    }

    public NativeBattleModuleState AppendFlash(short rise, short hold, short fall, short intensity)
    {
        (NativeBattleModuleState state, int slot) = Append(NativeBattleModuleKind.CriticalFlash571300);
        byte[] payload = NativeBattleTelemetry.Materialize(1068, state.Slots[slot].Payload.AsSpan());
        BinaryPrimitives.WriteInt16LittleEndian(payload, 0);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), unchecked((short)((ushort)rise | 16 * ((ushort)hold | 16 * (ushort)fall))));
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(16), intensity);
        return state with { Slots = state.Slots.SetItem(slot, state.Slots[slot] with { Payload = NativeBattleTelemetry.Immutable(1070, payload) }) };
    }

    public NativeBattleModuleStep Advance(NativeBattleModuleInputs inputs)
    {
        return Advance(inputs, null);
    }

    public NativeBattleModuleStep Advance(NativeBattleModuleInputs inputs, Func<NativeBattleModuleState, int, NativeBattleModuleInputs, NativeBattleModuleCallback>? dispatch, Action<NativeBattleModuleTask>? observe = null)
    {
        Validate();
        NativeBattleModuleState state = this;
        int? previous = null;
        int survivors = 0;
        ImmutableArray<NativeBattleModuleDraw>.Builder draws = ImmutableArray.CreateBuilder<NativeBattleModuleDraw>();
        for (int? next = Head; next is int index;)
        {
            observe?.Invoke(state.Slots[index]);
            NativeBattleModuleCallback callback = state.Slots[index].Callback is uint address
                ? (dispatch ?? throw new NotSupportedException($"Native shared hit-effect callback {address:X8} requires its original dispatcher."))(state, index, inputs)
                : state.Slots[index].Kind switch
            {
                NativeBattleModuleKind.CriticalFlash571300 => state.AdvanceFlash(index, inputs),
                NativeBattleModuleKind.Placement570CC0 => state.AdvancePlacement(index, inputs),
                NativeBattleModuleKind.Defensive5705A0 => state.AdvanceDefensive(index, inputs),
                NativeBattleModuleKind.DefensiveDraw570650 => state.AdvanceDefensiveDraw(index, inputs),
                NativeBattleModuleKind.Sprite570BC0 => state.AdvanceSprite(index, inputs),
                NativeBattleModuleKind.CastingSeed574C80 => state.AdvanceCastingSeed(index, inputs),
                NativeBattleModuleKind.CastingRing5753A0 => state.AdvanceCastingRing(index, inputs),
                NativeBattleModuleKind.CastingGround575250 => state.AdvanceCastingGround(index, inputs),
                NativeBattleModuleKind.CastingParticle574F50 => state.AdvanceCastingParticle(index, inputs),
                NativeBattleModuleKind.CastingTrail5751A0 => state.AdvanceCastingTrail(index, inputs),
                NativeBattleModuleKind.GuardianCastingSeed5742A0 => state.AdvanceGuardianCastingSeed(index, inputs),
                NativeBattleModuleKind.GuardianCastingRing574AE0 => state.AdvanceGuardianCastingRing(index, inputs),
                NativeBattleModuleKind.GuardianCastingGround574990 => state.AdvanceCastingGround(index, inputs),
                NativeBattleModuleKind.GuardianCastingParticle574670 => state.AdvanceGuardianCastingParticle(index, inputs),
                NativeBattleModuleKind.GuardianCastingTrail574580 => state.AdvanceGuardianCastingTrail(index, inputs),
                NativeBattleModuleKind.BoneSpriteSeed573560 => state.AdvanceBoneSpriteSeed(index, inputs),
                NativeBattleModuleKind.BoneSprite573640 => state.AdvanceBoneSprite(index, inputs),
                NativeBattleModuleKind.BoneSpriteLargeSeed5736E0 => state.AdvanceBoneSpriteSeed(index, inputs, 2),
                NativeBattleModuleKind.SpectralCastingSeed5737C0 => state.AdvanceSpectralCastingSeed(index, 0x3B27),
                NativeBattleModuleKind.SpectralCastingSeed574260 => state.AdvanceSpectralCastingSeed(index, 0x3C67),
                NativeBattleModuleKind.SpectralCasting573800 => state.AdvanceSpectralCasting(index, inputs),
                NativeBattleModuleKind.SpectralCastingParticle573C40 => state.AdvanceSpectralCastingParticle(index, inputs),
                NativeBattleModuleKind.SpectralCastingGround573DF0 => state.AdvanceSpectralCastingGround(index, inputs),
                NativeBattleModuleKind.SpectralCastingColumn573F40 => state.AdvanceSpectralCastingColumn(index, inputs),
                NativeBattleModuleKind.SpectralCastingRing5740D0 => state.AdvanceSpectralCastingRing(index, inputs),
                NativeBattleModuleKind.ImpactSeed5707A0 => state.AdvanceImpactSeed5707A0(index, inputs),
                NativeBattleModuleKind.Impact570860 => state.AdvanceImpact570860(index, inputs),
                NativeBattleModuleKind.PaletteSprite570AC0 => state.AdvancePaletteSprite570AC0(index, inputs),
                _ => throw new NotSupportedException("Native effect-module task requires its original callback owner.")
            };
            state = callback.State;
            if (callback.CrtRandomAfter is { } crt)
            {
                inputs = inputs with { CrtRandom = crt };
            }
            draws.AddRange(callback.Draws);
            NativeBattleModuleTask task = state.Slots[index];
            //508420 reads Next after the callback, including any task appended by that callback.
            next = task.Next;
            if (callback.Release)
            {
                state = state with { Slots = state.Slots.SetItem(index, task with { Flags = 0 }) };
                if (previous is int retained)
                {
                    state = state with { Slots = state.Slots.SetItem(retained, state.Slots[retained] with { Next = next }) };
                }
                else
                {
                    state = state with { Head = next };
                }
            }
            else
            {
                previous = index;
                survivors++;
            }
        }
        return new(state with { Tail = previous, PublishedCount = unchecked((byte)survivors) }, draws.ToImmutable()) { CrtRandomAfter = inputs.CrtRandom };
    }

    private NativeBattleModuleCallback AdvanceFlash(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        short frame = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan());
        short phases = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(2));
        short maximum = BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(16));
        int rise = phases & 15;
        int hold = phases >> 4 & 15;
        int fall = phases >> 8 & 15;
        int brightness;
        if (frame < rise)
        {
            brightness = frame * (maximum / rise);
        }
        else if (frame - rise < hold)
        {
            brightness = maximum;
        }
        else if (frame - rise - hold < fall)
        {
            brightness = maximum - (frame - rise - hold) * (maximum / fall);
        }
        else
        {
            brightness = -1;
        }
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, []);
        }
        byte[] payload = NativeBattleTelemetry.Materialize(1069, task.Payload.AsSpan());
        BinaryPrimitives.WriteInt16LittleEndian(payload, unchecked((short)(frame + 1)));
        NativeBattleModuleState state = this with { Slots = Slots.SetItem(slot, task with { Payload = NativeBattleTelemetry.Immutable(1071, payload) }) };
        return brightness < 0 ? new(state, true, []) : new(state, false, [new(NativeBattleModuleDrawKind.Flash, unchecked((byte)brightness))]);
    }
}
