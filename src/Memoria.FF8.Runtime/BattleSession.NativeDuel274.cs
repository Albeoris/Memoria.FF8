using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDuel274(NativeBattleGraphicsState graphics, NativeBattleDuel274Resources resources)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Duel opening requires its retained process owner.");
        NativeBattleDuel274State state = memory.Duel274 ?? throw new NotSupportedException("Native Duel opening requires its actual cold process publication.");
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native Duel opening requires its retained shared byte owner.");
        if (!state.MutableImagePublished)
        {
            NativeBattleStaticMeshResource mesh = resources.Meshes.Single(mesh => mesh.Address == 0x10E9530);
            staging = staging.Write(mesh.Address, mesh.Bytes.AsSpan());
            state = state with { MutableImagePublished = true };
        }
        memory = memory with { Duel274 = state with { Resources = resources } };
        NativeBattleDuel274Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Duel opening requires its selected module."), staging, nativeCrtRandom ?? throw new NotSupportedException("Native Duel opening requires its retained CRT random owner."));
        frame = NativeBattleDuel274Controller.Enter6898B0(frame, NativeDuel274Actors(() => graphics, value => graphics = value), current =>
        {
            NativeBattleEffectResourcePointer texture = current.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2508410) ?? throw new NotSupportedException("Native Duel opening requires its actual initializer texture publication.");
            NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Duel opening requires its retained texture queue.");
            (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native Duel opening requires the actual initializer TIM bytes."), texture.ArenaOffset);
            return current with { Memory = current.Memory with { Textures = textures }, Staging = current.Staging.Write(0x2508410, BitConverter.GetBytes(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)texture.ArenaOffset))) };
        });
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging.Write(NativeBattleDuel274Resources.Address, resources.ReadActorDat().AsSpan());
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleDuel274ActorServices NativeDuel274Actors(Func<NativeBattleGraphicsState> read, Action<NativeBattleGraphicsState> write)
    {
        return new(index => read().Models[index].Flags0, (index, flags) =>
        {
            NativeBattleGraphicsState graphics = read();
            write(graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Flags0 = flags }) });
        }, index => read().Models[index].ReadLinkedModelIndex140() is int linked ? NativeBattleDuel274Controller.Address(linked) : 0, index => NativeBattleDuel274Controller.Index(NativeBattleScanModelSelection.Minimum501FF0(NativeBattleScanPhysicalModels.Capture(read().Models), NativeBattleDuel274Controller.Address(index))), index =>
        {
            NativeBattleGraphicsState graphics = read();
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
            return NativeBattleDiablos325Recipients.Bounds6DFD90(graphics.Models[index], resource.PoseSource ?? throw new NotSupportedException("Native Duel bounds require the actual current skeleton."), DiablosActorGeometry(resource, graphics.Models[index]));
        });
    }

    private NativeBattleGraphicsState AdvanceNativeDuel274(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Duel opening requires its retained process owner.");
        NativeBattleGraphicsState publicationBase = NativeGraphics ?? graphics;
        NativeBattleOriginalEffectState? acceptedEffect = null;
        NativeBattlePrimitiveProcess acceptedCommon = memory.PrimitiveProcess ?? throw new NotSupportedException("Native publication requires its common packet owner.");
        NativeBattleFileStaging acceptedStaging = NativeFileStaging ?? throw new NotSupportedException("Native publication requires its staging owner.");
        NativeBattleLogicalOrdering acceptedOrdering = (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native publication requires its ordering frame.")).Pending;
        bool commonCheckpoint = false;
        bool orderingCheckpoint = false;
        bool stagingCheckpoint = false;
        graphics = graphics with { OriginalEffect = graphics.OriginalEffect! with { PresentationPackets = [] } };
        using NativeBattleEffectArenaTransaction arena = new(memory.Arena ?? throw new NotSupportedException("Native Duel drawing requires its retained effect arena."));
        using NativeBattleFileStagingTransaction staging = new(NativeFileStaging ?? throw new NotSupportedException("Native Duel opening requires its retained shared byte owner."));
        using NativeBattleCommonPacketTransaction common = new(memory.PrimitiveProcess ?? throw new NotSupportedException("Native Duel drawing requires its retained common packet owner."));
        using NativeBattleLogicalOrderingTransaction ordering = new((graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Duel drawing requires its retained ordering frame.")).Pending);
        memory = memory with { Arena = arena.View, PrimitiveProcess = common.View };
        graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.View } };
        NativeBattleDuel274Resources resources = memory.Duel274?.Resources ?? throw new NotSupportedException("Native Duel opening requires resources retained by its actual entry.");
        NativeBattleDuel274Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Duel opening requires its selected module."), staging.View, nativeCrtRandom ?? throw new NotSupportedException("Native Duel opening requires its retained CRT random owner."));
        frame = frame with { CapturePresentation = true, PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(274, frame.Effect.Pools[0].Slots[0]) };
        using NativeBattleTaskCallerOperation caller = new(arena, frame.Effect.Pools);
        caller.AdmitCommon(common.CurrentMemory);
        BeginAuxiliaryCheckpoint();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Duel opening requires its actual action parameters.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Duel drawing requires its current camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Duel drawing requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Duel drawing requires its current view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Duel drawing requires its original color arithmetic.");
        NativeBattleDuel274ActorServices actors = NativeDuel274Actors(() => graphics, value => graphics = value);
        NativeBattleDuel274MoveInputs moves = new(index => graphics.Models[index], WriteModel, (index, bone) => ReadNativeGraphicsBone(graphics, index, checked((byte)bone), 0), ReadBoneMatrix, DrawEcho, CaptureEcho, SubmitEffect, DrawSprite, DrawSecondary, Contact, Flash, Sound);
        NativeBattleDuel274SelectedServices selected = new(actors, index => graphics.Models[index], WriteModel, Anchor, Bind, Select, Camera, QueueFile, Contact, Texture, Variant, Stage, (current, amount) => SubmitEffect(current, NativeBattleDuel274Presentation.Screen(NativeBattleDuel274Screen.Draw68D4B0(amount), current.PilotIdentity)));
        NativeEffectMemory = memory;
        NativeFileStaging = staging.View;
        try
        {
            frame = NativeBattleDuel274Controller.Advance689990(frame, actors, sequence => nativeActionPackets.ReadContact(FirstContact(sequence)).Primary.ActorIndex, (current, slot) => NativeBattleDuel274Controller.Advance689AC0(current, slot, actors, (next, targetSlot) => NativeBattleDuel274Target.AdvanceSelected689AC0(next, targetSlot, resources, selected)));
            Publish(frame, final: true);
            frame = frame with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native final publication lost its process memory.") };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = acceptedOrdering } };
            nativeCrtRandom = frame.Random;
            return graphics with { OriginalEffect = frame.Effect };
        }
        catch
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo? cleanupFailure = null;
            try
            {
                EndAuxiliaryCheckpoint(commit: false);
            }
            catch (Exception exception)
            {
                cleanupFailure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
            }
            try
            {
                caller.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
            }
            if (NativeEffectMemory is { } published)
            {
                NativeEffectMemory = published with
                {
                    Arena = ReferenceEquals(published.Arena?.Transaction, arena) ? caller.SourceArena : published.Arena,
                    PrimitiveProcess = ReferenceEquals(published.PrimitiveProcess?.CommonTransaction, common) ? acceptedCommon : published.PrimitiveProcess
                };
            }
            if (NativeFileStaging is { } publishedStaging && ReferenceEquals(publishedStaging.Transaction, staging))
            {
                NativeFileStaging = acceptedStaging;
            }
            if (NativeGraphics is { } publishedGraphics)
            {
                if (acceptedEffect is not null)
                {
                    publishedGraphics = publishedGraphics with { OriginalEffect = acceptedEffect };
                }
                if (publishedGraphics.PrimitiveFrame is { } publishedFrame && ReferenceEquals(publishedFrame.Pending.Transaction, ordering))
                {
                    publishedGraphics = publishedGraphics with { PrimitiveFrame = publishedFrame with { Pending = acceptedOrdering } };
                }
                NativeGraphics = publishedGraphics;
            }
            cleanupFailure?.Throw();
            throw;
        }

        void WriteModel(int index, NativeBattleGraphicsModel model) => graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        NativeBattleModuleAnchor Anchor(int index) => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native Duel anchor requires actual model WORD36.")).Angle36 };
        NativeBattleActionMatrix ReadBoneMatrix(int index, int bone)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Duel bone matrix requires the current skeleton.");
            return NativeBattleBodyBoneMatrices.Require(graphics.Models[index], source)[bone];
        }
        int FirstContact(int sequence)
        {
            int header = checked(parameters.HeaderIndex7C + sequence);
            if ((uint)header >= NativeBattleActionPacketsState.HeaderCapacity)
            {
                throw new InvalidDataException("Native Duel sequence leaves the physical action-header arena.");
            }
            return nativeActionPackets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native Duel requires the selected header's actual contact pointer.");
        }
        void Publish(NativeBattleDuel274Frame current, bool final = false)
        {
            caller.Admit(current.Effect.Pools);
            caller.AdmitCommon(common.CurrentMemory);
            EndAuxiliaryCheckpoint(commit: true);
            caller.PreparePrefix();
            NativeBattlePrimitiveProcess process = common.Freeze(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native publication lost its common process."));
            NativeBattleFileStaging files = staging.Freeze(current.Staging);
            NativeBattleLogicalOrdering pending = ordering.Freeze();
            int before = caller.AcceptedPrefixes;
            try
            {
                if (final)
                {
                    caller.Complete(pending, files);
                }
                else
                {
                    caller.AcceptPrefix(pending, files);
                }
            }
            finally
            {
                if (caller.AcceptedPrefixes != before)
                {
                    acceptedCommon = process;
                    acceptedStaging = files;
                    acceptedOrdering = pending;
                    acceptedEffect = current.Effect;
                    //Physical publication also retains the scheduler metadata that identifies newly admitted banks.
                    NativeGraphics = NativeBattleTaskCallerOperation.PublishEffectMetadata(NativeGraphics ?? publicationBase, current.Effect);
                    NativeBattleEffectMemory published = current.Memory;
                    NativeEffectMemory = final ? published with { Arena = caller.SourceArena, PrimitiveProcess = process } : published;
                    NativeFileStaging = final ? files : current.Staging;
                    nativeCrtRandom = current.Random;
                }
            }
            if (!final)
            {
                BeginAuxiliaryCheckpoint();
            }
        }

        void BeginAuxiliaryCheckpoint()
        {
            common.BeginCheckpoint();
            commonCheckpoint = true;
            ordering.BeginCheckpoint();
            orderingCheckpoint = true;
            staging.BeginCheckpoint();
            stagingCheckpoint = true;
        }

        void EndAuxiliaryCheckpoint(bool commit)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
            if (stagingCheckpoint)
            {
                stagingCheckpoint = false;
                try
                {
                    if (commit)
                    {
                        staging.CommitCheckpoint();
                    }
                    else
                    {
                        staging.RollbackCheckpoint();
                    }
                }
                catch (Exception exception)
                {
                    failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
                }
            }
            if (orderingCheckpoint)
            {
                orderingCheckpoint = false;
                try
                {
                    if (commit)
                    {
                        ordering.CommitCheckpoint();
                    }
                    else
                    {
                        ordering.RollbackCheckpoint();
                    }
                }
                catch (Exception exception)
                {
                    failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
                }
            }
            if (commonCheckpoint)
            {
                commonCheckpoint = false;
                try
                {
                    if (commit)
                    {
                        common.CommitCheckpoint();
                    }
                    else
                    {
                        common.RollbackCheckpoint();
                    }
                }
                catch (Exception exception)
                {
                    failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
                }
            }
            failure?.Throw();
        }
        NativeBattleDuel274Frame Bind(NativeBattleDuel274Frame current, int index, uint row)
        {
            Publish(current);
            NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[index], NativeGraphicsPoseResource(graphics, index));
            var bound = NativeBattleVictoryResources.Bind5022C0(resources.ReadActorDat(), NativeModelCpuResources ?? throw new NotSupportedException("Native Duel requires its CPU resource rows."), model, row, NativeBattleDuel274Resources.Address, Read, (address, pointer) => current = current with { Effect = current.Effect with { Pools = NativeBattleEffectTaskMemory.WritePointer(current.Effect.Pools, address, pointer) } });
            WriteModel(index, bound.Model);
            return current;

            NativeBattleResourcePointer? Read(uint address)
            {
                if (NativeBattleEffectTaskMemory.TryReadPointer(current.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
                {
                    return pointer;
                }
                return IsNativeEffectArenaAddress(address) ? current.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : current.Staging.ReadResourcePointer(address);
            }
        }
        NativeBattleDuel274Frame Select(NativeBattleDuel274Frame current, int index, byte sequence)
        {
            Publish(current);
            graphics = SelectNativeHeaderSequence(graphics, checked((byte)index), sequence);
            return current;
        }
        NativeBattleDuel274Frame Camera(NativeBattleDuel274Frame current, byte selector)
        {
            Publish(current);
            StartNativeCamera(new(resources.ReadCameraBank()), selector);
            return current;
        }
        NativeBattleDuel274Frame QueueFile(NativeBattleDuel274Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.Files.SingleOrDefault(file => file.FileId == request.FileId) ?? throw new NotSupportedException("Native Duel requires the requested original archive file.");
            if (file.Data.IsDefaultOrEmpty)
            {
                throw new NotSupportedException("Native Duel requested archive bytes are unavailable.");
            }
            NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Native Duel file delivery requires its physical arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length), file.Data.AsSpan());
            NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Native Duel file delivery requires its retained IO clock.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
            return current with { Memory = current.Memory with { Arena = arena } };
        }
        NativeBattleDuel274Frame Contact(NativeBattleDuel274Frame current, int sequence)
        {
            Publish(current);
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(FirstContact(sequence))] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Duel contact lost its process owner.") };
        }
        NativeBattleDuel274Frame Texture(NativeBattleDuel274Frame current, uint destination, uint completion)
        {
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(destination, completion, (address, value) => current = WriteNativeDuel274Completion(current, address, value));
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }
        void Stage(int intensity)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Duel stage depth requires its retained groups.");
            NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity), ColorFlags = 0 }).ToImmutableArray() };
        }
        NativeBattleDuel274Frame Variant(NativeBattleDuel274Frame current, int slot, byte selector)
        {
            return selector switch
            {
                2 => NativeBattleDuel274BasicMoves.Advance68A050(current, slot, resources, view, projection, colors, moves),
                3 => NativeBattleDuel274BasicMoves.Advance68B2F0(current, slot, resources, view, projection, colors, moves),
                4 => NativeBattleDuel274BasicMoves.Advance68B700(current, slot, resources, view, projection, colors, moves),
                5 => NativeBattleDuel274BasicMoves.Advance68B950(current, slot, resources, view, projection, colors, new(moves, DrawMesh)),
                6 => NativeBattleDuel274Dolphin.Advance68C7B0(current, slot, resources, view, projection, colors, new(index => graphics.Models[index], WriteModel, bone => ReadNativeGraphicsBone(graphics, parameters.Actor78, checked((byte)bone), 0), DrawSprite, SubmitEffect, Contact, next => Flash(next, false), Sound)),
                7 => NativeBattleDuel274BasicMoves.Advance68CA60(current, slot, resources, view, projection, colors, new(moves, actors, Anchor, DrawMesh, DrawBody)),
                _ => throw new NotSupportedException("Native Duel selected an unsupported move callback.")
            };
        }
        NativeBattleDuel274Frame Flash(NativeBattleDuel274Frame current, bool strong)
        {
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, strong ? (short)1 : (short)0, 128) };
            return current;
        }
        NativeBattleDuel274Frame Sound(NativeBattleDuel274Frame current, uint address)
        {
            uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(resources.Read(address, 4).AsSpan());
            PublishNativeModuleSound(new(descriptor, 0, null) { Pan = 128, RawPan = 128 });
            return current;
        }
        NativeBattleDuel274Frame SubmitEffect(NativeBattleDuel274Frame current, NativeBattleGfGeometryBatch batch)
        {
            if (batch.AllocationBytes == 0 && batch.Attempts.IsEmpty)
            {
                return current;
            }
            uint physical = BinaryPrimitives.ReadUInt32LittleEndian(current.Staging.Read(0x2508438, 4));
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(physical, 0);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Duel drawing requires its current ordering table.");
            batch = NativeBattleCinematicActorPresentation.NativeDepths(batch);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch);
            current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, cursor, batch) } };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor }, Staging = current.Staging.Write(0x2508438, BitConverter.GetBytes(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor))) };
        }
        NativeBattleDuel274Frame DrawSprite(NativeBattleDuel274Frame current, NativeBattleModulePoolTask task, int sprite, short phase, short size, short depth)
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(resources.Read(checked(0x110E3B8u + (uint)sprite * 8), 4).AsSpan());
            return Sprite(current, task, address, phase, size, depth);
        }
        NativeBattleDuel274Frame DrawSecondary(NativeBattleDuel274Frame current, NativeBattleModulePoolTask task) => Sprite(current, task, 0x10E7868, unchecked((short)task.ReadWord(18)), 2048, -512);
        NativeBattleDuel274Frame Sprite(NativeBattleDuel274Frame current, NativeBattleModulePoolTask task, uint address, short phase, short size, short depth)
        {
            NativeBattleSearchSpriteResource source = resources.Sprites.Single(source => source.Address == address);
            NativeBattleModuleResource resource = new(0, address, 0, 0, source.Bytes);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(phase), Phase = unchecked((ushort)phase), Anchor = new(unchecked((short)task.ReadWord(12)), unchecked((short)task.ReadWord(14)), unchecked((short)task.ReadWord(16)), 0), Scale = size, DepthOffset = depth, SpriteFlags = 0, PilotIdentity = NativeBattleDuel274Presentation.Identity(task, address) };
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 180);
            NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, address, 0);
            NativeBattleModuleSpriteTemporaryResult drawn = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary.Address, draw, projection with { View = view }, camera.Roll, 17, 2);
            NativeBattleModulePacketBatch batch = NativeBattleDuel274Presentation.Depths(drawn.Batch);
            current = current with { Staging = drawn.Staging };
            if (batch.AllocationBytes == 0 && batch.Primitives.IsEmpty && (batch.CapturedWrites is null || batch.CapturedWrites.Attempts.IsEmpty))
            {
                return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 180) };
            }
            uint physical = BinaryPrimitives.ReadUInt32LittleEndian(current.Staging.Read(0x2508438, 4));
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(physical, 0);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Duel sprite drawing requires its current ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleDuel28Presentation.Record(current.Effect.PresentationPackets, "20DFAB8", 0, cursor, batch) } };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            staging = current.Staging.Write(0x2508438, BitConverter.GetBytes(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)));
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor }, Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 180) };
        }
        NativeBattleDuel274Frame DrawMesh(NativeBattleDuel274Frame current, uint address, NativeBattleActionMatrix matrix, uint flags, int? fade, uint? far)
        {
            NativeBattleStaticMeshResource mesh = resources.Meshes.Single(mesh => mesh.Address == address);
            ImmutableArray<byte> bytes = address == 0x10E9530 ? current.Staging.Read(address, mesh.Bytes.Length).ToImmutableArray() : mesh.Bytes;
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleFileStaging staging = temporary.Staging.Write(temporary.Address, BitConverter.GetBytes(address)).Write(temporary.Address + 28, BitConverter.GetBytes(flags));
            if (far is uint color)
            {
                staging = staging.Write(temporary.Address + 8, BitConverter.GetBytes(color));
            }
            if (fade is int factor)
            {
                staging = staging.Write(temporary.Address + 12, BitConverter.GetBytes(factor));
            }
            NativeBattleModuleMeshTemporaryResult drawn = NativeBattleModulePackets.EmitRetainedBatch572200(staging, temporary.Address, address, bytes, matrix, projection, new(flags, far, fade, null, null, null, 17, 2), colors, pilotIdentity: current.PilotIdentity is { } meshIdentity ? meshIdentity with { ResourceGeneration = address } : null);
            current = SubmitEffect(current with { Staging = drawn.Staging }, drawn.Batch);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 88) };
        }
        NativeBattleDuel274Frame DrawBody(NativeBattleDuel274Frame current, int index, NativeBattleVector angles, NativeBattleVector pivot, NativeBattleVector scale)
        {
            Publish(current);
            NativeBattleActionMatrix world = graphics.Models[index].GeometryWorldMatrix ?? throw new NotSupportedException("Native Duel body drawing requires its actual model world matrix.");
            NativeBattlePrimitiveReference before = NativeBattleDuel28Presentation.Start(current.Memory.PrimitiveProcess!);
            graphics = DrawNativeEffectModelContext92(graphics, index, NativeBattleDuel274Body.Matrix68D1D0(world, view, angles, pivot, scale), pivot, projection, 4112, 16, pilotIdentity: current.PilotIdentity);
            current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleDuel28Presentation.Common(current.Effect.PresentationPackets, graphics.PrimitiveFrame!.Pending, before, NativeEffectMemory!.PrimitiveProcess!) } };
            return current with { Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Duel body drawing lost its process owner."), Staging = NativeFileStaging ?? throw new NotSupportedException("Native Duel body drawing lost its shared byte owner.") };
        }
        NativeBattleDuel274Frame DrawEcho(NativeBattleDuel274Frame current, uint address)
        {
            var drawn = DrawNativeDuel274Echo(current, graphics, parameters.Actor78, address, view, projection);
            graphics = drawn.Graphics;
            return drawn.Frame;
        }
        NativeBattleDuel274Frame CaptureEcho(NativeBattleDuel274Frame current, uint address)
        {
            NativeBattleGraphicsModel model = graphics.Models[parameters.Actor78];
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, parameters.Actor78).PoseSource ?? throw new NotSupportedException("Native Duel echo capture requires its current body skeleton.");
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, source);
            return NativeBattleDuel274ArenaEcho.Capture68ADF0(current, address, source.BoneCount, bone =>
            {
                NativeBattleActionMatrix matrix = matrices[bone];
                byte[] data = new byte[32];
                bool[] known = Enumerable.Repeat(true, 32).ToArray();
                for (int index = 0; index < 9; index++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(index * 2, 2), matrix.Rotation[index]);
                }
                known[18] = false;
                known[19] = false;
                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(20, 4), matrix.X);
                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(24, 4), matrix.Y);
                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(28, 4), matrix.Z);
                return (data, known);
            });
        }
    }

    private static NativeBattleDuel274Frame WriteNativeDuel274Completion(NativeBattleDuel274Frame frame, uint address, byte value)
    {
        int index = NativeBattleDuel274Banks.Index(frame, 0x25084A8);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        uint delta = unchecked(address - pool.BankAddress);
        if (delta >= pool.Stride * pool.Slots.Length || delta % pool.Stride != 96)
        {
            throw new InvalidDataException("Native Duel completion leaves its actual target completion byte.");
        }
        int slot = checked((int)(delta / pool.Stride));
        if (pool.Slots[slot].Callback != 0x689AC0)
        {
            throw new InvalidDataException("Native Duel completion requires its actual target callback.");
        }
        pool = pool.WriteSlot(slot, pool.Slots[slot].WriteByte(96, value));
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    }

    private bool TryWriteNativeDuel274Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: 0x6897E0 } effect || effect.Pools.All(pool => pool.DescriptorAddress != 0x25084A8))
        {
            return false;
        }
        NativeBattleEffectTaskPool pool = effect.Pools.Single(pool => pool.DescriptorAddress == 0x25084A8);
        uint delta = unchecked(address - pool.BankAddress);
        if (delta >= pool.Stride * pool.Slots.Length || delta % pool.Stride != 96)
        {
            return false;
        }
        NativeBattleDuel274Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Duel completion requires its process owner."), effect, NativeFileStaging ?? throw new NotSupportedException("Native Duel completion requires its shared byte owner."), nativeCrtRandom ?? throw new NotSupportedException("Native Duel completion requires its CRT owner."));
        updated = graphics with { OriginalEffect = WriteNativeDuel274Completion(frame, address, value).Effect };
        return true;
    }
}
