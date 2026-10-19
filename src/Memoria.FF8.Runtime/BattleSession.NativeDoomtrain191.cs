using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeDoomtrain191(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Doomtrain requires its selected effect.");
        NativeBattleDoomtrain191Resources resources = effect.Binding.Doomtrain191Resources ?? throw new NotSupportedException("Native Doomtrain requires its original per-module resources.");
        NativeBattleDoomtrain191Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Doomtrain requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Doomtrain requires its retained CRT random state."));
        NativeBattleDoomtrain191EnvironmentServices environment = new(() => graphics.Flags, PublishDepth, HideStage, HideTargets);
        NativeBattleDoomtrain191Inputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], ReadCentroid, () => (NativeCamera ?? throw new NotSupportedException("Native Doomtrain requires its current camera.")).CapturePackedView1D97778(), ReadPose, () => graphics.Flags, () => NativeEffectIoResult ?? throw new NotSupportedException("Native Doomtrain requires its signed current IO result."), Execute, AdvanceLeaf);
        frame = initialize ? NativeBattleDoomtrain191Controller.Initialize63E730(frame.Memory, frame.Effect, inputs, resources, frame.Random) : NativeBattleDoomtrain191Controller.Advance6472C0(frame, inputs, resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleVector ReadCentroid(ushort mask)
        {
            return NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Native Doomtrain requires every selected target's current position.")).ToArray());
        }

        NativeBattleCameraPose ReadPose()
        {
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Doomtrain requires its current camera globals.");
            return (camera.CurrentPose ?? throw new NotSupportedException("Native Doomtrain requires its current eye and target.")) with { FocalLength = camera.FocalLength, Roll = camera.Roll };
        }

        NativeBattleDoomtrain191LeafStep AdvanceLeaf(NativeBattleDoomtrain191Frame current, int slot)
        {
            NativeBattleModulePoolTask task = current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor).Slots[slot];
            if (task.Callback == 0x641E60)
            {
                return NativeBattleDoomtrain191Finish.Advance641E60(current, slot, RequestFinish);
            }
            if (task.Callback == 0x646180)
            {
                return NativeBattleDoomtrain191Debris.AdvanceFactory646180(current, slot, () => graphics.Flags);
            }
            if (task.Callback == 0x6462D0)
            {
                return NativeBattleDoomtrain191Debris.Advance6462D0(current, slot, resources.Creature ?? throw new NotSupportedException("Native Doomtrain debris requires its original morph resources."), new(() => graphics.Flags, DrawCreatureMesh));
            }
            if (task.Callback == 0x646640)
            {
                return NativeBattleDoomtrain191Explosion.Advance646640(current, slot, resources.Particles ?? throw new NotSupportedException("Native Doomtrain explosion requires its original sprites."), new(() => graphics.Flags, ReadCurrentView, DrawSteam));
            }
            if (task.Callback == 0x646D30)
            {
                return NativeBattleDoomtrain191Knockback.Advance646D30(current, slot, KnockbackServices());
            }
            if (task.Callback == 0x646F70)
            {
                return NativeBattleDoomtrain191GroundBurst.Advance646F70(current, slot, (resources.Particles ?? throw new NotSupportedException("Native Doomtrain ground burst requires its original sprite.")).RequireSprite(0xE34404), new(() => graphics.Flags, ReadCurrentView, DrawSteam));
            }
            if (task.Callback == 0x642EF0)
            {
                return NativeBattleDoomtrain191WheelTrail.Advance642EF0(current, slot, resources.Files.Single(file => file.FileId == 556).Data.Length, (resources.Particles ?? throw new NotSupportedException("Native Doomtrain wheel trail requires its original sprite.")).RequireSprite(0xE33E40), new(() => graphics.Flags, DrawSteam));
            }
            if (task.Callback is 0x643340 or 0x6435B0)
            {
                return NativeBattleDoomtrain191Clouds.Advance(current, slot, (resources.Particles ?? throw new NotSupportedException("Native Doomtrain clouds require their original sprite.")).RequireSprite(0xE33E40), new(() => graphics.Flags, DrawSteam));
            }
            if (task.Callback is 0x644030 or 0x644530 or 0x644940 or 0x644E70 or 0x645540 or 0x645AE0)
            {
                return NativeBattleDoomtrain191Trails.Advance(current, slot, (resources.Particles ?? throw new NotSupportedException("Native Doomtrain trails require their original particle sprite.")).RequireSprite(0xE342A4), new(() => graphics.Flags, DrawSteam));
            }
            if (task.Callback == 0x643A00)
            {
                return NativeBattleDoomtrain191Glow.Advance643A00(current, slot, new(() => graphics.Flags, DrawCreatureMesh));
            }
            if (task.Callback == 0x641EA0)
            {
                return NativeBattleDoomtrain191Creature.Advance641EA0(current, slot, resources.Files.Single(file => file.FileId == 556).Data.Length, new(() => graphics.Flags, InvokeCreature, DrawCreature, ScaleCreatureCamera, FlashCreature));
            }
            if (task.Callback == 0x643D10)
            {
                return NativeBattleDoomtrain191Chimney.Advance643D10(current, slot, (resources.Particles ?? throw new NotSupportedException("Native Doomtrain chimney requires its original sprite resources.")).RequireSprite(0xE342A4), new(() => graphics.Flags, DrawSteam));
            }
            if (task.Callback == 0x643880)
            {
                return NativeBattleDoomtrain191Flare.Advance643880(current, slot, new(() => graphics.Flags, DrawCreatureMesh));
            }
            if (task.Callback is 0x641580 or 0x6418E0)
            {
                NativeBattleModuleResource sprite = (resources.Particles ?? throw new NotSupportedException("Native Doomtrain steam requires its original particle resources; migrate the selected binding explicitly.")).RequireSprite(0xE33E40);
                return NativeBattleDoomtrain191Steam.Advance(current, slot, sprite, new(() => graphics.Flags, DrawSteam));
            }
            if (task.Callback == 0x641AF0)
            {
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Doomtrain rain requires its current camera.");
                NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Doomtrain rain requires its actual GTE projection.");
                return NativeBattleDoomtrain191Rain.Advance641AF0(current, slot, resources.Sky?.RainColors ?? throw new NotSupportedException("Native Doomtrain rain requires its original color table; migrate the selected binding explicitly."), projection, new(() => graphics.Flags, () => camera.CapturePackedView1D97778(), DrawRain));
            }
            if (task.Callback == 0x640580)
            {
                return NativeBattleDoomtrain191Sky.Advance640580(current, slot, resources.Sky ?? throw new NotSupportedException("Native Doomtrain sky requires its original morph and palette resources; migrate the selected binding explicitly."), new(() => graphics.Flags, DrawSky));
            }
            if (task.Callback is 0x63F7D0 or 0x63F880)
            {
                return NativeBattleDoomtrain191Environment.AdvanceFlash(current, slot, environment);
            }
            if (task.Callback is 0x63FB40 or 0x63FDB0 or 0x63FEC0 or 0x63FFD0 or 0x640140 or 0x640280)
            {
                return NativeBattleDoomtrain191Rigs.Advance(current, slot, new(() => graphics.Flags, DrawRig, active => Execute(active, new NativeBattleDoomtrain191StreamPlay())));
            }
            throw new NotSupportedException($"Native Doomtrain callback{task.Callback:X8} at task{NativeBattleDoomtrain191Controller.ChildBank + 0x24u * (uint)slot:X8} phase{unchecked((short)task.ReadWord(0xC))} requires its original reached contract.");
        }

        NativeBattleDoomtrain191Frame PublishDepth(NativeBattleDoomtrain191Frame current, short depth)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Doomtrain requires its current stage rows.");
            NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = depth, ColorFlags = 0 }).ToImmutableArray() };
            return current;
        }

        NativeBattleDoomtrain191Frame HideStage(NativeBattleDoomtrain191Frame current, bool hidden)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Doomtrain requires its current stage rows.");
            NativeStage = stage with { Groups = stage.Groups.Select(group => group with { Flags = hidden ? (byte)(group.Flags | 2) : (byte)(group.Flags & 0xFD) }).ToImmutableArray() };
            return current;
        }

        NativeBattleDoomtrain191Frame HideTargets(NativeBattleDoomtrain191Frame current, bool hidden)
        {
            foreach (int actor in (current.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain requires its captured target list.")).Targets)
            {
                NativeBattleGraphicsModel model = graphics.Models[actor];
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = hidden ? (ushort)(model.Flags0 | 4) : (ushort)(model.Flags0 & 0xFFFB) }) };
            }
            return current;
        }

        NativeBattleDoomtrain191Frame InvokeCreature(NativeBattleDoomtrain191Frame current, uint address)
        {
            if (address == 0x646570)
            {
                return NativeBattleDoomtrain191Explosion.Spawn646570(current);
            }
            if (address == 0x646AA0)
            {
                NativeBattleEffectParameters parameters = current.Effect.Parameters!;
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
                int count = header.ReadByte(0x10);
                int first = count == 0 ? 0 : header.FirstContactIndex ?? throw new NotSupportedException("Native Doomtrain knockback requires its actual current contact pointer.");
                ImmutableArray<int> actors = Enumerable.Range(first, count).Select(index => (int)nativeActionPackets.ReadContact(index).Primary.ActorIndex).ToImmutableArray();
                return NativeBattleDoomtrain191Knockback.Spawn646AA0(current, actors, ReadCentroid(parameters.TargetMask7A), KnockbackServices());
            }
            if (address == 0x6437F0)
            {
                return NativeBattleDoomtrain191Flare.Spawn6437F0(current);
            }
            if (address is 0x6432F0 or 0x643560)
            {
                return NativeBattleDoomtrain191Clouds.Spawn(current, address);
            }
            if (address is 0x643FD0 or 0x644490 or 0x6444E0 or 0x6448F0 or 0x644E10 or 0x6454A0 or 0x6454F0 or 0x645A90)
            {
                return NativeBattleDoomtrain191Trails.Spawn(current, address);
            }
            if (address is 0x6439C0 or 0x643C10 or 0x643C50 or 0x643C90 or 0x643CD0)
            {
                return NativeBattleDoomtrain191Glow.Spawn(current, address);
            }
            if (address is 0x642EA0 or 0x642E40)
            {
                NativeBattleDoomtrain191State state = current.Memory.Doomtrain191!;
                var snapshots = state.ActorSnapshots;
                foreach (int actor in state.Targets)
                {
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Doomtrain target placement requires its current actor transform.");
                    if (address == 0x642EA0)
                    {
                        NativeBattleVector position = transform.Position28;
                        short fourth = transform.Vertical34;
                        byte[] raw = new byte[8];
                        BinaryPrimitives.WriteInt16LittleEndian(raw, position.X);
                        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(2), position.Y);
                        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(4), position.Z);
                        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(6), fourth);
                        NativeBattleDoomtrain191ActorSnapshot saved = snapshots[actor] ?? throw new NotSupportedException("Native Doomtrain target placement requires its captured scale.");
                        snapshots = snapshots.SetItem(actor, saved with { Position = raw.ToImmutableArray() });
                        transform = transform with { Position28 = position with { Z = unchecked((short)(position.Z - 24000)) } };
                    }
                    else
                    {
                        NativeBattleDoomtrain191ActorSnapshot saved = snapshots[actor] ?? throw new NotSupportedException("Native Doomtrain target restore requires its captured position and scale.");
                        transform = transform with { Position28 = ReadVector(saved.Position), Vertical34 = BinaryPrimitives.ReadInt16LittleEndian(saved.Position.AsSpan().Slice(6)), Scale148 = ReadVector(saved.Scale), Scale154 = BinaryPrimitives.ReadInt16LittleEndian(saved.Scale.AsSpan().Slice(6)) };
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform }) };
                }
                return current with { Memory = current.Memory with { Doomtrain191 = state with { ActorSnapshots = snapshots } } };
            }
            return Execute(current, new NativeBattleDoomtrain191Invoke(address));

            static NativeBattleVector ReadVector(ImmutableArray<byte> bytes) => new(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(4)));
        }

        NativeBattleActionMatrix ReadCurrentView() => (NativeCamera ?? throw new NotSupportedException("Native Doomtrain requires its actual camera matrix.")).CapturePackedView1D97778().ReadMatrix();

        NativeBattleDoomtrain191KnockbackServices KnockbackServices() => new(() => graphics.Flags, actor => graphics.Models[actor], actor => ReadNativeGraphicsBone(graphics, actor, 0xF1, 0), (current, actor, model) =>
        {
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            return current;
        });

        NativeBattleDoomtrain191Frame RequestFinish(NativeBattleDoomtrain191Frame current, uint destination, uint completion)
        {
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(destination, completion, (address, value) => current = current with { Effect = NativeBattleDoomtrain191Finish.WriteCompletionByte(current.Effect, address, value) });
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }

        NativeBattleDoomtrain191Frame FlashCreature(NativeBattleDoomtrain191Frame current)
        {
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 4, 128) };
            return current;
        }

        NativeBattleDoomtrain191Frame ScaleCreatureCamera(NativeBattleDoomtrain191Frame current)
        {
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Doomtrain creature requires its current camera matrix.");
            NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Doomtrain creature requires its current camera projection.");
            NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Doomtrain creature requires its actual current camera rotation.");
            NativeCamera = camera with { Projection = projection with { View = NativeBattlePoseMatrices.Scale56BEF0(view, 2048, 2048, 2048) } };
            return current;
        }

        NativeBattleDoomtrain191Frame DrawCreature(NativeBattleDoomtrain191Frame current)
        {
            NativeBattleDoomtrain191CreatureResources creature = resources.Creature ?? throw new NotSupportedException("Native Doomtrain creature requires its original object masks; migrate the selected binding explicitly.");
            NativeBattleDoomtrain191State state = current.Memory.Doomtrain191!;
            var composed = NativeBattleDoomtrain191Model.Compose642D80(current, state.BaseView.ReadMatrix(), creature.ClampMasks);
            current = composed.Frame;
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Doomtrain creature requires its canonical renderer process.");
            bool draw;
            (process, draw) = process.Enter5099D0(definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Doomtrain creature requires its configured renderer capability."));
            current = current with { Memory = current.Memory with { PrimitiveProcess = process } };
            if (draw)
            {
                NativeBattleCameraProjection projection = (NativeCamera ?? throw new NotSupportedException("Native Doomtrain creature requires its actual projection.")).Projection ?? throw new NotSupportedException("Native Doomtrain creature requires its actual projection registers.");
                NativeBattleDoomtrain191ModelDraw body = NativeBattleDoomtrain191Model.Render5099D0(current, projection, composed.Clipped, definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native Doomtrain creature requires its interpolation constants."));
                process = process.PublishModelColor(body.Color);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Doomtrain creature requires its current ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, body.Packets);
                process = written.Process.Publish50FDF0(unchecked((uint)(written.Process.ByteCursor ?? throw new NotSupportedException("Native Doomtrain creature requires its actual packet end cursor."))), body.SourceCursor);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = body.Frame with { Memory = body.Frame.Memory with { PrimitiveProcess = process } };
            }
            return NativeBattleDoomtrain191Model.Restore642830(current);
        }

        NativeBattleDoomtrain191Frame DrawCreatureMesh(NativeBattleDoomtrain191Frame current, NativeBattleDoomtrain191MeshDraw draw)
        {
            NativeBattleStaticMeshResource mesh = (resources.Creature ?? throw new NotSupportedException("Native Doomtrain creature requires its original mesh resources.")).RequireMesh(draw.ResourceAddress);
            NativeBattleCameraProjection projection = (NativeCamera ?? throw new NotSupportedException("Native Doomtrain creature mesh requires its current camera.")).Projection ?? throw new NotSupportedException("Native Doomtrain creature mesh requires its actual projection.");
            NativeBattleEffectNodeScratch scratch = (current.Memory.NodeScratch2793E30 ?? NativeBattleEffectNodeScratch.Unknown()).WritePrefix(0, BitConverter.GetBytes(0u)).WritePrefix(20, BitConverter.GetBytes(draw.Flags));
            if (draw.Fade is int fade)
            {
                scratch = scratch.WritePrefix(4, BitConverter.GetBytes(fade));
            }
            NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, draw.Matrix, projection, new(draw.Flags, 0, draw.Fade, null, null, null, 17, draw.DepthShift), definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Doomtrain creature mesh requires its interpolation constants."), readSelectedVertex: draw.SelectedVerticesOffset is int selected ? offset =>
            {
                NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain debris requires its current selected vertices.");
                return new(BinaryPrimitives.ReadInt16LittleEndian(arena.Read(selected + offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(arena.Read(selected + offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(arena.Read(selected + offset + 4, 2)));
            } : null, observeSourceCursor: cursor => scratch = scratch.WritePrefix(24, BitConverter.GetBytes(unchecked(mesh.Address + (uint)cursor))));
            NativeBattlePrimitiveProcess process = (current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Doomtrain creature mesh requires its COMMON packet owner.")).PublishModelColor(0);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Doomtrain creature mesh requires its ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process, NodeScratch2793E30 = scratch } };
        }

        NativeBattleDoomtrain191Frame DrawSteam(NativeBattleDoomtrain191Frame current, NativeBattleDoomtrain191SteamDraw draw)
        {
            NativeBattleModuleResource resource = (resources.Particles ?? throw new NotSupportedException("Native Doomtrain steam requires its original particle resources.")).RequireSprite(draw.ResourceAddress);
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Doomtrain steam requires its current camera.");
            NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Doomtrain steam requires its current GTE projection.");
            NativeBattleModuleDraw sprite = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(draw.Phase), Phase = draw.Phase, Anchor = new(0, 0, 0, 0), Scale = 4096, SpriteFlags = draw.Flags, SpriteColor = draw.Color, SpriteWorldRotation = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096] };
            NativeBattleModulePacketBatch packets = NativeBattleModulePackets.EmitSprite571DC0(sprite, projection with { View = draw.Matrix }, camera.Roll, 17, 2);
            NativeEffectMemory = current.Memory;
            graphics = AppendNativeModulePacketBatch(graphics, packets, null, null);
            return current with { Memory = NativeEffectMemory! };
        }

        NativeBattleDoomtrain191Frame DrawRain(NativeBattleDoomtrain191Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Doomtrain rain requires its current COMMON packet owner.");
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Doomtrain rain requires its current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }

        NativeBattleDoomtrain191Frame DrawSky(NativeBattleDoomtrain191Frame current, NativeBattleDoomtrain191SkyDraw draw)
        {
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Doomtrain sky requires its actual camera owner.");
            NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Doomtrain sky requires its current projection.");
            NativeBattleDoomtrain191SkyPackets packets = NativeBattleDoomtrain191Sky.Emit640830(resources.Sky ?? throw new NotSupportedException("Native Doomtrain sky requires its original source resources."), current.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain sky requires its actual current morphed vertices."), draw, projection with { FocalLength = 288 }, current.Memory.NodeScratch2793E30 ?? NativeBattleEffectNodeScratch.Unknown(), definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Doomtrain sky requires its color interpolation constants."));
            NativeBattlePrimitiveProcess process = (current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Doomtrain sky requires its COMMON packet owner.")).PublishModelColor(0);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Doomtrain sky requires its current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, packets.Batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeCamera = camera with { Projection = projection with { FocalLength = camera.FocalLength is short focal ? unchecked((ushort)focal) : throw new NotSupportedException("Native Doomtrain sky requires its current global focal WORD.") } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process, NodeScratch2793E30 = packets.Scratch } };
        }

        NativeBattleDoomtrain191Frame DrawRig(NativeBattleDoomtrain191Frame current, NativeBattleModulePoolTask task, NativeBattleActionMatrix matrix)
        {
            NativeBattleDoomtrain191RigResources rigs = resources.OpeningRigs ?? throw new NotSupportedException("Native Doomtrain rigs require their original mesh and sprite resources; migrate the selected binding explicitly.");
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Doomtrain rigs require their current camera.");
            NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Doomtrain rigs require their current GTE projection.");
            if (task.Callback == 0x640280)
            {
                ushort phase = (ushort)(task.ReadWord(0xC) & 7);
                NativeBattleModuleDraw sprite = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = rigs.Sprite, SpriteFrame = rigs.Sprite.ProjectedFrame571C80(phase), Phase = phase, Anchor = new(0, 0, 0, 0), Scale = 4096, SpriteFlags = 8, SpriteWorldRotation = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096] };
                NativeBattleModulePacketBatch packets = NativeBattleModulePackets.EmitSprite571DC0(sprite, projection with { View = matrix }, camera.Roll, 17, 3);
                NativeEffectMemory = current.Memory;
                graphics = AppendNativeModulePacketBatch(graphics, packets, null, null);
                return current with { Memory = NativeEffectMemory! };
            }
            NativeBattleStaticMeshResource source = task.Callback switch
            {
                0x63FDB0 => rigs.First,
                0x63FEC0 => rigs.Second,
                0x63FFD0 => rigs.Wheel,
                0x640140 => rigs.Steam,
                _ => throw new NotSupportedException("Native Doomtrain rig mesh requires its actual drawing callback.")
            };
            uint flags = task.Callback == 0x63FDB0 ? 0x30u : task.Callback == 0x640140 ? 3u : 0u;
            int? fade = null;
            if (task.Callback == 0x640140)
            {
                short age = unchecked((short)task.ReadWord(0xC));
                if (age == 0 || age >= 2)
                {
                    fade = age == 0 ? 2048 : unchecked((age - 2) * 682);
                    flags = 0xC3;
                }
            }
            NativeBattleEffectNodeScratch scratch = current.Memory.NodeScratch2793E30 ?? NativeBattleEffectNodeScratch.Unknown();
            scratch = scratch.WritePrefix(0, BitConverter.GetBytes(0u)).WritePrefix(20, BitConverter.GetBytes(flags));
            if (fade is int actualFade)
            {
                scratch = scratch.WritePrefix(4, BitConverter.GetBytes(actualFade));
            }
            NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(source.Bytes, 0, default, matrix, projection, new(flags, 0, fade, null, null, null, 17, task.Callback == 0x640140 ? (byte)4 : (byte)2), definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Doomtrain rigs require their color interpolation constants."), observeSourceCursor: cursor => scratch = scratch.WritePrefix(24, BitConverter.GetBytes(unchecked(source.Address + (uint)cursor))));
            NativeBattlePrimitiveProcess process = (current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Doomtrain rigs require their actual COMMON packet owner.")).PublishModelColor(0);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Doomtrain rigs require their current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process, NodeScratch2793E30 = scratch } };
        }

        NativeBattleDoomtrain191Frame Execute(NativeBattleDoomtrain191Frame current, NativeBattleDoomtrain191Operation operation)
        {
            NativeBattleDoomtrain191State state = current.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain lost its retained module owner.");
            switch (operation)
            {
                case NativeBattleDoomtrain191CameraPublish camera:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Doomtrain requires its current camera owner.")).WriteEffectGlobals(camera.Step.Pose);
                    return current;
                case NativeBattleDoomtrain191StreamStart stream:
                    NativeBattleSummonStreamState owner = current.Memory.SummonStream ?? throw new NotSupportedException("Native Doomtrain requires its retained summon stream.");
                    uint address = stream.Descriptor ?? 0x209FAB8;
                    int number = stream.Descriptor is null ? BinaryPrimitives.ReadInt32LittleEndian((current.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Native Doomtrain requires its actual571B70 source.")).Read(0, 4)) : resources.InitialStream;
                    return current with { Memory = current.Memory with { SummonStream = owner.Publish501860(address, number), Doomtrain191 = state with { StreamReady = 1 } } };
                case NativeBattleDoomtrain191StreamPlay:
                    PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Native Doomtrain requires its retained stream number.")).Play5018C0(128, 96));
                    return current;
                case NativeBattleDoomtrain191StartVibration:
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Doomtrain requires its vibration process.")).Enqueue(resources.Vibration.Bank, 1, 128);
                    NativeVibrationProcess = queued.State;
                    return current with { Memory = current.Memory with { Doomtrain191 = state with { VibrationHandle = queued.Handle } } };
                case NativeBattleDoomtrain191StopVibration:
                    NativeVibrationProcessState vibration = NativeVibrationProcess ?? throw new NotSupportedException("Native Doomtrain requires its vibration process.");
                    if (vibration.IsPlaying(state.VibrationHandle))
                    {
                        NativeVibrationProcess = vibration.Stop4A2940(state.VibrationHandle);
                    }
                    return current;
                case NativeBattleDoomtrain191Sound sound:
                    int soundOffset = checked((int)(sound.Descriptor - 0xE3C8A8));
                    if (soundOffset < 0 || soundOffset > resources.Sounds.Data.Length - 4 || (soundOffset & 3) != 0)
                    {
                        throw new InvalidDataException("Native Doomtrain sound descriptor leaves its original source.");
                    }
                    PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.Sounds.Data.AsSpan().Slice(soundOffset)), 0, null) { Pan = 128 });
                    return current;
                case NativeBattleDoomtrain191Load load:
                    NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == load.FileId);
                    NativeEffectIoResult = -1;
                    NativeBattleEffectMemory memory = load.ArenaOffset is int offset ? current.Memory with { Arena = (current.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain requires its current arena destination.")).Write(offset, file.Data.AsSpan()) } : current.Memory with { GuardianWorkBuffer = (current.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Native Doomtrain requires its retained work-buffer destination.")).Write(0, file.Data.AsSpan()) };
                    QueueNativeFileIo482610(file.Data.Length, 0x508470);
                    return current with { Memory = memory };
                case NativeBattleDoomtrain191Tim tim:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Doomtrain requires its texture queue.");
                    (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain requires its current TIM source."), tim.ArenaOffset);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleDoomtrain191Contacts:
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C];
                    int count = header.ReadByte(0x10);
                    if (count > 0)
                    {
                        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Doomtrain requires its actual contact pointer.");
                        ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                        NativeEffectMemory = current.Memory;
                        graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(first, count).Select(nativeActionPackets.ReadContact).ToImmutableArray() });
                        graphics = graphics with { PendingContacts = pending };
                        return current with { Memory = NativeEffectMemory! };
                    }
                    return current;
                case NativeBattleDoomtrain191Invoke { Address: 0x641530 }:
                    return NativeBattleDoomtrain191Steam.Spawn(current, false);
                case NativeBattleDoomtrain191Invoke { Address: 0x6418A0 }:
                    return NativeBattleDoomtrain191Steam.Spawn(current, true);
                case NativeBattleDoomtrain191Invoke { Address: 0x640390 }:
                    return NativeBattleDoomtrain191Sky.Spawn640390(current);
                case NativeBattleDoomtrain191Invoke call when call.Address is 0x63F7A0 or 0x63F850 or 0x63F780 or 0x63F830 or 0x63F8F0 or 0x63F930 or 0x5713E0:
                    return NativeBattleDoomtrain191Environment.Invoke(current, call.Address, environment);
                case NativeBattleDoomtrain191Invoke call:
                    throw new NotSupportedException($"Native Doomtrain helper{call.Address:X8} requires its original reached contract.");
                default:
                    throw new NotSupportedException("Native Doomtrain requires its actual module operation.");
            }
        }
    }
}
