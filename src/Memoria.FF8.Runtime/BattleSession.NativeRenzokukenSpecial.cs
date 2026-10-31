using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRenzokukenSpecial(NativeBattleGraphicsState graphics, NativeBattleRenzokuken160Resources resources)
    {
        return EnterNativeRenzokukenSpecialCore(graphics, resources, NativeBattleRenzokukenSpecialProfiles.ForEffect((graphics.OriginalEffect ?? throw new InvalidDataException("Native special Renzokuken requires its selected effect.")).Binding.Effect));
    }

    private NativeBattleGraphicsState EnterNativeRenzokukenSpecialCore(NativeBattleGraphicsState graphics, NativeBattleRenzokuken160Resources resources, NativeBattleRenzokukenSpecialProfile profile)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Renzokuken variant requires its selected effect.");
        NativeBattleRenzokuken160Frame frame = NativeBattleRenzokukenSpecialController.Enter(profile, NativeEffectMemory ?? throw new NotSupportedException("Native Renzokuken variant requires retained effect memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Renzokuken variant requires retained CRT random state."));
        frame = NativeBattleRenzokukenSpecialController.Store(frame, NativeBattleRenzokukenSpecialController.ReadState(frame) with { ExtraSprites = effect.Binding.RenzokukenSpecialExtraSprites ?? resources.SpecialSprites });
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native5099A0 requires its actual camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRenzokukenSpecial(NativeBattleGraphicsState graphics)
    {
        return AdvanceNativeRenzokukenSpecialCore(graphics, NativeBattleRenzokukenSpecialProfiles.ForEffect((graphics.OriginalEffect ?? throw new InvalidDataException("Native special Renzokuken requires its selected effect.")).Binding.Effect));
    }

    private NativeBattleGraphicsState AdvanceNativeRenzokukenSpecialCore(NativeBattleGraphicsState graphics, NativeBattleRenzokukenSpecialProfile profile)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Renzokuken variant requires its selected effect.");
        NativeBattleRenzokuken160Resources resources = NativeEffectMemory?.RenzokukenSpecials?.GetValueOrDefault(profile.Effect)?.Resources ?? throw new NotSupportedException("Native Renzokuken variant requires its immutable resources retained at actual entry.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Renzokuken variant requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Renzokuken variant requires its current camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Renzokuken variant requires its actual color interpolation constants.");
        NativeBattleSearchSpriteResource? commonSprite3 = null;
        if (profile.Effect == 341)
        {
            NativeBattleModuleResource resource3 = definition.NativeModuleResources.Single(resource => resource.Id == 3);
            commonSprite3 = new(resource3.Address, resource3.Bytes);
        }
        NativeBattleRenzokuken160Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Renzokuken variant requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Renzokuken variant requires retained CRT random state."));
        frame = NativeBattleRenzokukenSpecialController.AdvanceRoot(frame, profile, Child, Particles);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleRenzokuken160Inputs Inputs(NativeBattleRenzokuken160Frame current) => new(graphics.Flags, NativeBattleRenzokukenSpecialController.ReadState(current).Core.AbSegments.IsEmpty ? graphics.TriggerWidget?.Block.Segments ?? [] : NativeBattleRenzokukenSpecialController.ReadState(current).Core.AbSegments, actor => graphics.Models[actor], Execute)
        {
            ReadBoneCount = actor => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Renzokuken variant requires its actual body skeleton.")).BoneCount,
            ReadBone = (actor, bone, fraction) => ReadNativeGraphicsBone(graphics, actor, checked((byte)bone), fraction),
            ReadWeaponTip = actor => ReadNativeRenzokuken160WeaponTip(graphics, actor),
            CapturePresentation = profile.Effect != 161 && NativeBattleRenzokukenVisualLifetimes.Owns(current.Effect.Binding.Effect)
        };

        NativeBattleRenzokuken160ChildStep Child(NativeBattleRenzokuken160Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            if (profile.Effect is >= 334 and <= 337)
            {
                return NativeBattleRenzokukenSpecialController.AdvanceChild334337(current, pool, slot, Inputs(current), resources with { DefaultModelColor = NativeStage?.ModelColors?.Color40 ?? throw new NotSupportedException("Native special Renzokuken requires its stage color.") }, view, projection, colors, NativeCamera!.Roll, target => ReadNativeModelAnchor(graphics, target) with { W = (graphics.Models[target].Transform ?? throw new NotSupportedException("Native special Renzokuken requires target angleWORD36.")).Angle36 });
            }
            return NativeBattleRenzokukenSpecialController.AdvanceLateSpecialChild(current, pool, slot, Inputs(current), resources with { DefaultModelColor = NativeStage?.ModelColors?.Color40 ?? throw new NotSupportedException("Native special Renzokuken requires its stage color.") }, target => ReadNativeModelAnchor(graphics, target) with { W = (graphics.Models[target].Transform ?? throw new NotSupportedException("Native special Renzokuken requires target angleWORD36.")).Angle36 }, view, projection, current.Memory.ScriptedGraphics, colors, NativeCamera!.Roll, commonSprite3);
        }

        NativeBattleRenzokuken160Frame Particles(NativeBattleRenzokuken160Frame current)
        {
            return NativeBattleRenzokuken160Controller.AdvanceParticles600980(current, resources, graphics.Flags, (actual, sprite) =>
            {
                NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokuken160Drawing.DrawSprite571C80(sprite, projection, view, NativeCamera!.Roll);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Renzokuken variant requires its initialized ordering table.");
                if (profile.Effect != 161 && NativeBattleRenzokukenVisualLifetimes.Owns(actual.Effect.Binding.Effect))
                {
                    actual = actual with { Effect = actual.Effect with { PresentationPackets = NativeBattleRenzokukenVisualLifetimes.Record(actual.Effect.PresentationPackets, "20DFAB8", 0, actual.Effect.DrawArenaOffset!.Value, drawn.Packets) } };
                }
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(actual.Memory, actual.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Renzokuken variant lost its current packet cursor."), primitive.Pending, drawn.Packets, NativeBattleEffectGouraudWrites.FullDword);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return new(actual with { Memory = written.Memory, Effect = actual.Effect with { DrawArenaOffset = written.Cursor } }, drawn.AnimationEnded);
            });
        }

        NativeBattleRenzokuken160Frame Execute(NativeBattleRenzokuken160Frame current, NativeBattleRenzokuken160Operation operation)
        {
            NativeEffectMemory = current.Memory;
            byte actor = NativeBattleRenzokukenSpecialController.ReadState(current).Core.Actor;
            switch (operation)
            {
                case NativeBattleRenzokuken160ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = flags.Flags }) };
                    return current;
                case NativeBattleRenzokuken160ActorColor color:
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Color40 = color.Color }) };
                    return current;
                case NativeBattleRenzokuken160ActorResource binding:
                    NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[actor], NativeGraphicsPoseResource(graphics, actor));
                    model = model with { ResourceMagicId = binding.Resource.MagicId, ResourceRenzokukenTargetBank = null, Flags2 = (ushort)((model.Flags2 ?? throw new NotSupportedException("Native5022C0 requires the actual model WORD2.")) | 8) };
                    graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(actor, model) }, actor, binding.Sequence);
                    return current;
                case NativeBattleRenzokukenSpecialTargetResource binding:
                    NativeBattleGraphicsModel targetModel = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[binding.Target], NativeGraphicsPoseResource(graphics, binding.Target));
                    targetModel = targetModel with { ResourceMagicId = binding.Resource.MagicId, ResourceRenzokukenTargetBank = binding.Bank, Flags2 = (ushort)((targetModel.Flags2 ?? throw new NotSupportedException("Native5022C0 requires target model WORD2.")) | 8) };
                    graphics = graphics with { Models = graphics.Models.SetItem(binding.Target, targetModel) };
                    if (binding.StartSequence)
                    {
                        graphics = SelectNativeHeaderSequence(graphics, binding.Target, binding.Sequence);
                    }
                    return current;
                case NativeBattleRenzokukenSpecialTargetSequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Target, sequence.Sequence);
                    return current;
                case NativeBattleRenzokukenSpecialActorPosition position:
                    NativeBattleGraphicsModel positioned = graphics.Models[actor];
                    NativeBattleModelTransform transform = positioned.Transform ?? throw new NotSupportedException("Native special Renzokuken requires its actual actor transform.");
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, positioned with { Transform = transform with { Position28 = transform.Position28 with { X = position.X, Z = position.Z } } }) };
                    return current;
                case NativeBattleRenzokukenSpecialCameraSet set:
                    return SetCamera(current, set.Y);
                case NativeBattleRenzokukenSpecialCameraWord word:
                    return SetCamera(current, word.Value);
                case NativeBattleRenzokukenSpecialSpriteDraw sprite:
                    NativeBattlePrimitiveFrame spritePrimitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native special Renzokuken requires its ordering table.");
                    NativeBattleEffectPacketWrite spriteWritten = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native special Renzokuken lost its packet cursor."), spritePrimitive.Pending, sprite.Packets, NativeBattleEffectGouraudWrites.FullDword);
                    graphics = graphics with { PrimitiveFrame = spritePrimitive with { Pending = spriteWritten.Ordering } };
                    return current with { Memory = spriteWritten.Memory, Effect = current.Effect with { DrawArenaOffset = spriteWritten.Cursor } };
                case NativeBattleRenzokukenSpecialCameraRotation rotation:
                    NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native special Renzokuken requires its camera owner.");
                    NativeBattleCameraViewTranslationScratch scratch = camera.ViewTranslationScratch ?? throw new NotSupportedException("Native special Renzokuken requires its camera view translation scratch.");
                    NativeCamera = camera with { ViewTranslationScratch = scratch with { Y = unchecked((short)((scratch.Y ?? throw new NotSupportedException("Native special Renzokuken requires camera WORD1D97712.")) + rotation.Delta)) } };
                    return current;
                case NativeBattleRenzokuken160Stage stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained stage rows.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleRenzokuken160Sound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, unchecked((uint)sound.First), null) { Pan = checked((byte)sound.Second), RawPan = unchecked((uint)sound.Second) });
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleRenzokuken160SharedTask shared:
                    (NativeBattleModuleState modules, int slot) = graphics.ModuleEffects.AppendCallback(shared.Callback);
                    byte[] bytes = modules.Slots[slot].Payload.ToArray();
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes, shared.Counter);
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), shared.Actor);
                    modules = modules with { Slots = modules.Slots.SetItem(slot, modules.Slots[slot] with { Payload = bytes.ToImmutableArray() }) };
                    if (profile.Effect != 161 && NativeBattleRenzokukenVisualLifetimes.Owns(current.Effect.Binding.Effect))
                    {
                        NativeBattleModulePoolTask owner = current.Effect.Pools[0].Slots[0];
                        modules = NativeBattleRenzokukenVisualLifetimes.Shared(modules, slot, NativeBattleRenzokukenVisualLifetimes.Task(current.Effect, owner, shared.Callback));
                    }
                    graphics = graphics with { ModuleEffects = modules };
                    return current;
                case NativeBattleRenzokuken160TrailDraw trail:
                    return Mesh(current, trail.Packets, trail.CursorAdvance);
                case NativeBattleRenzokuken160MeshDraw mesh:
                    return Mesh(current, mesh.Packets, null);
                default:
                    throw new NotSupportedException("Native Renzokuken variant operation requires its original native owner.");
            }
        }

        NativeBattleRenzokuken160Frame SetCamera(NativeBattleRenzokuken160Frame current, short value)
        {
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native special Renzokuken requires its camera owner.");
            NativeBattleCameraViewTranslationScratch scratch = camera.ViewTranslationScratch ?? throw new NotSupportedException("Native special Renzokuken requires its camera scratch owner.");
            NativeCamera = camera with { ViewTranslationScratch = scratch with { Y = value } };
            return current;
        }

        NativeBattleRenzokuken160Frame Mesh(NativeBattleRenzokuken160Frame current, NativeBattleGfGeometryBatch batch, int? advance)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Renzokuken variant requires its initialized ordering table.");
            int start = current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Renzokuken variant lost its current packet cursor.");
            if (profile.Effect != 161 && NativeBattleRenzokukenVisualLifetimes.Owns(current.Effect.Binding.Effect))
            {
                current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, start, batch) } };
            }
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, start, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = advance is int bytes ? checked(start + bytes) : written.Cursor } };
        }
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleModuleCallback Callback) AdvanceNativeRenzokukenSpecialShared(NativeBattleGraphicsState graphics, NativeBattleModuleState modules, int slot, NativeBattleModuleInputs inputs)
    {
        ushort effect = modules.Slots[slot].Callback switch
        {
            0x587CA0 or 0x587F30 => 334,
            0x585C70 or 0x585F00 => 335,
            0x5835D0 or 0x583860 => 336,
            0x580F50 or 0x5811E0 => 337,
            0x57EC70 or 0x57EF00 => 339,
            0x57C390 or 0x57C620 => 340,
            0x57A010 or 0x57A2A0 => 341,
            0x577D90 or 0x578020 => 342,
            0x575BA0 or 0x575E30 => 343,
            _ => throw new NotSupportedException("Native special Renzokuken shared callback has no module owner.")
        };
        uint emitter = effect switch
        {
            334 => 0x587CA0u,
            335 => 0x585C70u,
            336 => 0x5835D0u,
            337 => 0x580F50u,
            339 => 0x57EC70u,
            340 => 0x57C390u,
            341 => 0x57A010u,
            342 => 0x577D90u,
            343 => 0x575BA0u,
            _ => throw new InvalidDataException()
        };
        modules = NativeBattleRenzokukenVisualLifetimes.EnsureShared(modules, slot, effect);
        inputs = inputs with { ReadModelAnchor = index => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native571400 requires its actual angleWORD36.")).Angle36 } };
        if (modules.Slots[slot].Callback == emitter)
        {
            return (graphics, NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(modules, slot, inputs, emitter + 0x290, skipUnavailable: effect == 343));
        }
        if (modules.Slots[slot].Callback != emitter + 0x290)
        {
            throw new NotSupportedException("Native Renzokuken shared callback does not belong to its retained module profile.");
        }
        NativeBattleRenzokuken160Resources resources = NativeEffectMemory?.RenzokukenSpecials?.GetValueOrDefault(effect)?.Resources ?? throw new NotSupportedException("Native Renzokuken shared callback requires immutable resources retained by its actual entry.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native5FF770 requires the current camera projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native5FF770 requires the current camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native5FF770 requires its actual color constants.");
        NativeBattleModuleCallback result = NativeBattleRenzokuken160Controller.AdvanceShared5FF770(modules, slot, inputs, resources, resources.StreakReferencePoint, view, projection, colors, batch =>
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native5FF770 requires retained packet memory.");
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native5FF770 requires its initialized ordering table.");
            if (graphics.OriginalEffect is { } selected && modules.Slots[slot].PilotIdentity?.Effect == selected.Binding.Effect && NativeBattleRenzokukenVisualLifetimes.Owns(selected.Binding.Effect))
            {
                NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new InvalidDataException("Shared presentation requires its actual COMMON process.");
                graphics = graphics with { OriginalEffect = selected with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(selected.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, batch) } };
            }
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(memory.PrimitiveProcess ?? throw new NotSupportedException("Native5FF770 requires its current common packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeEffectMemory = memory with { PrimitiveProcess = written.Process };
        });
        return (graphics, result);
    }
}
