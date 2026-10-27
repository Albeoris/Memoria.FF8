using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLaserEye71State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] ImmutableArray<byte>? ExecutableBytes, [property: JsonRequired] bool ColdSourcePending)
{
    public const uint BaseAddress = 0x25FA408;
    public const int Length = 0x29D0;
    public static NativeBattleLaserEye71State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, true);
    public static NativeBattleLaserEye71State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length), null, false);

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach ((int offset, NativeBattleResourcePointer? pointer) in Globals.EnumerateResourcePointers())
        {
            Require(checked(BaseAddress + (uint)offset), 4);
        }
        Globals.ValidateExtent(Length);
        if (ExecutableBytes is { } bytes && (bytes.IsDefault || bytes.Length != NativeBattleLaserEye71Profile.SourceLength) || ColdSourcePending && ExecutableBytes is not null)
        {
            throw new InvalidDataException("Laser Eye retained source or globals contradict their physical owner.");
        }
    }

    public uint ReadSourceFlag()
    {
        if (ExecutableBytes is { } bytes)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan());
        }
        return ColdSourcePending ? 0u : throw new NotSupportedException("Laser Eye requires its retained executable source bytes.");
    }

    public NativeBattleLaserEye71State SeedColdSource(NativeBattleLaserEye71Resources resources)
    {
        Validate();
        if (!ColdSourcePending)
        {
            return this;
        }
        resources.Validate();
        return this with { ExecutableBytes = resources.Executable.Data, ColdSourcePending = false };
    }

    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Globals.Read(Require(address, 2), 2));
    public NativeBattleLaserEye71State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Globals.Write(Require(address, 4), encoded);
        return this;
    }
    public NativeBattleLaserEye71State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        Globals.Write(Require(address, 2), encoded);
        return this;
    }
    public NativeBattleResourcePointer? ReadResourcePointer(uint address) => Globals.ReadResourcePointer(Require(address, 4));
    public NativeBattleLaserEye71State WriteResourcePointer(uint address, NativeBattleResourcePointer? pointer)
    {
        Globals.WriteResourcePointer(Require(address, 4), pointer);
        return this;
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleLaserEye71State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }

    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || address < BaseAddress || end > BaseAddress + Length || address < 0x25FAAE0 && end > 0x25FA430 || address < 0x25FC178 && end > 0x25FAAE8 || address < 0x25FC96C && end > 0x25FC180 || address < 0x25FCD80 && end > 0x25FC970)
        {
            throw new InvalidDataException("Laser Eye globals exclude the canonical task pools and descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
