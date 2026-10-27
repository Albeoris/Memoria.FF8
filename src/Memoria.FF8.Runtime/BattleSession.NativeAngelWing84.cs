using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeAngelWing84(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Angel Wing requires its actual selected module.");
        NativeBattleAngelWing84Resources resources = effect.Binding.AngelWing84Resources ?? throw new NotSupportedException("Native Angel Wing requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Angel Wing requires its retained effect process.");
        NativeBattleAngelWing84State retained = memory.AngelWing84 ?? throw new NotSupportedException("Native Angel Wing requires its actual process storage; missing imported storage is unknown.");
        NativeBattleAngelWing84Frame frame = NativeBattleAngelWing84Controller.Initialize(memory, effect, nativeActionPackets);
        frame = frame with { State = frame.State with { BodyDrawStorage = retained.BodyDrawStorage, BodyBones = retained.BodyBones, BodyShaderAngle = retained.BodyShaderAngle, NodeData = retained.NodeData ?? resources.Nodes.Data, Scene = retained.Scene } };
        NativeEffectMemory = frame.Memory with { AngelWing84 = frame.State };
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeAngelWing84(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Angel Wing requires its actual selected module.");
        NativeBattleAngelWing84Resources resources = effect.Binding.AngelWing84Resources ?? throw new NotSupportedException("Native Angel Wing requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Angel Wing requires its retained effect process.");
        NativeBattleAngelWing84Frame frame = new(memory, effect, memory.AngelWing84 ?? throw new NotSupportedException("Native Angel Wing requires its entered process storage."));
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Angel Wing requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Angel Wing requires its published view matrix.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Native Angel Wing requires its original graphics rules.");
        NativeBattleSummonColors colors = rules.Rules.SummonColors ?? throw new NotSupportedException("Native Angel Wing requires its original color constants.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Angel Wing requires its retained CRT random state.");
        NativeBattleAngelWing84Inputs controller = null!;
        controller = new(nativeActionPackets, view, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Angel Wing requires the actual actor skeleton."), AdvanceLeaf, ReloadCommon);
        frame = NativeBattleAngelWing84Controller.Advance(frame, controller);
        NativeEffectMemory = frame.Memory with { AngelWing84 = frame.State };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleAngelWing84BodyResources CurrentBodyResources()
        {
            NativeBattleStageColors processColors = NativeStage?.ModelColors ?? throw new NotSupportedException("Native Angel Wing requires its current shared model colors.");
            return resources.Body with { DefaultColor44 = processColors.Color44, Transition40 = processColors.Color40 };
        }

        NativeBattleLogicalOrdering Ordering() => (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Angel Wing requires its current ordering table.")).Pending;
        void StoreOrdering(NativeBattleLogicalOrdering ordering) => graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering } };

        NativeBattleAngelWing84LeafStep AdvanceLeaf(NativeBattleAngelWing84Frame current, uint address)
        {
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            switch (task.Callback)
            {
                case 0x78DBA0:
                    return NativeBattleAngelWing84Controller.AdvanceTarget(current, address, new(controller, (next, target) => NativeBattleAngelWing84Scene.InitializeTarget(next, target, controller), next => NativeBattleAngelWing84Scene.Read(next).Word(72), next => NativeBattleAngelWing84Scene.Read(next).Word(76), Contact));
                case 0x78DCE0:
                    return NativeBattleAngelWing84Scene.Advance(current, address, new(controller, SceneOperation));
                case 0x78DE40:
                    return NativeBattleAngelWing84Camera.Advance(current, address, new(resources.Camera, (next, camera) =>
                    {
                        NativeCamera = NativeBattleMoogleCamera.Publish733150(camera, NativeCamera ?? throw new NotSupportedException("Native Angel Wing requires its retained camera registers."));
                        return next;
                    }));
                case 0x7961E0:
                    return NativeBattleAngelWing84Body.Advance(current, address, new(CurrentBodyResources(), controller, BodyOperation));
                case 0x796FD0:
                case 0x7972B0:
                    NativeBattleAngelWing84GlowStep glow = NativeBattleAngelWing84Glows.Advance(current, address, new((actor, bone, fraction) => ReadNativeGraphicsBone(graphics, actor, bone, fraction), view, projection, Ordering(), colors, resources.Glows));
                    StoreOrdering(glow.Ordering);
                    return new(glow.Frame, glow.Result);
                case 0x7913C0:
                    NativeBattleVector SpawnOrigin(ref NativeCrtRandomState generator)
                    {
                        generator = generator.Draw(out ushort draw);
                        return NativeBattleAngelWing84Body.ReadBodyBone(current, resources.Body, checked((byte)resources.Body.OriginBones[draw % 14]));
                    }
                    NativeBattleAngelWing84NodeCallbackStep nodes = NativeBattleAngelWing84Nodes.AdvanceGeneric(current, address, resources.Nodes, controller, random, projection, NativeCamera!.Roll, Ordering(), colors, SpawnOrigin);
                    random = nodes.Random;
                    StoreOrdering(nodes.Ordering);
                    return new(nodes.Frame, nodes.Result);
                case 0x78ECD0:
                case 0x791260:
                case 0x796290:
                    NativeBattleAngelWing84ProgramStep program = NativeBattleAngelWing84Programs.Advance(current, address, new(controller, resources.Programs, projection, colors, Ordering(), (next, restore) =>
                    {
                        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Angel Wing requires its actual stage groups.");
                        NativeStage = stage with { Groups = stage.Groups.Select(group => group with { Flags = restore ? unchecked((byte)((group.Flags & ~2) | ((group.Flags >> 1) & 2))) : unchecked((byte)(group.Flags & ~2)) }).ToImmutableArray() };
                        return next;
                    }));
                    StoreOrdering(program.Ordering);
                    return new(program.Frame, program.Result);
                default:
                    throw new NotSupportedException($"Native Angel Wing callback0x{task.Callback:X} has no current implementation.");
            }
        }

        NativeBattleAngelWing84Frame Contact(NativeBattleAngelWing84Frame current, int contact)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory! };
        }

        NativeBattleAngelWing84Frame ReloadCommon(NativeBattleAngelWing84Frame current, uint root)
        {
            uint completion = checked(root + 98);
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(0x20DFAB8u + (uint)current.State.SourceArenaOffset), completion, (address, value) => current = WriteAngelCompletion(current, address, value));
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }

        NativeBattleAngelWing84Frame SceneOperation(NativeBattleAngelWing84Frame current, NativeBattleAngelWing84Operation operation)
        {
            switch (operation)
            {
                case NativeBattleAngelWing84StaticTexture:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Angel Wing requires its actual TIM queue.");
                    (textures, _) = textures.EnqueueTim505E30(resources.SceneTim);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleAngelWing84CameraControl:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Angel Wing requires its current camera process.")).Apply50A730();
                    return current;
                case NativeBattleAngelWing84ModelFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = flags.Flags }) };
                    return current;
                case NativeBattleAngelWing84PhaseBirth birth:
                    return NativeBattleAngelWing84SceneBirth.Apply(current, birth, controller, (next, body) => NativeBattleAngelWing84Body.Initialize(next, body, CurrentBodyResources()));
                default:
                    throw new NotSupportedException("Native Angel Wing scene operation has no current implementation.");
            }
        }

        NativeBattleAngelWing84Frame BodyOperation(NativeBattleAngelWing84Frame current, NativeBattleAngelWing84BodyOperation operation)
        {
            switch (operation)
            {
                case NativeBattleAngelWing84InitializeBodyShader shader:
                    return NativeBattleAngelWing84Body.InitializeShader(current, shader.Address);
                case NativeBattleAngelWing84BodySound sound:
                    PublishNativeModuleSound(new(sound.Sound, 0, null) { Pan = 128 });
                    return current;
                case NativeBattleAngelWing84BindCaster bind:
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(0x1576020, resources.Body.CasterOverride.Data.AsSpan());
                    NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(staging, NativeModelCpuResources ?? throw new NotSupportedException("Native Angel Wing requires its actual CPU resource rows."), actor, 0x25C9F10, 0x1576020);
                    NativeFileStaging = binding.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, binding.Model) };
                    graphics = SelectNativeHeaderSequence(graphics, bind.Actor, 1);
                    return current;
                case NativeBattleAngelWing84DrawBody body:
                    NativeBattleAngelWing84BodyDrawStep drawn = NativeBattleAngelWing84Body.Draw(current, body.Address, resources.Body, view, projection, rules.Rules.ShadowSource ?? throw new NotSupportedException("Native Angel Wing requires its original shadow vectors."), rules.PrimitiveCapabilities ?? throw new NotSupportedException("Native Angel Wing requires its current renderer capabilities."), colors, Ordering());
                    StoreOrdering(drawn.Ordering);
                    return drawn.Frame;
                default:
                    throw new NotSupportedException("Native Angel Wing body operation has no current implementation.");
            }
        }
    }

    private static NativeBattleAngelWing84Frame WriteAngelCompletion(NativeBattleAngelWing84Frame frame, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleAngelWing84Controller.Descriptor);
        uint delta = unchecked(address - pool.BankAddress);
        if (delta >= (uint)(pool.Stride * pool.Slots.Length) || delta % pool.Stride != 98)
        {
            throw new NotSupportedException("Native Angel Wing common texture completion requires its exact root BYTE98.");
        }
        int slot = checked((int)(delta / pool.Stride));
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(pool), pool.WriteSlot(slot, pool.Slots[slot].WriteByte(98, value))) } };
    }
}
