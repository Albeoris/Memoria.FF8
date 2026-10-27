using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleOilBlast137State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena Matrix32)
{
    public bool? ColdMatrixSourcePending { get; init; }
    public static NativeBattleOilBlast137State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear().MarkUnknown(0, 32)) { ColdMatrixSourcePending = true };
    public static NativeBattleOilBlast137State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleOilBlast137Profile.GlobalsLength), NativeBattleEffectArena.Clear().MarkUnknown(0, 32));

    public NativeBattleOilBlast137State SeedColdSource(NativeBattleOilBlast137Resources resources)
    {
        if (ColdMatrixSourcePending is null)
        {
            throw new NotSupportedException("Oil Blast requires its actually observed mutable matrix source.");
        }
        if (ColdMatrixSourcePending == false)
        {
            return this;
        }
        resources.Validate();
        return this with { Matrix32 = Matrix32.Write(0, resources.InitialMatrix32.Bytes.AsSpan()), ColdMatrixSourcePending = false };
    }

    public void Validate()
    {
        Globals.Validate();
        Matrix32.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(NativeBattleOilBlast137Profile.BaseAddress + (uint)region.Offset), region.Length);
        }
        if (Globals.EnumerateUnknownRanges().Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > NativeBattleOilBlast137Profile.GlobalsLength - region.Length) || Matrix32.EnumerateDataRanges().Any(region => region.Offset < 0 || region.Offset > 32 - region.Length) || Matrix32.EnumerateUnknownRanges().Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > 32 - region.Length))
        {
            throw new InvalidDataException("Oil Blast retained bytes leave their scalar or mutable matrix owner.");
        }
    }

    public byte[] Read(uint address, int length)
    {
        if (IsMatrix(address, length))
        {
            RequireSeededMatrix();
            return Matrix32.Read(checked((int)(address - NativeBattleOilBlast137Profile.MatrixAddress)), length);
        }
        return Globals.Read(Require(address, length), length);
    }

    public NativeBattleOilBlast137State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (IsMatrix(address, bytes.Length))
        {
            RequireSeededMatrix();
            Matrix32.Write(checked((int)(address - NativeBattleOilBlast137Profile.MatrixAddress)), bytes);
            return this;
        }
        Globals.Write(Require(address, bytes.Length), bytes);
        return this;
    }

    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleOilBlast137State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleOilBlast137State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        if (IsMatrix(address, length))
        {
            RequireSeededMatrix();
            return NativeBattleEffectNodeContextMemory.Copy(Matrix32, checked((int)(address - NativeBattleOilBlast137Profile.MatrixAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        return NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleOilBlast137State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (IsMatrix(address, length))
        {
            RequireSeededMatrix();
            NativeBattleEffectNodeContextMemory.Copy(source, 0, Matrix32, checked((int)(address - NativeBattleOilBlast137Profile.MatrixAddress)), length);
            return this;
        }
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }

    private void RequireSeededMatrix()
    {
        if (ColdMatrixSourcePending != false)
        {
            throw new NotSupportedException("Oil Blast cannot consume or replace an unobserved mutable matrix source.");
        }
    }

    private static bool IsMatrix(uint address, int length) => length >= 0 && address >= NativeBattleOilBlast137Profile.MatrixAddress && (ulong)address + (uint)length <= NativeBattleOilBlast137Profile.MatrixAddress + 32;
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(0, length);
        if (length < 0 || !(address >= 0x2520CC0 && end <= 0x2520CD0 || address >= 0x2520CE4 && end <= 0x2520CE8))
        {
            throw new InvalidDataException("Oil Blast scalar bytes exclude its actual root bank and all task descriptors.");
        }
        return checked((int)(address - NativeBattleOilBlast137Profile.BaseAddress));
    }
}
