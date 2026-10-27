using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeFloat115(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFloat115Resources resources = effect.Binding.Float115Resources ?? throw new NotSupportedException("Native Float requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures!;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            textures = textures.Register5099A0(resources.Camera);
            NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
            NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Float requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        }
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521808, 0x25217E0, 20, 1);
        (root, _) = root.Append(0x6C9420);
        root = root.WriteSlot(0, root.Slots[0].WriteDword(12, 0).WriteDword(16, 0));
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = 0x2521808, DrawArenaOffset = null, Float115 = new(memory.Cursor) } };
    }

    private NativeBattleGraphicsState AdvanceNativeFloat115(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFloat115Resources resources = effect.Binding.Float115Resources ?? throw new NotSupportedException("Native Float requires its original resources.");
        int buffer = effect.Float115?.BufferOffset ?? throw new NotSupportedException("Native Float requires its executed initializer buffer.");
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleEffectTaskPool? targets = effect.Pools.Length > 1 ? effect.Pools[1] : null;
        NativeBattleModulePoolTask root = roots.Slots[0];
        if (roots.DescriptorAddress != 0x2521808 || root.Callback != 0x6C9420)
        {
            throw new InvalidDataException("Native Float lost its original root bank.");
        }
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 0x1158 : 0x19158)) };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        if (unchecked((short)root.ReadWord(12)) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                targets = NativeBattleEffectTaskPool.Initialize(0x25217F8, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer), 0x568, 3);
            }
            int action = root.ReadByte(14);
            if (action <= nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C].ReadByte(17) && targets!.Slots.Any(task => (task.Flags & 1) == 0))
            {
                NativeBattleEffectTaskPool pool = targets ?? throw new InvalidDataException("Native Float lost its target pool.");
                (pool, int slot) = pool.Append(0x6C9580);
                NativeBattleModulePoolTask task = pool.Slots[slot].ClearPayload(0x55C);
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C + action];
                int contact = header.FirstContactIndex ?? throw new InvalidDataException("Native Float requires the original selected contact.");
                int target = nativeActionPackets.ReadContact(contact).Primary.ActorIndex;
                task = task.WriteWord(14, (ushort)action).WriteDword(16, checked(0x1D972C0u + (uint)target * 156));
                task = Init(task, 0x14, 0x2E8, resources.Glyph);
                task = Init(task, 0x2FC, 0x26C, resources.Upper);
                targets = pool.WriteSlot(slot, task);
                root = root.WriteByte(14, unchecked((byte)(action + 1)));
            }
        }
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep step = (targets ?? throw new InvalidDataException("Native Float has no active target pool.")).Advance((pool, slot) =>
            {
                targets = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                if (task.Callback != 0x6C9580)
                {
                    throw new NotSupportedException("Native Float selected an unrelated target callback.");
                }
                int result = Target(ref task);
                task = task.UpdateHeader(task.Flags, task.State, targets.Slots[slot].Next, task.Callback);
                targets = targets.WriteSlot(slot, task);
                return new(targets, result);
            });
            targets = step.Pool;
            survivors = step.Survivors;
        }
        roots = roots.WriteSlot(0, root);
        if (survivors == 0)
        {
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        else
        {
            int counter = unchecked((short)root.ReadWord(12)) + 1;
            roots = roots.WriteSlot(0, root.WriteWord(12, unchecked((ushort)(counter >= 10 ? 0 : counter))));
        }
        return graphics with { OriginalEffect = effect with { Pools = targets is null ? [roots] : [roots, targets], ReturnedDescriptor = survivors == 0 ? 0u : 0x2521808u } };

        int Target(ref NativeBattleModulePoolTask task)
        {
            uint relative = unchecked(task.ReadDword(16) - 0x1D972C0u);
            if (relative % 156 != 0 || relative / 156 >= 7)
            {
                throw new InvalidDataException("Native Float target leaves its physical model bank.");
            }
            int actor = (int)(relative / 156);
            int counter = unchecked((short)task.ReadWord(12));
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if (counter == 0)
            {
                NativeBattleVector soundAnchor = ReadNativeDefaultPosition571400(graphics, actor);
                PublishNativeModuleSound(resources.Sound, 1, new(soundAnchor.X, soundAnchor.Y, soundAnchor.Z, 0));
            }
            int scale = ((model.Transform!.Scale38 >> 1) + 2048) >> 2;
            NativeBattleVector anchor = ReadNativeGraphicsBone(graphics, actor, 0xF1, 0);
            NativeBattleActionMatrix world = NativeBattlePandemona291Restoration.Rotation701270(-model.Rotation14!.Value) with { X = anchor.X, Y = model.Transform.Angle36, Z = anchor.Z };
            Play(ref task, 0x14, 0x2E8, resources.Glyph, NativeBattleGfMatrices.Compose56C2F0(view, world), scale, 0);
            if (counter >= 12)
            {
                anchor = ReadNativeGraphicsBone(graphics, actor, 0xF0, 0);
                world = NativeBattlePandemona291Restoration.Rotation701270(-model.Rotation14.Value) with { X = anchor.X, Y = anchor.Y, Z = anchor.Z };
                NativeBattleActionMatrix context = NativeBattleGfMatrices.Compose56C2F0(view, world);
                int depth = NativeBattleFloat115Geometry.UpperDepth(context, resources.DepthPoint, model.Transform.Scale38);
                Play(ref task, 0x2FC, 0x26C, resources.Upper, context, scale, depth);
            }
            if (counter == 64)
            {
                int action = WindWord(task, 14);
                int contact = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C + action].FirstContactIndex ?? throw new InvalidDataException("Native Float requires its completing contact.");
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            }
            if (counter >= 70)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return 0;
        }

        int Play(ref NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleActionMatrix context, int scale, int depth)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (offset - 12), size);
            NativeBattleGfAnimationAdvance advance = workspace.Advance701970(source, 0, (current, node) =>
            {
                if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
                {
                    return current;
                }
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + unchecked((short)node.GeometryIndex) * 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
                ImmutableArray<byte> vertices;
                if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
                {
                    int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                    vertices = source.Data.Slice(geometry + 12 + shape * count * 8, count * 8);
                }
                else
                {
                    NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                    vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(buffer + 0x1038, checked(count * 8)).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(buffer + 0x1038, vertices.AsSpan()) };
                    vertices = vertices.Slice(0, count * 8);
                }
                NativeBattleActionMatrix matrix = NativeBattleFloat115Geometry.Compose(node, context, scale);
                uint flags = node.Scalar36 == 0 ? 0x2030u : 0x20F0u;
                NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, vertices, matrix, projection, new(flags, node.Color32, node.Scalar36, null, null, depth, 17, 2), colors);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
                NativeEffectMemory = written.Memory;
                effect = effect with { DrawArenaOffset = written.Cursor };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
                return current;
            });
            task = PublishWorkspace(task, offset, advance.Workspace);
            return advance.Remaining;
        }

        NativeBattleModulePoolTask Init(NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (offset - 12), size);
            return PublishWorkspace(task, offset, workspace.Initialize7016B0(source, size));
        }
    }
}
