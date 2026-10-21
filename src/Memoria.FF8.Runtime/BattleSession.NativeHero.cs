using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeHero(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("The Hero module requires its selected original effect.");
        NativeBattleHeroResources resources = effect.Binding.HeroResources ?? throw new NotSupportedException("The Hero module requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("The Hero module requires its retained process owners.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("The Hero module requires its retained CRT random owner.");
        NativeBattleHeroFrame frame;
        if (initialize)
        {
            //571B50 returns the fixed effect-arena base, independently of its TIM allocator cursor.
            frame = NativeBattleHeroController.Initialize(memory, effect, random, resources, 0);
            Publish(frame);
            return graphics with { OriginalEffect = frame.Effect };
        }
        frame = new(memory, effect, random, memory.RequireHero(effect.Binding.Effect));
        uint? fanTemporary = null;
        int? nextFanIndex = null;
        bool commonFrame = graphics.EntryLifecycle == 4 && nativePreRootStack is not null;
        NativeBattleHeroStackInput stack = new(commonFrame ? memory.ExecutionStack : null, NativeHostCallbacks ?? graphics.Callback, null, commonFrame ? memory.HeroCrtStack : null)
        {
            ResolveCurrentWord = initialized => initialized ? null : ReadNativeCurrentStackOperand().Bytes
        };
        NativeBattleHeroInputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], Anchor571400, stack, Execute) { ObserveAnchor571400 = ObserveAnchor };
        frame = NativeBattleHeroController.Advance(frame, resources, inputs);
        if (fanTemporary is not null || nextFanIndex is not null)
        {
            throw new InvalidDataException("The Hero callback left its native fan loop or temporary allocation open.");
        }
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        void Publish(NativeBattleHeroFrame current)
        {
            NativeEffectMemory = current.Memory.WithHero(current.State);
            nativeCrtRandom = current.Random;
        }

        NativeBattleModuleAnchor Anchor571400(byte actor)
        {
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return anchor with { W = (graphics.Models[actor].Transform ?? throw new NotSupportedException("The Hero anchor requires its actual model WORD24.")).Angle36 };
        }

        NativeBattleHeroFrame ObserveAnchor(NativeBattleHeroFrame current, byte actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return current;
            }
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("The Hero anchor requires its actual current skeleton and pose source.");
            foreach (byte selector in new byte[] { 240, 241 })
            {
                NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0);
                current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer) };
            }
            return current;
        }

        NativeBattleHeroFrame Execute(NativeBattleHeroFrame current, NativeBattleHeroOperation operation)
        {
            switch (operation)
            {
                case NativeBattleHeroSound sound:
                    NativeEffectMemory = current.Memory;
                    nativeCrtRandom = current.Random;
                    PublishNativeModuleSound(new(sound.Sound, unchecked((uint)sound.Channel), null) { Pan = checked((byte)sound.Pan) });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("The Hero sound lost its canonical process owner."), Random = nativeCrtRandom ?? throw new InvalidDataException("The Hero sound lost its retained CRT random owner.") };
                case NativeBattleHeroContact contact:
                    NativeEffectMemory = current.Memory;
                    nativeCrtRandom = current.Random;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("The Hero contact lost its canonical process owner."), Random = nativeCrtRandom ?? throw new InvalidDataException("The Hero contact lost its retained CRT random owner.") };
                case NativeBattleHeroAct act:
                    if (NativeBattleHeroGeometry.IsHidden(act.Node))
                    {
                        return current;
                    }
                    if (fanTemporary is not null)
                    {
                        throw new InvalidDataException("The Hero ACT cannot interleave its native fan allocation.");
                    }
                    NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(), 88);
                    NativeBattleCameraState camera = RequireCamera();
                    NativeBattleHeroDrawResult actDraw = NativeBattleHeroDrawing.DrawAct(current, act, resources, RequireProjection(camera), RequireColors(), camera.CapturePackedView1D97778(), allocated.Staging, allocated.Address);
                    NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(actDraw.Staging, 88);
                    return Append(actDraw);
                case NativeBattleHeroFanBegin begin:
                    if (begin.TemporaryBytes != 136 || fanTemporary is not null || nextFanIndex is not null)
                    {
                        throw new InvalidDataException("The Hero fan requires one original136-byte allocation per native loop.");
                    }
                    NativeBattleTemporaryAllocation fan = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(), 136);
                    NativeFileStaging = fan.Staging;
                    fanTemporary = fan.Address;
                    nextFanIndex = 0;
                    return current;
                case NativeBattleHeroFan draw:
                    uint address = fanTemporary ?? throw new NotSupportedException("The Hero fan requires its reached native allocation.");
                    if (nextFanIndex != draw.Index)
                    {
                        throw new InvalidDataException("The Hero fan operations lost their actual native loop order.");
                    }
                    NativeBattleCameraState fanCamera = RequireCamera();
                    NativeBattleHeroDrawResult fanDraw = NativeBattleHeroDrawing.DrawFan(current, draw, resources, RequireProjection(fanCamera), RequireColors(), fanCamera.CapturePackedView1D97778(), RequireStaging(), address);
                    NativeFileStaging = fanDraw.Staging;
                    nextFanIndex = draw.Index + 1;
                    return Append(fanDraw);
                case NativeBattleHeroFanEnd end:
                    if (end.TemporaryBytes != 136 || fanTemporary is null || nextFanIndex != 16)
                    {
                        throw new InvalidDataException("The Hero fan release requires all sixteen reached native iterations.");
                    }
                    NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(), 136);
                    fanTemporary = null;
                    nextFanIndex = null;
                    return current;
                default:
                    throw new NotSupportedException("The Hero operation requires its original immediate service owner.");
            }
        }

        NativeBattleHeroFrame Append(NativeBattleHeroDrawResult draw)
        {
            int first = draw.CursorStart ?? throw new InvalidDataException("The visible Hero draw lost its actual private packet cursor.");
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("The Hero draw requires its current main ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(draw.Frame.Memory, first, primitive.Pending, draw.Batch);
            if (draw.Frame.State.PacketCursor != written.Cursor)
            {
                throw new InvalidDataException("The Hero packet stores disagree with their native private cursor advance.");
            }
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return draw.Frame with { Memory = written.Memory };
        }

        NativeBattleFileStaging RequireStaging() => NativeFileStaging ?? throw new NotSupportedException("The Hero draw requires its current physical temporary memory.");
        NativeBattleCameraState RequireCamera() => NativeCamera ?? throw new NotSupportedException("The Hero draw requires its current published camera.");
        NativeBattleCameraProjection RequireProjection(NativeBattleCameraState camera) => camera.Projection ?? throw new NotSupportedException("The Hero draw requires its current published projection.");
        NativeBattleSummonColors RequireColors() => definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("The Hero draw requires its original color interpolation constants.");
    }
}
