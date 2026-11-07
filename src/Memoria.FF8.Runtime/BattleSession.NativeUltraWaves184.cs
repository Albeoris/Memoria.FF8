using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleUltraWaves184Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeUltraWaves184(NativeBattleGraphicsState graphics) => RunNativeUltraWaves184(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeUltraWaves184(NativeBattleGraphicsState graphics) => RunNativeUltraWaves184(graphics, false);

    private NativeBattleGraphicsState RunNativeUltraWaves184(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Ultra Waves requires its selected original effect.");
        NativeBattleUltraWaves184Resources resources = effect.Binding.UltraWaves184Resources ?? throw new NotSupportedException("Ultra Waves requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Ultra Waves requires its process memory.");
        NativeBattleUltraWaves184Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Ultra Waves requires its CRT stream."), memory.UltraWaves184 ?? throw new NotSupportedException("Ultra Waves requires its retained native banks.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Ultra Waves requires its addressed temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Ultra Waves requires its interpolation constants.");
        NativeBattleUltraWaves184Inputs inputs = new(nativeActionPackets, graphics.Flags, actor => graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Ultra Waves requires its caster world matrix."), Anchor, InitializeGrid, Emitter, Execute);
        frame = initialize ? NativeBattleUltraWaves184Controller.Initialize(frame, resources, inputs) : NativeBattleUltraWaves184Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Ultra Waves requires its current camera.");

        void Publish(NativeBattleUltraWaves184Frame current)
        {
            NativeEffectMemory = current.Memory with { UltraWaves184 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Ultra Waves lost its addressed memory.");
            nativeCrtRandom = current.Random;
            graphics = graphics with { OriginalEffect = current.Effect };
        }

        NativeBattleUltraWaves184Frame Import(NativeBattleUltraWaves184Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Ultra Waves external service lost its process memory.");
            return current with { Memory = latest, State = latest.UltraWaves184 ?? throw new InvalidDataException("Ultra Waves external service lost its retained banks."), Effect = graphics.OriginalEffect ?? throw new InvalidDataException("Ultra Waves lost its current tasks."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Ultra Waves lost its CRT stream.") };
        }

        NativeBattleUltraWaves184AnchorStep Anchor(NativeBattleUltraWaves184Frame current, int actor, short selector, short distance)
        {
            Publish(current);
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Ultra Waves requires its selected current skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, checked((byte)selector), distance);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, new(unchecked((short)point.X), unchecked((short)point.Y), unchecked((short)point.Z), 0));
        }

        NativeBattleUltraWaves184Frame InitializeGrid(NativeBattleUltraWaves184Frame current)
        {
            NativeBattleUltraWavesGridInitialization initialized = NativeBattleUltraWavesGrid.Initialize(current.State.Grid, current.State.Quads, GridAddress);
            return current with { State = current.State with { Grid = initialized.Grid, Quads = initialized.Quads } };
        }

        NativeBattleUltraWaves184Frame Deliver(NativeBattleUltraWaves184Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Ultra Waves requires its current ordering table.");
            int cursor = checked((int)(current.State.ReadCursor() - PacketAddress));
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(current.State.Packets, "22DB3C4", cursor, PacketDrawingLength, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = (current.State with { Packets = written.Arena }).WriteCursor(checked(PacketAddress + (uint)written.Cursor)) };
        }

        NativeBattleUltraWaves184TaskStep Emitter(NativeBattleUltraWaves184Frame current, uint address, NativeBattleUltraWaves184Resources selected)
        {
            NativeBattleUltraWavesEmitterStep step = new(current.Memory, current.Staging!, NativeBattleUltraWaves184Controller.Read(current, address), current.State.Particles, 0);
            step = NativeBattleUltraWavesRingEmitter.Advance(step, 184, selected.Ring, selected.RingColors, Camera().CapturePackedView1D97778(), Camera().Projection!, colors, (graphics.Flags & 0x201) != 0, (next, batch) =>
            {
                current = Deliver(current with { Memory = next.Memory, Staging = next.Staging, State = current.State with { Particles = next.Particles } }, batch);
                return next with { Memory = current.Memory, Staging = current.Staging! };
            }, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(184, step.Task));
            current = NativeBattleUltraWaves184Controller.Write(current with { Memory = step.Memory, Staging = step.Staging, State = current.State with { Particles = step.Particles } }, address, step.Task);
            return new(current, step.Result);
        }

        NativeBattleUltraWaves184Frame Execute(NativeBattleUltraWaves184Frame current, NativeBattleUltraWaves184Operation operation)
        {
            Publish(current);
            switch (operation)
            {
                case NativeBattleUltraWaves184BindCaster bind:
                    if (bind.ResourceAddress != CasterDatAddress || bind.SaveAddress != SavedBindingAddress)
                    {
                        throw new InvalidDataException("Ultra Waves caster binding leaves its original resource and saved row.");
                    }
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = current.Staging!.Write(bind.ResourceAddress, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Ultra Waves requires its CPU resource rows."), actor, bind.SaveAddress, bind.ResourceAddress, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = staging };

                    NativeBattleResourcePointer? ReadPointer(uint address)
                    {
                        return address >= SavedBindingAddress && address < SavedBindingAddress + 32 ? current.State.ReadResourcePointer(address) : staging.ReadResourcePointer(address);
                    }

                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        current = current with { State = current.State.WriteResourcePointer(address, pointer) };
                    }
                case NativeBattleUltraWaves184RingSequence sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, sequence.Actor, sequence.Sequence);
                    return Import(current);
                case NativeBattleUltraWaves184Sound sound:
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = 128 });
                    return Import(current);
                case NativeBattleUltraWaves184ContactAll:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = nativeActionPackets.ReadHeaderContacts506BA0(current.Effect.Parameters!.HeaderIndex7C) });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleUltraWaves184StageVisibility visible:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Ultra Waves requires its current stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { Flags = visible.Visible ? (byte)(group.Flags | 2) : (byte)(group.Flags & 0xFD) }).ToImmutableArray() };
                    return current;
                case NativeBattleUltraWaves184CaptureStage capture:
                    NativeUltraWavesCaptureResult captured = CaptureNativeUltraWaves(graphics, current.State.Packets, current.State.CapturePackets, checked((int)(current.State.ReadCursor() - PacketAddress)), "22DB3C4", "D34868", resources.StageDraw, pilotIdentity: capture.PilotIdentity);
                    graphics = captured.Graphics;
                    current = Import(current);
                    NativeBattleEffectArena offsets = current.State.ReadView(0x22DB3C0, 4).Write(0, BitConverter.GetBytes((short)384)).Write(2, BitConverter.GetBytes((short)256));
                    return current with { State = (current.State with { Packets = captured.Packets, CapturePackets = captured.CapturePackets }).WriteCursor(checked(PacketAddress + (uint)captured.Cursor)).WriteView(0x22DB3C0, offsets, 4) };
                case NativeBattleUltraWaves184DrawGrid draw:
                    NativeBattleUltraWavesGridDraw drawn = NativeBattleUltraWavesGrid.Draw(current.Memory, NativeBattleUltraWavesGrid.Wave(current.State.Grid, draw.Age), current.State.Quads, GridAddress, QuadsAddress, graphics.PrimitiveFrame!.Pending, Camera().Projection!, Camera().FocalLength ?? throw new NotSupportedException("Ultra Waves requires its current focal shadow."), current.Memory.PrimitiveProcess!.Buffer80 ?? throw new NotSupportedException("Ultra Waves requires its current display buffer."), draw.Color, pilotIdentity: draw.PilotIdentity);
                    graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = drawn.Ordering } };
                    return current with { Memory = drawn.Memory, State = current.State with { Grid = drawn.Grid, Quads = drawn.Quads } };
                case NativeBattleUltraWaves184RestoreTexture restore:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(restore.Destination, restore.CompletionAddress, (address, value) => current = NativeBattleUltraWaves184Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Ultra Waves operation requires its native implementation.");
            }
        }
    }
}
