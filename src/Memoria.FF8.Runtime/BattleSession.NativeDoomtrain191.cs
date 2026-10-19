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
