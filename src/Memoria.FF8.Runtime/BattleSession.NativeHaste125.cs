using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeHaste125(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleHaste125Resources resources = effect.Binding.Haste125Resources ?? throw new NotSupportedException("Native Haste requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures!;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            textures = textures.Register5099A0(resources.Camera);
            NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
            NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Haste requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        }
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25213C0, 0x2521378, 20, 1);
        (root, _) = root.Append(0x6BB750);
        root = root.WriteSlot(0, root.Slots[0].WriteDword(12, 0).WriteDword(16, 0));
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = 0x25213C0, DrawArenaOffset = null, Haste125 = new(memory.Cursor) } };
    }

    private NativeBattleGraphicsState AdvanceNativeHaste125(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleHaste125Resources resources = effect.Binding.Haste125Resources ?? throw new NotSupportedException("Native Haste requires its original resources.");
        int buffer = effect.Haste125?.BufferOffset ?? throw new NotSupportedException("Native Haste requires its executed initializer buffer.");
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleEffectTaskPool? targets = effect.Pools.Length > 1 ? effect.Pools[1] : null;
        NativeBattleModulePoolTask root = roots.Slots[0];
        if (roots.DescriptorAddress != 0x25213C0 || root.Callback != 0x6BB750)
        {
            throw new InvalidDataException("Native Haste lost its original root bank.");
        }
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleActionMatrix effectCamera = NativeBattleGfMatrices.Normalize67AAE0(view);
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 0x1A70 : 0x19A70)) };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        if (unchecked((short)root.ReadWord(12)) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                targets = NativeBattleEffectTaskPool.Initialize(0x25213B0, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer), 0x7D0, 3);
            }
            int action = root.ReadByte(14);
            if (action <= nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C].ReadByte(17))
            {
                NativeBattleEffectTaskPool pool = targets ?? throw new InvalidDataException("Native Haste lost its target pool.");
                (pool, int slot) = pool.Append(0x6BB8D0);
                NativeBattleModulePoolTask task = pool.Slots[slot].ClearPayload(0x7C4);
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C + action];
                int contact = header.FirstContactIndex ?? throw new InvalidDataException("Native Haste requires the original selected contact.");
                int target = nativeActionPackets.ReadContact(contact).Primary.ActorIndex;
                task = WindStore(WindStore(task.WriteWord(14, (ushort)action).WriteDword(16, checked(0x1D972C0u + (uint)target * 156)), 20, ReadNativeDefaultPosition571400(graphics, target)), 28, ReadNativeGraphicsBone(graphics, target, 0xF0, 0));
                task = Init(task, 0x24, 0x254, resources.Clock);
                task = Init(task, 0x278, 0x558, resources.Lower);
                targets = pool.WriteSlot(slot, task);
                root = root.WriteByte(14, unchecked((byte)(action + 1)));
            }
        }
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep step = (targets ?? throw new InvalidDataException("Native Haste has no active target pool.")).Advance((pool, slot) =>
            {
                targets = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                if (task.Callback != 0x6BB8D0)
                {
                    throw new NotSupportedException("Native Haste selected an unrelated target callback.");
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
        return graphics with { OriginalEffect = effect with { Pools = targets is null ? [roots] : [roots, targets], ReturnedDescriptor = survivors == 0 ? 0u : 0x25213C0u } };

        int Target(ref NativeBattleModulePoolTask task)
        {
            uint relative = unchecked(task.ReadDword(16) - 0x1D972C0u);
            if (relative % 156 != 0 || relative / 156 >= 7)
            {
                throw new InvalidDataException("Native Haste target leaves its physical model bank.");
            }
            int actor = (int)(relative / 156);
            int counter = unchecked((short)task.ReadWord(12));
            NativeBattleVector anchor = WindVector(task, 20);
            if (counter == 6)
            {
                PublishNativeModuleSound(resources.Sound, 1, new(anchor.X, anchor.Y, anchor.Z, 0));
            }
            NativeBattleGraphicsModel model = graphics.Models[actor];
            int scale = (model.Transform!.Scale38 >> 1) + 2048;
            NativeBattleActionMatrix world = NativeBattlePandemona291Restoration.Rotation701270(-model.Rotation14!.Value) with { X = anchor.X, Y = model.Transform.Angle36, Z = anchor.Z };
            int remaining = Play(ref task, 0x24, 0x254, resources.Clock, NativeBattleGfMatrices.Compose56C2F0(view, world), scale);
            world = world with { Y = WindWord(task, 30) - (scale >> 4) - (scale >> 2) };
            remaining |= Play(ref task, 0x278, 0x558, resources.Lower, NativeBattleGfMatrices.Compose56C2F0(view, world), scale);
            if (counter == 48)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 1, 96) };
            }
            if (remaining == 0)
            {
                int action = WindWord(task, 14);
                int contact = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C + action].FirstContactIndex ?? throw new InvalidDataException("Native Haste requires its completing contact.");
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return 0;
        }

        int Play(ref NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleActionMatrix context, int scale)
        {
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(offset - 12, size));
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
                    vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(buffer + 0x1770, checked(count * 8)).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(buffer + 0x1770, vertices.AsSpan()) };
                    vertices = vertices.Slice(0, count * 8);
                }
                NativeBattleActionMatrix matrix = NativeBattleHaste125Geometry.Compose(node, context, effectCamera, scale);
                uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
                flags |= (node.Header & 0x2000) != 0 ? 12u : 0;
                flags |= node.Scalar36 != 0 ? 0xC0u : 0;
                NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(source.Data, geometry, vertices, matrix, projection, new(flags, node.Scalar36 != 0 ? node.Color32 : null, node.Scalar36, null, null, null, 17, 2), colors);
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
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(offset - 12, size));
            return PublishWorkspace(task, offset, workspace.Initialize7016B0(source, size));
        }
    }
}
