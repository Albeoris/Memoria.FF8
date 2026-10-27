using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDuel112(NativeBattleGraphicsState graphics, NativeBattleDuel112Resources resources)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Duel requires its selected original module.");
        NativeBattleDuel112Frame frame = NativeBattleDuel112Controller.Enter6CBC10(NativeEffectMemory ?? throw new NotSupportedException("Native Duel requires its retained process owner."), effect, NativeFileStaging ?? NativeBattleFileStaging.Unknown(), nativeCrtRandom ?? throw new NotSupportedException("Native Duel requires its retained CRT random owner."), resources, current =>
        {
            NativeBattleEffectResourcePointer texture = current.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x25218E4) ?? throw new NotSupportedException("Native Duel requires its actual initializer texture pointer.");
            NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Duel requires its retained texture queue.");
            (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native Duel requires the actual initializer TIM bytes."), texture.ArenaOffset);
            return current with { Memory = current.Memory with { Textures = textures }, Staging = current.Staging.Write(0x25218E4, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)texture.ArenaOffset))) };
        });
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging.Write(NativeBattleDuel112Resources.Address, resources.ReadActorDat().AsSpan());
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeDuel112(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Duel requires its retained effect memory.");
        NativeBattleDuel112Resources resources = memory.Duel112?.Resources ?? throw new NotSupportedException("Native Duel requires resources retained by its actual entry.");
        NativeBattleEffectParameters parameters = memory.Duel112?.Parameters ?? throw new NotSupportedException("Native Duel requires its entered action parameters.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Duel requires its current camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Duel requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Duel requires its current view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Duel requires its original color arithmetic.");
        NativeBattleDuel112Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Duel requires its selected module."), NativeFileStaging ?? throw new NotSupportedException("Native Duel requires its entered shared byte owner."), nativeCrtRandom ?? throw new NotSupportedException("Native Duel requires its CRT random owner."));
        NativeBattleDuel112ActorServices actor = new(index => graphics.Models[index], (index, model) => graphics = graphics with { Models = graphics.Models.SetItem(index, model) }, sequence => nativeActionPackets.ReadContact(FirstContact(sequence)).Primary.ActorIndex, Bind, Select);
        NativeBattleDuel112DrawingInputs drawing = new(actor, (index, bone, fraction) => ReadNativeGraphicsBone(graphics, index, bone, fraction), view, projection, colors, (rise, hold, fall, strength) => graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash((short)rise, (short)hold, (short)fall, (short)strength) });
        NativeBattleDuel112TargetInputs target = new(new(drawing, index => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native571400 requires its actual model WORD36.")).Angle36 }), descriptor => PublishNativeModuleSound(new(descriptor, 0, null) { Pan = 128, RawPan = 128 }), intensity =>
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Duel stage depth requires its actual retained groups.");
            NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity), ColorFlags = 0 }).ToImmutableArray() };
        }, Contact);
        frame = NativeBattleDuel112Controller.Advance6CBCF0(frame, () => nativeActionPackets.Headers[parameters.HeaderIndex7C].ReadByte(17), (current, slot, sequence) => NativeBattleDuel112Target.Initialize6CBF90(current, slot, sequence, actor), Child, current =>
        {
            NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Duel animation registration requires its retained animation owner.");
            textures = textures.Register5099A0(new("Duel112", resources.StaticData), checked((int)(0x12DAFA8u - NativeBattleDuel112Resources.Address)));
            return current with { Memory = current.Memory with { Textures = textures } };
        });
        Publish(frame);
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        int FirstContact(int sequence)
        {
            int header = checked(parameters.HeaderIndex7C + sequence);
            if ((uint)header >= NativeBattleActionPacketsState.HeaderCapacity)
            {
                throw new InvalidDataException("Native Duel sequence leaves the actual physical action-header arena.");
            }
            return nativeActionPackets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native Duel requires the selected header's actual contact pointer.");
        }
        void Publish(NativeBattleDuel112Frame current)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            graphics = graphics with { OriginalEffect = current.Effect };
        }
        NativeBattleDuel112Frame Bind(NativeBattleDuel112Frame current, int index, uint row)
        {
            Publish(current);
            NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[index], NativeGraphicsPoseResource(graphics, index));
            var bound = NativeBattleVictoryResources.Bind5022C0(resources.ReadActorDat(), NativeModelCpuResources ?? throw new NotSupportedException("Native Duel requires its actual CPU resource rows."), model, row, NativeBattleDuel112Resources.Address, Read, (address, pointer) => current = current with { Effect = current.Effect with { Pools = NativeBattleEffectTaskMemory.WritePointer(current.Effect.Pools, address, pointer) } });
            graphics = graphics with { Models = graphics.Models.SetItem(index, bound.Model), OriginalEffect = current.Effect };
            return current;

            NativeBattleResourcePointer? Read(uint address)
            {
                if (NativeBattleEffectTaskMemory.TryReadPointer(current.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
                {
                    return pointer;
                }
                return IsNativeEffectArenaAddress(address) ? (current.Memory.Arena ?? throw new NotSupportedException("Native Duel source pointer requires its actual arena.")).ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : current.Staging.ReadResourcePointer(address);
            }
        }
        NativeBattleDuel112Frame Select(NativeBattleDuel112Frame current, int index, byte sequence)
        {
            Publish(current);
            graphics = SelectNativeHeaderSequence(graphics, checked((byte)index), sequence);
            return current;
        }
        NativeBattleDuel112Frame Contact(NativeBattleDuel112Frame current, int sequence)
        {
            Publish(current);
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(FirstContact(sequence))] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Duel contact lost its actual effect-memory owner.") };
        }
        NativeBattleDuel112TaskStep Child(NativeBattleDuel112Frame current, int poolIndex, NativeBattleEffectTaskPool pool, int slot)
        {
            Publish(current);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Duel requires its initialized ordering table.");
            if (poolIndex == 1 && pool.Slots[slot].Callback == 0x6CBF90)
            {
                NativeBattleDuel112TargetStep step = NativeBattleDuel112Target.Advance6CBF90(current, slot, target, primitive.Pending);
                NativeBattlePrimitiveFrame currentPrimitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Duel target lost its current ordering owner.");
                graphics = graphics with { PrimitiveFrame = currentPrimitive with { Pending = step.Ordering } };
                return new(step.Frame, step.Pool, step.Result);
            }
            if (poolIndex == 4 && pool.Slots[slot].Callback == 0x6CD870)
            {
                return NativeBattleDuel112Target.Advance6CD870(current, slot, (next, destination, completion) =>
                {
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(destination, completion, (address, value) => next = next with { Effect = NativeBattleDuel112Controller.WriteCompletionByte(next.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return next;
                });
            }
            NativeBattleDuel112ParticleStep particle = pool.Slots[slot].Callback switch
            {
                0x6CD5C0 when poolIndex == 2 => NativeBattleDuel112Particles.Advance6CD5C0(pool.Slots[slot], Sprite),
                0x6CD750 when poolIndex == 3 => NativeBattleDuel112Particles.Advance6CD750(pool.Slots[slot], Sprite),
                0x6CD640 when poolIndex == 4 => NativeBattleDuel112Particles.Advance6CD640(pool.Slots[slot], Debris),
                _ => throw new NotSupportedException("Native Duel selected an unsupported child callback or physical bank.")
            };
            pool = current.Effect.Pools[poolIndex];
            pool = pool.WriteSlot(slot, particle.Task);
            current = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(poolIndex, pool) } };
            return new(current, pool, particle.Result);

            void Sprite(NativeBattleDuel112Sprite sprite)
            {
                NativeBattleSearchSpriteResource source = resources.Sprites.Single(resource => resource.Address == sprite.ResourceAddress);
                NativeBattleModuleResource resource = new(0, source.Address, 0, 0, source.Bytes);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(sprite.Phase), Phase = sprite.Phase, Anchor = new(sprite.Position.X, sprite.Position.Y, sprite.Position.Z, 0), Scale = unchecked((short)sprite.Scale), DepthOffset = sprite.DepthOffset, SpriteFlags = 0 };
                NativeBattleDuel112DrawingStep drawn = NativeBattleDuel112Target.Append6CBF90(current, Ordering(), NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, camera.Roll, 17, 2));
                current = drawn.Frame;
                StoreOrdering(drawn.Ordering);
            }
            void Debris(NativeBattleModulePoolTask debris)
            {
                NativeBattleStaticMeshResource source = resources.Meshes.Single(mesh => mesh.Address == 0x12C7230);
                NativeBattleVector position = new(Signed(16), Signed(18), Signed(20));
                NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(new(Signed(32), Signed(34), Signed(36))) with { X = position.X, Y = position.Y, Z = position.Z };
                NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(source.Bytes, 0, default, NativeBattleGfMatrices.Compose56C2F0(view, local), projection, new(0, null, null, null, null, null, 17, 2), colors);
                int cursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Duel debris requires its actual packet cursor.");
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, Ordering(), batch);
                current = current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor }, Staging = current.Staging.Write(0x2521914, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)written.Cursor))) };
                StoreOrdering(written.Ordering);

                short Signed(int offset) => unchecked((short)debris.ReadWord(offset));
            }
        }
        NativeBattleLogicalOrdering Ordering() => (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Duel requires its current ordering owner.")).Pending;
        void StoreOrdering(NativeBattleLogicalOrdering ordering)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Duel requires its current ordering owner.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordering } };
        }
    }

    private bool TryWriteNativeDuel112Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x20E0A00u);
        if (delta >= 16 * 48 || delta % 48 != 14 || graphics.OriginalEffect is not { Binding.Entry: 0x6CBBE0 } effect)
        {
            return false;
        }
        updated = graphics with { OriginalEffect = NativeBattleDuel112Controller.WriteCompletionByte(effect, address, value) };
        return true;
    }
}
