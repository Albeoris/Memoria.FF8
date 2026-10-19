using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePackedMatrix32([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] ImmutableArray<bool> Known)
{
    public NativeBattleActionMatrix ReadMatrix()
    {
        Validate();
        for (int index = 0; index < 32; index++)
        {
            if (index is not (18 or 19) && !Known[index])
            {
                throw new NotSupportedException("Native matrix consumer requires its actual rotation and translation bytes.");
            }
        }
        short[] rotation = new short[9];
        for (int index = 0; index < rotation.Length; index++)
        {
            rotation[index] = BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan().Slice(index * 2));
        }
        return new(rotation.ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan().Slice(20)), BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan().Slice(24)), BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan().Slice(28)), 4096);
    }

    public void Validate()
    {
        if (Bytes.IsDefault || Bytes.Length != 32 || Known.IsDefault || Known.Length != 32)
        {
            throw new InvalidDataException("Native packed matrix requires its complete raw bytes and knownness.");
        }
    }
}

public sealed partial record NativeBattleTonberry90State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] uint? DirectorAddress1547168)
{
    public const uint GlobalAddress = 0x259EEA8;
    public const int GlobalLength = 0x5EF0;

    public static NativeBattleTonberry90State FromColdProcess() => new(NativeBattleEffectArena.Clear(), 0x25A3DF0) { CameraAddress1547510 = 0x25A3D00 };

    public void Validate()
    {
        Globals.Validate();
        if (Globals.Regions.Any(region => region.Offset > GlobalLength - region.Data.Length) || Globals.UnknownRegions.Any(region => region.Offset > GlobalLength - region.Length))
        {
            throw new InvalidDataException("Native Tonberry module globals leave their physical span.");
        }
        if (Io is { } io)
        {
            if (io.Script.IsDefault || io.Script.Length != 0x40)
            {
                throw new InvalidDataException("Native Tonberry IO requires its complete retained script.");
            }
            io.Banks?.Validate();
        }
    }

    public NativeBattleTonberry90IoState? Io { get; init; }
    public uint? CameraAddress1547510 { get; init; }
    public byte[] Read(uint address, int length) => Globals.Read(Offset(address, length), length);
    public NativeBattleTonberry90State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Offset(address, bytes.Length), bytes) };
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Offset(address, 4), 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Globals.Read(Offset(address, 2), 2));
    public NativeBattleTonberry90State WriteDword(uint address, uint value) => this with { Globals = Globals.Write(Offset(address, 4), BitConverter.GetBytes(value)) };

    private static int Offset(uint address, int length)
    {
        if (address < GlobalAddress || length < 0 || length > GlobalLength || (ulong)address - GlobalAddress > (uint)(GlobalLength - length))
        {
            throw new InvalidDataException("Native Tonberry global access leaves its addressed process owner.");
        }
        return checked((int)(address - GlobalAddress));
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleTonberry90State? Tonberry90 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePackedMatrix32? SharedEffectView2793E58 { get; init; }
}

public sealed record NativeBattleTonberry90Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleTonberry90TaskStep(NativeBattleTonberry90Frame Frame, bool Complete);
public sealed record NativeBattleTonberry90Inputs(NativeBattleActionPacketsState Packets, NativeBattlePackedMatrix32 CameraMatrix, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleTonberry90Frame, uint, NativeBattleTonberry90Frame> InitializeTarget, Func<NativeBattleTonberry90Frame, uint, NativeBattleTonberry90TaskStep> AdvanceLeaf, Func<NativeBattleTonberry90Frame, int, NativeBattleTonberry90Frame> ApplyContact, Func<NativeBattleTonberry90Frame, NativeBattleChoco97Operation, NativeBattleTonberry90Frame> Execute);
