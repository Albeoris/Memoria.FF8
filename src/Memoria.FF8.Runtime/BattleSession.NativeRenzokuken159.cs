using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRenzokuken159(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native159 requires its selected effect.");
        NativeBattleRenzokuken159Resources resources = effect.Binding.Renzokuken159Resources ?? throw new NotSupportedException("Native159 requires its original module resources.");
        NativeBattleRenzokuken159Frame frame = NativeBattleRenzokuken159Controller.Enter600BC0(NativeEffectMemory ?? throw new NotSupportedException("Native159 requires retained effect memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native159 requires retained CRT random state."));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native5099A0 requires its actual camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRenzokuken159(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native159 requires its selected effect.");
        NativeBattleRenzokuken159Resources resources = effect.Binding.Renzokuken159Resources ?? throw new NotSupportedException("Native159 requires its original module resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native159 requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native159 requires its current camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native159 requires its actual color interpolation constants.");
        NativeBattleRenzokuken159Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native159 requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native159 requires retained CRT random state."));
        frame = NativeBattleRenzokuken159Controller.Advance602C40(frame, Child, Particles);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleRenzokuken159Inputs Inputs(NativeBattleRenzokuken159Frame current) => new(graphics.Flags, current.Memory.Renzokuken159!.AbSegments.IsEmpty ? graphics.TriggerWidget?.Block.Segments ?? [] : current.Memory.Renzokuken159.AbSegments, actor => graphics.Models[actor], Execute)
        {
            ReadAnchor = actor => ReadNativeModelAnchor(graphics, actor),
            ReadBoneCount = actor => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native159 requires its actual body skeleton.")).BoneCount,
            ReadBone = (actor, bone, fraction) => ReadNativeGraphicsBone(graphics, actor, checked((byte)bone), fraction),
            ReadWeaponTip = actor => ReadNativeRenzokuken159WeaponTip(graphics, actor)
        };

        NativeBattleRenzokuken159ChildStep Child(NativeBattleRenzokuken159Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            return pool.Slots[slot].Callback switch
            {
                0x600CC0 => NativeBattleRenzokuken159Controller.Advance600CC0(current, pool, slot, Inputs(current), resources with { DefaultModelColor = NativeStage?.ModelColors?.Color40 ?? throw new NotSupportedException("Native Renzokuken requires its current stage transition color.") }),
                0x601980 => NativeBattleRenzokuken159Controller.Advance601980(current, pool, slot, Inputs(current), view, projection, current.Memory.ScriptedGraphics, colors),
                0x601EC0 => NativeBattleRenzokuken159Controller.Advance601EC0(current, pool, slot, Inputs(current)),
                0x601FF0 => NativeBattleRenzokuken159Controller.Advance601FF0(current, pool, slot, Inputs(current), resources),
                0x602390 => NativeBattleRenzokuken159Controller.Advance602390(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x602670 => NativeBattleRenzokuken159Controller.Advance602670(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x6027C0 => NativeBattleRenzokuken159Controller.Advance6027C0(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x602910 => NativeBattleRenzokuken159Controller.Advance602910(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x602A50 => NativeBattleRenzokuken159Controller.Advance602A50(current, pool, slot, Inputs(current), resources, view, projection, colors),
                _ => throw new NotSupportedException($"Native159 callback{pool.Slots[slot].Callback:X} requires its actual native contract.")
            };
        }

        NativeBattleRenzokuken159Frame Particles(NativeBattleRenzokuken159Frame current)
        {
            return NativeBattleRenzokuken159Controller.AdvanceParticles602C90(current, resources, graphics.Flags, (actual, sprite) =>
            {
                NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokuken159Drawing.DrawSprite571C80(sprite, projection, view, NativeCamera!.Roll);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native159 requires its initialized ordering table.");
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(actual.Memory, actual.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native159 lost its current packet cursor."), primitive.Pending, drawn.Packets, NativeBattleEffectGouraudWrites.FullDword);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return new(actual with { Memory = written.Memory, Effect = actual.Effect with { DrawArenaOffset = written.Cursor } }, drawn.AnimationEnded);
            });
        }

        NativeBattleRenzokuken159Frame Execute(NativeBattleRenzokuken159Frame current, NativeBattleRenzokuken159Operation operation)
        {
            NativeEffectMemory = current.Memory;
            byte actor = current.Memory.Renzokuken159!.Actor;
            switch (operation)
            {
                case NativeBattleRenzokuken159ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = flags.Flags }) };
                    return current;
                case NativeBattleRenzokuken159ActorColor color:
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Color40 = color.Color }) };
                    return current;
                case NativeBattleRenzokuken159ActorResource binding:
                    NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[actor], NativeGraphicsPoseResource(graphics, actor));
                    model = model with { ResourceMagicId = binding.Resource.MagicId, ResourceRenzokukenTargetBank = null, Flags2 = (ushort)((model.Flags2 ?? throw new NotSupportedException("Native5022C0 requires the actual model WORD2.")) | 8) };
                    graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(actor, model) }, actor, binding.Sequence);
                    return current;
                case NativeBattleRenzokuken159TargetResource targetBinding:
                    byte target = checked((byte)current.Memory.Renzokuken159!.FirstTarget);
                    NativeBattleGraphicsModel targetModel = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[target], NativeGraphicsPoseResource(graphics, target));
                    targetModel = targetModel with { ResourceMagicId = targetBinding.Resource.MagicId, ResourceRenzokukenTargetBank = 0, Flags2 = (ushort)((targetModel.Flags2 ?? throw new NotSupportedException("Native5022C0 requires target model WORD2.")) | 8) };
                    graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(target, targetModel) }, target, 1);
                    return current;
                case NativeBattleRenzokuken159Stage stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained stage rows.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleRenzokuken159Sound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, unchecked((uint)sound.First), null) { Pan = checked((byte)sound.Second), RawPan = unchecked((uint)sound.Second) });
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleRenzokuken159SharedTask shared:
                    (NativeBattleModuleState modules, int slot) = graphics.ModuleEffects.AppendCallback(shared.Callback);
                    byte[] bytes = modules.Slots[slot].Payload.ToArray();
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes, shared.Counter);
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), shared.Actor);
                    modules = modules with { Slots = modules.Slots.SetItem(slot, modules.Slots[slot] with { Payload = bytes.ToImmutableArray() }) };
                    graphics = graphics with { ModuleEffects = modules };
                    return current;
                case NativeBattleRenzokuken159TrailDraw trail:
                    return Mesh(current, trail.Packets, trail.CursorAdvance);
                case NativeBattleRenzokuken159MeshDraw mesh:
                    return Mesh(current, mesh.Packets, null);
                default:
                    throw new NotSupportedException("Native159 operation requires its original native owner.");
            }
        }

        NativeBattleRenzokuken159Frame Mesh(NativeBattleRenzokuken159Frame current, NativeBattleGfGeometryBatch batch, int? advance)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native159 requires its initialized ordering table.");
            int start = current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native159 lost its current packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, start, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = advance is int bytes ? checked(start + bytes) : written.Cursor } };
        }
    }

    private NativeBattleRenzokuken159WeaponTip ReadNativeRenzokuken159WeaponTip(NativeBattleGraphicsState graphics, byte actor)
    {
        NativeBattleRenzokuken160WeaponTip tip = ReadNativeRenzokuken160WeaponTip(graphics, actor);
        return new(tip.Position, tip.DirectionX, tip.DirectionY, tip.DirectionZ, tip.PositionPadding, tip.EndpointPadding);
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleModuleCallback Callback) AdvanceNativeRenzokuken159Shared(NativeBattleGraphicsState graphics, NativeBattleModuleState modules, int slot, NativeBattleModuleInputs inputs)
    {
        inputs = inputs with { ReadModelAnchor = index => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native571400 requires its actual angleWORD36.")).Angle36 } };
        if (modules.Slots[slot].Callback == 0x6013C0)
        {
            return (graphics, NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(modules, slot, inputs, 0x601650));
        }
        NativeBattleRenzokuken159Resources resources = NativeEffectMemory?.Renzokuken159?.RetainedResources ?? throw new NotSupportedException("Native601650 requires immutable159 resources retained by its actual entry.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native601650 requires the current camera projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native601650 requires the current camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native601650 requires its actual color constants.");
        NativeBattleModuleCallback result = NativeBattleRenzokuken160Controller.AdvanceShared5FF770(modules, slot, inputs, resources.Shared, resources.StreakReferencePoint, view, projection, colors, batch =>
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native601650 requires retained packet memory.");
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native601650 requires its initialized ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(memory.PrimitiveProcess ?? throw new NotSupportedException("Native601650 requires its current common packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeEffectMemory = memory with { PrimitiveProcess = written.Process };
        });
        return (graphics, result);
    }
}
