using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeLionHeart165(NativeBattleGraphicsState graphics, NativeBattleLionHeart165Resources resources)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Lion Heart requires its selected original module.");
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native Lion Heart requires its entered parameters.");
        NativeBattleGraphicsModel model = graphics.Models[parameters.Actor78];
        NativeBattleVector rotation = new(model.Rotation12 ?? throw new NotSupportedException("Native Lion Heart requires actor rotationX."), model.Rotation14 ?? throw new NotSupportedException("Native Lion Heart requires actor rotationY."), model.Rotation16 ?? throw new NotSupportedException("Native Lion Heart requires actor rotationZ."));
        NativeBattleVector bone = ReadNativeGraphicsBone(graphics, parameters.Actor78, 241, 0);
        NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (parameters.TargetMask7A & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Native Lion Heart requires actual target positions.")).ToArray());
        NativeBattleLionHeart165EntryStep entry = NativeBattleLionHeart165Controller.Enter5E9470(NativeEffectMemory ?? throw new NotSupportedException("Native Lion Heart requires its retained process memory."), effect, NativeFileStaging ?? NativeBattleFileStaging.Unknown(), nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Lion Heart requires its retained CRT random owner."), rotation, bone, center, model.RotationPadding18);
        NativeEffectMemory = entry.Frame.Memory;
        NativeFileStaging = entry.Frame.Staging;
        nativeCrtRandom = entry.Frame.Random;
        return graphics with { OriginalEffect = entry.Frame.Effect, Models = graphics.Models.SetItem(parameters.Actor78, model with { Rotation14 = entry.Facing14 }) };
    }

    private NativeBattleGraphicsState AdvanceNativeLionHeart165(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Lion Heart requires its retained process memory.");
        NativeBattleLionHeart165Resources resources = memory.LionHeart165?.RetainedResources ?? throw new NotSupportedException("Native Lion Heart requires resources retained by actual entry.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Lion Heart requires its actual camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Lion Heart requires its actual projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Lion Heart requires its published view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Lion Heart requires its original color arithmetic.");
        NativeBattleLionHeart165Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Lion Heart requires its entered module."), NativeFileStaging ?? throw new NotSupportedException("Native Lion Heart requires its physical resource bytes."), nativeCrtRandom ?? throw new NotSupportedException("Native Lion Heart requires its retained CRT random owner."));
        NativeBattlePilotVisualIdentity drawingIdentity = default;
        NativeBattleLionHeart165Inputs inputs = new(graphics.Flags, () => NativeEffectIoResult ?? throw new NotSupportedException("Native Lion Heart requires its signed file IO result."), actor => graphics.Models[actor], actor => ReadNativeModelAnchor(graphics, actor), (actor, selector, fraction) => ReadNativeGraphicsBone(graphics, actor, selector, fraction), actor => ReadNativeRenzokuken160WeaponTip(graphics, actor), Execute);
        frame = NativeBattleLionHeart165Controller.Advance5F01F0(frame, Child);
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleLionHeart165ChildStep Child(NativeBattleLionHeart165Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            drawingIdentity = NativeBattlePilotVisualLifetimes.Identity(165, pool.Slots[slot]);
            return pool.Slots[slot].Callback switch
            {
                0x5E9650 => NativeBattleLionHeart165Controller.Advance5E9650(current, pool, slot, inputs, view),
                0x63E9C0 => NativeBattleLionHeart165Controller.AdvanceCamera63E9C0(current, pool, slot, graphics.Flags, (NativeCamera ?? throw new NotSupportedException("Native Lion Heart requires its current camera owner.")).CurrentPose ?? throw new NotSupportedException("Native Lion Heart requires its current camera pose."), PublishCamera),
                0x5E9D50 => NativeBattleLionHeart165Controller.Advance5E9D50(current, pool, slot, inputs),
                0x5E9E00 => NativeBattleLionHeart165Controller.Advance5E9E00(current, pool, slot, inputs, view, projection, colors),
                0x5EB790 or 0x5ED760 or 0x5EE850 => NativeBattleLionHeart165Controller.AdvanceBillboardMesh(current, pool, slot, inputs, view),
                0x5EBFC0 or 0x5ED300 or 0x5EE0F0 or 0x5EFFF0 => NativeBattleLionHeart165Controller.AdvanceWorldMesh(current, pool, slot, inputs, view),
                0x5EAAF0 or 0x5EAD50 or 0x5EB8D0 or 0x5ED8E0 => NativeBattleLionHeart165Controller.AdvanceMeshParticles(current, pool, slot, inputs, view),
                0x5EB070 or 0x5EB410 or 0x5EBBD0 or 0x5EC550 or 0x5EC7D0 or 0x5EE240 or 0x5EE980 or 0x5EFCE0 => NativeBattleLionHeart165Controller.AdvanceSpriteParticles(current, pool, slot, inputs, Sprite(pool.Slots[slot].Callback), view, projection, camera.Roll),
                0x5ECB40 or 0x5ECCD0 or 0x5EDC10 => NativeBattleLionHeart165Controller.AdvanceAlignedMesh(current, pool, slot, inputs, view, projection),
                0x5EA8E0 or 0x5ED590 => NativeBattleLionHeart165Controller.AdvanceWindowedMesh(current, pool, slot, inputs, view, projection),
                0x5ECE60 or 0x5EE4C0 => NativeBattleLionHeart165Controller.AdvanceOrientedParticles(current, pool, slot, inputs, view),
                0x5EC140 => NativeBattleLionHeart165Controller.Advance5EC140(current, pool, slot, inputs),
                0x5ED1A0 => NativeBattleLionHeart165Controller.Advance5ED1A0(current, pool, slot, inputs),
                0x5ED4E0 => NativeBattleLionHeart165Controller.Advance5ED4E0(current, pool, slot, inputs),
                0x5EECD0 => NativeBattleLionHeart165Controller.Advance5EECD0(current, pool, slot, inputs),
                0x5F0130 => NativeBattleLionHeart165Controller.Advance5F0130(current, pool, slot, inputs),
                0x5F01B0 => NativeBattleLionHeart165Controller.Advance5F01B0(current, pool, slot, TextureLoad),
                uint callback => throw new NotSupportedException($"Native Lion Heart callback0x{callback:X} has no original implementation.")
            };
        }
        NativeBattleSearchSpriteResource Sprite(uint callback)
        {
            uint address = callback is 0x5EB070 or 0x5EB410 or 0x5EFCE0 ? 0xD7B1C8u : callback == 0x5EBBD0 ? 0xD7B4D4u : callback is 0x5EC550 or 0x5EE240 ? 0xD7AFE0u : 0xD7B340u;
            return resources.Sprites.Single(sprite => sprite.Address == address);
        }
        NativeBattleLionHeart165Frame PublishCamera(NativeBattleLionHeart165Frame current, NativeBattleGuardianCameraStep step)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Lion Heart requires its current camera owner.")).WriteEffectGlobals(step.Pose);
            return current;
        }
        NativeBattleLionHeart165Frame Execute(NativeBattleLionHeart165Frame current, NativeBattleLionHeart165Operation operation)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            switch (operation)
            {
                case NativeBattleLionHeart165Silence:
                    events.Add(new(Frame, "NativeSoundStopAll", null, null, 0, "Native46B450 stops all32 sound-effect voices.") { NativeStopAllSounds = true });
                    return current;
                case NativeBattleLionHeart165BindActor:
                    byte actor = current.Parameters.Actor78;
                    NativeBattleGraphicsModel actorModel = graphics.Models[actor];
                    RetainNativeDecodedPoseBeforeResourceChange(actorModel, NativeGraphicsPoseResource(graphics, actor));
                    NativeBattleVictoryResourceBinding bound = NativeBattleVictoryResources.Bind5022C0(NativeFileStaging!, NativeModelCpuResources ?? throw new NotSupportedException("Native Lion Heart requires its actual CPU resource rows."), actorModel, 0x23C5688, 0xD780C8);
                    NativeFileStaging = bound.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, bound.Model) };
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleLionHeart165ForceCamera:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Lion Heart requires its actual camera owner.")).Apply50A730();
                    return current;
                case NativeBattleLionHeart165Sequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Actor, sequence.Sequence);
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleLionHeart165ModelFlags flags:
                    NativeBattleGraphicsModel changed = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, changed with { Flags0 = unchecked((ushort)((changed.Flags0 | flags.Set) & ~flags.Clear)) }) };
                    return current;
                case NativeBattleLionHeart165Position position:
                    NativeBattleGraphicsModel moved = graphics.Models[position.Actor];
                    NativeBattleModelTransform transform = moved.Transform ?? throw new NotSupportedException("Native Lion Heart requires its actual raw model position.");
                    graphics = graphics with { Models = graphics.Models.SetItem(position.Actor, moved with { Transform = transform with { Position28 = position.Position, Vertical34 = position.Vertical34 } }) };
                    return current;
                case NativeBattleLionHeart165Rotation rotation:
                    NativeBattleGraphicsModel rotated = graphics.Models[rotation.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(rotation.Actor, rotated with { Rotation12 = rotation.Rotation.X, Rotation14 = rotation.Rotation.Y, Rotation16 = rotation.Rotation.Z, RotationPadding18 = rotation.Padding }) };
                    return current;
                case NativeBattleLionHeart165StageFlags flags:
                    NativeBattleStageState stageFlags = NativeStage ?? throw new NotSupportedException("Native Lion Heart requires its actual stage rows.");
                    NativeStage = stageFlags with { Groups = stageFlags.Groups.Select(group => group with { Flags = unchecked((byte)(flags.Set ? group.Flags | 2 : group.Flags & ~2)) }).ToImmutableArray() };
                    return current;
                case NativeBattleLionHeart165Sound sound:
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = 128, RawPan = 128 });
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleLionHeart165ReadTexture read:
                    NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == read.FileId);
                    NativeEffectIoResult = -1;
                    current = current with { Memory = current.Memory with { Arena = (current.Memory.Arena ?? throw new NotSupportedException("Native Lion Heart requires its retained resource arena.")).Write(0, file.Data.AsSpan()) } };
                    NativeEffectMemory = current.Memory;
                    QueueNativeFileIo482610(file.Data.Length, 0x508470);
                    return current;
                case NativeBattleLionHeart165EnqueueTexture:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Lion Heart requires its actual texture queue.");
                    (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native Lion Heart requires loaded texture bytes."), 0);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleLionHeart165Flash flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(flash.Rise, flash.Hold, flash.Fall, flash.Intensity) };
                    return current;
                case NativeBattleLionHeart165Contact contact:
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[current.Parameters.HeaderIndex7C];
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Lion Heart receipt requires its selected contact pointer.");
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(checked(first + contact.RelativeIndex))] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleLionHeart165Stage stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Native Lion Heart requires its actual stage rows.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleLionHeart165Shake shake:
                    NativeBattleCameraState shaking = NativeCamera ?? throw new NotSupportedException("Native Lion Heart requires its camera owner.");
                    NativeCamera = shaking with { ViewTranslationScratch = (shaking.ViewTranslationScratch ?? throw new NotSupportedException("Native Lion Heart requires its retained translation scratch.")) with { Y = shake.Offset } };
                    return current;
                case NativeBattleLionHeart165MeshDraw mesh:
                    NativeBattleStaticMeshResource resource = resources.Meshes.Single(resource => resource.Address == mesh.Address);
                    return AppendGeometry(current, NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, mesh.Matrix, projection, mesh.Context, colors, pilotIdentity: mesh.PilotIdentity ?? drawingIdentity with { Primitive = unchecked((int)mesh.Address) }), mesh.Common);
                case NativeBattleLionHeart165WindowedMeshDraw mesh:
                    NativeBattleStaticMeshResource windowed = resources.Meshes.Single(resource => resource.Address == mesh.Address);
                    return AppendGeometry(current, NativeBattleHoly175WindowedMeshPackets.EmitBatch650720(windowed.Bytes, mesh.Matrix, projection, mesh.Context, colors, pilotIdentity: mesh.PilotIdentity ?? drawingIdentity with { Primitive = unchecked((int)mesh.Address) }), mesh.Common);
                case NativeBattleLionHeart165Sprite sprite:
                    return AppendGeometry(current, sprite.Batch.CapturedWrites ?? throw new NotSupportedException("Native Lion Heart sprite requires its physical571DC0 packet stores."), sprite.Common);
                case NativeBattleLionHeart165TrailDraw trail:
                    current = AppendGeometry(current, trail.Batch, false);
                    int cursor = checked(current.Effect.DrawArenaOffset!.Value + trail.CursorAdvance - trail.Batch.AllocationBytes);
                    return current with { Effect = current.Effect with { DrawArenaOffset = cursor }, Staging = Dword(current.Staging, 0x23C6948, checked(0x209FAB8u + (uint)cursor)) };
                case NativeBattleLionHeart165Echo echo:
                    (current, graphics) = DrawNativeLionHeart165Echo(current, graphics, echo.Actor, echo.TemporaryAddress, projection, drawingIdentity);
                    return current;
                default:
                    throw new NotSupportedException("Native Lion Heart service has no original implementation.");
            }
        }
        NativeBattleLionHeart165Frame AppendGeometry(NativeBattleLionHeart165Frame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Lion Heart requires its initialized ordering table.");
            if (common)
            {
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Lion Heart requires its common packet owner."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }
            NativeBattleEffectPacketWrite effectWritten = NativeBattleGuardianWorkPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Lion Heart lost its actual private cursor."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = effectWritten.Ordering } };
            return current with { Memory = effectWritten.Memory, Effect = current.Effect with { DrawArenaOffset = effectWritten.Cursor }, Staging = Dword(current.Staging, 0x23C6948, checked(0x209FAB8u + (uint)effectWritten.Cursor)) };
        }
        NativeBattleLionHeart165Frame TextureLoad(NativeBattleLionHeart165Frame current, uint completion)
        {
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(0x20DFAB8, completion, (address, value) => current = current with { Effect = NativeBattleLionHeart165Controller.WriteCompletionByte(current.Effect, address, value) });
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }
        static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
    }

    private bool TryWriteNativeLionHeart165Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x23C5920);
        if (delta >= 100 * 36 || delta % 36 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5E9470 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleLionHeart165Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x5E9470 } current ? current : throw new NotSupportedException("Native Lion Heart completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleLionHeart165Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
