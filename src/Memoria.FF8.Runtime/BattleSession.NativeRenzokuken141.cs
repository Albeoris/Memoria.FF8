using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRenzokuken141(NativeBattleGraphicsState graphics, NativeBattleRenzokuken160Resources resources)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native141 requires its selected effect.");
        NativeBattleRenzokuken160Frame frame = NativeBattleRenzokukenVariantController.Enter61DE70(NativeEffectMemory ?? throw new NotSupportedException("Native141 requires retained effect memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native141 requires retained CRT random state."));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRenzokuken141(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native141 requires its selected effect.");
        NativeBattleRenzokuken160Resources resources = NativeEffectMemory?.RenzokukenVariants?.GetValueOrDefault((ushort)141)?.RetainedResources ?? throw new NotSupportedException("Native141 requires its immutable resources retained at actual entry.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native141 requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native141 requires its current camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native141 requires its actual color interpolation constants.");
        NativeBattleRenzokuken160Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native141 requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native141 requires retained CRT random state."));
        frame = NativeBattleRenzokukenVariantController.Advance61F710(frame, Child, Particles);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleRenzokuken160Inputs Inputs(NativeBattleRenzokuken160Frame current) => new(graphics.Flags, NativeBattleRenzokukenVariantController.ReadState(current).Core.AbSegments.IsEmpty ? graphics.TriggerWidget?.Block.Segments ?? [] : NativeBattleRenzokukenVariantController.ReadState(current).Core.AbSegments, actor => graphics.Models[actor], Execute)
        {
            ReadBoneCount = actor => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native141 requires its actual body skeleton.")).BoneCount,
            ReadBone = (actor, bone, fraction) => ReadNativeGraphicsBone(graphics, actor, checked((byte)bone), fraction),
            ReadWeaponTip = actor => ReadNativeRenzokuken160WeaponTip(graphics, actor)
        };

        NativeBattleRenzokuken160ChildStep Child(NativeBattleRenzokuken160Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            return pool.Slots[slot].Callback switch
            {
                0x61DF70 => NativeBattleRenzokukenVariantController.Advance61DF70(current, pool, slot, Inputs(current), resources),
                0x61E8B0 => NativeBattleRenzokuken160Controller.Advance5FFAA0(current, pool, slot, Inputs(current), view, projection, current.Memory.ScriptedGraphics, colors),
                0x61EDF0 => NativeBattleRenzokuken160Controller.Advance5FFFE0(current, pool, slot, Inputs(current)),
                0x61EF20 => NativeBattleRenzokuken160Controller.Advance600110(current, pool, slot, Inputs(current), resources, 0x61F2B0),
                0x61F2B0 => NativeBattleRenzokuken160Controller.Advance6004A0(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x61F590 => NativeBattleRenzokukenVariantController.Advance61F590(current, pool, slot, Inputs(current), resources, view, projection, colors),
                _ => throw new NotSupportedException($"Native141 callback{pool.Slots[slot].Callback:X} requires its actual native contract.")
            };
        }

        NativeBattleRenzokuken160Frame Particles(NativeBattleRenzokuken160Frame current)
        {
            return NativeBattleRenzokuken160Controller.AdvanceParticles600980(current, resources, graphics.Flags, (actual, sprite) =>
            {
                NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokuken160Drawing.DrawSprite571C80(sprite, projection, view, NativeCamera!.Roll);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native141 requires its initialized ordering table.");
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(actual.Memory, actual.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native141 lost its current packet cursor."), primitive.Pending, drawn.Packets, NativeBattleEffectGouraudWrites.FullDword);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return new(actual with { Memory = written.Memory, Effect = actual.Effect with { DrawArenaOffset = written.Cursor } }, drawn.AnimationEnded);
            });
        }

        NativeBattleRenzokuken160Frame Execute(NativeBattleRenzokuken160Frame current, NativeBattleRenzokuken160Operation operation)
        {
            NativeEffectMemory = current.Memory;
            byte actor = NativeBattleRenzokukenVariantController.ReadState(current).Core.Actor;
            switch (operation)
            {
                case NativeBattleRenzokuken160ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = flags.Flags }) };
                    return current;
                case NativeBattleRenzokuken160ActorColor color:
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Color44 = color.Color }) };
                    return current;
                case NativeBattleRenzokuken160ActorResource binding:
                    NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[actor], NativeGraphicsPoseResource(graphics, actor));
                    model = model with { ResourceMagicId = binding.Resource.MagicId, Flags2 = (ushort)((model.Flags2 ?? throw new NotSupportedException("Native5022C0 requires the actual model WORD2.")) | 8) };
                    graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(actor, model) }, actor, binding.Sequence);
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
                    throw new NotSupportedException("Native141 operation requires its original native owner.");
            }
        }

        NativeBattleRenzokuken160Frame Mesh(NativeBattleRenzokuken160Frame current, NativeBattleGfGeometryBatch batch, int? advance)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native141 requires its initialized ordering table.");
            int start = current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native141 lost its current packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, start, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = advance is int bytes ? checked(start + bytes) : written.Cursor } };
        }
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleModuleCallback Callback) AdvanceNativeRenzokuken141Shared(NativeBattleGraphicsState graphics, NativeBattleModuleState modules, int slot, NativeBattleModuleInputs inputs)
    {
        inputs = inputs with { ReadModelAnchor = index => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native571400 requires its actual angleWORD36.")).Angle36 } };
        if (modules.Slots[slot].Callback == 0x61E2F0)
        {
            return (graphics, NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(modules, slot, inputs, 0x61E580));
        }
        NativeBattleRenzokuken160Resources resources = NativeEffectMemory?.RenzokukenVariants?.GetValueOrDefault((ushort)141)?.RetainedResources ?? throw new NotSupportedException("Native5FF770 requires immutable141 resources retained by its actual entry.");
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
