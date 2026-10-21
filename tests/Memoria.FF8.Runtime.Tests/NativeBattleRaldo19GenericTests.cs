using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRaldo19GenericTests
{
    [Fact]
    public void Sprite_then_mesh_share_COMMON_and_preserve_private_banks_across_JSON_restore()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x1000, new byte[] { 11, 22, 33, 44 }), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() };
        NativeBattleOriginalEffectState effect = new(19, 18, new(19, NativeBattleRaldo19Profile.Entry, NativeBattleRaldo19Profile.Initializer), null, null);
        NativeBattleCuragaFrame frame = new(memory, effect, new(0x1000, 0x1000, 0x5000, 0x9000, null));
        NativeBattleGeometryPrimitive primitive = new(17, [0x01000000, 0xE1000220], []);
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, primitive.Words[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), primitive.Words[1]);
        NativeBattleGfGeometryBatch batch = new([new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), primitive)], 8);
        NativeBattleCuragaNodeDrawStep sprite = NativeBattleCuragaNodeKinematics.CommitBatch(frame, NativeBattleLogicalOrdering.Clear(), batch, false);
        NativeBattleCuragaNodeDrawStep restored = JsonSerializer.Deserialize<NativeBattleCuragaNodeDrawStep>(JsonSerializer.Serialize(sprite))!;
        NativeBattleCuragaNodeDrawStep mesh = NativeBattleCuragaNodeKinematics.CommitBatch(sprite.Frame, sprite.Ordering, batch, true);
        NativeBattleCuragaNodeDrawStep restoredMesh = NativeBattleCuragaNodeKinematics.CommitBatch(restored.Frame, restored.Ordering, batch, true);
        Assert.Equal(16, mesh.Frame.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(frame.State, mesh.Frame.State);
        Assert.Equal(new byte[] { 11, 22, 33, 44 }, mesh.Frame.Memory.Arena!.Read(0x1000, 4));
        Assert.Equal(new[] { 0, 8 }, mesh.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset));
        Assert.All(mesh.Ordering.Proxies, proxy => Assert.Equal("off_B6D070", proxy.Packet.Bank));
        Assert.Equal(JsonSerializer.Serialize(mesh), JsonSerializer.Serialize(restoredMesh));
    }

    [Fact]
    public void Retained_node_bank_uses_the_original_module_identity_and_rejects_foreign_bytes()
    {
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(19);
        Assert.Throws<NotSupportedException>(() => profile.RequireExecutable(NativeBattleEffectMemory.FromColdProcess()));
        NativeBattleCuragaExecutableState cold = NativeBattleCuragaExecutableState.FromColdProcess(19);
        cold.Validate();
        Assert.Equal(0x4760, cold.ByteLength);
        Assert.Throws<InvalidDataException>(() => profile.WithExecutable(NativeBattleEffectMemory.FromColdProcess(), NativeBattleCuragaExecutableState.FromColdProcess(28)));
        NativeBattleEffectMemory memory = profile.WithExecutable(NativeBattleEffectMemory.FromColdProcess(), cold);
        Assert.Equal(cold, profile.RequireExecutable(memory));
        Assert.Equal(10 * 108, profile.EmitterStorageLength);
        Assert.Equal(70 * 672, profile.GeometryStorageLength);
        Assert.Equal(0x4000, profile.DrawBankStep);
    }
}
