using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect326BonesTests
{
    [Fact]
    public void LocalQueryUsesItsZeroStackVectorWithoutTouchingTheGlobalAnchor()
    {
        byte[] source = new byte[100];
        BitConverter.GetBytes(4).CopyTo(source, 0);
        BitConverter.GetBytes(24).CopyTo(source, 4);
        BitConverter.GetBytes(88).CopyTo(source, 8);
        BitConverter.GetBytes(92).CopyTo(source, 12);
        BitConverter.GetBytes(100).CopyTo(source, 16);
        BitConverter.GetBytes(100).CopyTo(source, 20);
        source[24] = 1;
        BitConverter.GetBytes((short)40).CopyTo(source, 42);
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 };
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(local, unchecked((ushort)0x5AA5));
        packed.Bytes.CopyTo(source, 56);
        NativeBattlePoseSource pose = new(source.AsSpan(24, 64).ToArray().ToImmutableArray(), [0]);
        NativeBattleEffectOwnedModelResource modelResource = new(ArenaAddress, source.ToImmutableArray(), pose, []);
        NativeBattleEffect326Resources resources = new(null!, null!, null!, [], ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, [], 0)
        {
            MainModel = modelResource,
            EmbeddedModel = null!,
            LateModel = null!,
            ScrollPixels = [],
            StaticFronts = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty
        };
        NativeBattleEffect326State state = NativeBattleEffect326State.FromColdProcess().WriteWord(0x24F1268, 2);
        NativeBattleCactuarRawModelState model = state.Models[0x24F1268]! with { ResourceAddress = ArenaAddress, WorkingBase = ArenaAddress };
        state = state with { Models = state.Models.SetItem(0x24F1268, model) };
        ImmutableArray<byte?> anchor = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, null, null];
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown() with { AnchorScratch8 = anchor };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0, source), GeometryRegisters = geometry, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleOriginalEffectState effect = new(Effect, Effect - 1, new(Effect, Entry, Initializer), null, 0);
        NativeBattleEffect326Frame frame = new(memory, effect, new(1), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear().Write(6, [0xA5, 0x5A]);

        NativeBattleEffect326PointStep result = NativeBattleEffect326Controller.ReadOwnedBone631050(frame, resources, 0x24F1268, 0, 2048, destination);

        Assert.Equal(new byte[] { 100, 0, 200, 0, 64, 1, 0xA5, 0x5A }, result.Point8.Read(0, 8));
        Assert.Equal(anchor, result.Frame.Memory.GeometryRegisters!.AnchorScratch8);
        Assert.Equal((byte?)0xA5, result.Frame.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Equal((byte?)0x5A, result.Frame.Memory.GeometryRegisters.Rotation20[19]);
        Assert.All(result.Frame.Staging!.ReadSnapshot(NativeBattleTemporaryMemory.Address, 32).Known, known => Assert.False(known));
        Assert.Equal(BitConverter.GetBytes(NativeBattleTemporaryMemory.Address), result.Frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }
}
