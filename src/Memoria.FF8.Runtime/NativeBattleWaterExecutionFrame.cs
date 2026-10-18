using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Current owners passed between finite services during one balanced effect invocation.</summary>
public sealed record NativeBattleWaterExecutionFrame(NativeBattleScriptedEffectMemory Core, NativeBattleEffectArena Arena, NativeBattleWaterTemporaryFrame TemporaryFrame, NativeCrtRandomState Random)
{
    public ImmutableArray<byte> ModuleViewBackup { get; init; } = [];

    public byte[] ReadTemporary(int offset, int length)
    {
        RequireTemporary(offset, length);
        if (TemporaryFrame.Known.Skip(offset).Take(length).Any(value => !value))
        {
            throw new NotSupportedException("Native effect temporary read requires its actual assigned fields.");
        }
        return TemporaryFrame.Bytes.AsSpan().Slice(offset, length).ToArray();
    }

    public byte ReadTemporaryByte(int offset) => ReadTemporary(offset, 1)[0];
    public ushort ReadTemporaryWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(ReadTemporary(offset, 2));
    public uint ReadTemporaryDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadTemporary(offset, 4));

    public NativeBattleWaterExecutionFrame WriteTemporary(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireTemporary(offset, bytes.Length);
        byte[] current = TemporaryFrame.Bytes.ToArray();
        bool[] known = TemporaryFrame.Known.ToArray();
        bytes.CopyTo(current.AsSpan(offset));
        Array.Fill(known, true, offset, bytes.Length);
        return this with { TemporaryFrame = TemporaryFrame with { Bytes = current.ToImmutableArray(), Known = known.ToImmutableArray() } };
    }

    public NativeBattleWaterExecutionFrame WriteTemporaryByte(int offset, byte value) => WriteTemporary(offset, new byte[] { value });

    public NativeBattleWaterExecutionFrame WriteTemporaryWord(int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return WriteTemporary(offset, bytes);
    }

    public NativeBattleWaterExecutionFrame WriteTemporaryDword(int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return WriteTemporary(offset, bytes);
    }

    private void RequireTemporary(int offset, int length)
    {
        if (TemporaryFrame.Bytes.IsDefault || TemporaryFrame.Known.IsDefault || TemporaryFrame.Bytes.Length != 0x180 || TemporaryFrame.Known.Length != 0x180 || offset < 0 || length < 0 || offset > 0x180 - length)
        {
            throw new InvalidDataException("Native effect temporary access requires its complete bounded reservation.");
        }
    }
}

public sealed record NativeBattleWaterFrameBindingResult(NativeBattleScriptedEffectMemory Core, NativeBattleWaterTemporaryFrame TemporaryFrame);

public static class NativeBattleWaterFrameBinding
{
    public static NativeBattleWaterExecutionFrame Bind(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Profile profile)
    {
        NativeBattleWaterFrameBindingResult binding = Bind(frame.Core, frame.TemporaryFrame, profile);
        return frame with { Core = binding.Core, TemporaryFrame = binding.TemporaryFrame };
    }

    public static NativeBattleWaterFrameBindingResult Bind(NativeBattleScriptedEffectMemory core, NativeBattleWaterTemporaryFrame temporaryFrame, NativeBattleWater222Profile profile)
    {
        profile.Validate();
        uint address = temporaryFrame.Address;
        if (temporaryFrame.Bytes.IsDefault || temporaryFrame.Known.IsDefault || temporaryFrame.Bytes.Length != 0x180 || temporaryFrame.Known.Length != 0x180 || address == 0 || address > uint.MaxValue - 0x180)
        {
            throw new InvalidDataException("Native effect binding requires its actual bounded temporary reservation.");
        }
        byte[] bytes = temporaryFrame.Bytes.ToArray();
        bool[] known = temporaryFrame.Known.ToArray();
        TemporaryDword(0x100, address);
        TemporaryDword(0x104, address + 0x100);
        for (int index = 0; index < 5; index++)
        {
            TemporaryDword(0x108 + 4 * index, profile.OwnerAddress(index));
        }
        Store(0x2797624, address);
        Store(0x27973B8, address + 0x100);
        Store(0x27973EC, profile.OwnerAddress(0));
        Store(0x27973C4, profile.OwnerAddress(1));
        Store(0x279744C, profile.OwnerAddress(2));
        Store(0x27973BC, profile.OwnerAddress(3));
        Store(0x27973C0, profile.OwnerAddress(4));
        return new(core, temporaryFrame with { Bytes = bytes.ToImmutableArray(), Known = known.ToImmutableArray() });

        void Store(uint destination, uint value)
        {
            Span<byte> valueBytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(valueBytes, value);
            core = core.Write(destination, valueBytes);
        }

        void TemporaryDword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(known, true, offset, 4);
        }
    }
}
