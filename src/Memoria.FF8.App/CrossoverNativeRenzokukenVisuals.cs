using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeRenzokukenVisualStep(CrossoverNativeRenzokukenEntryState State, NativeBattleGraphicsModel Model, ImmutableArray<NativeBattleRenzokuken160Sound> Sounds, ImmutableArray<NativeBattleRenzokuken160SharedTask> SharedTasks);

internal static partial class CrossoverNativeRenzokukenEntry
{
    public static CrossoverNativeRenzokukenVisualStep AdvanceVisuals(CrossoverNativeRenzokukenEntryState source, NativeBattleGraphicsModel caster, NativeBattleGraphicsResource resource, NativeBattleTriggerWidget? widget, NativeBattleCameraProjection projection, short roll, NativeBattleSummonColors colors, NativeBattleLogicalOrdering? priorOrdering = null)
    {
        if (source.Callbacks < 2 || source.Frame.Effect.ReturnedDescriptor == 0 || caster is not { Index: 0, ModelId4: 0 } || (caster.Flags0 & 2) == 0)
        {
            throw new InvalidOperationException("Renzokuken visual callbacks require their active source replacement and descriptor.");
        }
        ushort variant = source.Resources.ActorResource.MagicId;
        NativeBattleRenzokukenVariantProfile profile = NativeBattleRenzokukenVariantProfile.Renzokuken141;
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("Renzokuken has no original source body pose.");
        NativeBattleActionMatrix view = projection.View ?? throw new InvalidDataException("Renzokuken has no host projection in source coordinates.");
        NativeBattleRenzokuken160Resources resources = source.Resources with { DefaultModelColor = colors.Transition };
        NativeBattleLogicalOrdering ordering = priorOrdering ?? NativeBattleLogicalOrdering.Clear();
        short depth = source.StageDepth;
        ImmutableArray<NativeBattleRenzokuken160Sound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleRenzokuken160Sound>();
        ImmutableArray<NativeBattleRenzokuken160SharedTask>.Builder shared = ImmutableArray.CreateBuilder<NativeBattleRenzokuken160SharedTask>();
        NativeBattleRenzokuken160Frame frame = variant == 141 ? NativeBattleRenzokukenVariantController.Advance61F710(source.Frame, AdvanceChild, AdvanceParticles) : NativeBattleRenzokuken160Controller.Advance600930(source.Frame, AdvanceChild, AdvanceParticles);
        return new(source with { Frame = frame, Callbacks = source.Callbacks + 1, Ordering = ordering, StageDepth = depth }, caster, sounds.ToImmutable(), shared.ToImmutable());

        NativeBattleRenzokuken160State Core(NativeBattleRenzokuken160Frame current) => variant == 141 ? NativeBattleRenzokukenVariantController.ReadState(current).Core : current.Memory.Renzokuken160!;

        NativeBattleGraphicsModel ReadModel(byte actor) => actor == 0 ? caster : throw new InvalidDataException("Renzokuken queried an unbound graphics actor.");

        NativeBattleRenzokuken160ChildStep AdvanceChild(NativeBattleRenzokuken160Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            ImmutableArray<byte> segments = Core(current).AbSegments;
            if (segments.IsEmpty && widget is { SourceMagicId: ushort magic } && magic == variant)
            {
                segments = widget.Block.Segments;
            }
            NativeBattleRenzokuken160Inputs inputs = new(0, segments, ReadModel, Execute)
            {
                ReadBoneCount = actor => ReadModel(actor).Index == 0 ? pose.BoneCount : throw new InvalidDataException("Renzokuken queried an unbound skeleton."),
                ReadBone = (actor, bone, fraction) => NativeBattlePoseMatrices.ReadBone(ReadModel(actor), pose, checked((byte)bone), fraction),
                ReadWeaponTip = actor => NativeBattleRenzokuken160Controller.ReadWeaponTip5FFEE0(ReadModel(actor), resource)
            };
            uint callback = pool.Slots[slot].Callback;
            return variant == 141 ? callback switch
            {
                0x61DF70 => NativeBattleRenzokukenVariantController.AdvanceController(current, pool, slot, inputs, resources, profile),
                0x61E8B0 => NativeBattleRenzokuken160Controller.Advance5FFAA0(current, pool, slot, inputs, view, projection, current.Memory.ScriptedGraphics, colors),
                0x61EDF0 => NativeBattleRenzokuken160Controller.Advance5FFFE0(current, pool, slot, inputs),
                0x61EF20 => NativeBattleRenzokuken160Controller.Advance600110(current, pool, slot, inputs, resources, profile.ImpactCallback),
                0x61F2B0 => NativeBattleRenzokuken160Controller.Advance6004A0(current, pool, slot, inputs, resources, view, projection, colors),
                0x61F590 => NativeBattleRenzokukenVariantController.Advance61F590(current, pool, slot, inputs, resources, view, projection, colors),
                _ => throw new NotSupportedException($"Renzokuken141 callback {callback:X} has no source owner.")
            } : callback switch
            {
                0x5FF180 => NativeBattleRenzokuken160Controller.Advance5FF180(current, pool, slot, inputs, resources),
                0x5FFAA0 => NativeBattleRenzokuken160Controller.Advance5FFAA0(current, pool, slot, inputs, view, projection, current.Memory.ScriptedGraphics, colors),
                0x5FFFE0 => NativeBattleRenzokuken160Controller.Advance5FFFE0(current, pool, slot, inputs),
                0x600110 => NativeBattleRenzokuken160Controller.Advance600110(current, pool, slot, inputs, resources),
                0x6004A0 => NativeBattleRenzokuken160Controller.Advance6004A0(current, pool, slot, inputs, resources, view, projection, colors),
                0x600780 => NativeBattleRenzokuken160Controller.Advance600780(current, pool, slot, inputs, resources, view, projection, colors),
                _ => throw new NotSupportedException($"Renzokuken160 callback {callback:X} has no source owner.")
            };
        }

        NativeBattleRenzokuken160Frame AdvanceParticles(NativeBattleRenzokuken160Frame current) => NativeBattleRenzokuken160Controller.AdvanceParticles600980(current, resources, 0, (actual, sprite) =>
        {
            NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokuken160Drawing.DrawSprite571C80(sprite, projection, view, roll);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(actual.Memory, Cursor(actual), ordering, drawn.Packets, NativeBattleEffectGouraudWrites.FullDword);
            ordering = written.Ordering;
            return new(actual with { Memory = written.Memory, Effect = actual.Effect with { DrawArenaOffset = written.Cursor } }, drawn.AnimationEnded);
        });

        NativeBattleRenzokuken160Frame Execute(NativeBattleRenzokuken160Frame current, NativeBattleRenzokuken160Operation operation)
        {
            switch (operation)
            {
                case NativeBattleRenzokuken160ActorFlags flags:
                    caster = caster with { Flags0 = flags.Flags };
                    break;
                case NativeBattleRenzokuken160ActorColor color:
                    caster = caster with { Color40 = color.Color };
                    break;
                case NativeBattleRenzokuken160Stage stage:
                    depth = checked((short)stage.Intensity);
                    break;
                case NativeBattleRenzokuken160Sound sound:
                    sounds.Add(sound);
                    break;
                case NativeBattleRenzokuken160SharedTask task:
                    shared.Add(task);
                    break;
                case NativeBattleRenzokuken160TrailDraw trail:
                    return Append(current, trail.Packets, trail.CursorAdvance);
                case NativeBattleRenzokuken160MeshDraw mesh:
                    return Append(current, mesh.Packets, null);
                default:
                    throw new InvalidDataException("Renzokuken emitted a source resource operation outside its entry.");
            }
            return current;
        }

        NativeBattleRenzokuken160Frame Append(NativeBattleRenzokuken160Frame current, NativeBattleGfGeometryBatch batch, int? advance)
        {
            int start = Cursor(current);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, start, ordering, batch);
            ordering = written.Ordering;
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = advance is int bytes ? checked(start + bytes) : written.Cursor } };
        }

        static int Cursor(NativeBattleRenzokuken160Frame current) => current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Renzokuken lost its native packet cursor.");
    }
}
