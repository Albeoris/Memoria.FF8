using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModuleAnchor(short X, short Y, short Z, short W);

public sealed partial record NativeBattleModuleInputs
{
    public ImmutableArray<NativeBattleModuleResource> Resources { get; init; } = [];
    public Func<int, NativeBattleModuleAnchor>? ReadModelAnchor { get; init; }
    public Func<int, short>? ReadModelRotation14 { get; init; }
    public Func<int, byte, short, NativeBattleVector>? ReadModelBone { get; init; }
    public NativeCrtRandomState? CrtRandom { get; init; }
}

public sealed partial record NativeBattleModuleStep
{
    public NativeCrtRandomState? CrtRandomAfter { get; init; }
}

public sealed partial record NativeBattleModuleCallback
{
    public NativeCrtRandomState? CrtRandomAfter { get; init; }
}

public sealed partial record NativeBattleModuleDraw
{
    public NativeBattleModuleAnchor? Anchor { get; init; }
    public ushort Phase { get; init; }
    public byte Variant { get; init; }
    public short Rotation { get; init; }
    public short Orientation { get; init; }
    public int Scale { get; init; }
    public int DepthOffset { get; init; }
    public int Direction { get; init; }
    public uint PackedParameters { get; init; }
    public NativeBattleModuleResource? Resource { get; init; }
    public NativeBattleModuleSpriteFrame? SpriteFrame { get; init; }
    public ushort Sound { get; init; }
    public ushort SoundFlags { get; init; }
}

