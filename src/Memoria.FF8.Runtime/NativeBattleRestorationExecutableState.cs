using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRestorationExecutableSource(ushort Effect, ImmutableArray<byte> Data);

/// <summary>Separate retained17E48E4/17DE100 initialization DWORDs and lazily materialized executable banks.</summary>
[method: JsonConstructor]
public sealed record NativeBattleRestorationExecutableState([property: JsonRequired] ushort Effect, [property: JsonRequired] uint InitializationFlag, [property: JsonRequired, JsonConverter(typeof(NativeBattleNodeSource.Converter))] NativeBattleNodeSource? Data)
{
    public NativeBattleRestorationExecutableState(ushort effect, uint initializationFlag, ImmutableArray<byte> data) : this(effect, initializationFlag, new NativeBattleNodeSource(data))
    {
    }

    public static NativeBattleRestorationExecutableState FromColdProcess(ushort effect)
    {
        _ = NativeBattleRestorationProfile.ForEffect(effect);
        return new(effect, 0, null);
    }

    public static int Length(ushort effect) => NativeBattleRestorationProfile.ForEffect(effect).ExecutableLength;

    public void Validate()
    {
        _ = NativeBattleRestorationProfile.ForEffect(Effect);
        if (Data is { } bytes && (bytes.Length != Length(Effect) || bytes.ReadUInt32(0) != InitializationFlag))
        {
            throw new InvalidDataException("Native restoration bank contradicts its own original extent/initialization DWORD.");
        }
    }

    public NativeBattleRestorationExecutableState Initialize(NativeBattleRestorationExecutableSource source)
    {
        using NativeBattleNodeInitialization<NativeBattleRestorationExecutableState> initialization = Prepare(source);
        return initialization.Publish();
    }

    internal NativeBattleNodeInitialization<NativeBattleRestorationExecutableState> Prepare(NativeBattleRestorationExecutableSource source)
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
            return new(this, null);
        }
        if (source.Data.IsDefault || source.Data.Length != Length(Effect) || BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("Native restoration first-use materialization requires its own authenticated unfixed resource.");
        }
        uint address = NativeBattleRestorationProfile.ForEffect(Effect).ExecutableAddress;
        NativeBattleNodeSource data = Data ?? new(source.Data);
        NativeBattleTaskOperation.AdmitCurrentRetainedArena(data.Arena ?? throw new InvalidDataException("Native executable fixups require their arena-backed resource owner."));
        NativeBattleNodeBankOperation operation = new(data);
        try
        {
            NativeBattleEffectNodeBankFixups.Relocate(operation, address);
            operation.WriteUInt32(0, 1);
            return new(this with { InitializationFlag = 1, Data = data }, operation);
        }
        catch
        {
            operation.Dispose();
            throw;
        }
    }

    public byte[] Read(uint address, int count)
    {
        Validate();
        NativeBattleNodeSource bytes = Data ?? throw new NotSupportedException("Native restoration requires its materialized original executable bytes.");
        uint start = NativeBattleRestorationProfile.ForEffect(Effect).ExecutableAddress;
        if (address < start || count < 0 || address - start > bytes.Length || count > bytes.Length - (address - start))
        {
            throw new InvalidDataException("Native restoration executable read leaves its own original resource.");
        }
        byte[] result = new byte[count];
        bytes.ReadInto(checked((int)(address - start)), result);
        return result;
    }
}
