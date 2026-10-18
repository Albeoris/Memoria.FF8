using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeScan40(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Scan requires its retained effect process.");
        NativeBattleScanProcess process = memory.Scan ?? throw new NotSupportedException("Native Scan requires its explicitly initialized process owners.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Scan requires its current module.");
        NativeBattleScanVisualResources resources = effect.Binding.ScanVisualResources ?? throw new NotSupportedException("Native Scan requires its original visual resources.");
        NativeBattleScanFrame frame;
        if (initialize)
        {
            frame = NativeBattleScanController.Initialize84D030(memory, effect, nativeActionPackets);
        }
        else
        {
            frame = new(memory, effect, process.Controller ?? throw new InvalidDataException("Native Scan requires its retained controller state."));
            frame = NativeBattleScanController.Advance84D110(frame, new(nativeActionPackets, index => graphics.Models[index], PoseSource, Begin, SetMode), AdvanceChild);
        }
        process = frame.Memory.Scan!;
        NativeEffectMemory = frame.Memory with { Scan = process with { Controller = frame.State } };
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int index) => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Scan requires the current physical model skeleton.");
        NativeBattleScanFrame Begin(NativeBattleScanFrame current)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Scan requires its current camera process.")).Apply50A730();
            return current;
        }
        NativeBattleScanFrame SetMode(NativeBattleScanFrame current, int mode)
        {
            NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Native Scan requires its actual UI flags.");
            graphics = graphics with { UiServices = mode == 0 ? ui.SetFlags(0x200, 0x100) : ui.SetFlags(0x100, 0x200) };
            return current;
        }
        NativeBattleScanChildStep AdvanceChild(NativeBattleScanFrame current, uint address)
        {
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            NativeBattleScanProcess shared = current.Memory.Scan!;
            switch (task.Callback)
            {
                case 0x84D230:
                    NativeBattleScanTargetStep target = NativeBattleScanTarget.Advance84D230(new(current, graphics.Models, NativeBattleScanPhysicalModels.Capture(graphics.Models)), address, new(nativeActionPackets, PoseSource, ReadExternal, Sound, Contact));
                    graphics = graphics with { Models = target.Frame.Models };
                    return new(target.Frame.Scan, target.Result);
                case 0x84D4B0:
                    NativeBattleScanPrimaryStep primary = NativeBattleScanPrimary.Advance84D4B0(new(current, graphics.Models, shared.Presentation.Highlight269AA90, unchecked((short)shared.Presentation.Camera269A168.ReadWord(0x3A))), address, nativeActionPackets);
                    graphics = graphics with { Models = primary.Frame.Models };
                    return new(SetHighlight(primary.Frame.Scan, primary.Frame.Highlight269AA90), primary.Result);
                case 0x84D5B0:
                    NativeBattleScanReticleStep reticle = NativeBattleScanReticle.Advance84D5B0(new(current, shared.Presentation.Highlight269AA90), address, new(nativeActionPackets, resources.Trajectory15D1898, (f, t, radius, angle, count) => DrawNativeScanRing(f, t, radius, angle, count, ref graphics), (f, t) => DrawNativeScanFourLines(f, t, ref graphics)));
                    return new(SetHighlight(reticle.Frame.Scan, reticle.Frame.Highlight269AA90), reticle.Result);
                case 0x84DDD0:
                case 0x84E010:
                case 0x84E3D0:
                case 0x84E510:
                    NativeBattleScanOpeningInputs opening = new(nativeActionPackets, pointer => resources.LineArrays15D1718[checked((int)((pointer - 0x15D1718) / 48))], pointer => pointer == 0x15D0AF8 ? resources.Sprite15D0AF8 : pointer == 0x15D07D4 ? resources.Sprite15D07D4 : throw new NotSupportedException("Native Scan sprite pointer has no original resource."), Camera().Projection!, Camera().Roll, (f, batch) => f with { Scan = SubmitNativeScanPackets(f.Scan, batch, ref graphics) });
                    NativeBattleScanReticleFrame openingFrame = new(current, shared.Presentation.Highlight269AA90);
                    NativeBattleScanReticleStep opened = task.Callback switch
                    {
                        0x84DDD0 => NativeBattleScanOpeningChildren.Advance84DDD0(openingFrame, address, opening),
                        0x84E010 => NativeBattleScanOpeningChildren.Advance84E010(openingFrame, address, opening),
                        0x84E3D0 => NativeBattleScanOpeningChildren.Advance84E3D0(openingFrame, address, opening),
                        _ => NativeBattleScanOpeningChildren.Advance84E510(openingFrame, address, opening)
                    };
                    return new(SetHighlight(opened.Frame.Scan, opened.Frame.Highlight269AA90), opened.Result);
                case 0x84D810:
                case 0x84DAA0:
                    NativeBattleScanPairMorphFrame morphFrame = new(current, shared.Presentation.Highlight269AA90, shared.MutableMeshes);
                    NativeBattleScanPairMorphStep morph = task.Callback == 0x84D810 ? NativeBattleScanPairMorph.Advance84D810(morphFrame, address, VisualInputs()) : NativeBattleScanPairMorph.Advance84DAA0(morphFrame, address, VisualInputs());
                    return new(CaptureVisual(morph.Frame), morph.Result);
                case 0x84EFF0:
                    NativeBattleScanNormalBandStep band = NativeBattleScanNormalBand.Advance84EFF0(new(current, shared.Presentation.Result269D938), address, (f, batch) => f with { Scan = SubmitNativeScanPackets(f.Scan, batch, ref graphics) });
                    return new(band.Frame.Scan, band.Result);
                case 0x84F300:
                case 0x84F430:
                case 0x84F520:
                case 0x84F9E0:
                    NativeBattleScanDecorationsStep decoration = NativeBattleScanDecorations.Advance(new(new(current, shared.Presentation.Highlight269AA90, shared.MutableMeshes), shared.Presentation.Exit269CE2C, shared.Presentation.Result269D938), address, new(nativeActionPackets, VisualInputs(), actor => ReadNativeScanDescriptor208(actor)));
                    return new(CaptureVisual(decoration.Frame.Visual), decoration.Result);
                case 0x84ED90:
                case 0x8507F0:
                    NativeBattleScanVisibilityStep visibility = NativeBattleScanModelVisibility.Advance(new(current, graphics.Models, NativeStage ?? throw new NotSupportedException("Native Scan requires its current stage."), definition.NativeGraphics!.Rules.SummonColors!.Transition), address);
                    graphics = graphics with { Models = visibility.Frame.Models };
                    NativeStage = visibility.Frame.Stage;
                    return new(visibility.Frame.Scan, visibility.Result);
                case 0x850B40:
                    NativeBattleScanAlternateStep alternate = NativeBattleScanAlternate.Advance850B40(new(CameraFrame(current), graphics.Models, shared.Presentation.Highlight269AA90, shared.Complete269D93A, shared.Presentation.Exit269CE2C), address, new(nativeActionPackets, actor => Profile(current, actor)));
                    graphics = graphics with { Models = alternate.Frame.Models };
                    PublishCamera(alternate.Frame.Camera.Eye, alternate.Frame.Camera.Target, alternate.Frame.Camera.Zoom);
                    current = SetHighlight(alternate.Frame.Camera.Scan, alternate.Frame.Highlight269AA90);
                    shared = current.Memory.Scan!;
                    return new(current with { Memory = current.Memory with { Scan = shared with { Complete269D93A = alternate.Frame.Complete269D93A, Presentation = shared.Presentation with { Exit269CE2C = alternate.Frame.Exit269CE2C } } } }, alternate.Result);
                case 0x850A30:
                    NativeBattleCameraPose saved = Camera().CapturedPose ?? throw new NotSupportedException("Native Scan restoration requires its retained original camera capture.");
                    NativeBattleScanCameraStep restored = NativeBattleScanAlternate.Advance850A30(CameraFrame(current), address, new(CameraVector(saved.Eye, Camera().CapturedEyePadding806), CameraVector(saved.Target, Camera().CapturedTargetPadding80E), unchecked((ushort)(saved.FocalLength ?? throw new NotSupportedException("Native Scan restoration requires its captured zoom.")))));
                    PublishCamera(restored.Frame.Eye, restored.Frame.Target, restored.Frame.Zoom);
                    return new(restored.Frame.Scan, restored.Result);
                case 0x850C70:
                case 0x850EB0:
                    NativeBattleScanPairMorphStep alternateDecoration = NativeBattleScanAlternateDecorations.Advance(new(current, shared.Presentation.Highlight269AA90, shared.MutableMeshes), address, shared.Presentation.Exit269CE2C, new(nativeActionPackets, (f, draw) => DrawNativeScanMesh(f, draw, resources, ref graphics), DrawAlternateSpinner));
                    return new(CaptureVisual(alternateDecoration.Frame), alternateDecoration.Result);
                case 0x84E810:
                    NativeBattleScanViewerProfile profile = Profile(current, task.ReadByte(0x2D));
                    NativeBattleActionMatrix view = Camera().Projection?.View ?? throw new NotSupportedException("Native Scan requires its current published camera view.");
                    NativeBattleScanCameraStep camera = NativeBattleScanCameraTransition.Advance84E810(new(current, CameraVector(Camera().CurrentPose!.Eye, Camera().EyePaddingF6), CameraVector(Camera().CurrentPose!.Target, Camera().TargetPaddingFE), unchecked((ushort)(Camera().FocalLength ?? throw new NotSupportedException("Native Scan requires its actual focal length.")))), address, new(NativeBattleScanPhysicalModels.Capture(graphics.Models), profile.Bytes16, profile.Category, view.Rotation[2], view.Rotation[8], (index, selector, fraction) => ReadNativeGraphicsBone(graphics, index, selector, checked((short)fraction))));
                    PublishCamera(camera.Frame.Eye, camera.Frame.Target, camera.Frame.Zoom);
                    return new(camera.Frame.Scan, camera.Result);
                case 0x84F860:
                    NativeBattleScanViewerStep viewer = NativeBattleScanViewer.Advance84F860(new(current, NativeBattleScanPhysicalModels.Capture(graphics.Models), CameraVector(Camera().CurrentPose!.Target, Camera().TargetPaddingFE), ReadText(current)), address, new(nativeActionPackets, shared.Input.Input, Options(), definition.NativeInputButtonMap, ReadNativeScanName24, actor => current.Effect.Binding.ScanTextResources!.DescriptionPointer(ReadNativeScanDescriptor208(actor).ReadByte(0xBB)), actor => Profile(current, actor), (f, index, selector, fraction) => ReadNativeGraphicsBone(graphics with { Models = NativeBattleScanPhysicalModels.Apply(graphics.Models, f.Models) }, index, selector, checked((short)fraction))));
                    graphics = graphics with { Models = NativeBattleScanPhysicalModels.Apply(graphics.Models, viewer.Frame.Models) };
                    NativeBattleCameraPose pose = Camera().CurrentPose!;
                    PublishCamera(CameraVector(pose.Eye, Camera().EyePaddingF6), viewer.Frame.CameraTarget8, unchecked((ushort)Camera().FocalLength!.Value));
                    return new(CaptureText(viewer.Frame.Scan, viewer.Frame.Text), viewer.Result);
                case 0x84FAF0:
                case 0x850FD0:
                    NativeBattleScanResultTaskFrame resultFrame = new(current, shared.Results, ReadText(current), shared.Complete269D93A);
                    NativeBattleScanResultServices resultServices = new(ReadNativeScanDescriptor208, id => current.Effect.Binding.ScanTextResources!.ReadEncodedMiscText(id) ?? throw new NotSupportedException("Native Scan requires its actual reached miscellaneous text template."), actor => ReadNativeScanEnemyInformation(actor)[0xF7], ReadNativeScanExtra71);
                    ushort held = NativeInputConfiguration.Project(shared.Input.Input.Read49ED30(0, 0), Options(), definition.NativeInputButtonMap);
                    NativeBattleScanResultTaskStep result = task.Callback == 0x84FAF0 ? NativeBattleScanResultTask.Advance84FAF0(resultFrame, address, held, resultServices) : NativeBattleScanResultTask.Advance850FD0(resultFrame, address, held, resultServices);
                    current = CaptureText(result.Frame.Scan, result.Frame.Text);
                    shared = current.Memory.Scan!;
                    return new(current with { Memory = current.Memory with { Scan = shared with { Results = result.Frame.Results, Complete269D93A = result.Frame.Complete269D93A } } }, result.Result);
                default:
                    throw new NotSupportedException($"Native Scan callback{task.Callback:X8} requires its reached implementation.");
            }
        }
        NativeBattleScanCameraFrame CameraFrame(NativeBattleScanFrame current) => new(current, CameraVector(Camera().CurrentPose!.Eye, Camera().EyePaddingF6), CameraVector(Camera().CurrentPose!.Target, Camera().TargetPaddingFE), unchecked((ushort)(Camera().FocalLength ?? throw new NotSupportedException("Native Scan requires its actual zoom."))));
        NativeBattleScanPairMorphFrame DrawAlternateSpinner(NativeBattleScanPairMorphFrame f, NativeBattleScanMeshDraw draw)
        {
            NativeBattleScriptedGraphicsMemory registers = f.Scan.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Scan alternate spinner requires its actual GTE registers.");
            NativeBattleGfGeometryBatch batch = NativeBattleScanAltMeshPackets.Emit8DE9D0(resources.MeshesByAddress[draw.ResourceAddress], draw.Matrix, Camera().Projection!, new(0xF0, draw.Color, draw.Fade, null, null, 0, 17, 2), registers.Zsf4 ?? throw new NotSupportedException("Native Scan requires initialized ZSF4."), registers.Dqa, registers.Dqb, definition.NativeGraphics!.Rules.SummonColors);
            return f with { Scan = SubmitNativeScanGeometry(f.Scan, batch, ref graphics) };
        }
        NativeBattleScanPairMorphInputs VisualInputs() => new(resources, (f, draw) => DrawNativeScanMesh(f, draw, resources, ref graphics));
        NativeBattleScanFrame CaptureVisual(NativeBattleScanPairMorphFrame f)
        {
            NativeBattleScanFrame current = SetHighlight(f.Scan, f.Highlight269AA90);
            return current with { Memory = current.Memory with { Scan = current.Memory.Scan! with { MutableMeshes = f.MutableMeshes } } };
        }
        NativeBattleScanViewerProfile Profile(NativeBattleScanFrame current, int actor)
        {
            ushort category = NativeBattleScanModelSelection.Category84D410(NativeBattleScanPhysicalModels.Capture(graphics.Models), checked((short)actor), ReadExternal);
            NativeBattleScanCameraProfile profile = current.Effect.Binding.ScanCameraProfiles?.Single(item => item.Category == category) ?? throw new NotSupportedException("Native Scan requires its original category camera profile.");
            return new(category, new(profile.Bytes, Enumerable.Repeat(true, 16).ToImmutableArray()));
        }
        NativeBattleScanTargetFrame Sound(NativeBattleScanTargetFrame f, uint pointer, uint argument, byte pan)
        {
            if (pointer != 0x15D0B3C)
            {
                throw new NotSupportedException("Native Scan sound requires its actual descriptor.");
            }
            PublishNativeModuleSound(new(resources.Sound15D0B3C, argument, null) { Pan = pan, RawPan = pan });
            return f;
        }
        NativeBattleScanTargetFrame Contact(NativeBattleScanTargetFrame f, int index)
        {
            NativeEffectMemory = f.Scan.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return f with { Scan = f.Scan with { Memory = NativeEffectMemory! } };
        }
        NativeBattleScanTextFrame ReadText(NativeBattleScanFrame f) => f.Memory.Scan!.Text.ReadFrame(graphics.UiServices!.Flags942);
        NativeBattleScanFrame CaptureText(NativeBattleScanFrame f, NativeBattleScanTextFrame text)
        {
            NativeBattleScanTextProcess captured = f.Memory.Scan!.Text.CaptureFrame(text, out ushort flags);
            graphics = graphics with { UiServices = graphics.UiServices! with { Flags942 = flags } };
            return f with { Memory = f.Memory with { Scan = f.Memory.Scan! with { Text = captured } } };
        }
        uint Options() => definition.NativeSavedOptions ?? throw new NotSupportedException("Native Scan requires its actual saved input mapping.");
        byte ReadExternal(uint pointer) => throw new NotSupportedException($"Native Scan model source{pointer:X8} requires its retained owner.");
        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Native Scan requires its current camera.");
        void PublishCamera(NativeBattleScanByteOwner eye, NativeBattleScanByteOwner target, ushort zoom)
        {
            NativeBattleCameraState camera = Camera();
            NativeCamera = camera.WriteEffectGlobals(new(Point(eye), Point(target), unchecked((short)zoom), camera.Roll)) with { EyePaddingF6 = unchecked((short)eye.ReadWord(6)), TargetPaddingFE = unchecked((short)target.ReadWord(6)) };
        }
    }

    private static NativeBattleScanFrame SetHighlight(NativeBattleScanFrame frame, ushort highlight)
    {
        NativeBattleScanProcess process = frame.Memory.Scan!;
        return frame with { Memory = frame.Memory with { Scan = process with { Presentation = process.Presentation with { Highlight269AA90 = highlight } } } };
    }

    private static NativeBattleScanByteOwner CameraVector(NativeBattleCameraPoint point, short? padding)
    {
        NativeBattleScanByteOwner bytes = new(new byte[8].ToImmutableArray(), new bool[8].ToImmutableArray());
        bytes = bytes.WriteWord(0, unchecked((ushort)point.X)).WriteWord(2, unchecked((ushort)point.Y)).WriteWord(4, unchecked((ushort)point.Z));
        return padding is short value ? bytes.WriteWord(6, unchecked((ushort)value)) : bytes;
    }

    private static NativeBattleCameraPoint Point(NativeBattleScanByteOwner bytes) => new(unchecked((short)bytes.ReadWord(0)), unchecked((short)bytes.ReadWord(2)), unchecked((short)bytes.ReadWord(4)));
}
