using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime.Tests;

internal static class NativeBattleEffect65LowerFixture
{
    internal static NativeBattlePackedMatrix32 Identity(int z = 0) => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), null);
    internal static NativeBattleEffect65Frame Frame(int z = 0)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            Arena = NativeBattleEffectArena.Clear(),
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity(z).ReadMatrix(), true, true, null),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAB000000 },
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf3 = 4096, Zsf4 = 4096 }
        };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[400]);
        return new(memory, new(65, 64, new(65, Entry, Initializer), null, null) { Pools = [] }, new(123), NativeBattleEffect65State.FromColdProcess().WriteDword(ArenaGlobal, ArenaAddress)) { Staging = staging };
    }
    internal static NativeBattleEffect65Inputs Inputs(List<NativeBattleEffect65Operation>? operations = null, Func<NativeBattleEffect65Frame, NativeBattleEffect65Operation, NativeBattleEffect65Frame>? execute = null, int viewZ = 2000) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, _ => Identity(viewZ), _ => new(null, 100, 160, 108), _ => 0, null!, null!, null!, null!, null!, null!, null!, (frame, operation) =>
    {
        operations?.Add(operation);
        return execute is null ? frame : execute(frame, operation);
    });
    internal static NativeBattleEffect65Resources Resources()
    {
        byte[] trig = new byte[16384];
        for (int index = 0; index < trig.Length; index += 4)
        {
            BinaryPrimitives.WriteInt16LittleEndian(trig.AsSpan(index), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(trig.AsSpan(index + 2), 4096);
        }
        return new(new("texture", []), new("vibration", [], []), new(0, SpriteAddress, 0, 0, []), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(TrigonometryAddress, trig.ToImmutableArray()).Add(DistanceAddress, Enumerable.Repeat((byte)1, 16384).ToImmutableArray()).Add(0x1342D2C, ImmutableArray.Create((byte)0)), ImmutableDictionary<short, NativeBattleEffectFileAsset>.Empty);
    }
    internal static NativeBattleEffect65Frame Write(NativeBattleEffect65Frame frame, uint address, NativeBattleEffectArena bytes, int length) => NativeBattleEffect65Controller.WriteBytes(frame, address, bytes, length);
    internal static NativeBattleEffectArena Read(NativeBattleEffect65Frame frame, uint address, int length) => NativeBattleEffect65Controller.ReadBytes(frame, address, length);
    internal static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}

public sealed class NativeBattleEffect65LowerTests
{
    [Fact]
    public void Camera_interpolation_publishes_saturated_IR_and_final_GTE_state()
    {
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Frame frame = NativeBattleEffect65LowerFixture.Frame();
        NativeBattleEffect65Frame result = NativeBattleEffect65Camera.Interpolate6FC250(frame, new(100, -100, 50), new(300, 100, -50), new(300, 100, -50), new(100, -100, 50), -10, 10, 1, 3, NativeBattleEffect65LowerFixture.Inputs(operations));
        Assert.Equal((short)0, Assert.IsType<NativeBattleEffect65CameraRoll>(operations[0]).Roll);
        NativeBattleEffect65CameraPoseXYZ pose = Assert.IsType<NativeBattleEffect65CameraPoseXYZ>(operations[1]);
        Assert.Equal(new NativeBattleCameraPoint(200, 0, 0), pose.Target);
        Assert.Equal(new NativeBattleCameraPoint(200, 0, 0), pose.Eye);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new int?[] { 200, 0, 0 }, result.Memory.ScriptedColorRegisters.Ir);
        Assert.Equal(frame.Random, result.Random);
    }
}
