using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleCameraState? NativeCamera { get; private set; }
    public byte NativeCameraTaskMask => unchecked((byte)(NativeCamera ?? throw new NotSupportedException("Native camera completion requires the original two-slot camera task owner.")).Mask);

    private void InitializeNativeStageCamera()
    {
        if (definition.NativeStageCameraBank is null)
        {
            return;
        }
        if (definition.NativeStageCameraSelectors.IsDefault || definition.NativeStageCameraSelectors.Length != 2 || definition.NativeStageCameraControl.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native stage camera requires both actual scene selector bytes.");
        }
        ushort roll = NextNativeGlobalRandom();
        byte selector = definition.NativeStageCameraSelectors[definition.EncounterId == 33 ? 0 : roll & 1];
        NativeCamera = NativeCamera! with { Variables = NativeCamera.Variables.SetItem(0, selector), ControlCursor = 0, ControlResource = null, ControlStagingSource = null };
    }

    internal void StartNativeCamera(NativeBattleCameraBank bank, byte selector)
    {
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native camera dispatch requires its initialized camera task owner.")).Start(bank, selector);
    }

    internal void AdvanceNativeCamera(uint graphicsFlags)
    {
        if (NativeCamera is not null)
        {
            if ((graphicsFlags & 0x101) == 0 && NativeCamera.ControlCursor is not null)
            {
                if (NativeCamera.ControlStagingSource is { } staged)
                {
                    NativeBattleEffectAnimationRegisters current = NativeBattleCameraState.ReadStagedControlResource(NativeFileStaging ?? throw new NotSupportedException("Native camera control requires its current shared staging source."), staged);
                    NativeCamera = NativeCamera with { ControlResource = current };
                }
                NativeBattleEffectAnimationRegisters? resource = NativeCamera.ControlResource;
                NativeBattleCameraBank bank = resource is null ? definition.NativeStageCameraBank! : new(resource.Source.Data.AsSpan().Slice(resource.ModelTableOffset).ToArray().ToImmutableArray());
                ImmutableArray<byte> program = resource is null ? definition.NativeStageCameraControl : resource.Source.Data.AsSpan().Slice(resource.StreamOffset).ToArray().ToImmutableArray();
                NativeCamera = NativeCamera.AdvanceControl(bank, program, new() { GlobalRandom = NextNativeGlobalRandom, ReadExternal = ReadNativeCameraExternal, ScheduleBlend = ScheduleNativeCameraBlend });
            }
            NativeCamera = NativeCamera.Tick(graphicsFlags, new() { TransformReference = (reference, local) => TransformNativeCameraReference(reference, local, graphicsFlags) });
        }
    }

    private NativeBattleCameraPoint TransformNativeCameraReference(byte reference, NativeBattleCameraPoint local, uint graphicsFlags)
    {
        if (reference == 0xF9)
        {
            NativeBattleActionMatrix matrix = (NativeEffectMemory?.SharedCustomMatrix ?? throw new NotSupportedException("Native camera requires its process custom matrix producer.")).ReadMatrix50D790();
            NativeBattleVector result = NativeBattlePoseMatrices.TransformCameraPoint(matrix, new(local.X, local.Y, local.Z), (graphicsFlags & 0x20) != 0);
            return new(result.X, result.Y, result.Z);
        }
        if (reference is 0xF2 or >= 0xF5 and <= 0xF8 || reference >= 0xB0)
        {
            NativeBattleActionGeometry context = RequireNativeGraphics().ActionContext ?? throw new NotSupportedException("Native50D790 matrix references require their captured50D300 action context.");
            NativeBattleActionMatrix matrix = NativeBattleCameraGraphics.ReadMatrix(context, reference);
            NativeBattleVector result = NativeBattlePoseMatrices.TransformCameraPoint(matrix, new(local.X, local.Y, local.Z), (graphicsFlags & 0x20) != 0);
            return new(result.X, result.Y, result.Z);
        }
        int model = reference >> 4;
        if (model is 9 or 10)
        {
            NativeBattleActionGeometry context = RequireNativeGraphics().ActionContext ?? throw new NotSupportedException("Native50D790 camera aliases require the actual50D300 command geometry owner.");
            model = model == 9 ? context.ActorIndex : context.TargetIndex ?? throw new NotSupportedException("Native camera target alias requires its resolved physical target packet.");
        }
        if (model >= 7)
        {
            throw new NotSupportedException($"Native camera reference{reference:X2} requires its active command or auxiliary matrix owner.");
        }
        return TransformNativeGraphicsCameraPoint(model, (byte)(reference & 15), local, (graphicsFlags & 0x20) != 0);
    }

    private void ScheduleNativeCameraBlend(byte duration)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeGraphics = graphics with { Auxiliary = graphics.Auxiliary.AppendCameraBlend(duration) };
    }

    private NativeBattleGraphicsState AdvanceNativeAuxiliary(NativeBattleGraphicsState graphics)
    {
        NativeBattleAuxiliaryStep step = graphics.Auxiliary.Advance(new()
        {
            ApplyScreenFade501D10 = (fade, complete) => graphics = PublishNativeScreenFade501D10(graphics, fade, complete),
            ApplyScreenBlend501E80 = color => graphics = PublishNativeScreenBlend501E80(graphics, color),
            ReadBone = (model, bone, fraction) => ReadNativeGraphicsBone(graphics, model, bone, fraction),
            ReadPan = point => (NativeCamera?.Projection ?? throw new NotSupportedException("Native50F830 requires its actual prior5033E0/500900 projection owner.")).ReadPan(point),
            ReadModel = model => graphics.Models[model],
            ApplyModelRing = (kind, model, argument) => graphics = ApplyNativeModelRingCallback(graphics, kind, model, argument),
            WriteModel = (model, value) => graphics = graphics with { Models = graphics.Models.SetItem(model, value) },
            DrawModelGroup50F190 = (model, group, counter, color) => graphics = SubmitNativeModelGroup50F190(graphics, model, group, counter, color),
            AdvanceModelDeformation50F2E0 = task => AdvanceNativeModelDeformation50F2E0(ref graphics, task),
            WriteStageColorDepth = value =>
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native501F90 requires its four retained stage rows.");
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = value }).ToImmutableArray() };
            },
            WriteCameraTranslationY = value =>
            {
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native50F6C0 requires its camera process.");
                NativeBattleCameraViewTranslationScratch scratch = camera.ViewTranslationScratch ?? throw new NotSupportedException("Native50F6C0 requires its retained camera translation scratch.");
                NativeCamera = camera with { ViewTranslationScratch = scratch with { Y = value } };
            },
            ApplyTexture = (model, selector, variant) => ApplyNativeTextureMotion(graphics, model, selector, variant),
            RequestFile5084B0 = QueueNativeCommonTexture5084B0,
            ReadEffectIoResult508470 = () => NativeEffectIoResult ?? throw new NotSupportedException("Native508660 requires its actual shared signed IO result."),
            EnqueueTexture505E30 = EnqueueNativeCommonTexture505E30,
            WriteCompletionByte = (address, value) =>
            {
                uint relative = unchecked(address - NativeBattleDescriptorState.CompletionAddress503040(0));
                if (relative < 80 && relative % 20 == 0)
                {
                    graphics = graphics with { Descriptors = graphics.Descriptors.WriteCompletion501A70(address, value) };
                }
                else if (TryWriteNativeGravija214Completion(graphics, address, value, out NativeBattleGraphicsState completedGravija214))
                {
                    graphics = completedGravija214;
                }
                else if (TryWriteNativeSonic139Completion(graphics, address, value, out NativeBattleGraphicsState completedSonic))
                {
                    graphics = completedSonic;
                }
                else if (TryWriteNativeBreath157Completion(graphics, address, value, out NativeBattleGraphicsState completedBreath))
                {
                    graphics = completedBreath;
                }
                else if (TryWriteNativeBlaster186Completion(graphics, address, value, out NativeBattleGraphicsState completedBlaster))
                {
                    graphics = completedBlaster;
                }
                else if (TryWriteNativeCannonBlow151Completion(graphics, address, value, out NativeBattleGraphicsState completedCannon))
                {
                    graphics = completedCannon;
                }
                else if (TryWriteNativeWildCannonBlow156Completion(graphics, address, value, out NativeBattleGraphicsState completedWildCannon))
                {
                    graphics = completedWildCannon;
                }
                else if (TryWriteNativeSigh46Completion(graphics, address, value, out NativeBattleGraphicsState completedSigh46))
                {
                    graphics = completedSigh46;
                }
                else if (TryWriteNativeResonance49Completion(graphics, address, value, out NativeBattleGraphicsState completedResonance49))
                {
                    graphics = completedResonance49;
                }
                else if (TryWriteNativeCurse47Completion(graphics, address, value, out NativeBattleGraphicsState completedCurse47))
                {
                    graphics = completedCurse47;
                }
                else if (TryWriteNativeDegenerator306Completion(graphics, address, value, out NativeBattleGraphicsState completedDegenerator306))
                {
                    graphics = completedDegenerator306;
                }
                else if (TryWriteNativeDoom8Completion(graphics, address, value, out NativeBattleGraphicsState completedDoom8))
                {
                    graphics = completedDoom8;
                }
                else if (TryWriteNativeDeathCompletion(graphics, address, value, out NativeBattleGraphicsState completedDeath))
                {
                    graphics = completedDeath;
                }
                else if (TryWriteNativeUltrasonic152Completion(graphics, address, value, out NativeBattleGraphicsState completedUltrasonic152))
                {
                    graphics = completedUltrasonic152;
                }
                else if (TryWriteNativeRayBomb10Completion(graphics, address, value, out NativeBattleGraphicsState rayBombCompleted))
                {
                    graphics = rayBombCompleted;
                }
                else if (TryWriteNativeRayBomb307Completion(graphics, address, value, out NativeBattleGraphicsState blueRayBombCompleted))
                {
                    graphics = blueRayBombCompleted;
                }
                else if (TryWriteNativeUltraWaves184Completion(graphics, address, value, out NativeBattleGraphicsState blueWavesCompleted))
                {
                    graphics = blueWavesCompleted;
                }
                else if (TryWriteNativeUltraWaves154Completion(graphics, address, value, out NativeBattleGraphicsState wavesCompleted))
                {
                    graphics = wavesCompleted;
                }
                else if (TryWriteNativeEffect311Completion(graphics, address, value, out NativeBattleGraphicsState completed))
                {
                    graphics = completed;
                }
                else if (TryWriteNativeFiraCompletion(graphics, address, value, out NativeBattleGraphicsState firaCompleted))
                {
                    graphics = firaCompleted;
                }
                else if (TryWriteNativeAngeloRestorationCompletion(graphics, address, value, out NativeBattleGraphicsState recoverCompleted))
                {
                    graphics = recoverCompleted;
                }
                else if (TryWriteNativeHoly175Completion(graphics, address, value, out NativeBattleGraphicsState holyCompleted))
                {
                    graphics = holyCompleted;
                }
                else if (TryWriteNativeThundaga105Completion(graphics, address, value, out NativeBattleGraphicsState thundagaCompleted))
                {
                    graphics = thundagaCompleted;
                }
                else if (TryWriteNativeGilgameshCompletion(graphics, address, value, out NativeBattleGraphicsState gilgameshCompleted))
                {
                    graphics = gilgameshCompleted;
                }
                else if (TryWriteNativeShivaCompletion(graphics, address, value, out NativeBattleGraphicsState shivaCompleted))
                {
                    graphics = shivaCompleted;
                }
                else if (TryWriteNativeWall189Completion(graphics, address, value, out NativeBattleGraphicsState wallCompleted))
                {
                    graphics = wallCompleted;
                }
                else if (TryWriteNativeTornado146Completion(graphics, address, value, out NativeBattleGraphicsState tornadoCompleted))
                {
                    graphics = tornadoCompleted;
                }
                else if (TryWriteNativeUltima149Completion(graphics, address, value, out NativeBattleGraphicsState ultimaCompleted))
                {
                    graphics = ultimaCompleted;
                }
                else if (TryWriteNativeCactuarCompletion(graphics, address, value, out NativeBattleGraphicsState cactuarCompleted))
                {
                    graphics = cactuarCompleted;
                }
                else if (TryWriteNativeChoco100Completion(graphics, address, value, out NativeBattleGraphicsState choco100Completed))
                {
                    graphics = choco100Completed;
                }
                else if (TryWriteNativeChoco99Completion(graphics, address, value, out NativeBattleGraphicsState choco99Completed))
                {
                    graphics = choco99Completed;
                }
                else if (TryWriteNativeChoco98Completion(graphics, address, value, out NativeBattleGraphicsState choco98Completed))
                {
                    graphics = choco98Completed;
                }
                else if (TryWriteNativeCarbuncle278Completion(graphics, address, value, out NativeBattleGraphicsState carbuncleCompleted))
                {
                    graphics = carbuncleCompleted;
                }
                else if (TryWriteNativeSiren95Completion(graphics, address, value, out NativeBattleGraphicsState sirenCompleted))
                {
                    graphics = sirenCompleted;
                }
                else if (TryWriteNativeTheEnd85Completion(graphics, address, value, out NativeBattleGraphicsState theEndCompleted))
                {
                    graphics = theEndCompleted;
                }
                else if (TryWriteNativeDoomtrain191Completion(graphics, address, value, out NativeBattleGraphicsState doomtrainCompleted))
                {
                    graphics = doomtrainCompleted;
                }
                else if (TryWriteNativeTonberry90Completion(graphics, address, value, out NativeBattleGraphicsState tonberryCompleted))
                {
                    graphics = tonberryCompleted;
                }
                else if (TryWriteNativeChoco97Completion(graphics, address, value, out NativeBattleGraphicsState chocoCompleted))
                {
                    graphics = chocoCompleted;
                }
                else if (TryWriteNativeFatedCircle162Completion(graphics, address, value, out NativeBattleGraphicsState fatedCircle162Completed))
                {
                    graphics = fatedCircle162Completed;
                }
                else if (TryWriteNativeNoMercy200Completion(graphics, address, value, out NativeBattleGraphicsState noMercy200Completed))
                {
                    graphics = noMercy200Completed;
                }
                else if (TryWriteNativeLionHeart165Completion(graphics, address, value, out NativeBattleGraphicsState lionHeart165Completed))
                {
                    graphics = lionHeart165Completed;
                }
                else if (TryWriteNativeBlastingZone164Completion(graphics, address, value, out NativeBattleGraphicsState blastingZone164Completed))
                {
                    graphics = blastingZone164Completed;
                }
                else if (TryWriteNativeDuel112Completion(graphics, address, value, out NativeBattleGraphicsState duel112Completed))
                {
                    graphics = duel112Completed;
                }
                else if (TryWriteNativeDuel274Completion(graphics, address, value, out NativeBattleGraphicsState duel274Completed))
                {
                    graphics = duel274Completed;
                }
                else if (TryWriteNativeRoughDivide163Completion(graphics, address, value, out NativeBattleGraphicsState roughDivide163Completed))
                {
                    graphics = roughDivide163Completed;
                }
                else if (TryWriteNativeEffect60Completion(graphics, address, value, out NativeBattleGraphicsState effect60Completed))
                {
                    graphics = effect60Completed;
                }
                else if (TryWriteNativePsychoBlast23Completion(graphics, address, value, out NativeBattleGraphicsState psychoBlast23Completed))
                {
                    graphics = psychoBlast23Completed;
                }
                else if (TryWriteNativeAngelWing84Completion(graphics, address, value, out NativeBattleGraphicsState angelWing84Completed))
                {
                    graphics = angelWing84Completed;
                }
                else if (TryWriteNativeRinoa87Completion(graphics, address, value, out NativeBattleGraphicsState rinoa87Completed))
                {
                    graphics = rinoa87Completed;
                }
                else if (TryWriteNativeRinoa88Completion(graphics, address, value, out NativeBattleGraphicsState rinoa88Completed))
                {
                    graphics = rinoa88Completed;
                }
                else if (TryWriteNativeRinoa86Completion(graphics, address, value, out NativeBattleGraphicsState rinoa86Completed))
                {
                    graphics = rinoa86Completed;
                }
                else if (TryWriteNativeShot197Completion(graphics, address, value, out NativeBattleGraphicsState shot197Completed))
                {
                    graphics = shot197Completed;
                }
                else if (TryWriteNativeShot193Completion(graphics, address, value, out NativeBattleGraphicsState shot193Completed))
                {
                    graphics = shot193Completed;
                }
                else if (TryWriteNativeShot194Completion(graphics, address, value, out NativeBattleGraphicsState shot194Completed))
                {
                    graphics = shot194Completed;
                }
                else if (TryWriteNativeShot192Completion(graphics, address, value, out NativeBattleGraphicsState shot192Completed))
                {
                    graphics = shot192Completed;
                }
                else if (TryWriteNativeMoogleCompletion(graphics, address, value, out NativeBattleGraphicsState moogleCompleted))
                {
                    graphics = moogleCompleted;
                }
                else if (TryWriteNativeRushCompletion(graphics, address, value, out NativeBattleGraphicsState rushCompleted))
                {
                    graphics = rushCompleted;
                }
                else if (TryWriteNativeSearchCompletion(graphics, address, value, out NativeBattleGraphicsState searchCompleted))
                {
                    graphics = searchCompleted;
                }
                else
                {
                    WriteNativeAuxiliaryCompletionByte(address, value);
                }
            },
            InitializeTimerDisplay47D910 = InitializeNativeTimerDisplay47D910,
            AdvanceMusicLoad501A70 = (phase, address) =>
            {
                NativeBattleMusicProcessState music = NativeMusicProcess ?? throw new NotSupportedException("Native501A70 requires its current501A20 music process.");
                NativeBattleMusicLoadStep loading = music.AdvanceLoad501A70(NativeGlobalSoundProcess ?? throw new NotSupportedException("Native501A70 requires its current sound process."), NativeFileStaging, phase, address, (target, value) => graphics = graphics with { Descriptors = graphics.Descriptors.WriteCompletion501A70(target, value) });
                NativeMusicProcess = loading.State;
                NativeGlobalSoundProcess = loading.Sound;
                return (loading.Phase, loading.Completed);
            },
            RestoreTextureBlink = model => graphics = graphics with { Models = graphics.Models.SetItem(model, graphics.Models[model] with { Flags0 = (ushort)(graphics.Models[model].Flags0 | 0x400) }) }
        }, task => ObserveNativeAuxiliaryStack(graphics, task));
        if (step.CameraBlend is ushort blend)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native509930 requires its initialized camera owner.")) with { BlendProgress = blend };
        }
        foreach (NativeBattleAuxiliaryEmission emission in step.Emissions)
        {
            if (emission.Particle)
            {
                NativeBattleModuleResource resource = definition.NativeModuleResources.Single(candidate => candidate.Id == 3);
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendBoneParticle(emission.Model, emission.Bone, short.MinValue, resource, 3u | (uint)emission.Size << 16) };
            }
            if (emission.Sound is ushort sound)
            {
                NativeBattleVector point = emission.SoundAnchor ?? throw new InvalidDataException("Native B1 sound lacks its502170 anchor.");
                PublishNativeModuleSound(new(sound, 0, new(point.X, point.Y, point.Z, 0)) { Pan = emission.Pan }, graphics.ActorIds[emission.Model]);
            }
        }
        return graphics with { Auxiliary = step.State };
    }

    private void RestoreNativeCamera(BattleSnapshot snapshot)
    {
        if ((snapshot.NativeCamera is null) != (definition.NativeStageCameraBank is null))
        {
            throw new InvalidDataException("Native battle camera task owner is missing; migrate the snapshot explicitly.");
        }
        snapshot.NativeCamera?.Validate();
        NativeBattleEffectAnimationRegisters? current = snapshot.NativeCamera?.ControlStagingSource is { } staged ? NativeBattleCameraState.ReadStagedControlResource(snapshot.NativeFileStaging ?? throw new InvalidDataException("Native staged camera snapshot has no shared staging owner."), staged) : snapshot.NativeCamera?.ControlResource;
        int programLength = current is { } control ? control.Source.Data.Length - control.StreamOffset : definition.NativeStageCameraControl.IsDefault ? 0 : definition.NativeStageCameraControl.Length;
        if (snapshot.NativeCamera?.ControlCursor is int cursor && cursor >= programLength)
        {
            throw new InvalidDataException("Native camera control cursor exceeds its saved original program.");
        }
        NativeCamera = snapshot.NativeCamera;
    }

    private int ReadNativeCameraExternal(byte variable)
    {
        if (variable == 23)
        {
            int active = nativeActionPackets.ActiveHeaderIndex ?? throw new NotSupportedException("Native50A6D0 requires its active104 action header.");
            return nativeActionPackets.Headers[active].ReadByte(17);
        }
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new NotSupportedException("Native camera participant queries require the actual loaded model bank.");
        return NativeBattleCameraGraphics.ReadVariable(graphics, variable);
    }
}
