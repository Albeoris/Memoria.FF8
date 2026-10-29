using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativePain34(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Pain requires its selected effect.");
        NativeBattlePain34Resources resources = effect.Binding.Pain34Resources ?? throw new NotSupportedException("Native Pain requires its original executable resources.");
        NativeBattlePain34Frame entered = NativeBattlePain34Controller.Initialize865600(new(NativeEffectMemory ?? throw new NotSupportedException("Native Pain requires its current process memory."), effect, NativeFileStaging ?? throw new NotSupportedException("Native Pain requires its retained temporary storage.")), resources, nativeActionPackets);
        NativeEffectMemory = entered.Memory;
        NativeFileStaging = entered.Staging;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Pain requires its current camera.")).RegisterControlResource(entered.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = entered.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativePain34(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Pain requires its selected effect.");
        NativeBattlePain34Resources resources = effect.Binding.Pain34Resources ?? throw new NotSupportedException("Native Pain requires its original executable resources.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Pain requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Pain requires its current projection.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Pain requires its current interpolation constants.");
        NativeBattlePain34Inputs inputs = new(nativeActionPackets, camera.CapturePackedView1D97778(), actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Pain requires its current actor pose."), Player, Contact, sound => PublishNativeModuleSound(new(sound, 0, null) { Pan = 128 }));
        NativeBattlePain34Frame advanced = NativeBattlePain34Controller.Advance865720(new(NativeEffectMemory ?? throw new NotSupportedException("Native Pain requires its current process memory."), effect, NativeFileStaging ?? throw new NotSupportedException("Native Pain requires its retained temporary storage.")), resources, inputs);
        NativeEffectMemory = advanced.Memory;
        NativeFileStaging = advanced.Staging;
        return graphics with { OriginalEffect = advanced.Effect };

        NativeBattlePain34LeafStep Player(NativeBattlePain34Frame current, uint address) => NativeBattlePain34Players.Advance(current, address, resources, new(actor => ReadNativeGraphicsBone(graphics, actor, 241, 0), (drawing, source, node, anchor) => Draw(drawing, source, node, anchor, null)) { DrawVisualNode = (drawing, source, node, anchor, identity) => Draw(drawing, source, node, anchor, identity) });
        NativeBattlePain34Frame Draw(NativeBattlePain34Frame current, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattlePilotVisualIdentity? identity)
        {
            NativeBattlePain34DrawingStep drawn = NativeBattlePain34Drawing.Draw865AE0(current, source, node, anchor, projection, colors, identity);
            current = drawn.Frame;
            if (drawn.DrawMode2795BD0 is uint mode)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Pain requires its actual shared draw-mode owner.");
                current = current with { Memory = current.Memory with { PrimitiveProcess = process with { DrawMode2795BD0 = mode } } };
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Pain requires its current ordering table.");
            NativeBattlePain34State state = current.Memory.Pain34 ?? throw new NotSupportedException("Native Pain requires its retained packet cursor.");
            uint cursor = state.Dword(0x26BA9E4);
            if (cursor < 0x20DFAB8 || cursor > 0x21DFAB8)
            {
                throw new InvalidDataException("Native Pain packet cursor leaves its physical arena.");
            }
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - 0x20DFAB8)), primitive.Pending, drawn.Packets);
            state = (written.Memory.Pain34 ?? throw new InvalidDataException("Native Pain packet write lost its retained process.")).WriteDword(0x26BA9E4, checked(0x20DFAB8u + (uint)written.Cursor));
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory with { Pain34 = state }, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
        NativeBattlePain34Frame Contact(NativeBattlePain34Frame current, int index)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Pain contact lost its current memory."), Staging = NativeFileStaging ?? throw new InvalidDataException("Native Pain contact lost its current temporary storage.") };
        }
    }
}