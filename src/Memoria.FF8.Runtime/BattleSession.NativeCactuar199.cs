using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeCactuar199(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleCactuar199Resources resources = effect.Binding.Cactuar199Resources ?? throw new NotSupportedException("Native Cactuar requires its original per-module resources.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Cactuar requires its depth-cue constants.");
        NativeBattleCactuar199Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Cactuar requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Cactuar requires its retained CRT random state."));
        NativeBattleCactuar199Inputs inputs = new(graphics.Flags, nativeActionPackets, ReadView, ReadCameraPose, () => NativeEffectMemory?.GuardianCameraProcess ?? throw new NotSupportedException("Native Cactuar requires the retained paired camera banks."), actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Cactuar requires its target's current skeleton."), ReadBone, ReadCentroid, DrawBody, DrawMesh, DrawParticle, DrawNeedle, StartVibration, Execute);
        frame = initialize ? NativeBattleCactuar199Controller.Initialize5A8750(frame.Memory, frame.Effect, inputs, resources, frame.Random) : NativeBattleCactuar199Controller.Advance5AA3A0(frame, inputs, resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraProjection ReadProjection()
        {
            return NativeCamera?.Projection ?? throw new NotSupportedException("Native Cactuar requires its published projection.");
        }

        NativeBattleActionMatrix ReadView()
        {
            return ReadProjection().View ?? throw new NotSupportedException("Native Cactuar requires its published view matrix.");
        }

        NativeBattleCameraPose ReadCameraPose()
        {
            return NativeCamera?.CurrentPose ?? throw new NotSupportedException("Native Cactuar requires its current camera globals.");
        }

        ImmutableArray<byte> ReadBone(int actor, byte selector, short fraction)
        {
            return RawVector(ReadNativeGraphicsBone(graphics, actor, selector, fraction));
        }

        ImmutableArray<byte> ReadCentroid(ushort mask)
        {
            NativeBattleVector[] positions = Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => (graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Cactuar requires its target's actual transform.")).Position28).ToArray();
            return RawVector(NativeBattleActionGeometryRules.TargetCenter(positions));
        }

        NativeBattleCactuar199Frame DrawBody(NativeBattleCactuar199Frame current)
        {
            NativeBattleCactuar199State shared = current.Memory.Cactuar199!;
            NativeBattleCactuarRawModelDraw drawn = NativeBattleCactuarRawModel.Draw5A9010(shared.RawModel, resources.Model, shared.BaseView ?? throw new NotSupportedException("Native Cactuar requires its retained base-view matrix."), ReadProjection(), colors);
            current = current with { Memory = current.Memory with { Cactuar199 = shared with { RawModel = drawn.State } } };
            return AppendCommon(current, drawn.Packets);
        }

        NativeBattleCactuar199Frame DrawMesh(NativeBattleCactuar199Frame current, NativeBattleModulePoolTask task)
        {
            NativeBattleCactuar199State shared = current.Memory.Cactuar199!;
            int modelY = BinaryPrimitives.ReadInt32LittleEndian(shared.RawModel.Model.AsSpan().Slice(0x58));
            return AppendCommon(current, NativeBattleCactuarAuxiliaryDrawing.Mesh(task, resources, modelY, shared.BaseView ?? throw new NotSupportedException("Native Cactuar requires its retained base-view matrix."), ReadProjection(), colors));
        }

        NativeBattleCactuar199ParticleStep DrawParticle(NativeBattleCactuar199Frame current, ImmutableArray<byte> particle)
        {
            NativeBattleCactuarParticleDraw drawn = NativeBattleCactuarAuxiliaryDrawing.Particle5A9290(particle, resources.Sprite, current.Memory.Cactuar199!.BaseView ?? throw new NotSupportedException("Native Cactuar requires its retained base-view matrix."), ReadProjection()) ?? throw new InvalidDataException("Native Cactuar submitted an inactive particle for drawing.");
            NativeEffectMemory = current.Memory;
            graphics = AppendNativeModulePacketBatch(graphics, drawn.Packets, null, null);
            return new(current with { Memory = NativeEffectMemory! }, drawn.FrameResult);
        }

        NativeBattleCactuar199NeedleStep DrawNeedle(NativeBattleCactuar199Frame current, NativeBattleActionMatrix matrix, NativeBattleVector position, uint color)
        {
            NativeBattleCactuarNeedleDraw drawn = NativeBattleCactuarNeedleDrawing.Emit5A9C10(matrix, position, color, ReadView(), ReadProjection());
            bool accepted = drawn.Packets.Attempts.Count(attempt => attempt.Primitive is not null) == 2;
            return new(AppendCommon(current, drawn.Packets), accepted);
        }

        NativeBattleCactuar199VibrationStep StartVibration(NativeBattleCactuar199Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Cactuar requires its retained vibration process.")).Enqueue(resources.Vibration.Bank, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, queued.Handle);
        }

        NativeBattleCactuar199Frame Execute(NativeBattleCactuar199Frame current, NativeBattleCactuar199Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleCactuar199Sound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, 0, null) { Pan = 128 });
                    return current;
                case NativeBattleCactuar199StreamStart stream:
                    NativeBattleSummonStreamState owner = current.Memory.SummonStream ?? throw new NotSupportedException("Native Cactuar requires its retained summon stream owner.");
                    int number = stream.Descriptor switch
                    {
                        0xE94910 => unchecked((int)resources.Voice1),
                        0xE94914 => unchecked((int)resources.Voice2),
                        _ => throw new NotSupportedException("Native Cactuar stream requires its original leading DWORD.")
                    };
                    return current with { Memory = current.Memory with { SummonStream = owner.Publish501860(stream.Descriptor, number) } };
                case NativeBattleCactuar199StreamPlay stream:
                    PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Native Cactuar requires its retained stream number.")).Play5018C0(checked((uint)stream.Pan), checked((uint)stream.Volume)));
                    return current;
                case NativeBattleCactuar199Flash flash:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Cactuar requires its retained stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)flash.Value), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleCactuar199FullscreenFlash flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(flash.A, flash.B, flash.C, flash.D) };
                    return current;
                case NativeBattleCactuar199CameraPublish camera:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Cactuar requires its current camera globals.")).WriteEffectGlobals(camera.Step.Pose);
                    return current with { Memory = current.Memory with { GuardianCameraProcess = camera.Step.Task.Process } };
                case NativeBattleCactuar199Contact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleCactuar199HitReaction reaction:
                    graphics = SelectNativeHeaderSequence(graphics, checked((byte)reaction.Actor), reaction.Mode);
                    return current;
                case NativeBattleCactuar199CommonTextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleCactuar199Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleCactuar199StopVibration vibration:
                    NativeVibrationProcessState vibrationProcess = NativeVibrationProcess ?? throw new NotSupportedException("Native Cactuar requires its retained vibration process.");
                    if (vibrationProcess.IsPlaying(vibration.Handle))
                    {
                        NativeVibrationProcess = vibrationProcess.Stop4A2940(vibration.Handle);
                    }
                    return current;
                default:
                    throw new NotSupportedException("Native Cactuar operation requires its original callback.");
            }
        }

        NativeBattleCactuar199Frame AppendCommon(NativeBattleCactuar199Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Cactuar requires its initialized ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Cactuar requires its common packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }

        static ImmutableArray<byte> RawVector(NativeBattleVector vector)
        {
            // Only XYZ are produced components; bone callers preserve the destination's fourth WORD.
            byte[] result = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(result, vector.X);
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(2), vector.Y);
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(4), vector.Z);
            return result.ToImmutableArray();
        }
    }

    private bool TryWriteNativeCactuarCompletion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleCactuar199Controller.ChildBank);
        if (delta >= 100 * 0x24 || delta % 0x24 != 0xE)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5A8750 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleCactuar199Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState current = graphics.OriginalEffect is { Binding.Entry: 0x5A8750 } selected ? selected : throw new NotSupportedException("Native Cactuar completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleCactuar199Controller.WriteCompletionByte(current, address, value) };
        }
        return true;
    }
}
