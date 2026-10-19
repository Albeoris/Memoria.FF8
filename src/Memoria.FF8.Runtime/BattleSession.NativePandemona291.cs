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
                NativeCamera = NativeBattlePandemona291Timeline.AdvanceOpeningCamera(NativeCamera ?? throw new NotSupportedException("Native Pandemona requires its current camera globals."), center, age);
                return current;
            })
            {
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
