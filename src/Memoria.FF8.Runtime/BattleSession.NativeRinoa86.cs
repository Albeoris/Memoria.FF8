using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRinoa86(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Rinoa86 requires its actual selected module.");
        NativeBattleRinoa86Resources resources = effect.Binding.Rinoa86Resources ?? throw new NotSupportedException("Native Rinoa86 requires its original resources.");
        NativeBattleRinoa86Frame frame = NativeBattleRinoa86Controller.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Rinoa86 requires its retained process."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory with { Rinoa86 = frame.State };
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRinoa86(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Rinoa86 requires its actual selected module.");
        NativeBattleRinoa86Resources resources = effect.Binding.Rinoa86Resources ?? throw new NotSupportedException("Native Rinoa86 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Rinoa86 requires its retained process.");
        NativeBattleRinoa86Frame frame = new(memory, effect, memory.Rinoa86 ?? throw new NotSupportedException("Native Rinoa86 requires its entered process storage."));
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Rinoa86 requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Rinoa86 requires its published view matrix.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Native Rinoa86 requires its original graphics rules.");
        NativeBattleSummonColors colors = rules.Rules.SummonColors ?? throw new NotSupportedException("Native Rinoa86 requires its actual color constants.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Rinoa86 requires its retained CRT generator.");
        NativeBattleRinoa86Inputs controller = null!;
        controller = new(nativeActionPackets, view, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Rinoa86 requires its actual actor skeleton."), AdvanceLeaf, ReloadCommon);
        frame = NativeBattleRinoa86Controller.Advance(frame, controller);
        NativeEffectMemory = frame.Memory with { Rinoa86 = frame.State };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleLogicalOrdering Ordering() => (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Rinoa86 requires its initialized ordering table.")).Pending;
        void StoreOrdering(NativeBattleLogicalOrdering ordering) => graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering } };
        NativeBattleRinoa86Resources CurrentResources() => resources with { DefaultColor44 = (NativeStage?.ModelColors ?? throw new NotSupportedException("Native8DD0F0 requires its current shared model colors.")).Color44 };
        NativeBattleEffectTaskActors SelectActors(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(effect.Parameters!, nativeActionPackets, pointer, group, target);
        NativeBattleVector ReadBone(NativeBattleRinoa86Frame current, uint body, byte selector) => NativeBattleRinoa86BodyDrawing.ReadBone502170(current, body, resources, selector);

        NativeBattleRinoa86LeafStep AdvanceLeaf(NativeBattleRinoa86Frame current, uint address)
        {
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            switch (task.Callback)
            {
                case 0x77F5B0:
                    return NativeBattleRinoa86Controller.AdvanceTarget(current, address, new(controller, (next, target) => NativeBattleRinoa86Scene.InitializeTarget(next, target, controller, resources), next => NativeBattleRinoa86Scene.Read(next).Word(72), next => NativeBattleRinoa86Scene.Read(next).Word(76), Contact));
                case 0x77FBF0:
                    return NativeBattleRinoa86Scene.Advance(current, address, new(controller, CurrentResources(), Execute));
                case 0x77F780:
                    NativeBattleRinoa86IoAdvance io = NativeBattleRinoa86Io.Advance77F780(task, current.State.Io ?? throw new NotSupportedException("Native77F780 requires its initialized IO workspace."));
                    current = current with { State = current.State with { Io = io.State }, Effect = current.Effect with { Pools = new NativeBattleEffectHierarchy(current.Effect.Pools).Write(address, io.Task).Pools } };
                    if (io.Request is { } request)
                    {
                        NativeBattleRinoa86IoQueued queued = NativeBattleRinoa86Io.CopyAndQueueFile(io.State, current.Memory, NativeFileIo ?? throw new NotSupportedException("Native77F780 requires its retained asynchronous file queue."), resources.Io.Files.Single(file => file.FileId == request.FileId));
                        NativeFileIo = queued.FileIo;
                        current = current with { Memory = queued.Memory };
                    }
                    if (io.ReadyToComplete)
                    {
                        current = current with { Effect = current.Effect with { Pools = new NativeBattleEffectHierarchy(current.Effect.Pools).Complete8DC530(address).Pools } };
                    }
                    return new(current, io.ReadyToComplete ? 2 : 0);
                case 0x77FF90:
                    return NativeBattleRinoa86Body.Advance(current, address, new(controller, resources, (_, actor) => graphics.Models[actor].Flags0, Execute, DrawBody, ReadBone));
                case 0x783350:
                case 0x783DC0:
                case 0x782930:
                case 0x780640:
                    return NativeBattleRinoa86Particles.Advance(current, address, new((model, selector) => ReadBone(current, checked(model - 48), selector), resources.GroundReferenceZ, Roll, (next, particle, callback) =>
                    {
                        NativeBattleRinoa86ParticleDraw draw = NativeBattleRinoa86ParticleDrawing.Draw(new NativeBattleEffectHierarchy(next.Effect.Pools).Read(particle), callback, resources, view, projection, colors, NativeCamera!.Roll);
                        return Append(next, draw.Packets, draw.CommonCursor);
                    }));
                case 0x7836E0:
                case 0x783C90:
                case 0x783F80:
                    return NativeBattleRinoa86Program.Advance(current, address, new(pointer => resources.Programs.Single(source => source.Address == pointer), (next, source, node) =>
                    {
                        NativeBattleRinoa86NodeDraw draw = NativeBattleRinoa86ProgramDrawing.Draw7838C0(source, node, next.State.MorphScratch, projection, colors);
                        return Append(next with { State = next.State with { MorphScratch = draw.MorphScratch } }, draw.Packets, false);
                    }, view));
                default:
                    throw new NotSupportedException($"Native Rinoa86 callback {task.Callback:X8} has no current implementation.");
            }
        }

        NativeBattleRinoa86Frame Execute(NativeBattleRinoa86Frame current, NativeBattleRinoa86Operation operation)
        {
            switch (operation)
            {
                case NativeBattleRinoa86StaticTexture texture when texture.Address == 0x170CDCC:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Rinoa86 requires its retained TIM queue.");
                    (textures, _) = textures.EnqueueTim505E30(resources.SceneTim);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleRinoa86BindActor bind when bind.Resource == 0x156AA60 && bind.SavedModel == 0x25BC1F8:
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(bind.Resource, resources.ActorOverride.Data.AsSpan());
                    NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(staging, NativeModelCpuResources ?? throw new NotSupportedException("Native Rinoa86 requires its actual CPU resource rows."), actor, bind.SavedModel, bind.Resource);
                    NativeFileStaging = binding.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, binding.Model) };
                    return current;
                case NativeBattleRinoa86ActorSequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Actor, checked((ushort)sequence.Sequence));
                    return current;
                case NativeBattleRinoa86ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = flags.Flags }) };
                    return current;
                case NativeBattleRinoa86Sound sound:
                    PublishNativeModuleSound(new(resources.Sounds[sound.Descriptor], unchecked((uint)sound.Argument), null) { RawPan = unchecked((uint)sound.Volume) });
                    return current;
                case NativeBattleRinoa86Emit emit:
                    NativeBattleRinoa86ParticleEmission emitted = NativeBattleRinoa86Emitters.Emit(new(current.Effect.Pools), emit.Body, emit.Mode, random, selector => ReadBone(current, emit.Body, selector), SelectActors);
                    random = emitted.Random;
                    return current with { Effect = current.Effect with { Pools = emitted.Hierarchy.Pools } };
                case NativeBattleRinoa86PublishWorld matrix:
                    return current with { Memory = current.Memory with { SharedCustomMatrix = new(matrix.Matrix, Enumerable.Repeat(true, 32).ToImmutableArray()) } };
                default:
                    throw new NotSupportedException("Native Rinoa86 operation has no selected original owner.");
            }
        }

        NativeBattleRinoa86Frame DrawBody(NativeBattleRinoa86Frame current, uint address)
        {
            NativeBattleRinoa86BodyDraw drawn = NativeBattleRinoa86BodyDrawing.Draw780130(current, address, resources, view, projection, rules.Rules.ShadowSource ?? throw new NotSupportedException("Native Rinoa86 requires its actual shadow source."), rules.PrimitiveCapabilities ?? throw new NotSupportedException("Native Rinoa86 requires its actual primitive capabilities."), colors, Ordering());
            StoreOrdering(drawn.Ordering);
            return drawn.Frame;
        }

        NativeBattleRinoa86Frame Contact(NativeBattleRinoa86Frame current, int index)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory! };
        }

        NativeBattleRinoa86Frame ReloadCommon(NativeBattleRinoa86Frame current, uint root)
        {
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(0x20DFAB8u + (uint)current.State.SourceArenaOffset), checked(root + 98), (address, value) => current = WriteRinoa86Completion(current, address, value));
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }

        NativeBattleRinoa86Frame Append(NativeBattleRinoa86Frame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            if (common)
            {
                NativeEffectMemory = current.Memory;
                graphics = AppendNativeModulePacketBatch(graphics, new([], batch.AllocationBytes) { CapturedWrites = batch }, null, null);
                return current with { Memory = NativeEffectMemory! };
            }
            var appended = NativeBattleEffectPacketArena.Append(current.Memory, current.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Rinoa86 requires its current private packet cursor."), Ordering(), batch);
            StoreOrdering(appended.Ordering);
            return current with { Memory = appended.Memory, State = current.State with { PrimaryDrawCursor = appended.Cursor } };
        }

        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private static NativeBattleRinoa86Frame WriteRinoa86Completion(NativeBattleRinoa86Frame frame, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleRinoa86Controller.Descriptor);
        uint delta = unchecked(address - pool.BankAddress);
        if (delta >= (uint)(pool.Stride * pool.Slots.Length) || delta % pool.Stride != 98)
        {
            throw new NotSupportedException("Native Rinoa86 common texture completion requires its exact root BYTE98.");
        }
        int slot = checked((int)(delta / pool.Stride));
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(pool), pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(98, value)) }) } };
    }

    private void CompleteNativeRinoa86Io(uint callback)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native77F980 requires its retained effect process.");
        NativeBattleRinoa86State state = memory.Rinoa86 ?? throw new NotSupportedException("Native77F980 requires its entered module storage.");
        NativeBattleRinoa86IoCompletion completed = NativeBattleRinoa86Io.Complete77F980(state.Io ?? throw new NotSupportedException("Native77F980 requires its current TIM request."), memory, callback);
        NativeEffectMemory = completed.Memory with { Rinoa86 = state with { Io = completed.State } };
    }
}
