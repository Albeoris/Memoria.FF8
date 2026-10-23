using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeFatedCircle162(NativeBattleGraphicsState graphics, NativeBattleFatedCircle162Resources resources)
    {
        NativeBattleFatedCircle162Frame frame = NativeBattleFatedCircle162Controller.Enter5F90F0(NativeEffectMemory ?? throw new NotSupportedException("Native Fated Circle requires its retained effect memory."), graphics.OriginalEffect ?? throw new InvalidDataException("Native Fated Circle requires its selected original module."), NativeFileStaging ?? NativeBattleFileStaging.Unknown(), nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Fated Circle requires its retained CRT random owner."));
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native5099A0 requires its actual camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeFatedCircle162(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Fated Circle requires its retained effect memory.");
        NativeBattleFatedCircle162Resources resources = memory.FatedCircle162?.RetainedResources ?? throw new NotSupportedException("Native Fated Circle requires resources retained by its actual entry.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Fated Circle requires its current camera registers.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Fated Circle requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Fated Circle requires its actual current view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Fated Circle requires its original color arithmetic.");
        NativeBattleFatedCircle162Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Fated Circle requires its selected module."), NativeFileStaging ?? throw new NotSupportedException("Native Fated Circle requires its entered physical resource bytes."), nativeCrtRandom ?? throw new NotSupportedException("Native Fated Circle requires its retained CRT random owner."));
        NativeBattleFatedCircle162Inputs inputs = new(graphics.Flags, nativeActionPackets, () => NativeEffectIoResult ?? throw new NotSupportedException("Native Fated Circle requires its actual signed file IO result."), Anchor571400, Target5020A0, Bone240, actor => graphics.Models[actor], actor => graphics.Models[actor].Transform?.Scale38 ?? throw new NotSupportedException("Native Fated Circle requires its actual target scaleWORD26."), Execute, Capture);
        frame = NativeBattleFatedCircle162Controller.Advance5FD090(frame, Child, TextureLoad);
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleModuleAnchor Anchor571400(byte actor)
        {
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return anchor with { W = (graphics.Models[actor].Transform ?? throw new NotSupportedException("Native571400 requires the actor angleWORD36.")).Angle36 };
        }

        NativeBattleModuleAnchor Target5020A0(ushort mask)
        {
            NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Native5020A0 requires each selected physical model position.")).ToArray());
            return new(center.X, center.Y, center.Z, 0);
        }

        NativeBattleModuleAnchor Bone240(byte actor)
        {
            NativeBattleVector bone = ReadNativeGraphicsBone(graphics, actor, 240, 0);
            return new(bone.X, bone.Y, bone.Z, 0);
        }

        NativeBattleFatedCircle162ChildStep Child(NativeBattleFatedCircle162Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            return pool.Slots[slot].Callback switch
            {
                0x5F91D0 => NativeBattleFatedCircle162Controller.Advance5F91D0(current, pool, slot, inputs),
                0x5F99C0 => NativeBattleFatedCircle162Controller.Advance5F99C0(current, pool, slot, inputs, actor => ReadNativeRenzokuken160WeaponTip(graphics, actor), view, projection, current.Memory.ScriptedGraphics, colors),
                0x5F9F70 => NativeBattleFatedCircle162Controller.Advance5F9F70(current, pool, slot, inputs, (graphics.Models[current.Memory.FatedCircle162!.Parameters!.Actor78].GeometryBounds52 ?? throw new NotSupportedException("Native5F9F70 requires actual actor boundsWORD3C.")).Maximum.Y, view, projection, colors),
                0x5FA0E0 => NativeBattleFatedCircle162Controller.Advance5FA0E0(current, pool, slot, inputs, view, projection, camera.Roll),
                0x5FA340 => NativeBattleFatedCircle162Controller.Advance5FA340(current, pool, slot, inputs, view, projection, colors),
                0x5FA480 => NativeBattleFatedCircle162Controller.Advance5FA480(current, pool, slot, inputs, view, projection, colors),
                0x5FA540 => NativeBattleFatedCircle162Controller.Advance5FA540(current, pool, slot, inputs, view, projection, colors),
                0x5FA8F0 => NativeBattleFatedCircle162Controller.Advance5FA8F0(current, pool, slot, inputs, view, projection, camera.Roll),
                0x5FABD0 => NativeBattleFatedCircle162Controller.Advance5FABD0(current, pool, slot, inputs, view, projection, colors),
                0x5FAF50 => NativeBattleFatedCircle162Controller.Advance5FAF50(current, pool, slot, inputs, view, projection, colors, camera.CurrentPose ?? throw new NotSupportedException("Native5FAF50 requires its actual camera eye and target.")),
                0x5FB3F0 => NativeBattleFatedCircle162Controller.Advance5FB3F0(current, pool, slot, inputs, view, projection, colors),
                0x5FB630 => NativeBattleFatedCircle162Controller.Advance5FB630(current, pool, slot, inputs, view, projection, colors),
                0x5FB7A0 => NativeBattleFatedCircle162Controller.Advance5FB7A0(current, pool, slot, inputs, view, projection, colors),
                0x5FBD00 => NativeBattleFatedCircle162Controller.Advance5FBD00(current, pool, slot, inputs, view, projection, colors),
                0x5FC480 => NativeBattleFatedCircle162Controller.Advance5FC480(current, pool, slot, inputs, view, projection, colors),
                0x5FC850 => NativeBattleFatedCircle162Controller.Advance5FC850(current, pool, slot, inputs, view, projection, camera.Roll),
                0x5FCB00 => NativeBattleFatedCircle162Controller.Advance5FCB00(current, pool, slot, inputs, view, projection, camera.Roll),
                0x5FCD60 => NativeBattleFatedCircle162Controller.Advance5FCD60(current, pool, slot, inputs, view, projection, camera.Roll),
                _ => throw new NotSupportedException($"Native Fated Circle callback0x{pool.Slots[slot].Callback:X} has no original implementation.")
            };
        }

        NativeBattleFatedCircle162Frame Execute(NativeBattleFatedCircle162Frame current, NativeBattleFatedCircle162Operation operation)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            switch (operation)
            {
                case NativeBattleFatedCircle162BindActor:
                    byte actor = current.Memory.FatedCircle162!.Parameters!.Actor78;
                    NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[actor], NativeGraphicsPoseResource(graphics, actor));
                    NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(current.Staging, NativeModelCpuResources ?? throw new NotSupportedException("Native Fated Circle requires actual CPU resource rows."), model, 0x23CEA20, 0xDA0100);
                    NativeFileStaging = binding.Staging;
                    graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(actor, binding.Model) }, actor, 1);
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleFatedCircle162Sound sound:
                    PublishNativeModuleSound(new(sound.Address, 0x8000, null) { Pan = 128, RawPan = 128 });
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleFatedCircle162Stage stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained stage rows.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleFatedCircle162Flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 10, 255) };
                    return current;
                case NativeBattleFatedCircle162Contacts:
                    graphics = ApplyNativeHeaderContacts(graphics, current.Memory.FatedCircle162!.Parameters!.HeaderIndex7C);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleFatedCircle162ReadTexture:
                    NativeEffectIoResult = -1;
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native508480 requires its current physical effect arena.");
                    current = current with { Memory = current.Memory with { Arena = arena.Write(0x10000, resources.TextureFile.Data.AsSpan()) } };
                    NativeEffectMemory = current.Memory;
                    QueueNativeFileIo482610(resources.TextureFile.Data.Length, 0x508470);
                    return current;
                case NativeBattleFatedCircle162EnqueueTexture:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native505E30 requires its actual texture queue.");
                    (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native505E30 requires actual loaded file bytes."), 0x10000);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleFatedCircle162Geometry geometry:
                    return AppendGeometry(current, geometry.Batch, null);
                case NativeBattleFatedCircle162Trail trail:
                    return AppendGeometry(current, trail.Batch, trail.CursorAdvance);
                case NativeBattleFatedCircle162Sprite sprite:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Fated Circle requires its initialized ordering table.");
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Fated Circle lost its current packet cursor."), primitive.Pending, sprite.Batch, NativeBattleEffectGouraudWrites.FullDword);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return PublishCursor(current with { Memory = written.Memory }, written.Cursor);
                default:
                    throw new NotSupportedException("Native Fated Circle service has no original implementation.");
            }
        }

        NativeBattleFatedCircle162Frame AppendGeometry(NativeBattleFatedCircle162Frame current, NativeBattleGfGeometryBatch packets, int? cursorAdvance)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Fated Circle requires its initialized ordering table.");
            int start = current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Fated Circle lost its current packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, start, primitive.Pending, packets);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return PublishCursor(current with { Memory = written.Memory }, cursorAdvance is int advance ? checked(start + advance) : written.Cursor);
        }

        NativeBattleFatedCircle162Frame PublishCursor(NativeBattleFatedCircle162Frame current, int cursor)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, checked(0x20DFAB8u + (uint)cursor));
            return current with { Effect = current.Effect with { DrawArenaOffset = cursor }, Staging = current.Staging.Write(0x23D0D88, bytes) };
        }

        NativeBattleFatedCircle162Frame TextureLoad(NativeBattleFatedCircle162Frame current, uint completion)
        {
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(0x20DFAB8 + 0x10000, completion, (address, value) => current = current with { Effect = NativeBattleFatedCircle162Controller.WriteCompletionByte(current.Effect, address, value) });
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }

        NativeBattleFatedCircle162Frame Capture(NativeBattleFatedCircle162Frame current, NativeBattleModuleAnchor point)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native5FC5E0 requires its initialized ordering table.");
            NativeBattleThundaga105CaptureTransform transform = NativeBattleFatedCircle162Capture.Transform5FC5E0(point, projection, camera.FocalLength ?? throw new NotSupportedException("Native5FC5E0 requires its current focalWORD1D8E038."));
            NativeBattleFatedCircle162CaptureStep captured = NativeBattleFatedCircle162Capture.Append5FC5E0(current, primitive.Pending, transform, DrawStage);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = captured.Ordering } };
            return captured.Frame;
        }

        NativeBattleFatedCircle162CaptureStep DrawStage(NativeBattleFatedCircle162Frame current, NativeBattleThundaga105CaptureTransform capture, NativeBattleLogicalOrdering ordering)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native5FC5E0 requires its actual stage group1 owner.");
            NativeBattleStageGroup group = stage.Groups[1];
            if ((group.Flags & 1) == 0)
            {
                return new(current, ordering);
            }
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native5FC5E0 requires the current stage group1 pose source.");
            NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(source, pose), capture.Matrix, 0) };
            NativeStage = stage with { Groups = stage.Groups.SetItem(1, group) };
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native5099D0 requires its actual primitive process.");
            bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native5099D0 requires its actual renderer capability.");
            var gate = process.Enter5099D0(hardware);
            current = current with { Memory = current.Memory with { PrimitiveProcess = gate.Process } };
            if (!gate.Draw)
            {
                return new(current, ordering);
            }
            ImmutableArray<NativeBattleRawMesh> meshes = RequireNativeStageGeometry().Where(mesh => mesh.StageGroup == 1).ToImmutableArray();
            int length = meshes.IsEmpty ? 0 : meshes.Max(mesh => checked(mesh.Vertices.Length * 8));
            NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, group.Matrices, capture.Projection, group.ColorFlags, uint.MaxValue, new byte[length].ToImmutableArray(), colors, 16, new(0, 0, 320, 216), 17, group.ColorDepth);
            if (drawn.ProjectedBytesWritten != length)
            {
                throw new NotSupportedException("Native5FC5E0 requires complete original projected records before their packet consumers.");
            }
            int start = current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native5FC5E0 lost its actual packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory with { PrimitiveProcess = gate.Process.PublishModelColor(group.ColorFlags) }, start, ordering, drawn.Packets);
            current = PublishCursor(current with { Memory = written.Memory with { PrimitiveProcess = (written.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native stage drawing lost its renderer process.")).Publish50FDF0(checked(0x20DFAB8u + (uint)written.Cursor), null) } }, written.Cursor);
            return new(current, written.Ordering);
        }
    }

    private bool TryWriteNativeFatedCircle162Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x23CE620);
        if (delta >= 2 * 16 || delta % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5F90F0 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleFatedCircle162Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x5F90F0 } current ? current : throw new NotSupportedException("Native Fated Circle completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleFatedCircle162Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
