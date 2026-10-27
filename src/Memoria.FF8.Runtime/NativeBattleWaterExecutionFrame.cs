using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

/// <summary>Current owners passed between finite services during one balanced effect invocation.</summary>
public sealed record NativeBattleWaterExecutionFrame(NativeBattleScriptedEffectMemory Core, NativeBattleEffectArena Arena, NativeBattleWaterTemporaryFrame TemporaryFrame, NativeCrtRandomState Random)
{
    public ImmutableArray<byte> ModuleViewBackup { get; init; } = [];

    public byte[] ReadTemporary(int offset, int length) => ReadTemporarySpan(offset, length).ToArray();

    private ReadOnlySpan<byte> ReadTemporarySpan(int offset, int length)
    {
        RequireTemporary(offset, length);
        if (TemporaryFrame.Known.AsSpan().Slice(offset, length).Contains(false))
        {
            throw new NotSupportedException("Native effect temporary read requires its actual assigned fields.");
        }
        return TemporaryFrame.Bytes.AsSpan().Slice(offset, length);
    }

    public byte ReadTemporaryByte(int offset) => ReadTemporarySpan(offset, 1)[0];
    public ushort ReadTemporaryWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(ReadTemporarySpan(offset, 2));
    public uint ReadTemporaryDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadTemporarySpan(offset, 4));

    public NativeBattleWaterExecutionFrame WriteTemporary(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireTemporary(offset, bytes.Length);
        bool allKnown = !TemporaryFrame.Known.AsSpan().Slice(offset, bytes.Length).Contains(false);
        if (allKnown && bytes.SequenceEqual(TemporaryFrame.Bytes.AsSpan().Slice(offset, bytes.Length)))
        {
            return this;
        }
        byte[] current = TemporaryFrame.Bytes.ToArray();
        bytes.CopyTo(current.AsSpan(offset));
        ImmutableArray<bool> known = TemporaryFrame.Known;
        if (!allKnown)
        {
            bool[] assigned = known.ToArray();
            Array.Fill(assigned, true, offset, bytes.Length);
            known = ImmutableCollectionsMarshal.AsImmutableArray(assigned);
        }
        return this with { TemporaryFrame = TemporaryFrame with { Bytes = ImmutableCollectionsMarshal.AsImmutableArray(current), Known = known } };
    }

    public NativeBattleWaterExecutionFrame WriteTemporaryByte(int offset, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        return WriteTemporary(offset, bytes);
    }

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
        return Bind(core, temporaryFrame, new NativeBattleScriptedInitializationDirectories(profile.InitializationDirectory, profile.OwnerDirectory));
    }

    public static NativeBattleWaterFrameBindingResult Bind(NativeBattleScriptedEffectMemory core, NativeBattleWaterTemporaryFrame temporaryFrame, NativeBattleScriptedInitializationDirectories directories)
    {
        directories.Validate();
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
            TemporaryDword(0x108 + 4 * index, directories.OwnerAddress(index));
        }
        Store(0x2797624, address);
        Store(0x27973B8, address + 0x100);
        Store(0x27973EC, directories.OwnerAddress(0));
        Store(0x27973C4, directories.OwnerAddress(1));
        Store(0x279744C, directories.OwnerAddress(2));
        Store(0x27973BC, directories.OwnerAddress(3));
        Store(0x27973C0, directories.OwnerAddress(4));
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
