using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativePandemona291(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Pandemona requires its selected effect.");
        NativeBattlePandemona291Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Pandemona requires its current effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Pandemona requires its current CRT state."));
        if (initialize)
        {
            NativeBattlePandemona291Resources resources = effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona requires its original static TIM resources.");
            frame = NativeBattlePandemona291Entry.Initialize6ED250(frame, resources, nativeActionPackets, actor => graphics.Models[actor]);
        }
        else
        {
            NativeBattlePandemona291ConstructionInputs construction = new(nativeActionPackets, actor => graphics.Models[actor], (current, actor) =>
            {
                var captured = CaptureNativePandemona291Bounds(graphics, current.Memory, actor);
                graphics = captured.Graphics;
                return new(current with { Memory = captured.Memory }, captured.Bounds);
            }, current =>
            {
                NativeVibrationBank vibration = current.Effect.Binding.Pandemona291Resources?.Vibration ?? throw new NotSupportedException("Native Pandemona requires its original vibration bank.");
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Pandemona requires its current vibration process.")).Enqueue(vibration, 1, 128);
                NativeVibrationProcess = queued.State;
                return new(current, queued.Handle);
            });
            NativeBattlePandemona291TimelineServices timeline = new((current, request) =>
            {
                NativeBattleEffectFileAsset file = current.Effect.Binding.Pandemona291Resources?.Files.SingleOrDefault(file => file.FileId == request.FileId) ?? throw new NotSupportedException($"Native Pandemona requires its requested original archive file{request.FileId}.");
                if (file.Data.IsDefaultOrEmpty)
                {
                    throw new NotSupportedException("Native Pandemona requested archive bytes are unavailable.");
                }
                NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native Pandemona file delivery requires its actual arena.");
                arena = arena.Write(checked((int)(request.DestinationAddress - NativeBattleEffectNodeMemory.ArenaAddress)), file.Data.AsSpan());
                NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Native Pandemona requires its actual IO clock.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
                return current with { Memory = current.Memory with { Arena = arena } };
            }, (current, sound) =>
            {
                PublishNativeModuleSound(new(sound, 1, null) { Pan = 128 });
                return current;
            }, (current, center, age) =>
            {
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Pandemona requires its current camera globals.");
                if (age is 183 or 334)
                {
                    PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Native Pandemona requires its current stream number.")).Play5018C0(128, 127));
                }
                if (age is 223 or 376 or 448)
                {
                    uint sound = current.Effect.Binding.Pandemona291Resources!.EndingSounds[age == 223 ? 0 : age == 376 ? 1 : 2];
                    PublishNativeModuleSound(new(sound, 1, null) { Pan = 128 });
                }
                if (age < 154)
                {
                    NativeCamera = NativeBattlePandemona291Timeline.AdvanceOpeningCamera(camera, center, age);
                }
                else
                {
                    NativeBattleVector? position = null;
                    short? scale = null;
                    byte? state = null;
                    if (age == 376 || age is >= 377 and < 417 || age is >= 440 and < 480)
                    {
                        var target = ReadNativePandemona291FirstTarget(current, graphics);
                        NativeBattleModelTransform transform = target.Actor.Transform ?? throw new NotSupportedException("Native Pandemona camera requires its current first target transform.");
                        position = transform.Position28;
                        scale = transform.Scale38;
                        state = target.State;
                    }
                    NativeCamera = NativeBattlePandemona291LateCamera.Advance(camera, center, current.Memory.Pandemona291!.Ground25562E0 ?? throw new NotSupportedException("Native Pandemona late camera requires its current ground coordinate."), age, position, scale, state);
                }
                if (age is >= 183 and < 224 || age is >= 334 and < 377)
                {
                    NativeBattleScriptedColorRegisters registers = current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona camera interpolation requires its GTE owner.");
                    current = current with { Memory = current.Memory with { ScriptedColorRegisters = registers with { Mac = [null, null, null], Ir = [null, null, null], Flags = null } } };
                }
                return current;
            })
            {
                LoadEndingTim = LoadNativePandemona291EndingTim,
                StopVibration = (current, handle) =>
                {
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Native Pandemona requires its actual vibration process.")).Stop4A2940(unchecked((int)handle));
                    return current;
                },
                DrawTrails = (current, address) =>
                {
                    var drawn = DrawNativePandemona291Trails(current, address, graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                },
                DrawSurface = (current, address, age) =>
                {
                    var drawn = DrawNativePandemona291Surface(current, address, age, graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                },
                DrawLayers = (current, age) =>
                {
                    var drawn = DrawNativePandemona291Layers(current, age, graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                },
                DrawTargets = (current, address, age) =>
                {
                    var drawn = AdvanceNativePandemona291Targets(current, address, age, null, graphics, true);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                },
                DrawLate = (current, address, age) =>
                {
                    var drawn = DrawNativePandemona291Late(current, address, age, graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                },
                DrawRising = (current, address, age) =>
                {
                    var drawn = DrawNativePandemona291Rising(current, address, age, graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                },
                DrawBody = (current, address) =>
                {
                    var drawn = DrawNativePandemona291Body(current, address, graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                },
                PlayStream = current => PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Native Pandemona requires its retained summon-stream number.")).Play5018C0(128, 127)),
                InitializeBody = (current, address, task) =>
                {
                    uint caster = current.Memory.Pandemona291?.Caster2556260 ?? throw new NotSupportedException("Native Pandemona model initialization requires its actual caster pointer.");
                    uint delta = unchecked(caster - 0x1D972C0);
                    if (delta % 156 != 0 || delta / 156 >= graphics.Models.Length)
                    {
                        throw new InvalidDataException("Native Pandemona caster pointer leaves its current graphics-model rows.");
                    }
                    int index = checked((int)(delta / 156));
                    NativeBattlePandemona291ChildStep initialized = NativeBattlePandemona291Body.Initialize99(current, address, task, () => graphics = graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Alpha7 = 0 }) });
                    NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Pandemona requires its current camera globals.");
                    int center = initialized.Frame.Memory.Pandemona291!.Center25562E8!.Value;
                    NativeCamera = camera.WriteEffectGlobals(NativeBattlePandemona291Timeline.FinalBodyCamera99(center, camera.FocalLength, camera.Roll));
                    uint sound = current.Effect.Binding.Pandemona291Resources?.BodySound ?? throw new NotSupportedException("Native Pandemona requires its original model sound descriptor.");
                    PublishNativeModuleSound(new(sound, 1, null) { Pan = 128 });
                    return initialized;
                },
                DrawVortex = (current, address, age) =>
                {
                    var drawn = DrawNativePandemona291Vortex(current, address, age, graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                }
            };
            timeline = timeline with { Completion = new(current => NativePandemona291TargetsAwaitResource40(current, graphics), (current, address) => RestoreNativePandemona291Targets(current, address, ref graphics), timeline.QueueFile) };
            NativeBattlePandemona291MasterInputs inputs = new(construction, (current, address, task) =>
            {
                if (task.Callback == 0x6ED900)
                {
                    return NativeBattlePandemona291Timeline.Advance6ED900(current, address, task, timeline);
                }
                if (task.Callback == 0x6F1060)
                {
                    var ribbon = AdvanceNativePandemona291RisingChild(current, address, task, graphics);
                    graphics = ribbon.Graphics;
                    return ribbon.Step;
                }
                if (task.Callback is 0x6F0D90 or 0x6F0E30)
                {
                    var child = AdvanceNativePandemona291AttachedChild(current, address, task, graphics);
                    graphics = child.Graphics;
                    return child.Step;
                }
                if (task.Callback == 0x6F0FB0)
                {
                    NativeBattlePandemona291SpriteResource sprite = current.Effect.Binding.Pandemona291Resources!.LateSprite ?? throw new NotSupportedException("Native Pandemona late particle requires its original sprite.");
                    var particle = AdvanceNativePandemona291LayerParticle(current, address, task, graphics, NativeBattlePandemona291LateParticles.Draw6F0FB0(task, sprite), NativeBattlePandemona291LateParticles.Finish6F0FB0);
                    graphics = particle.Graphics;
                    return particle.Step;
                }
                if (task.Callback == 0x6F0ED0)
                {
                    var particle = AdvanceNativePandemona291LayerParticle(current, address, task, graphics);
                    graphics = particle.Graphics;
                    return particle.Step;
                }
                throw new NotSupportedException($"Native Pandemona callback0x{task.Callback:X8} at0x{address:X8} requires its complete timeline contract.");
            });
            frame = NativeBattlePandemona291Controller.Advance6ED350(frame, (depth, flags) =>
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained stage groups.");
                stage.Validate();
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)depth), ColorFlags = unchecked((uint)flags) }).ToImmutableArray() };
            }, inputs);
        }
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }
}
