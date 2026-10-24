using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect167Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect167StaticTests
{
    [Theory]
    [InlineData(0x5E38D0u)]
    [InlineData(0x5E3A00u)]
    public void Static_draw_uses_local_matrix_and_restores_scalar_H_before_paused_return(uint callback)
    {
        List<NativeBattleEffect167Operation> operations = [];
        ushort h = 512;
        NativeBattleEffect167Inputs inputs = Inputs(0x201, operations, () => h, value => h = value);
        NativeBattleEffect167Frame frame = Frame(callback, 0);
        NativeBattleEffect167Step step = NativeBattleEffect167Lower.AdvanceStatic(frame, ChildBank, Resources(), inputs);
        Assert.Equal(new NativeBattleEffect167SetProjectionH(288), operations[0]);
        Assert.IsType<NativeBattleEffect167DrawBatch>(operations[1]);
        Assert.Equal(new NativeBattleEffect167SetProjectionH(65535), operations[2]);
        Assert.Equal(ushort.MaxValue, h);
        Assert.Equal(new int?[] { -170, -50, 288 }, step.Frame.Memory.GeometryRegisters!.Translation);
        Assert.Equal(0, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(0, step.Result);
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Theory]
    [InlineData(0x5E38D0u, 12, 13)]
    [InlineData(0x5E3A00u, 8, 9)]
    public void Static_motion_uses_signed_truncating_thirds_and_its_actual_lifetime(uint callback, short age, ushort next)
    {
        List<NativeBattleEffect167Operation> operations = [];
        ushort h = 512;
        NativeBattleEffect167Frame frame = Frame(callback, age);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x1E, unchecked((ushort)-5)));
        NativeBattleEffect167Step step = NativeBattleEffect167Lower.AdvanceStatic(frame, ChildBank, Resources(), Inputs(0, operations, () => h, value => h = value));
        Assert.Equal(next, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(95, Read(step.Frame, ChildBank).ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)-4), Read(step.Frame, ChildBank).ReadWord(0x1E));
        Assert.Equal(2, step.Result);
    }

    private static NativeBattleEffect167Frame Frame(uint callback, short age)
    {
        NativeBattleEffect167Frame frame = NativeBattleEffect167ControllerTests.Frame(age, callback);
        frame = frame with { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() } };
        return Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x10, unchecked((ushort)-170)).WriteWord(0x12, unchecked((ushort)-50)).WriteWord(0x1C, 100).WriteWord(0x1E, 30).WriteWord(0x20, 200));
    }

    private static NativeBattleEffect167Inputs Inputs(uint flags, List<NativeBattleEffect167Operation> operations, Func<ushort> readH, Action<ushort> writeH) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => flags, null!, null!, null!, null!, null!, _ => throw new InvalidOperationException("Static callback must not compose the current view."), _ => new(null, readH(), 160, 108), _ => -1, _ => 0, null!, null!, (frame, operation) =>
    {
        operations.Add(operation);
        if (operation is NativeBattleEffect167SetProjectionH projection)
        {
            writeH(projection.Value);
        }
        return frame;
    });

    private static NativeBattleEffect167Resources Resources()
    {
        ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Builder meshes = ImmutableDictionary.CreateBuilder<uint, NativeBattleStaticMeshResource>();
        foreach (uint address in MeshAddresses)
        {
            byte[] bytes = new byte[48];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 16);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
            meshes[address] = new(0, address, bytes.ToImmutableArray());
        }
        return new(null!, null!, meshes.ToImmutable(), null!, 0, [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
    }
}
