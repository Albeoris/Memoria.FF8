using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleSiren95State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] uint? DirectorAddress1533010)
{
    public const uint GlobalAddress = 0x257F8A0;
    public const int GlobalLength = 0x10454;

    public static NativeBattleSiren95State FromColdProcess() => new(NativeBattleEffectArena.Clear(), 0x258EAA8) { CameraAddress15339D0 = 0x258E7B0, StaticNodeDataKnownCold = true };

    public void Validate()
    {
        Globals.Validate();
        if (StaticNodeData is { } nodeData && (nodeData.IsDefault || nodeData.Length != 0x1294))
        {
            throw new InvalidDataException("Native Siren static node data requires its complete retained physical span.");
        }
        if (Io is { } io)
        {
            if (io.Script.IsDefault || io.Script.Length != 0x180)
            {
                throw new InvalidDataException("Native Siren IO requires its complete retained script.");
            }
        }
        Globals.ValidateExtent(GlobalLength);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleNodeSource? StaticNodeData { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool StaticNodeDataKnownCold { get; init; }

    public NativeBattleSiren95IoState? Io { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? CameraAddress15339D0 { get; init; }

    public byte[] Read(uint address, int length) => Globals.Read(Offset(address, length), length);
    public NativeBattleSiren95State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Globals.Write(Offset(address, bytes.Length), bytes);
        return this;
    }
    public uint ReadDword(uint address) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleSiren95State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    private static int Offset(uint address, int length)
    {
        if (address < GlobalAddress || length < 0 || length > GlobalLength || (ulong)address - GlobalAddress > (uint)(GlobalLength - length))
        {
            throw new InvalidDataException("Native Siren global access leaves its addressed process owner.");
        }
        return checked((int)(address - GlobalAddress));
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSiren95State? Siren95
    {
        get => effectState.Siren95;
        init => effectState = effectState with { Siren95 = value };
    }
}

public sealed record NativeBattleSiren95Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleSiren95TaskStep(NativeBattleSiren95Frame Frame, bool Complete);
public sealed record NativeBattleSiren95Inputs(NativeBattleActionPacketsState Packets, NativeBattlePackedMatrix32 CameraMatrix, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleSiren95Frame, uint, NativeBattleSiren95Frame> InitializeTarget, Func<NativeBattleSiren95Frame, uint, NativeBattleSiren95TaskStep> AdvanceLeaf, Func<NativeBattleSiren95Frame, int, NativeBattleSiren95Frame> ApplyContact, Func<NativeBattleSiren95Frame, NativeBattleChoco97Operation, NativeBattleSiren95Frame> Execute);