public sealed partial record NativeBattleModuleState
{
    public NativeBattleModuleState AppendModelResource(int model, NativeBattleModuleResource resource, uint packedParameters)
    {
        if ((uint)model >= 7 || (byte)packedParameters != resource.Id)
        {
            throw new InvalidDataException("Native module model or packed resource index differs from its original producer.");
        }
        resource.Validate();
        if (resource.WrapperCallback != 0x570CC0)
        {
            throw new NotSupportedException($"Native module wrapper{resource.WrapperCallback:X} requires its own original callback.");
        }
        (NativeBattleModuleState state, int slot) = Append(NativeBattleModuleKind.Placement570CC0);
        byte[] payload = state.Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, 0);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), ModelAddress(model));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), packedParameters);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20), resource.Address);
        return state with { Slots = state.Slots.SetItem(slot, state.Slots[slot] with { Payload = payload.ToImmutableArray() }) };
    }

    private NativeBattleModuleCallback AdvancePlacement(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        short placement = S16(task.Payload, 2);
        int model = ModelIndex(U32(task.Payload, 4));
        NativeBattleModuleAnchor anchor;
        if (placement == 1)
        {
            anchor = (inputs.ReadModelAnchor ?? throw new NotSupportedException("Native module placement requires the current571400 bone240/241 anchor."))(model);
        }
        else if (placement == 3)
        {
            short fraction = S16(task.Payload, 14);
            NativeBattleVector point = (inputs.ReadModelBone ?? throw new NotSupportedException("Native570CC0 placement3 requires its actual502170 bone."))(model, checked((byte)S16(task.Payload, 12)), (short)(fraction & 0x7FFF));
            anchor = new(point.X, fraction < 0 ? (short)0 : point.Y, point.Z, 0);
        }
        else
        {
            throw new NotSupportedException($"Native module placement{placement} requires its original bone or transform input.");
        }
        NativeBattleModuleResource resource = Resource(inputs, U32(task.Payload, 20));
        NativeBattleModuleKind kind = resource.Callback switch
        {
            0x5705A0 => NativeBattleModuleKind.Defensive5705A0,
            0x570BC0 => NativeBattleModuleKind.Sprite570BC0,
            _ => throw new NotSupportedException($"Native module callback{resource.Callback:X} requires its original implementation.")
        };
        (NativeBattleModuleState state, int child) = Append(kind);
        byte[] payload = state.Slots[child].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, 0);
        WriteAnchor(payload.AsSpan(4), anchor);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), ModelAddress(model));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), U32(task.Payload, 16));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20), resource.Address);
        state = state with { Slots = state.Slots.SetItem(child, state.Slots[child] with { Payload = payload.ToImmutableArray() }) };
        return new(state, true, []);
    }

    private NativeBattleModuleCallback AdvanceDefensive(int slot, NativeBattleModuleInputs inputs)
    {
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, []);
        }
        NativeBattleModuleTask task = Slots[slot];
        uint modelAddress = U32(task.Payload, 12);
        if (modelAddress == 0)
        {
            return new(this, true, []);
        }
        int model = ModelIndex(modelAddress);
        short rotation = (inputs.ReadModelRotation14 ?? throw new NotSupportedException("Native defensive effect requires the current model rotationWORD14."))(model);
        NativeCrtRandomState crt = inputs.CrtRandom ?? throw new NotSupportedException("Native defensive effect requires the existing CRT thread random owner.");
        NativeCrtRandomState after = crt.Draw(out ushort roll);
        NativeBattleModuleResource resource = Resource(inputs, U32(task.Payload, 20));
        uint packed = U32(task.Payload, 16);
        (NativeBattleModuleState state, int child) = Append(NativeBattleModuleKind.DefensiveDraw570650);
        byte[] payload = state.Slots[child].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, 0);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), resource.Bytes[0]);
        task.Payload.AsSpan().Slice(4, 8).CopyTo(payload.AsSpan(4));
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(12), rotation);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(14), (short)(roll % 4096));
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(16), unchecked((short)(((packed >> 16) & 0x7F) * 320)));
        state = state with { Slots = state.Slots.SetItem(child, state.Slots[child] with { Payload = payload.ToImmutableArray() }) };
        return new(state, true, [new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = ReadAnchor(task.Payload, 4), Sound = 0x72, SoundFlags = 0x8000 }]) { CrtRandomAfter = after };
    }

    private NativeBattleModuleCallback AdvanceDefensiveDraw(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        short phase = S16(task.Payload, 0);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Defensive, 0) { Anchor = ReadAnchor(task.Payload, 4), Phase = unchecked((ushort)phase), Variant = unchecked((byte)S16(task.Payload, 2)), Rotation = S16(task.Payload, 12), Orientation = S16(task.Payload, 14), Scale = S16(task.Payload, 16) };
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = task.Payload.ToArray();
        short next = unchecked((short)(phase + 1));
        BinaryPrimitives.WriteInt16LittleEndian(payload, next);
        return new(this with { Slots = Slots.SetItem(slot, task with { Payload = payload.ToImmutableArray() }) }, next >= 8, [draw]);
    }

    private NativeBattleModuleCallback AdvanceSprite(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        NativeBattleModuleResource resource = Resource(inputs, U32(task.Payload, 20));
        ushort phase = unchecked((ushort)S16(task.Payload, 0));
        NativeBattleModuleSpriteFrame frame = resource.SpriteFrame(phase);
        NativeBattleModuleAnchor anchor = ReadAnchor(task.Payload, 4);
        uint packed = U32(task.Payload, 16);
        int scale = (int)((packed >> 16) & 0x7F) << 8;
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = anchor, Phase = phase, Scale = scale, DepthOffset = -(anchor.W == 0 ? scale >> 4 : anchor.W), Rotation = unchecked((short)(((packed >> 8) & 255) << 4)), Direction = (packed & 0x800000) != 0 ? -4096 : 4096, PackedParameters = packed, Resource = resource, SpriteFrame = frame, SpriteFlags = (ushort)((packed & 0x80000000) != 0 ? 0x13 : 3), SpritePaletteOffsets = (packed & 0x80000000) != 0 ? [(byte)((packed >> 24) & 0x7F), 0, 0, 0] : [] };
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = task.Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, unchecked((short)(phase + 1)));
        return new(this with { Slots = Slots.SetItem(slot, task with { Payload = payload.ToImmutableArray() }) }, frame.NextOffset < 0, [draw]);
    }

    private static NativeBattleModuleResource Resource(NativeBattleModuleInputs inputs, uint address)
    {
        NativeBattleModuleResource resource = inputs.Resources.FirstOrDefault(resource => resource.Address == address) ?? throw new NotSupportedException($"Native module resource{address:X} has no original immutable binding.");
        resource.Validate();
        return resource;
    }

    private static uint ModelAddress(int model)
    {
        return checked(0x1D972C0u + 156u * (uint)model);
    }

    private static int ModelIndex(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0u);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Native module payload does not identify a physical graphics model.");
        }
        return (int)(offset / 156);
    }

    private static short S16(ImmutableArray<byte> payload, int offset)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan().Slice(offset));
    }

    private static uint U32(ImmutableArray<byte> payload, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan().Slice(offset));
    }

    private static NativeBattleModuleAnchor ReadAnchor(ImmutableArray<byte> payload, int offset)
    {
        return new(S16(payload, offset), S16(payload, offset + 2), S16(payload, offset + 4), S16(payload, offset + 6));
    }

    private static void WriteAnchor(Span<byte> destination, NativeBattleModuleAnchor anchor)
    {
        BinaryPrimitives.WriteInt16LittleEndian(destination, anchor.X);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(2), anchor.Y);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(4), anchor.Z);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(6), anchor.W);
    }
}
