using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeNoMercy200(NativeBattleGraphicsState graphics, NativeBattleNoMercy200Resources resources)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native No Mercy requires its selected module.");
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native No Mercy requires its actual parameters.");
        NativeBattleGraphicsModel model = graphics.Models[parameters.Actor78];
        NativeBattleVector rotation = new(model.Rotation12 ?? throw new NotSupportedException("Native No Mercy requires actor rotation X."), model.Rotation14 ?? throw new NotSupportedException("Native No Mercy requires actor rotation Y."), model.Rotation16 ?? throw new NotSupportedException("Native No Mercy requires actor rotation Z."));
        NativeBattleVector bone = ReadNativeGraphicsBone(graphics, parameters.Actor78, 241, 0);
        NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (parameters.TargetMask7A & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Native No Mercy requires actual target positions.")).ToArray());
        NativeBattleNoMercy200EntryStep entered = NativeBattleNoMercy200Controller.Enter6391D0(NativeEffectMemory ?? throw new NotSupportedException("Native No Mercy requires its retained process memory."), effect, NativeFileStaging ?? NativeBattleFileStaging.Unknown(), nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native No Mercy requires its retained CRT random owner."), rotation, model.RotationPadding18, bone, center);
        NativeEffectMemory = entered.Frame.Memory;
        NativeFileStaging = entered.Frame.Staging;
        nativeCrtRandom = entered.Frame.Random;
        return graphics with { OriginalEffect = entered.Frame.Effect, Models = graphics.Models.SetItem(parameters.Actor78, model with { Rotation14 = entered.Facing14 }) };
    }

    private NativeBattleGraphicsState AdvanceNativeNoMercy200(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native No Mercy requires its retained process memory.");
        NativeBattleNoMercy200Resources resources = memory.NoMercy200?.Resources ?? throw new NotSupportedException("Native No Mercy requires resources retained by actual entry.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native No Mercy requires its actual camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native No Mercy requires its actual projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native No Mercy requires its published view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native No Mercy requires its original color arithmetic.");
        NativeBattleNoMercy200Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native No Mercy requires its entered module."), NativeFileStaging ?? throw new NotSupportedException("Native No Mercy requires its retained resource memory."), nativeCrtRandom ?? throw new NotSupportedException("Native No Mercy requires its retained CRT random owner."));
        NativeBattleNoMercy200TargetInputs targets = new(nativeActionPackets, actor => ReadNativeModelAnchor(graphics, actor) with { W = (graphics.Models[actor].Transform ?? throw new NotSupportedException("Native571400 requires the actual model WORD36.")).Angle36 }, actor => graphics.Models[actor].Transform?.Scale38 ?? throw new NotSupportedException("Native No Mercy requires actual target scale."), actor => graphics.Models[actor].Transform?.Angle36 ?? throw new NotSupportedException("Native No Mercy requires actual target WORD36."));
        NativeBattleNoMercy200BoneInputs bones = new(actor => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native No Mercy requires its current actor skeleton.")).BoneCount, (actor, selector, fraction) => ReadNativeGraphicsBone(graphics, actor, checked((byte)selector), fraction));
        NativeBattleNoMercy200Inputs inputs = new(graphics.Flags, view, () => NativeEffectIoResult ?? throw new NotSupportedException("Native No Mercy requires its signed file IO result."), Execute);
        frame = NativeBattleNoMercy200Controller.AdvanceRoot(frame, inputs, Child);
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleNoMercy200ChildStep Child(NativeBattleNoMercy200Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            return pool.Slots[slot].Callback switch
            {
                0x639380 => NativeBattleNoMercy200Controller.Advance639380(current, pool, slot, inputs),
                0x63E9C0 => NativeBattleNoMercy200Controller.AdvanceCamera63E9C0(current, pool, slot, graphics.Flags, (NativeCamera ?? throw new NotSupportedException("Native No Mercy requires its current camera owner.")).CurrentPose ?? throw new NotSupportedException("Native No Mercy requires its current camera pose."), PublishCamera),
                0x639800 or 0x639FD0 => NativeBattleNoMercy200Controller.AdvanceTarget(current, pool, slot, graphics.Flags, targets),
                0x639A60 or 0x639DE0 or 0x639EF0 => NativeBattleNoMercy200Controller.AdvanceTargetParticle(current, pool, slot, inputs, definition.NativeModuleResources.Single(resource => resource.Id == 5)),
                0x63A250 => NativeBattleNoMercy200Controller.Advance63A250(current, pool, slot, inputs, ReadNativeRenzokuken160WeaponTip(graphics, current.Parameters.Actor78), projection, current.Memory.ScriptedGraphics, colors),
                0x63AD20 or 0x63AF40 or 0x63CD00 or 0x63D230 or 0x63DBD0 or 0x63E4E0 => NativeBattleNoMercy200Controller.AdvanceSimpleMesh(current, pool, slot, inputs),
                0x63B080 => NativeBattleNoMercy200Controller.Advance63B080(current, pool, slot, inputs),
                0x63CEF0 or 0x63DE90 or 0x63E170 => NativeBattleNoMercy200Controller.AdvanceSharedSparks(current, pool, slot, inputs, bones),
                0x63D670 => NativeBattleNoMercy200Controller.Advance63D670(current, pool, slot, inputs),
                0x63D800 => NativeBattleNoMercy200Controller.Advance63D800(current, pool, slot, inputs),
                uint callback => throw new NotSupportedException($"Native No Mercy callback0x{callback:X} has no original implementation.")
            };
        }

        NativeBattleNoMercy200Frame PublishCamera(NativeBattleNoMercy200Frame current, NativeBattleGuardianCameraStep step)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native No Mercy requires its current camera owner.")).WriteEffectGlobals(step.Pose);
            return current;
        }

        NativeBattleNoMercy200Frame Spawn(NativeBattleNoMercy200Frame current, uint callback)
        {
            NativeBattleEffectTaskPool pool = current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleNoMercy200Controller.Children);
            NativeBattleNoMercy200ChildStep born;
            switch (callback)
            {
                case 0x639780:
                    born = NativeBattleNoMercy200Controller.Begin639780(current, pool, targets);
                    break;
                case 0x63A220:
                    born = NativeBattleNoMercy200Controller.Begin63A220(current, pool);
                    break;
                case 0x63ACB0:
                    born = NativeBattleNoMercy200Controller.Begin63ACB0(current, pool, targets.ReadAnchor(current.Parameters.Actor78));
                    break;
                case 0x63AE80:
                    born = NativeBattleNoMercy200Controller.Begin63AE80(current, pool);
                    break;
                case 0x63CBD0:
                    born = NativeBattleNoMercy200Controller.Begin63CBD0(current, pool);
                    break;
                case 0x63D1F0:
                    born = NativeBattleNoMercy200Controller.Begin63D1F0(current, pool);
                    break;
                case 0x63D370:
                    born = NativeBattleNoMercy200Controller.Begin63D370(current, pool, targets);
                    break;
                case 0x63DAF0:
                    born = NativeBattleNoMercy200Controller.Begin63DAF0(current, pool, targets);
                    break;
                case 0x63DD10:
                    born = NativeBattleNoMercy200Controller.Begin63DD10(current, pool, targets);
                    break;
                case 0x63B080:
                case 0x63CEF0:
                    (pool, int slot) = pool.Append(callback);
                    pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 0).WriteWord(14, 0)) };
                    born = new(current, pool, 0);
                    break;
                default:
                    throw new NotSupportedException("Native No Mercy scene requires its original child initializer.");
            }
            return born.Frame with { Effect = born.Frame.Effect with { Pools = born.Frame.Effect.Pools.SetItem(born.Frame.Effect.Pools.IndexOf(born.Frame.Effect.Pools.Single(existing => existing.DescriptorAddress == pool.DescriptorAddress)), born.Pool) } };
        }

        NativeBattleNoMercy200Frame Execute(NativeBattleNoMercy200Frame current, NativeBattleNoMercy200Operation operation)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            switch (operation)
            {
                case NativeBattleNoMercy200Camera:
                    NativeBattleEffectTaskPool children = current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleNoMercy200Controller.Children);
                    NativeBattleNoMercy200ChildStep started = NativeBattleNoMercy200Controller.BeginCamera63E960(current, children);
                    current = started.Frame with { Effect = started.Frame.Effect with { Pools = started.Frame.Effect.Pools.SetItem(started.Frame.Effect.Pools.IndexOf(children), started.Pool) } };
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native No Mercy requires its actual camera owner.")).Apply50A730();
                    return current;
                case NativeBattleNoMercy200Bind:
                    byte actor = current.Parameters.Actor78;
                    NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[actor], NativeGraphicsPoseResource(graphics, actor));
                    NativeBattleVictoryResourceBinding bound = NativeBattleVictoryResources.Bind5022C0(current.Staging, NativeModelCpuResources ?? throw new NotSupportedException("Native No Mercy requires its actual CPU resource rows."), model, 0x24F9710, 0xE2B518);
                    NativeFileStaging = bound.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, bound.Model) };
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleNoMercy200Sequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, checked((byte)sequence.Actor), sequence.Sequence);
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleNoMercy200TargetSequence sequence:
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[current.Parameters.HeaderIndex7C];
                    int count = header.ReadByte(16);
                    if (count > 0)
                    {
                        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native No Mercy requires its selected contact pointer.");
                        for (int index = 0; index < count; index++)
                        {
                            graphics = SelectNativeHeaderSequence(graphics, nativeActionPackets.ReadContact(first + index).Primary.ActorIndex, sequence.Sequence);
                        }
                    }
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleNoMercy200Spawn spawn:
                    return Spawn(current, spawn.Callback);
                case NativeBattleNoMercy200Sound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, 0, null) { Pan = 128, RawPan = 128 });
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleNoMercy200Read read:
                    NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == read.File);
                    NativeEffectIoResult = -1;
                    current = current with { Memory = current.Memory with { Arena = (current.Memory.Arena ?? throw new NotSupportedException("Native No Mercy requires its effect arena.")).Write(read.ArenaOffset, file.Data.AsSpan()) } };
                    NativeEffectMemory = current.Memory;
                    QueueNativeFileIo482610(file.Data.Length, 0x508470);
                    return current;
                case NativeBattleNoMercy200Upload upload:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native No Mercy requires its actual texture queue.");
                    (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native No Mercy requires actual streamed texture bytes."), upload.ArenaOffset);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleNoMercy200Stage stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Native No Mercy requires its actual stage groups.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleNoMercy200Flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 6, 255) };
                    return current;
                case NativeBattleNoMercy200Contacts:
                    graphics = ApplyNativeHeaderContacts(graphics, current.Parameters.HeaderIndex7C);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleNoMercy200Reload reload:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(0x20DFAB8 + 114688, reload.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleNoMercy200Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleNoMercy200MeshDraw mesh:
                    NativeBattleStaticMeshResource resource = resources.Meshes.Single(resource => resource.Address == mesh.Address);
                    NativeBattleGfGeometryBatch batch;
                    if (mesh.Scrolling is { } scrolling)
                    {
                        NativeBattleScriptedGraphicsMemory registers = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native No Mercy requires its actual GTE registers.");
                        batch = NativeBattleShivaScrollingPackets.Emit64E080(resource, mesh.Matrix, projection, scrolling, registers.Zsf3 ?? throw new NotSupportedException("Native No Mercy requires ZSF3."), registers.Zsf4 ?? throw new NotSupportedException("Native No Mercy requires ZSF4."), registers.Dqa, registers.Dqb, colors);
                    }
                    else
                    {
                        batch = mesh.Tint is uint tint ? NativeBattleTintedMeshPackets.Emit(resource.Bytes, mesh.Matrix, projection, mesh.Context, tint, colors) : NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, mesh.Matrix, projection, mesh.Context, colors);
                    }
                    return AppendGeometry(current, batch, mesh.Common);
                case NativeBattleNoMercy200SpriteDraw sprite:
                    NativeBattleTemporaryAllocation? allocated = sprite.TemporaryAddress is null ? NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 180) : null;
                    uint context = sprite.TemporaryAddress ?? allocated!.Address;
                    NativeBattleFileStaging staging = allocated?.Staging ?? current.Staging;
                    NativeBattleModuleDraw draw = sprite.Draw;
                    if (allocated is not null)
                    {
                        staging = NativeBattleModuleSpriteTemporary.Begin571C80(staging, context, draw.Resource?.Address ?? throw new InvalidDataException("Native No Mercy sprite requires its resource."), draw.SpriteFlags);
                    }
                    if ((draw.SpriteFlags & 4) != 0)
                    {
                        byte[] color = new byte[4];
                        BinaryPrimitives.WriteUInt32LittleEndian(color, draw.SpriteColor);
                        staging = staging.Write(context + 28, color);
                    }
                    for (int index = 0; index < 4; index++)
                    {
                        if ((draw.SpriteFlags & (16 << index)) != 0)
                        {
                            staging = staging.Write(context + 32 + (uint)index, new byte[] { draw.SpritePaletteOffsets[index] });
                        }
                    }
                    NativeBattleModuleSpriteTemporaryResult emitted = NativeBattleModuleSpriteTemporary.Emit571C80(staging, context, draw, projection, camera.Roll, 17, 2);
                    current = AppendPackets(current with { Staging = emitted.Staging }, emitted.Batch, false);
                    return allocated is null ? current : current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 180) };
                case NativeBattleNoMercy200TrailDraw trail:
                    current = AppendGeometry(current, trail.Batch, false);
                    int cursor = checked((current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native No Mercy lost its private cursor.")) + trail.CursorAdvance - trail.Batch.AllocationBytes);
                    byte[] pointer = new byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(pointer, checked(0x20DFAB8u + (uint)cursor));
                    return current with { Effect = current.Effect with { DrawArenaOffset = cursor }, Staging = current.Staging.Write(0x24FBD64, pointer) };
                default:
                    throw new NotSupportedException("Native No Mercy service has no original implementation.");
            }
        }

        NativeBattleNoMercy200Frame AppendGeometry(NativeBattleNoMercy200Frame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native No Mercy requires its initialized ordering table.");
            if (common)
            {
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native No Mercy requires its common packet owner."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }
            NativeBattleEffectPacketWrite effectWritten = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native No Mercy lost its actual private cursor."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = effectWritten.Ordering } };
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, checked(0x20DFAB8u + (uint)effectWritten.Cursor));
            return current with { Memory = effectWritten.Memory, Effect = current.Effect with { DrawArenaOffset = effectWritten.Cursor }, Staging = current.Staging.Write(0x24FBD64, bytes) };
        }

        NativeBattleNoMercy200Frame AppendPackets(NativeBattleNoMercy200Frame current, NativeBattleModulePacketBatch batch, bool common)
        {
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            foreach (NativeBattleAllocatedPrimitive packet in batch.Primitives)
            {
                byte[] data = new byte[packet.Primitive.Words.Length * 4];
                for (int index = 0; index < packet.Primitive.Words.Length; index++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4, 4), packet.Primitive.Words[index]);
                }
                ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, data.Length).ToImmutableArray();
                attempts.Add(new(packet.ByteOffset, data.ToImmutableArray(), mask, packet.Primitive with { PhysicalWriteMask = mask }));
            }
            return AppendGeometry(current, batch.CapturedWrites ?? new(attempts.ToImmutable(), batch.AllocationBytes), common);
        }

    }

    private bool TryWriteNativeNoMercy200Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x24F8D60);
        if (delta >= 2 * 16 || delta % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x6391D0 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleNoMercy200Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x6391D0 } current ? current : throw new NotSupportedException("Native No Mercy completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleNoMercy200Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
