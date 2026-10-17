using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRestorationExecutableSource(ushort Effect, ImmutableArray<byte> Data);

/// <summary>Separate retained17E48E4/17DE100 initialization DWORDs and lazily materialized executable banks.</summary>
public sealed record NativeBattleRestorationExecutableState([property: JsonRequired] ushort Effect, [property: JsonRequired] uint InitializationFlag, [property: JsonRequired] ImmutableArray<byte>? Data)
{
    public static NativeBattleRestorationExecutableState FromColdProcess(ushort effect)
    {
        _ = NativeBattleRestorationProfile.ForEffect(effect);
        return new(effect, 0, null);
    }

    public static int Length(ushort effect) => effect switch
    {
        24 => 0x5C0C,
        27 => 0x67E4,
        _ => throw new NotSupportedException("Native restoration executable extent requires its original effect.")
    };

    public void Validate()
    {
        _ = NativeBattleRestorationProfile.ForEffect(Effect);
        if (Data is { } bytes && (bytes.IsDefault || bytes.Length != Length(Effect) || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != InitializationFlag))
        {
            throw new InvalidDataException("Native restoration bank contradicts its own original extent/initialization DWORD.");
        }
    }

    public NativeBattleRestorationExecutableState Initialize(NativeBattleRestorationExecutableSource source)
    {
        Validate();
        if (source.Effect != Effect)
        {
            throw new InvalidDataException("Native restoration mutable banks cannot share another spell's source.");
        }
        if (InitializationFlag != 0)
        {
            if (Data is null)
            {
                throw new NotSupportedException("Native restoration bank requires previously fixed bytes when its retained flag is nonzero.");
            }
            return this;
        }
        if (source.Data.IsDefault || source.Data.Length != Length(Effect) || BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("Native restoration first-use materialization requires its own authenticated unfixed resource.");
        }
        uint address = NativeBattleRestorationProfile.ForEffect(Effect).ExecutableAddress;
        byte[] data = NativeBattleEffectNodeBankFixups.Relocate(Data ?? source.Data, address);
        BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
        return this with { InitializationFlag = 1, Data = data.ToImmutableArray() };
    }

    public byte[] Read(uint address, int count)
    {
        Validate();
        ImmutableArray<byte> bytes = Data ?? throw new NotSupportedException("Native restoration requires its materialized original executable bytes.");
        uint start = NativeBattleRestorationProfile.ForEffect(Effect).ExecutableAddress;
        if (address < start || count < 0 || address - start > bytes.Length || count > bytes.Length - (address - start))
        {
            throw new InvalidDataException("Native restoration executable read leaves its own original resource.");
        }
        return bytes.AsSpan().Slice(checked((int)(address - start)), count).ToArray();
    }
}
