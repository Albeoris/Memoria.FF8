using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRoughDivide163(NativeBattleGraphicsState graphics, NativeBattleRoughDivide163Resources resources)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Rough Divide requires its selected original module.");
        byte actor = effect.Parameters?.Actor78 ?? throw new InvalidDataException("Native Rough Divide requires its actual caster.");
        NativeBattleActionMatrix matrix = graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Native5F5B80 requires its actual actor matrix64.");
        NativeBattleRoughDivide163Frame frame = NativeBattleRoughDivide163Controller.Enter5F5B80(NativeEffectMemory ?? throw new NotSupportedException("Native Rough Divide requires its retained effect memory."), effect, NativeFileStaging ?? NativeBattleFileStaging.Unknown(), nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Rough Divide requires its retained CRT random owner."), matrix);
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native5099A0 requires its actual camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRoughDivide163(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Rough Divide requires its retained effect memory.");
        NativeBattleRoughDivide163Resources resources = memory.RoughDivide163?.RetainedResources ?? throw new NotSupportedException("Native Rough Divide requires resources retained by its actual entry.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Rough Divide requires its current camera registers.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Rough Divide requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Rough Divide requires its actual current view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Rough Divide requires its original color arithmetic.");
        NativeBattleRoughDivide163Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Rough Divide requires its selected module."), NativeFileStaging ?? throw new NotSupportedException("Native Rough Divide requires its entered physical resource bytes."), nativeCrtRandom ?? throw new NotSupportedException("Native Rough Divide requires its retained CRT random owner."));
        NativeBattleRoughDivide163Inputs inputs = new(graphics.Flags, Position, Bone, Execute);
        frame = NativeBattleRoughDivide163Controller.Advance5F9020(frame, Child, TextureLoad);
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleModuleAnchor Position(byte actor)
        {
            NativeBattleModelTransform transform = graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Rough Divide requires actual model position WORDs.");
            return new(transform.Position28.X, transform.Position28.Y, transform.Position28.Z, transform.Vertical34);
        }

        NativeBattleModuleAnchor Bone(byte actor, byte selector, short fraction)
        {
            NativeBattleVector bone = ReadNativeGraphicsBone(graphics, actor, selector, fraction);
            return new(bone.X, bone.Y, bone.Z, 0);
        }

        NativeBattleRoughDivide163ChildStep Child(NativeBattleRoughDivide163Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            uint callback = pool.Slots[slot].Callback;
            return callback switch
            {
                0x5F5C80 => NativeBattleRoughDivide163Controller.Advance5F5C80(current, pool, slot, inputs),
                0x5F5FE0 => NativeBattleRoughDivide163Controller.Advance5F5FE0(current, pool, slot, inputs, actor => ReadNativeRenzokuken160WeaponTip(graphics, actor), view, projection, current.Memory.ScriptedGraphics, colors),
                0x5F65E0 or 0x5F78D0 or 0x5F8540 or 0x5F8770 or 0x5F8960 or 0x5F8B80 => NativeBattleRoughDivide163Controller.AdvanceMesh(current, pool, slot, inputs, Mesh(callback), view, projection, colors),
                0x5F67A0 or 0x5F6A30 or 0x5F7A80 => NativeBattleRoughDivide163Controller.AdvanceSpriteParticles(current, pool, slot, inputs, resources.Sprites.Single(sprite => sprite.Address == 0xD9B458), view, projection, camera.Roll),
                0x5F6CA0 or 0x5F7000 or 0x5F81D0 or 0x5F8D10 => NativeBattleRoughDivide163Controller.AdvanceMeshParticles(current, pool, slot, inputs, view, projection, colors),
                0x5F7390 => NativeBattleRoughDivide163Controller.Advance5F7390(current, pool, slot, inputs, view, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Rough Divide requires its actual GTE registers."), colors),
                0x5F7E00 => NativeBattleRoughDivide163Controller.Advance5F7E00(current, pool, slot, inputs, view, projection, colors, camera.Roll),
                _ => throw new NotSupportedException($"Native Rough Divide callback0x{callback:X} has no original implementation.")
            };
        }

        NativeBattleStaticMeshResource Mesh(uint callback)
        {
            uint address = callback switch
            {
                0x5F65E0 => 0xD9E2D4,
                0x5F78D0 => 0xD9EC8C,
                0x5F8540 => 0xD9B5D0,
                0x5F8770 => 0xD9D594,
                0x5F8960 => 0xD9DD34,
                0x5F8B80 => 0xD9CB38,
                _ => throw new InvalidOperationException()
            };
            return resources.Meshes.Single(mesh => mesh.Address == address);
        }

        NativeBattleRoughDivide163Frame Execute(NativeBattleRoughDivide163Frame current, NativeBattleRoughDivide163Operation operation)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            switch (operation)
            {
                case NativeBattleRoughDivide163ComposeActor:
                    return NativeBattleRoughDivide163Controller.ComposeCapturedActor(current, view);
                case NativeBattleRoughDivide163BindActor:
                    byte actor = current.Parameters.Actor78;
                    NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[actor], NativeGraphicsPoseResource(graphics, actor));
                    NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(current.Staging, NativeModelCpuResources ?? throw new NotSupportedException("Native Rough Divide requires actual CPU resource rows."), model, 0x23CD0B8, 0xD99DC8);
                    NativeFileStaging = binding.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, binding.Model) };
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleRoughDivide163Sequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Actor, sequence.Sequence);
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleRoughDivide163SetPosition moved:
                    NativeBattleGraphicsModel positioned = graphics.Models[moved.Actor];
                    NativeBattleModelTransform transform = positioned.Transform ?? throw new NotSupportedException("Native Rough Divide requires its actual actor transform.");
                    transform = transform with { Position28 = new(moved.Position.X, moved.Position.Y, moved.Position.Z), Vertical34 = moved.Position.W };
                    graphics = graphics with { Models = graphics.Models.SetItem(moved.Actor, positioned with { Transform = transform }) };
                    return current;
                case NativeBattleRoughDivide163Sound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, 0, null) { Pan = 128, RawPan = 128 });
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleRoughDivide163Stage stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained stage rows.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleRoughDivide163Contacts:
                    graphics = ApplyNativeHeaderContacts(graphics, current.Parameters.HeaderIndex7C);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleRoughDivide163Geometry geometry:
                    return AppendGeometry(current, geometry.Batch, null);
                case NativeBattleRoughDivide163Trail trail:
                    return AppendGeometry(current, trail.Batch, trail.CursorAdvance);
                case NativeBattleRoughDivide163Sprite sprite:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Rough Divide requires its initialized ordering table.");
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Rough Divide lost its current packet cursor."), primitive.Pending, sprite.Batch, NativeBattleEffectGouraudWrites.FullDword);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return PublishCursor(current with { Memory = written.Memory }, written.Cursor);
                default:
                    throw new NotSupportedException("Native Rough Divide service has no original implementation.");
            }
        }

        NativeBattleRoughDivide163Frame AppendGeometry(NativeBattleRoughDivide163Frame current, NativeBattleGfGeometryBatch packets, int? cursorAdvance)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Rough Divide requires its initialized ordering table.");
            int start = current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Rough Divide lost its current packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, start, primitive.Pending, packets);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return PublishCursor(current with { Memory = written.Memory }, cursorAdvance is int advance ? checked(start + advance) : written.Cursor);
        }

        NativeBattleRoughDivide163Frame PublishCursor(NativeBattleRoughDivide163Frame current, int cursor)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, checked(0x20DFAB8u + (uint)cursor));
            return current with { Effect = current.Effect with { DrawArenaOffset = cursor }, Staging = current.Staging.Write(0x23CE584, bytes) };
        }

        NativeBattleRoughDivide163Frame TextureLoad(NativeBattleRoughDivide163Frame current, uint completion)
        {
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(0x20DFAB8, completion, (address, value) => current = current with { Effect = NativeBattleRoughDivide163Controller.WriteCompletionByte(current.Effect, address, value) });
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }
    }

    private bool TryWriteNativeRoughDivide163Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x23CA1A8);
        if (delta >= 2 * 16 || delta % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5F5B80 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleRoughDivide163Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x5F5B80 } current ? current : throw new NotSupportedException("Native Rough Divide completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleRoughDivide163Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
