using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeAqua(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("The Aqua module requires its selected original effect.");
        NativeBattleAquaResources resources = effect.Binding.AquaResources ?? throw new NotSupportedException("The Aqua module requires its original resources.");
        NativeBattleAquaFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("The Aqua module requires its retained process owners."), effect, nativeCrtRandom ?? throw new NotSupportedException("The Aqua module requires its retained CRT random owner."));
        if (initialize)
        {
            frame = NativeBattleAquaController.Initialize(frame, resources, nativeActionPackets);
            NativeEffectMemory = frame.Memory;
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("The Aqua module requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("The Aqua module requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("The Aqua module requires its published projection.");
        Stack<(uint Address, int Size)> temporary = [];
        NativeBattleAquaInputs? renderingInputs = null;
        NativeBattleAquaInputs inputs = new(nativeActionPackets, projection.View ?? throw new NotSupportedException("The Aqua module requires its current camera matrix."), projection, camera.Roll, definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("The Aqua module requires its original geometry constants."), index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("The Aqua module requires the actual posed model source."), ReadAnchor, Execute) { CameraPadding18 = camera.CanonicalViewPadding, PackedCamera = camera.CapturePackedView1D97778() };
        renderingInputs = inputs;
        frame = NativeBattleAquaController.Advance(frame, resources, inputs);
        if (temporary.Count != 0)
        {
            throw new InvalidDataException("The Aqua callback left an original temporary allocation active.");
        }
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleModuleAnchor ReadAnchor(int actor, short selector, int fraction)
        {
            NativeBattleVector point = NativeBattlePoseMatrices.ReadBone(graphics.Models[actor], NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("The Aqua anchor requires its actual skeleton."), checked((byte)selector), checked((short)fraction));
            return new(point.X, point.Y, point.Z, 0);
        }

        NativeBattleAquaFrame Execute(NativeBattleAquaFrame current, NativeBattleAquaOperation operation)
        {
            switch (operation)
            {
                case NativeBattleAquaStage stageWrite:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("The Aqua stage fade requires its actual four stage groups.");
                    if (stage.Groups.IsDefault || stage.Groups.Length != 4)
                    {
                        throw new InvalidDataException("The Aqua stage fade requires all four original stage groups.");
                    }
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = stageWrite.Depth, ColorFlags = stageWrite.ClearColor ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleAquaSound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleAquaContact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("The Aqua contact lost its process owner.") };
                case NativeBattleAquaSequence sequence:
                    if (((graphics.Models[sequence.Actor].ResourceFlags44 ?? throw new NotSupportedException("The Aqua contact sequence requires its selected resource flags.")) & 0x140) != 0)
                    {
                        graphics = SelectNativeHeaderSequence(graphics, checked((byte)sequence.Actor), sequence.Sequence);
                    }
                    return current;
                case NativeBattleAquaBindCaster bind:
                    ImmutableArray<byte> dat = resources.CasterDat72 ?? throw new NotSupportedException("The Aqua caster binding requires its original Blue Magic DAT.");
                    NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = (NativeFileStaging ?? throw new NotSupportedException("The Aqua caster binding requires its actual staging owner.")).Write(0x158D9AC, dat.AsSpan());
                    NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(staging, NativeModelCpuResources ?? throw new NotSupportedException("The Aqua caster binding requires its actual CPU resource rows."), model, 0x25FA3D8, 0x158D9AC);
                    NativeFileStaging = binding.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, binding.Model) };
                    graphics = SelectNativeHeaderSequence(graphics, checked((byte)bind.Actor), 1);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, graphics.Models[bind.Actor] with { Rotation14 = current.State.Word(0x4A) }) };
                    return current;
                case NativeBattleAquaAnchor anchor:
                    NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[anchor.Actor], NativeGraphicsPoseResource(graphics, anchor.Actor).PoseSource ?? throw new NotSupportedException("The Aqua anchor requires its actual posed skeleton."), checked((byte)anchor.Selector), checked((short)anchor.Fraction));
                    return current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer) };
                case NativeBattleAquaPackets packets:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("The Aqua drawing requires its current pending ordering table.");
                    NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("The Aqua drawing requires its current common packet owner."), primitive.Pending, packets.Batch);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
                case NativeBattleAquaSpriteTask sprite:
                    return NativeBattleAquaRendering.DrawSprite(current, sprite.Task, resources, renderingInputs ?? throw new InvalidOperationException("The Aqua drawing inputs have not been published."));
                case NativeBattleAquaWaveTask wave:
                    return NativeBattleAquaRendering.DrawWave(current, wave.Task, resources, renderingInputs ?? throw new InvalidOperationException("The Aqua drawing inputs have not been published."));
                case NativeBattleAquaTemporaryBegin begin:
                    NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(), begin.Size);
                    NativeFileStaging = allocation.Staging;
                    temporary.Push((allocation.Address, begin.Size));
                    return current;
                case NativeBattleAquaTemporaryPatch patch:
                    NativeFileStaging = RequireStaging().Write(TemporaryAddress(patch.Offset, patch.Bytes.Length), patch.Bytes.AsSpan());
                    return current;
                case NativeBattleAquaTemporaryUnknown unknown:
                    NativeFileStaging = RequireStaging().MarkUnknown(TemporaryAddress(unknown.Offset, unknown.Length), unknown.Length);
                    return current;
                case NativeBattleAquaTemporaryCopy copy:
                    NativeFileStaging = RequireStaging().CopyMemory(TemporaryAddress(copy.Source, copy.Length), TemporaryAddress(copy.Destination, copy.Length), copy.Length);
                    return current;
                case NativeBattleAquaTemporaryFlags flags:
                    uint value = current.Memory.ScriptedColorRegisters?.Flags ?? throw new NotSupportedException("The Aqua flag store requires its reached geometry result.");
                    byte[] bytes = new byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
                    NativeFileStaging = RequireStaging().Write(TemporaryAddress(flags.Offset, 4), bytes);
                    return current;
                case NativeBattleAquaTemporaryEnd end:
                    if (temporary.Count == 0 || temporary.Peek().Size != end.Size)
                    {
                        throw new InvalidDataException("The Aqua drawing released a different temporary allocation.");
                    }
                    NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(), end.Size);
                    temporary.Pop();
                    return current;
                default:
                    return ExecuteNativeAquaGeometry(current, operation, ReadTemporaryWord);
            }
        }

        NativeBattleFileStaging RequireStaging() => NativeFileStaging ?? throw new NotSupportedException("The Aqua temporary helper requires its retained physical staging owner.");

        short? ReadTemporaryWord(int offset)
        {
            (byte[] data, bool[] known) = RequireStaging().ReadSnapshot(TemporaryAddress(offset, 2), 2);
            return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(data) : null;
        }

        uint TemporaryAddress(int offset, int length)
        {
            if (temporary.Count == 0 || offset < 0 || length < 0 || offset > temporary.Peek().Size - length)
            {
                throw new InvalidDataException("The Aqua temporary store leaves its active native allocation.");
            }
            return checked(temporary.Peek().Address + (uint)offset);
        }
    }

}
