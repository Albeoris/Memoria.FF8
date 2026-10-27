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
        byte[] bytes = new byte[length];
        TemporaryFrame.ReadInto(offset, bytes);
        return bytes;
    }

    public byte ReadTemporaryByte(int offset) => TemporaryFrame.ReadByte(offset);
    public ushort ReadTemporaryWord(int offset) => TemporaryFrame.ReadUInt16(offset);
    public uint ReadTemporaryDword(int offset) => TemporaryFrame.ReadUInt32(offset);

    public NativeBattleWaterExecutionFrame WriteTemporary(int offset, ReadOnlySpan<byte> bytes)
    {
        TemporaryFrame.Write(offset, bytes);
        return this;
    }

    public NativeBattleWaterExecutionFrame WriteTemporaryByte(int offset, byte value)
    {
        TemporaryFrame.WriteByte(offset, value);
        return this;
    }

    public NativeBattleWaterExecutionFrame WriteTemporaryWord(int offset, ushort value)
    {
        TemporaryFrame.WriteUInt16(offset, value);
        return this;
    }

    public NativeBattleWaterExecutionFrame WriteTemporaryDword(int offset, uint value)
    {
        TemporaryFrame.WriteUInt32(offset, value);
        return this;
    }

    private void RequireTemporary(int offset, int length)
    {
        TemporaryFrame.Validate();
        if (offset < 0 || length < 0 || offset > TemporaryFrame.Length - length)
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
        temporaryFrame.Validate();
        if (address == 0 || address > uint.MaxValue - 0x180)
        {
            throw new InvalidDataException("Native effect binding requires its actual bounded temporary reservation.");
        }
        using NativeBattleWaterTemporaryFrame.PrivateWrite writes = temporaryFrame.BeginPrivateWrite();
        NativeBattleWaterTemporaryFrame current = writes.View;
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
        return new(core, writes.Publish());

        void Store(uint destination, uint value)
        {
            Span<byte> valueBytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(valueBytes, value);
            core = core.Write(destination, valueBytes);
        }

        void TemporaryDword(int offset, uint value)
        {
            current.WriteUInt32(offset, value);
        }
    }
}
