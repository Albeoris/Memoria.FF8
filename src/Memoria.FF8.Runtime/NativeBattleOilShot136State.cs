using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleOilShot136Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleOilShot136State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] ImmutableArray<byte>? Mesh, [property: JsonRequired] NativeBattleEffectArena? ScaleMatrix32, [property: JsonRequired] bool? PendingColdSources)
{
    public static NativeBattleOilShot136State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, null, true);
    public static NativeBattleOilShot136State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, GlobalsLength), null, null, null);

    public NativeBattleOilShot136State SeedColdSources(NativeBattleOilShot136Resources resources)
    {
        if (PendingColdSources is null)
        {
            throw new NotSupportedException("Oil Shot requires its actually observed mutable source owners.");
        }
        if (PendingColdSources == false)
        {
            return this;
        }
        if (Mesh is not null || ScaleMatrix32 is not null)
        {
            throw new InvalidDataException("Oil Shot cold source seeding cannot replace existing mutable owners.");
        }
        resources.Validate();
        return this with { Mesh = resources.InitialMesh.Bytes, ScaleMatrix32 = NativeBattleEffectArena.Clear().Write(0, resources.InitialScaleMatrix32.Bytes.AsSpan()), PendingColdSources = false };
    }

    public ImmutableArray<byte> ReadMesh() => Mesh ?? throw new NotSupportedException("Oil Shot requires its current mutable mesh.");
    public NativeBattleOilShot136State WriteMesh(ImmutableArray<byte> mesh)
    {
        RequireMesh(mesh);
        return this with { Mesh = mesh };
    }

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > GlobalsLength - region.Length))
        {
            throw new InvalidDataException("Oil Shot unknown globals leave their scalar owner.");
        }
        if (Mesh is { } mesh)
        {
            RequireMesh(mesh);
        }
        if (ScaleMatrix32 is { } matrix)
        {
            matrix.Validate();
            if (matrix.Regions.Any(region => region.Offset < 0 || region.Offset > 32 - region.Data.Length) || matrix.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > 32 - region.Length))
            {
                throw new InvalidDataException("Oil Shot scale matrix leaves its single physical owner.");
            }
        }
    }

    public NativeBattlePackedMatrix32 ReadCurrentMatrix()
    {
        (byte[] bytes, bool[] known) = Globals.ReadSnapshot(checked((int)(CurrentMatrixAddress - BaseAddress)), 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public byte[] Read(uint address, int length) => IsMatrix(address, length) ? RequireMatrix().Read(checked((int)(address - MatrixAddress)), length) : Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleOilShot136State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (IsMatrix(address, bytes.Length))
        {
            NativeBattleEffectArena matrix = ScaleMatrix32 ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
            return this with { ScaleMatrix32 = matrix.Write(checked((int)(address - MatrixAddress)), bytes) };
        }
        return this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    }
    public NativeBattleOilShot136State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleOilShot136State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(IsMatrix(address, length) ? RequireMatrix() : Globals, IsMatrix(address, length) ? checked((int)(address - MatrixAddress)) : Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleOilShot136State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (IsMatrix(address, length))
        {
            return this with { ScaleMatrix32 = NativeBattleEffectNodeContextMemory.Copy(source, 0, ScaleMatrix32 ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 32), checked((int)(address - MatrixAddress)), length) };
        }
        return this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    }
    private NativeBattleEffectArena RequireMatrix() => ScaleMatrix32 ?? throw new NotSupportedException("Oil Shot requires its current mutable scale matrix.");
    private static bool IsMatrix(uint address, int length) => length >= 0 && address >= MatrixAddress && (ulong)address + (uint)length <= MatrixAddress + 32;
    private static void RequireMesh(ImmutableArray<byte> mesh)
    {
        if (mesh.IsDefault || mesh.Length != MeshBytes || BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan()) != 0x368 || BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(4)) != 108)
        {
            throw new InvalidDataException("Oil Shot requires its complete current 108-vertex mesh owner.");
        }
    }
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !(address >= 0x2520D28 && end <= 0x2520D38 || address >= 0x2520D4C && end <= 0x2520D50 || address >= CurrentMatrixAddress && end <= CurrentMatrixAddress + 32))
        {
            throw new InvalidDataException("Oil Shot scalar bytes exclude its root task and all six descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
