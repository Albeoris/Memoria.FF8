using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeRoughDivideState(NativeBattleRoughDivide163Frame Frame, NativeBattleActionPacketsState Packets, NativeBattleGraphicsModel Model, NativeBattleAuxiliaryState Auxiliary, NativeBattleLogicalOrdering Ordering, short StageDepth, bool ContactPublished, int ApproachDistance)
{
    public bool Complete => ContactPublished && Frame.Effect.ReturnedDescriptor == 0 && Model.ResourceMagicId is null && Auxiliary.Head is null;
}

internal sealed record CrossoverNativeRoughDivideStep(CrossoverNativeRoughDivideState State, bool Contact, byte? TargetCue, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>Original Rough Divide effect163, with foreign target anchors supplied by the FFVII occurrence.</summary>
internal sealed class CrossoverNativeRoughDivideProgram(GameAssets game, NativeBattleGraphicsResource original, NativeBattleSummonColors colors)
{
    public CrossoverNativeRoughDivideState Begin(CrossoverNativeRenzokukenProgramState previous, NativeBattleContactHalf contact)
    {
        if (previous.FinisherHandoff is null || previous.Effect.Frame.Effect.ReturnedDescriptor != 0 || previous.PreparedContact is not null || contact.ActorIndex != 3)
        {
            throw new InvalidDataException("Rough Divide requires its original Renzokuken handoff and bound foreign contact.");
        }
        NativeBattleActionPacketsState packets = previous.Effect.Packets;
        (packets, int header) = packets.Prepare(new(0, 250, 0, -1, ushort.MaxValue, 163, null));
        (packets, _) = packets.AppendContact(new(contact, new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(header, 1).ActivateHeader(header);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(163, game.ReadNativeEffectCatalogForEffects([163]));
        NativeBattleActionGraphicsState action = NativeBattleActionGraphics.Normalize(packets, header);
        selected = selected with { Parameters = new(0, action.HitFlags, action.TargetMask, header, 0, 0) };
        NativeBattleRenzokuken160Frame retained = previous.Effect.Frame;
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(retained.Memory, selected);
        NativeBattleRoughDivide163Resources resources = effect.Binding.RoughDivide163Resources ?? throw new InvalidDataException("Rough Divide has no authenticated original resources.");
        NativeBattleFileStaging staging = game.ReadColdProcessNativeBattleMemory().FileStaging ?? throw new InvalidDataException("Rough Divide has no original file staging owner.");
        NativeBattleRoughDivide163Frame frame = NativeBattleRoughDivide163Controller.Enter5F5B80(memory, effect, staging, packets, resources, retained.Random, previous.Model.GeometryWorldMatrix!);
        frame = frame with { Memory = frame.Memory with { Textures = frame.Memory.Textures!.Drain505D20(frame.Memory.Arena!).State } };
        return new(frame, packets, previous.Model, NativeBattleAuxiliaryState.Clear(), NativeBattleLogicalOrdering.Clear(), previous.Effect.StageDepth, false, previous.ApproachDistance);
    }

    public CrossoverNativeRoughDivideStep Advance(CrossoverNativeRoughDivideState source, NativeBattleGraphicsResource previousResource, Func<NativeBattleModuleAnchor> readTarget, NativeBattleCameraProjection projection, short roll)
    {
        if (source.Complete)
        {
            throw new InvalidOperationException("Rough Divide already returned to standing.");
        }
        NativeBattleRoughDivide163Frame frame = source.Frame;
        NativeBattleAuxiliaryState auxiliary = source.Auxiliary;
        NativeBattleGraphicsModel model = source.Model;
        NativeBattleGraphicsResource resource = model.ResourceMagicId switch
        {
            null => original,
            163 => ReadResource(frame),
            _ => previousResource
        };
        NativeBattleRoughDivide163Resources resources = frame.Memory.RoughDivide163!.RetainedResources!;
        NativeBattleActionMatrix view = projection.View ?? throw new InvalidDataException("Rough Divide requires its actual host projection.");
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        short depth = source.StageDepth;
        bool contact = false;
        byte? targetCue = null;
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleEffectInitializerAsset common = game.ReadNativeCommonEffectTexture();
        auxiliary = auxiliary.Advance(new()
        {
            RequestFile5084B0 = (file, address) =>
            {
                if (file != 0xA3)
                {
                    throw new InvalidDataException("Rough Divide requested an unbound file.");
                }
                frame = frame with { Staging = frame.Staging.Write(address, common.Data.AsSpan()) };
            },
            ReadEffectIoResult508470 = () => 0,
            EnqueueTexture505E30 = address =>
            {
                ImmutableArray<byte> bytes = frame.Staging.Read(address, common.Data.Length).ToImmutableArray();
                (NativeBattleEffectTextureState textures, _) = frame.Memory.Textures!.EnqueueTim505E30(new(common.Name, bytes));
                frame = frame with { Memory = frame.Memory with { Textures = textures } };
            },
            WriteCompletionByte = (address, value) => frame = frame with { Effect = NativeBattleRoughDivide163Controller.WriteCompletionByte(frame.Effect, address, value) }
        }).State;
        NativeBattleRoughDivide163Inputs inputs = new(0, Position, Bone, Execute);
        if (frame.Effect.ReturnedDescriptor != 0)
        {
            frame = NativeBattleRoughDivide163Controller.Advance5F9020(frame, AdvanceChild, TextureLoad);
        }
        NativeBattleSequenceStep pose = NativeBattleSequenceMachine.Advance(model, resource, new(0, 0)
        {
            OriginalResource = original,
            ReadExternal = variable => variable switch
            {
                17 => unchecked((short)source.ApproachDistance),
                18 or 19 => frame.Memory.ReadSequenceGlobal5044B0(variable),
                _ => throw new NotSupportedException($"Rough Divide has no owner for source variable {variable:X2}.")
            },
            WriteExternal = (variable, value) => frame = frame with { Memory = frame.Memory.WriteSequenceGlobal5048E0(variable, value) }
        });
        if (pose.Cues.Any(cue => cue.Kind != NativeBattleSequenceCueKind.RestoreStanding))
        {
            throw new NotSupportedException("Rough Divide reached an unowned source model cue.");
        }
        model = pose.Model with { Flags2 = (ushort)(pose.Model.Flags2!.Value & ~4) };
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, (model.ResourceMagicId is null ? original : resource).PoseSource!) };
        if (frame.Memory.Textures is { Count: > 0 } upload)
        {
            frame = frame with { Memory = frame.Memory with { Textures = upload.Drain505D20(frame.Memory.Arena).State } };
        }
        return new(source with { Frame = frame, Model = model, Auxiliary = auxiliary, Ordering = ordering, StageDepth = depth, ContactPublished = source.ContactPublished || contact }, contact, targetCue, sounds.ToImmutable());

        NativeBattleModuleAnchor Position(byte actor)
        {
            if (actor == 3)
            {
                return readTarget();
            }
            if (actor != 0 || model.Transform is not { } transform)
            {
                throw new InvalidDataException("Rough Divide queried an unbound actor position.");
            }
            return new(transform.Position28.X, transform.Position28.Y, transform.Position28.Z, transform.Vertical34);
        }

        NativeBattleModuleAnchor Bone(byte actor, byte selector, short fraction)
        {
            if (actor != 0)
            {
                throw new InvalidDataException("Rough Divide queried an unbound source bone.");
            }
            NativeBattleVector point = NativeBattlePoseMatrices.ReadBone(model, resource.PoseSource!, selector, fraction);
            return new(point.X, point.Y, point.Z, 0);
        }

        NativeBattleRoughDivide163ChildStep AdvanceChild(NativeBattleRoughDivide163Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            uint callback = pool.Slots[slot].Callback;
            return callback switch
            {
                0x5F5C80 => NativeBattleRoughDivide163Controller.Advance5F5C80(current, pool, slot, inputs),
                0x5F5FE0 => NativeBattleRoughDivide163Controller.Advance5F5FE0(current, pool, slot, inputs, actor => actor == 0 ? NativeBattleRenzokuken160Controller.ReadWeaponTip5FFEE0(model, resource) : throw new InvalidDataException("Rough Divide queried an unbound weapon."), view, projection, current.Memory.ScriptedGraphics, colors),
                0x5F65E0 or 0x5F78D0 or 0x5F8540 or 0x5F8770 or 0x5F8960 or 0x5F8B80 => NativeBattleRoughDivide163Controller.AdvanceMesh(current, pool, slot, inputs, ReadMesh(callback), view, projection, colors),
                0x5F67A0 or 0x5F6A30 or 0x5F7A80 => NativeBattleRoughDivide163Controller.AdvanceSpriteParticles(current, pool, slot, inputs, resources.Sprites.Single(sprite => sprite.Address == 0xD9B458), view, projection, roll),
                0x5F6CA0 or 0x5F7000 or 0x5F81D0 or 0x5F8D10 => NativeBattleRoughDivide163Controller.AdvanceMeshParticles(current, pool, slot, inputs, view, projection, colors),
                0x5F7390 => NativeBattleRoughDivide163Controller.Advance5F7390(current, pool, slot, inputs, view, projection, current.Memory.ScriptedGraphics ?? throw new InvalidDataException("Rough Divide lost its retained GTE registers."), colors),
                0x5F7E00 => NativeBattleRoughDivide163Controller.Advance5F7E00(current, pool, slot, inputs, view, projection, colors, roll),
                _ => throw new NotSupportedException($"Rough Divide callback {callback:X} has no original source owner.")
            };
        }

        NativeBattleStaticMeshResource ReadMesh(uint callback)
        {
            uint address = callback switch
            {
                0x5F65E0 => 0xD9E2D4,
                0x5F78D0 => 0xD9EC8C,
                0x5F8540 => 0xD9B5D0,
                0x5F8770 => 0xD9D594,
                0x5F8960 => 0xD9DD34,
                0x5F8B80 => 0xD9CB38,
                _ => throw new InvalidDataException("Rough Divide mesh callback has no original address.")
            };
            return resources.Meshes.Single(mesh => mesh.Address == address);
        }

        NativeBattleRoughDivide163Frame Execute(NativeBattleRoughDivide163Frame current, NativeBattleRoughDivide163Operation operation)
        {
            switch (operation)
            {
                case NativeBattleRoughDivide163ComposeActor:
                    return NativeBattleRoughDivide163Controller.ComposeCapturedActor(current, view);
                case NativeBattleRoughDivide163BindActor:
                    model = NativeBattleSequenceSelections.RetainDecodedPose5022C0(model, resource);
                    resource = ReadResource(current);
                    NativeBattleSequenceState state = model.SequenceState!;
                    model = model with { ResourceMagicId = 163, Flags2 = (ushort)(model.Flags2!.Value | 8), SequenceState = state with { Locals = state.Locals.SetItem(7, state.Default2), Default2 = 1 } };
                    break;
                case NativeBattleRoughDivide163Sequence { Actor: 0 } sequence:
                    model = NativeBattleSequenceSelections.SelectResource505BC0(model, resource, sequence.Sequence);
                    break;
                case NativeBattleRoughDivide163Sequence { Actor: 3, Sequence: 6 }:
                    targetCue = 6;
                    break;
                case NativeBattleRoughDivide163SetPosition { Actor: 0 } move:
                    model = model with { Transform = model.Transform! with { Position28 = new(move.Position.X, move.Position.Y, move.Position.Z), Vertical34 = move.Position.W } };
                    break;
                case NativeBattleRoughDivide163Sound sound:
                    sounds.Add(new(sound.Descriptor, 0, null) { Pan = 128, RawPan = 128 });
                    break;
                case NativeBattleRoughDivide163Stage stage:
                    depth = checked((short)stage.Intensity);
                    break;
                case NativeBattleRoughDivide163Contacts:
                    if (contact || source.ContactPublished)
                    {
                        throw new InvalidDataException("Rough Divide published its original single contact twice.");
                    }
                    contact = true;
                    break;
                case NativeBattleRoughDivide163Geometry geometry:
                    return Append(current, geometry.Batch, null);
                case NativeBattleRoughDivide163Trail trail:
                    return Append(current, trail.Batch, trail.CursorAdvance);
                case NativeBattleRoughDivide163Sprite sprite:
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, Cursor(current), ordering, sprite.Batch, NativeBattleEffectGouraudWrites.FullDword);
                    ordering = written.Ordering;
                    return PublishCursor(current with { Memory = written.Memory }, written.Cursor);
                default:
                    throw new NotSupportedException("Rough Divide reached an unsupported source callback operation.");
            }
            return current;
        }

        NativeBattleRoughDivide163Frame Append(NativeBattleRoughDivide163Frame current, NativeBattleGfGeometryBatch batch, int? advance)
        {
            int start = Cursor(current);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, start, ordering, batch);
            ordering = written.Ordering;
            return PublishCursor(current with { Memory = written.Memory }, advance is int bytes ? checked(start + bytes) : written.Cursor);
        }

        NativeBattleRoughDivide163Frame TextureLoad(NativeBattleRoughDivide163Frame current, uint completion)
        {
            (auxiliary, bool allocated) = auxiliary.AppendCommonTextureLoad508630(0x20DFAB8, completion, (address, value) => current = current with { Effect = NativeBattleRoughDivide163Controller.WriteCompletionByte(current.Effect, address, value) });
            return allocated ? current : throw new InvalidOperationException("Rough Divide could not allocate its native common texture callback.");
        }

        static int Cursor(NativeBattleRoughDivide163Frame current) => current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Rough Divide lost its original packet cursor.");

        static NativeBattleRoughDivide163Frame PublishCursor(NativeBattleRoughDivide163Frame current, int cursor)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, checked(0x20DFAB8u + (uint)cursor));
            return current with { Effect = current.Effect with { DrawArenaOffset = cursor }, Staging = current.Staging.Write(0x23CE584, bytes) };
        }
    }

    public NativeBattleGraphicsResource ReadResource(NativeBattleRoughDivide163Frame frame)
    {
        ImmutableArray<byte> bytes = frame.Staging.ReadDat(NativeBattleRoughDivide163Resources.StaticAddress);
        int animations = NativeBattleDatDirectory.Section(bytes.AsSpan(), 0);
        int sequences = NativeBattleDatDirectory.Section(bytes.AsSpan(), 1);
        int linked = NativeBattleDatDirectory.Section(bytes.AsSpan(), 2);
        NativeBattlePoseSource body = original.PoseSource! with { Animations = bytes.AsSpan().Slice(animations, sequences - animations).ToArray().ToImmutableArray() };
        NativeBattlePoseSource weapon = original.LinkedPoseSource! with { Animations = bytes.AsSpan().Slice(linked).ToArray().ToImmutableArray() };
        NativeBattleGraphicsResource resource = original with { PoseSource = body, LinkedPoseSource = weapon, AnimationFrames = NativeBattlePoseDecoder.ReadFrameCounts(body.Animations.AsSpan()), LinkedAnimationFrames = NativeBattlePoseDecoder.ReadFrameCounts(weapon.Animations.AsSpan()), SequenceSection = bytes.AsSpan().Slice(sequences, linked - sequences).ToArray().ToImmutableArray() };
        resource.Validate();
        return resource;
    }

    public static CrossoverNativeRoughDivideState Capture(CrossoverNativeRoughDivideState source) => source with { Frame = NativeBattleEffectComponentStorage.Capture(source.Frame), Ordering = NativeBattleEffectComponentStorage.Capture(source.Ordering) };
    public static CrossoverNativeRoughDivideState Restore(CrossoverNativeRoughDivideState source) => source with { Frame = NativeBattleEffectComponentStorage.Restore(source.Frame), Ordering = NativeBattleEffectComponentStorage.Restore(source.Ordering) };
}
