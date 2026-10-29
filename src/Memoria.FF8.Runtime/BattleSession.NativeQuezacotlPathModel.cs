using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState SubmitNativeQuezacotlPathModel6A79B0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native6A79B0 requires its actual Quezacotl controller.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6A79B0 requires its retained process memory.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6A79B0 requires its prior projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native6A79B0 requires its previously published1D97778 camera matrix.");
        NativeBattleQuezacotlRigWrite composed = NativeBattleQuezacotlRig86.ComposePathModel5095B0(memory, effect, view);
        memory = composed.Memory;
        NativeBattleEffectArena arena = memory.Arena!;
        const uint arenaAddress = 0x20DFAB8;
        int geometry = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(NativeBattleQuezacotlRig86.Directory + 4, 4)) - arenaAddress));
        int animation = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(NativeBattleQuezacotlRig86.Directory + 8, 4)) - arenaAddress));
        uint rgb = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(NativeBattleQuezacotlRig86.Model + 0x28, 4));
        uint mask = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(NativeBattleQuezacotlRig86.Model + 0x7C, 4));
        NativeBattleQuezacotlPathModelBatch batch = NativeBattleQuezacotlPathModelPackets.Emit6A79B0(ImmutableCollectionsMarshal.AsImmutableArray(arena.Read(geometry, checked(animation - geometry))), selector => NativeBattleQuezacotlRig86.ReadBoneMatrix(memory, selector), projection, rgb, mask, NativeBattlePilotVisualLifetimes.Identity(116, effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0], geometry));
        using NativeBattleEffectArenaWriter scratch = new(arena);
        foreach (NativeBattleGfGeometryAttempt write in batch.VertexScratchWrites)
        {
            //50F900 writes all eight bytes even for vertices later rejected by50FDF0.
            scratch.Write(checked(0x8D0 + write.ByteOffsetRelative), write.Data.AsSpan());
        }
        NativeEffectMemory = memory with { Arena = scratch.Freeze() };
        graphics = AppendNativePrimitivePackets(graphics, batch.Primitives);
        NativeBattleQuezacotlRigWrite restored = NativeBattleQuezacotlRig86.Restore508C90(NativeEffectMemory!, effect);
        NativeEffectMemory = restored.Memory;
        return graphics with { OriginalEffect = restored.Effect };
    }
}
