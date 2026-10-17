using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAuxiliaryProgram([property: JsonRequired] ImmutableArray<byte> Source, [property: JsonRequired] int Offset)
{
    public void Validate()
    {
        if (Source.IsDefault || (uint)Offset >= Source.Length)
        {
            throw new InvalidDataException("Native50F830 operand pointer exceeds its retained original sequence resource.");
        }
    }
}

public sealed record NativeBattleAuxiliaryEmission(int Model, byte Bone, byte Size, bool Particle, ushort? Sound, NativeBattleVector? SoundAnchor, byte? Pan);
public sealed record NativeBattleAuxiliaryInputs
{
    public Func<int, byte, short, NativeBattleVector>? ReadBone { get; init; }
    public Func<NativeBattleVector, byte>? ReadPan { get; init; }
    public Func<int, NativeBattleGraphicsModel>? ReadModel { get; init; }
    public Action<int, byte, byte>? ApplyTexture { get; init; }
    public Action<int>? RestoreTextureBlink { get; init; }
    public Func<byte, uint, (byte Phase, bool Completed)>? AdvanceMusicLoad501A70 { get; init; }
}

public sealed partial record NativeBattleAuxiliaryState
{
    public (NativeBattleAuxiliaryState State, NativeGlobalRandomState Random, bool Allocated) AppendSoundParticle(int model, bool soundTwo, byte bone, NativeBattleAuxiliaryProgram program, NativeGlobalRandomState random)
    {
        Validate();
        program.Validate();
        if ((uint)model >= 7)
        {
            throw new ArgumentOutOfRangeException(nameof(model));
        }
        int slot = Enumerable.Range(0, 16).FirstOrDefault(index => (Slots[index].Flags & 1) == 0, -1);
        if (slot < 0)
        {
            //50F7D0 skips its operand bytes even when506C10 returnsNULL; no534AA0 call occurs.
            return (this, random, false);
        }
        random = random.Draw(out ushort roll);
        byte[] payload = Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(payload, soundTwo ? 1u : 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), bone);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), 0x1D972C0u + 156u * (uint)model);
        payload[20] = (byte)((roll & 7) + 9);
        NativeBattleAuxiliaryTask task = Slots[slot] with { Flags = (ushort)(Slots[slot].Flags | 1), State = 0, Next = null, Kind = NativeBattleAuxiliaryKind.SoundParticle50F830, Payload = payload.ToImmutableArray(), Program = program, UnknownByteMask = Slots[slot].UnknownByteMask & ~0x1F0FFFu | 0xF000u };
        ImmutableArray<NativeBattleAuxiliaryTask> slots = Slots.SetItem(slot, task);
        if (Tail is int tail)
        {
            slots = slots.SetItem(tail, slots[tail] with { Next = slot });
        }
        return (this with { Slots = slots, Head = Tail is null ? slot : Head, Tail = slot }, random, true);
    }

    public static int SkipSoundParticleProgram(ImmutableArray<byte> source, int offset)
    {
        while ((uint)offset < source.Length)
        {
            if (source[offset++] == 255)
            {
                return offset;
            }
        }
        throw new InvalidDataException("Native50F7D0 operand program lacks itsFF terminator in the original resource.");
    }

    private static (NativeBattleAuxiliaryTask Task, bool Release, NativeBattleAuxiliaryEmission? Emission) AdvanceSoundParticle(NativeBattleAuxiliaryTask task, NativeBattleAuxiliaryInputs? inputs)
    {
        NativeBattleAuxiliaryProgram program = task.Program ?? throw new InvalidDataException("Native50F830 lacks its retained program.");
        program.Validate();
        byte[] payload = task.Payload.ToArray();
        uint counter = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(8)) + 1);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), counter);
        byte operand = program.Source[program.Offset];
        task = task with { Payload = payload.ToImmutableArray() };
        if (counter != (operand & 63))
        {
            return (task, false, null);
        }
        uint address = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(16));
        uint relative = unchecked(address - 0x1D972C0u);
        if (relative % 156 != 0 || relative / 156 >= 7)
        {
            throw new InvalidDataException("Native50F830 retained model pointer is invalid.");
        }
        int model = (int)(relative / 156);
        byte bone = checked((byte)BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4)));
        ushort? sound = null;
        NativeBattleVector? anchor = null;
        byte? pan = null;
        if ((operand & 0x80) == 0)
        {
            sound = (payload[0] & 1) != 0 ? (ushort)2 : (ushort)3;
            anchor = (inputs?.ReadBone ?? throw new NotSupportedException("Native50F830 sound requires its current502170 bone."))(model, bone, 0) with { Y = 0 };
            pan = (inputs?.ReadPan ?? throw new NotSupportedException("Native50F830 sound requires actual5011B0 camera projection."))(anchor.Value);
        }
        NativeBattleAuxiliaryEmission emission = new(model, bone, payload[20], (operand & 0x40) == 0, sound, anchor, pan);
        program = program with { Offset = checked(program.Offset + 1) };
        program.Validate();
        return (task with { Program = program }, program.Source[program.Offset] == 255, emission);
    }
}
