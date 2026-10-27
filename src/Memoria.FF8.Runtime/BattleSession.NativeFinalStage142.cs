using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void UpdateNativeFinalStage142Target3(NativeBattleGraphicsState graphics)
    {
        if (definition.NativeStageSource?.Callback?.Address != NativeBattleFinalStage142.CallbackAddress)
        {
            throw new NotSupportedException("Native50E510 requires the installed stage's original initializer service.");
        }
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native512500 requires its initialized stage.");
        NativeBattleFinalStage142State state = stage.FinalStage142 ?? throw new NotSupportedException("Native512500 requires its retained process owner.");
        ImmutableArray<NativeBattleFinalStage142Actor>.Builder actors = ImmutableArray.CreateBuilder<NativeBattleFinalStage142Actor>(4);
        for (int index = 3; index <= 6; index++)
        {
            NativeBattleGraphicsModel model = graphics.Models[index];
            byte identity = (model.Flags0 & 2) == 0 ? (byte)0 : model.ModelId4 ?? throw new NotSupportedException("Native512500 requires each enabled actor identity byte.");
            bool selected = (model.Flags0 & 2) != 0 && identity == 0x8C;
            BattleCombatant? actor = selected ? GraphicsActor(graphics, index) : null;
            actors.Add(new(index, unchecked((byte)model.Flags0), identity, actor?.CurrentHp ?? 0, actor?.MaximumHp ?? 0));
        }
        NativeStage = stage with { FinalStage142 = NativeBattleFinalStage142.UpdateTarget3(state, actors.MoveToImmutable()) };
    }

    private NativeBattleGraphicsState AdvanceNativeFinalStage142(NativeBattleGraphicsState graphics, NativeBattleStageState stage, NativeBattleStageTask task)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native512980 requires its effect process.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native512980 requires its actual framebuffer owner.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native512980 requires its framebuffer selector.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native512980 requires its camera process.");
        short shake = camera.ScreenOffsetScratchY ?? (camera.ProjectionOffsetsCleared ? (short)0 : throw new NotSupportedException("Native512980 requires its actual screen-offset Y scratch WORD."));
        NativeBattleFinalStage142Step step = NativeBattleFinalStage142.Advance(stage.FinalStage142 ?? throw new NotSupportedException("Native512980 requires its retained specialized stage owner."), stage, task.Phase, buffer, shake);
        NativeStage = step.Stage with { Task = task with { Phase = step.Phase }, FinalStage142 = step.State };
        if (step.CameraShake != shake)
        {
            NativeCamera = camera with { ScreenOffsetScratchY = step.CameraShake, ProjectionOffsetsCleared = false };
        }
        foreach (NativeBattleFinalStage142Command command in step.Commands)
        {
            switch (command.Kind)
            {
                case NativeBattleFinalStage142CommandKind.FarColor:
                    memory = NativeEffectMemory!;
                    NativeEffectMemory = memory with { PrimitiveProcess = memory.PrimitiveProcess!.PublishFarColor45DDA0(unchecked((uint)command.X), unchecked((uint)command.Y), unchecked((uint)command.Z)) };
                    break;
                case NativeBattleFinalStage142CommandKind.Overlay:
                    graphics = SubmitNativeFinalStage142Overlay(graphics, command, buffer);
                    break;
                case NativeBattleFinalStage142CommandKind.PcRenderRequest:
                    memory = NativeEffectMemory!;
                    NativeBattlePcRenderRequest request = memory.PcRenderRequest ?? throw new NotSupportedException("Native512960 requires its retained PC render request.");
                    NativeEffectMemory = memory with { PcRenderRequest = request.Request47CF50(0, 255, 255, 255, 127) };
                    break;
                case NativeBattleFinalStage142CommandKind.Texture0:
                    AdvanceNativeFinalStage142Texture0();
                    break;
                case NativeBattleFinalStage142CommandKind.StandardStage:
                    NativeBattleCameraProjection projection = NativeCamera!.Projection ?? throw new NotSupportedException("Native512980 requires its prior camera projection.");
                    NativeStage = NativeStage!.Step500FD0(projection.View ?? throw new NotSupportedException("Native512980 requires its prior camera matrix."));
                    graphics = SubmitNativeStagePackets(graphics, NativeStage);
                    break;
                default:
                    throw new NotSupportedException("Native512980 reached an unsupported stage service.");
            }
        }
        return graphics;
    }

    private void AdvanceNativeFinalStage142Texture0()
    {
        NativeBattleStageState stage = NativeStage!;
        NativeBattleStageTextureStep motion = NativeBattleStageTextureMotion.Advance50CB20(stage.TextureAnimations, 0);
        NativeStage = stage with { TextureAnimations = motion.Source };
        if (motion.Copies.IsEmpty)
        {
            return;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native512980 requires its retained texture-copy queue.");
        foreach (NativeBattleTextureCopy copy in motion.Copies)
        {
            textures = textures.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
        }
        NativeEffectMemory = memory with { Textures = textures };
    }

    private NativeBattleGraphicsState SubmitNativeFinalStage142Overlay(NativeBattleGraphicsState graphics, NativeBattleFinalStage142Command command, byte buffer)
    {
        NativeBattleStageState stage = NativeStage!;
        NativeBattleFinalStage142State state = stage.FinalStage142!;
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native5127B0 requires its retained CLUT upload owner.");
        textures = textures.EnqueueRectangle505DF0(new(0, 240, 256, 16), state.ClutSource ?? throw new NotSupportedException("Native5127B0 requires its original stage CLUT."));
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess!;
        NativeBattleScriptedDpcsResult color = NativeBattleScriptedDpcs.Transform45F270(memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native56CA00 requires its retained GTE color registers."), 0, unchecked((uint)command.X), process.ModelRendererRegisters ?? throw new NotSupportedException("Native56CA00 requires its retained far-color controls."), definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native45F270 requires its authenticated interpolation constants."));
        NativeEffectMemory = memory with { Textures = textures, ScriptedColorRegisters = color.Registers };
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native500830 requires its actual DRAWENV records.");
        NativeBattleFinalStage142OverlayStep drawn = NativeBattleFinalStage142Overlay.Build(state, command, buffer == 0 ? environments.Buffer1 : environments.Buffer0, color.PackedRgb);
        state = drawn.State;
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native5127B0 requires its initialized ordering table.");
        using NativeBattleLogicalOrdering.Builder links = new(frame.Pending);
        foreach (NativeBattleFinalStage142OverlayPacket packet in drawn.Packets)
        {
            int wordCount = checked(packet.Bytes[3] + 1);
            ImmutableArray<uint> words = Enumerable.Range(0, wordCount).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.Bytes.AsSpan().Slice(index * 4))).ToImmutableArray();
            NativeBattlePrimitiveReference reference = new("1D9A020", 0, checked((int)(packet.Address - NativeBattleFinalStage142.OverlayBankAddress)));
            links.InvalidatePacketRange(reference, packet.AllocatedBytes);
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            ushort flags = packet.AllocatedBytes == 16 || packet.Address == command.PacketAddress + 64 ? (ushort)1 : (ushort)0;
            links.Prepend(packet.Bucket, reference, words, flags, [0, 0, 0, 0]);
            if (linked)
            {
                ImmutableArray<byte> link = NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).ToImmutableArray();
                state = state.WriteOverlay(packet.Address, link, NativeBattleCommonPacketArena.KnownLinkMask);
            }
        }
        NativeStage = stage with { FinalStage142 = state };
        return graphics with { PrimitiveFrame = frame with { Pending = links.ToImmutable() } };
    }
}
