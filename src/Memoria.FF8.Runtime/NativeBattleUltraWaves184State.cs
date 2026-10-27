using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleUltraWaves184Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltraWaves184Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleUltraWaves184State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleUltraWaves184State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena Particles, [property: JsonRequired] NativeBattleEffectArena Quads, [property: JsonRequired] NativeBattleEffectArena Grid, [property: JsonRequired] NativeBattleEffectArena Packets, [property: JsonRequired] NativeBattleEffectArena CapturePackets, [property: JsonRequired] bool ColdSourcesPending)
{
    public const uint GlobalsAddress = 0x22BD108;
    public const int GlobalsLength = 0x362BC;
    public static NativeBattleUltraWaves184State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear().MarkUnknown(0, 48), true);
    public static NativeBattleUltraWaves184State FromUnknown() => new(Unknown(GlobalsLength), Unknown(ParticlesLength), Unknown(QuadsLength), Unknown(GridLength), Unknown(PacketPhysicalLength), Unknown(48), false);
    public NativeBattleUltraWaves184State SeedColdSources(NativeBattleUltraWaves184Resources resources)
    {
        if (!ColdSourcesPending)
        {
            return this;
        }
        resources.Validate();
        return this with { CapturePackets = NativeBattleEffectArena.Clear().Write(0, resources.CapturePackets.AsSpan()), ColdSourcesPending = false };
    }
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(RequireGlobal(address, 4), 4));
    public NativeBattleUltraWaves184State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Globals.Write(RequireGlobal(address, 4), encoded);
        return this;
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, RequireGlobal(address, length), Unknown(length), 0, length);
    public NativeBattleUltraWaves184State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, RequireGlobal(address, length), length);
        return this;
    }
    public NativeBattleResourcePointer? ReadResourcePointer(uint address) => Globals.ReadResourcePointer(RequireGlobal(address, 4));
    public NativeBattleUltraWaves184State WriteResourcePointer(uint address, NativeBattleResourcePointer? pointer)
    {
        Globals.WriteResourcePointer(RequireGlobal(address, 4), pointer);
        return this;
    }
    public uint ReadCursor() => BinaryPrimitives.ReadUInt32LittleEndian(Packets.Read(PacketDrawingLength, 4));
    public NativeBattleUltraWaves184State WriteCursor(uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Packets.Write(PacketDrawingLength, encoded);
        return this;
    }
    public NativeBattleUltraWaves184State CopyCommonTexture(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != PacketPhysicalLength)
        {
            throw new InvalidDataException("Ultra Waves common texture must cover its complete physical destination.");
        }
        Packets.Write(0, bytes);
        return this;
    }
    public void Validate()
    {
        ValidateArena(Globals, GlobalsLength);
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            _ = RequireGlobal(checked(GlobalsAddress + (uint)region.Offset), region.Length);
        }
        ValidateArena(Particles, ParticlesLength);
        ValidateArena(Quads, QuadsLength);
        ValidateArena(Grid, GridLength);
        ValidateArena(Packets, PacketPhysicalLength);
        ValidateArena(CapturePackets, 48);
    }
    private static NativeBattleEffectArena Unknown(int length) => NativeBattleEffectArena.Clear().MarkUnknown(0, length);
    private static void ValidateArena(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        arena.ValidateExtent(length);
    }
    private static int RequireGlobal(uint address, int length)
    {
        (uint Address, int Length)[] segments = [(0x22BD108, 8), (SavedBindingAddress, 32), (0x22BE8E0, 8), (InitializerGlobal, 8), (0x22DB3C0, 4)];
        if (length < 0 || !segments.Any(segment => address >= segment.Address && (ulong)address + (uint)length <= segment.Address + (uint)segment.Length))
        {
            throw new InvalidDataException("Ultra Waves globals exclude its pools, drawing banks and physical packet cursor.");
        }
        return checked((int)(address - GlobalsAddress));
    }
}
