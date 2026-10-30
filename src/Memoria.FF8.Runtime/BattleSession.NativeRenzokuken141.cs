using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRenzokuken141(NativeBattleGraphicsState graphics, NativeBattleRenzokuken160Resources resources)
    {
        return EnterNativeRenzokukenVariant(graphics, resources, NativeBattleRenzokukenVariantProfile.Renzokuken141);
    }

    private NativeBattleGraphicsState EnterNativeRenzokukenVariant(NativeBattleGraphicsState graphics, NativeBattleRenzokuken160Resources resources, NativeBattleRenzokukenVariantProfile profile)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Renzokuken variant requires its selected effect.");
        NativeBattleRenzokuken160Frame frame = NativeBattleRenzokukenVariantController.Enter(profile, NativeEffectMemory ?? throw new NotSupportedException("Native Renzokuken variant requires retained effect memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Renzokuken variant requires retained CRT random state."));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native5099A0 requires its actual camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRenzokuken141(NativeBattleGraphicsState graphics)
    {
        return AdvanceNativeRenzokukenVariant(graphics, NativeBattleRenzokukenVariantProfile.Renzokuken141);
    }

    private NativeBattleGraphicsState AdvanceNativeRenzokukenVariant(NativeBattleGraphicsState graphics, NativeBattleRenzokukenVariantProfile profile)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Renzokuken variant requires its selected effect.");
        NativeBattleRenzokuken160Resources resources = NativeEffectMemory?.RenzokukenVariants?.GetValueOrDefault(profile.Effect)?.RetainedResources ?? throw new NotSupportedException("Native Renzokuken variant requires its immutable resources retained at actual entry.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Renzokuken variant requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Renzokuken variant requires its current camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Renzokuken variant requires its actual color interpolation constants.");
        NativeBattleRenzokuken160Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Renzokuken variant requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Renzokuken variant requires retained CRT random state."));
        frame = NativeBattleRenzokukenVariantController.AdvanceRoot(frame, profile, Child, Particles);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleRenzokuken160Inputs Inputs(NativeBattleRenzokuken160Frame current) => new(graphics.Flags, NativeBattleRenzokukenVariantController.ReadState(current).Core.AbSegments.IsEmpty ? graphics.TriggerWidget?.Block.Segments ?? [] : NativeBattleRenzokukenVariantController.ReadState(current).Core.AbSegments, actor => graphics.Models[actor], Execute)
        {
            ReadBoneCount = actor => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Renzokuken variant requires its actual body skeleton.")).BoneCount,
            ReadBone = (actor, bone, fraction) => ReadNativeGraphicsBone(graphics, actor, checked((byte)bone), fraction),
            ReadWeaponTip = actor => ReadNativeRenzokuken160WeaponTip(graphics, actor),
            CapturePresentation = NativeBattleRenzokukenVisualLifetimes.Owns(current.Effect.Binding.Effect)
        };

        NativeBattleRenzokuken160ChildStep Child(NativeBattleRenzokuken160Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            return pool.Slots[slot].Callback switch
            {
                uint callback when callback == profile.ControllerCallback => NativeBattleRenzokukenVariantController.AdvanceController(current, pool, slot, Inputs(current), resources with { DefaultModelColor = NativeStage?.ModelColors?.Color40 ?? throw new NotSupportedException("Native Renzokuken requires its current stage transition color.") }, profile),
                uint callback when callback == profile.TrailCallback => NativeBattleRenzokuken160Controller.Advance5FFAA0(current, pool, slot, Inputs(current), view, projection, current.Memory.ScriptedGraphics, colors),
                uint callback when callback == profile.SpiralCallback => NativeBattleRenzokuken160Controller.Advance5FFFE0(current, pool, slot, Inputs(current)),
                uint callback when callback == profile.ContactCallback => NativeBattleRenzokuken160Controller.Advance600110(current, pool, slot, Inputs(current), resources, profile.ImpactCallback, profile.HasContactOffsets ? contact => ContactOrigin(current, contact) : null),
                uint callback when callback == profile.ImpactCallback => NativeBattleRenzokuken160Controller.Advance6004A0(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x61F590 when profile.Effect == 141 => NativeBattleRenzokukenVariantController.Advance61F590(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x5A8340 when profile.Effect == 259 => NativeBattleRenzokukenVariantController.Advance5A8340(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x58D130 when profile.Effect == 332 => NativeBattleRenzokukenVariantController.Advance58D130(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x58D2D0 when profile.Effect == 332 => NativeBattleRenzokukenVariantController.Advance58D2D0(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x58B3A0 when profile.Effect == 333 => NativeBattleRenzokukenVariantController.Advance58B3A0(current, pool, slot, Inputs(current), resources, view, projection, colors),
                0x58B520 when profile.Effect == 333 => NativeBattleRenzokukenVariantController.Advance58B520(current, pool, slot, Inputs(current), resources, view, projection, colors),
                _ => throw new NotSupportedException($"Native Renzokuken variant callback{pool.Slots[slot].Callback:X} requires its actual native contract.")
            };
        }

        NativeBattleRenzokukenContactOrigin ContactOrigin(NativeBattleRenzokuken160Frame current, int contact)
        {
            NativeBattleRenzokuken160State state = NativeBattleRenzokukenVariantController.ReadState(current).Core;
            if (resources.ContactOffsets is not { } offsets || contact < 0 || contact >= offsets.Length)
            {
                throw new NotSupportedException("Native Renzokuken contact requires its original offset-vector table.");
            }
            NativeBattleActionMatrix body = graphics.Models[state.Actor].GeometryWorldMatrix ?? throw new NotSupportedException("Native Renzokuken contact requires its actual actor matrix64.");
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(body, offsets[contact]);
            NativeBattleVector origin = new(unchecked((short)(state.Origin.X + offset.X)), unchecked((short)(state.Origin.Y + offset.Y)), unchecked((short)(state.Origin.Z + offset.Z)));
            return new(origin, null);
        }

        NativeBattleRenzokuken160Frame Particles(NativeBattleRenzokuken160Frame current)
        {
            return NativeBattleRenzokuken160Controller.AdvanceParticles600980(current, resources, graphics.Flags, (actual, sprite) =>
            {
                NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokuken160Drawing.DrawSprite571C80(sprite, projection, view, NativeCamera!.Roll);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Renzokuken variant requires its initialized ordering table.");
                if (NativeBattleRenzokukenVisualLifetimes.Owns(actual.Effect.Binding.Effect))
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
            byte actor = NativeBattleRenzokukenVariantController.ReadState(current).Core.Actor;
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
                    modules = NativeBattleRenzokukenVisualLifetimes.Shared(modules, slot, NativeBattleRenzokukenVisualLifetimes.Task(current.Effect, current.Effect.Pools[0].Slots[0], 0));
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
            if (NativeBattleRenzokukenVisualLifetimes.Owns(current.Effect.Binding.Effect))
            {
                current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, start, batch) } };
            }
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, start, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = advance is int bytes ? checked(start + bytes) : written.Cursor } };
        }
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleModuleCallback Callback) AdvanceNativeRenzokuken141Shared(NativeBattleGraphicsState graphics, NativeBattleModuleState modules, int slot, NativeBattleModuleInputs inputs)
    {
        return AdvanceNativeRenzokukenVariantShared(graphics, modules, slot, inputs, NativeBattleRenzokukenVariantProfile.Renzokuken141);
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleModuleCallback Callback) AdvanceNativeRenzokukenVariantShared(NativeBattleGraphicsState graphics, NativeBattleModuleState modules, int slot, NativeBattleModuleInputs inputs, NativeBattleRenzokukenVariantProfile profile)
    {
        modules = NativeBattleRenzokukenVisualLifetimes.EnsureShared(modules, slot, profile.Effect);
        inputs = inputs with { ReadModelAnchor = index => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native571400 requires its actual angleWORD36.")).Angle36 } };
        if (modules.Slots[slot].Callback == profile.SharedEmitterCallback)
        {
            return (graphics, NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(modules, slot, inputs, profile.SharedStreakCallback));
        }
        if (modules.Slots[slot].Callback != profile.SharedStreakCallback)
        {
            throw new NotSupportedException("Native Renzokuken shared callback does not belong to its retained module profile.");
        }
        NativeBattleRenzokuken160Resources resources = NativeEffectMemory?.RenzokukenVariants?.GetValueOrDefault(profile.Effect)?.RetainedResources ?? throw new NotSupportedException("Native Renzokuken shared callback requires immutable resources retained by its actual entry.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native5FF770 requires the current camera projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native5FF770 requires the current camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native5FF770 requires its actual color constants.");
        NativeBattleModuleCallback result = NativeBattleRenzokuken160Controller.AdvanceShared5FF770(modules, slot, inputs, resources, resources.StreakReferencePoint, view, projection, colors, batch =>
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native5FF770 requires retained packet memory.");
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native5FF770 requires its initialized ordering table.");
            if (graphics.OriginalEffect is { } selected && modules.Slots[slot].PilotIdentity?.Effect == selected.Binding.Effect && NativeBattleRenzokukenVisualLifetimes.Owns(selected.Binding.Effect))
            {
                NativeBattlePrimitiveProcess process = memory.PrimitiveProcess!;
                graphics = graphics with { OriginalEffect = selected with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(selected.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, batch) } };
            }
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(memory.PrimitiveProcess ?? throw new NotSupportedException("Native5FF770 requires its current common packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeEffectMemory = memory with { PrimitiveProcess = written.Process };
        });
        return (graphics, result);
    }
}
