using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRenzokukenSpecial161(NativeBattleGraphicsState graphics, NativeBattleRenzokuken160Resources resources)
    {
        return EnterNativeRenzokukenSpecial161Core(graphics, resources, NativeBattleRenzokukenSpecialProfiles.Renzokuken161);
    }

    private NativeBattleGraphicsState EnterNativeRenzokukenSpecial161Core(NativeBattleGraphicsState graphics, NativeBattleRenzokuken160Resources resources, NativeBattleRenzokukenSpecialProfile profile)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Renzokuken variant requires its selected effect.");
        NativeBattleRenzokuken160Frame frame = NativeBattleRenzokukenSpecialController.Enter(profile, NativeEffectMemory ?? throw new NotSupportedException("Native Renzokuken variant requires retained effect memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Renzokuken variant requires retained CRT random state."));
        frame = NativeBattleRenzokukenSpecialController.Store(frame, NativeBattleRenzokukenSpecialController.ReadState(frame) with { ExtraSprites = effect.Binding.RenzokukenSpecialExtraSprites ?? throw new NotSupportedException("Native161 requires its original extra particle sprite resources.") });
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native5099A0 requires its actual camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRenzokukenSpecial161(NativeBattleGraphicsState graphics)
    {
        return AdvanceNativeRenzokukenSpecial161Core(graphics, NativeBattleRenzokukenSpecialProfiles.Renzokuken161);
    }

    private NativeBattleGraphicsState AdvanceNativeRenzokukenSpecial161Core(NativeBattleGraphicsState graphics, NativeBattleRenzokukenSpecialProfile profile)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Renzokuken variant requires its selected effect.");
        NativeBattleRenzokuken160Resources resources = NativeEffectMemory?.RenzokukenSpecials?.GetValueOrDefault(profile.Effect)?.Resources ?? throw new NotSupportedException("Native Renzokuken variant requires its immutable resources retained at actual entry.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Renzokuken variant requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Renzokuken variant requires its current camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Renzokuken variant requires its actual color interpolation constants.");
        NativeBattleRenzokuken160Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Renzokuken variant requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Renzokuken variant requires retained CRT random state."));
        frame = NativeBattleRenzokukenSpecialController.AdvanceRoot(frame, profile, Child, Particles);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleRenzokuken160Inputs Inputs(NativeBattleRenzokuken160Frame current) => new(graphics.Flags, NativeBattleRenzokukenSpecialController.ReadState(current).Core.AbSegments.IsEmpty ? graphics.TriggerWidget?.Block.Segments ?? [] : NativeBattleRenzokukenSpecialController.ReadState(current).Core.AbSegments, actor => graphics.Models[actor], Execute)
        {
            ReadBoneCount = actor => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Renzokuken variant requires its actual body skeleton.")).BoneCount,
            ReadBone = (actor, bone, fraction) => ReadNativeGraphicsBone(graphics, actor, checked((byte)bone), fraction),
            ReadWeaponTip = actor => ReadNativeRenzokuken160WeaponTip(graphics, actor)
        };

        NativeBattleRenzokuken160ChildStep Child(NativeBattleRenzokuken160Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            return pool.Slots[slot].Callback switch
            {
                0x5FD230 => NativeBattleRenzokukenSpecialController.Advance5FD230(current, pool, slot, Inputs(current), resources with { DefaultModelColor = NativeStage?.ModelColors?.Color40 ?? throw new NotSupportedException("Native161 requires its current stage transition color.") }, target => ReadNativeModelAnchor(graphics, target) with { W = (graphics.Models[target].Transform ?? throw new NotSupportedException("Native161 requires target angleWORD36.")).Angle36 }),
                0x5FDDD0 => NativeBattleRenzokuken160Controller.Advance5FFAA0(current, pool, slot, Inputs(current), view, projection, current.Memory.ScriptedGraphics, colors),
                0x5FE310 => NativeBattleRenzokuken160Controller.Advance5FFFE0(current, pool, slot, Inputs(current)),
                0x5FE440 => NativeBattleRenzokukenSpecialController.Advance5FE440(current, pool, slot, Inputs(current), resources),
                0x5FE790 => NativeBattleRenzokuken160Controller.Advance6004A0(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x5FEA70 => NativeBattleRenzokukenSpecialController.Advance5FEA70(current, pool, slot, Inputs(current), resources, view, projection, colors),
                _ => throw new NotSupportedException($"Native Renzokuken variant callback{pool.Slots[slot].Callback:X} requires its actual native contract.")
            };
        }

        NativeBattleRenzokuken160Frame Particles(NativeBattleRenzokuken160Frame current)
        {
            return NativeBattleRenzokukenSpecialController.AdvanceParticles5FEC60(current, graphics.Flags, (actual, sprite) =>
            {
                NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokuken160Drawing.DrawSprite571C80(sprite, projection, view, NativeCamera!.Roll);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Renzokuken variant requires its initialized ordering table.");
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
                    graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(binding.Target, targetModel) }, binding.Target, binding.Sequence);
                    return current;
                case NativeBattleRenzokukenSpecialCameraRotation rotation:
                    NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native161 requires its camera owner.");
                    NativeBattleCameraViewTranslationScratch scratch = camera.ViewTranslationScratch ?? throw new NotSupportedException("Native161 requires its camera view translation scratch.");
                    NativeCamera = camera with { ViewTranslationScratch = scratch with { Y = unchecked((short)((scratch.Y ?? throw new NotSupportedException("Native161 requires camera WORD1D97712.")) + rotation.Delta)) } };
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

        NativeBattleRenzokuken160Frame Mesh(NativeBattleRenzokuken160Frame current, NativeBattleGfGeometryBatch batch, int? advance)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Renzokuken variant requires its initialized ordering table.");
            int start = current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Renzokuken variant lost its current packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, start, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = advance is int bytes ? checked(start + bytes) : written.Cursor } };
        }
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleModuleCallback Callback) AdvanceNativeRenzokukenSpecial161Shared(NativeBattleGraphicsState graphics, NativeBattleModuleState modules, int slot, NativeBattleModuleInputs inputs)
    {
        inputs = inputs with { ReadModelAnchor = index => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native571400 requires its actual angleWORD36.")).Angle36 } };
        if (modules.Slots[slot].Callback == 0x5FD810)
        {
            return (graphics, NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(modules, slot, inputs, 0x5FDAA0));
        }
        if (modules.Slots[slot].Callback != 0x5FDAA0)
        {
            throw new NotSupportedException("Native Renzokuken shared callback does not belong to its retained module profile.");
        }
        NativeBattleRenzokuken160Resources resources = NativeEffectMemory?.RenzokukenSpecials?.GetValueOrDefault((ushort)161)?.Resources ?? throw new NotSupportedException("Native Renzokuken shared callback requires immutable resources retained by its actual entry.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native5FF770 requires the current camera projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native5FF770 requires the current camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native5FF770 requires its actual color constants.");
        NativeBattleModuleCallback result = NativeBattleRenzokuken160Controller.AdvanceShared5FF770(modules, slot, inputs, resources, resources.StreakReferencePoint, view, projection, colors, batch =>
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native5FF770 requires retained packet memory.");
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native5FF770 requires its initialized ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(memory.PrimitiveProcess ?? throw new NotSupportedException("Native5FF770 requires its current common packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeEffectMemory = memory with { PrimitiveProcess = written.Process };
        });
        return (graphics, result);
    }
}
