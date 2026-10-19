using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeRayBomb10(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Ray-Bomb requires its selected module.");
        NativeBattleRayBomb10Resources resources = effect.Binding.RayBomb10Resources ?? throw new NotSupportedException("Native Ray-Bomb requires its authenticated executable resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Ray-Bomb requires retained process memory.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Ray-Bomb requires its actual CRT random owner.");
        if (initialize)
        {
            NativeBattleRayBomb10Frame initialized = NativeBattleRayBomb10Controller.Initialize627E60(memory, effect, resources, random);
            NativeEffectMemory = initialized.Memory;
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Ray-Bomb requires its actual camera.")).RegisterControlResource(initialized.Memory.Textures!.Animation);
            return graphics with { OriginalEffect = initialized.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Ray-Bomb requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Ray-Bomb requires its current projection.");
        NativeBattleCameraPoint eye = camera.CurrentPose?.Eye ?? throw new NotSupportedException("Native Ray-Bomb requires its actual camera eye.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Ray-Bomb requires its original depth-cue colors.");
        NativeBattleRayBomb10Inputs inputs = new(graphics.Flags, nativeActionPackets, projection, camera.Roll, new(eye.X, eye.Y, eye.Z), actor => graphics.Models[actor], actor => graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Native627F10 requires its caster's actual prior502AB0 matrix."), (actor, selector, fraction) => ReadNativeGraphicsBone(graphics, actor, selector, fraction), ReadCentre, actor => ReadNativeModelAnchor(graphics, actor), Execute);
        NativeBattleRayBomb10Frame frame = NativeBattleRayBomb10Controller.Advance629730(new(memory, effect, random), resources, inputs);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleVector ReadCentre(ushort mask)
        {
            NativeBattleVector[] positions = Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => (graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Ray-Bomb requires actual target positions.")).Position28).ToArray();
            return NativeBattleActionGeometryRules.TargetCenter(positions);
        }

        NativeBattleRayBomb10Frame Execute(NativeBattleRayBomb10Frame current, NativeBattleRayBomb10Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleRayBomb10Model model:
                    graphics = graphics with { Models = graphics.Models.SetItem(model.Model.Index, model.Model) };
                    return current;
                case NativeBattleRayBomb10Sequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Actor, sequence.Sequence);
                    return current;
                case NativeBattleRayBomb10Contact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Ray-Bomb contact lost its process memory.") };
                case NativeBattleRayBomb10Sound sound:
                    PublishNativeModuleSound(sound.Sound, sound.Flags, sound.Position);
                    return current;
                case NativeBattleRayBomb10Flash flash:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Ray-Bomb requires its four stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)flash.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleRayBomb10Restore restore:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(restore.Destination, restore.Completion, (address, value) => current = current with { Effect = NativeBattleRayBomb10Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleRayBomb10Sprite sprite:
                    return AppendSprite(current, NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, camera.Roll, 17, 2));
                case NativeBattleRayBomb10WorldSprite sprite:
                    NativeBattleModuleResource resource = resources.Sprites.Single(item => item.Address == sprite.Resource);
                    NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(unchecked((ushort)sprite.Phase)), SpriteFlags = sprite.Flags, SpriteColor = sprite.Color };
                    NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Native6289B0 requires its actual view."), sprite.World);
                    return AppendSprite(current, NativeBattleModulePackets.EmitSprite571DC0(draw, projection, camera.Roll, 17, 2, matrix));
                case NativeBattleRayBomb10Mesh mesh:
                    NativeBattleStaticMeshResource modelResource = resources.Meshes.Single(item => item.Address == mesh.Resource);
                    NativeBattleActionMatrix modelMatrix = mesh.CameraSpace ? mesh.Matrix : NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Native Ray-Bomb requires its current view."), mesh.Matrix);
                    NativeBattleEffectMeshPacketContext context = new(mesh.Flags, mesh.FarColor, mesh.Fade, null, null, 0, false, null, 17, 2);
                    NativeBattleGfGeometryBatch batch = NativeBattleEffectMeshPackets.EmitBatch(modelResource.Bytes, modelMatrix, projection, mesh.Flags, null, (group, face, corner, offset) => group switch
                    {
                        0 or 1 => NativeBattleEffectMeshPackets.EmitFlat(face, corner, group == 1, context, colors, offset),
                        2 or 3 => NativeBattleEffectMeshPackets.EmitTextured(face, corner, group == 3, false, context, colors, offset),
                        4 or 5 => NativeBattleEffectMeshPackets.EmitGouraud(face, corner, group == 5, context, colors, offset),
                        _ => NativeBattleEffectMeshPackets.EmitTextured(face, corner, group == 7, true, context, colors, offset)
                    });
                    return Append(current, (process, cursor, ordering) => NativeBattleEffectPacketArena.Append(process, cursor, ordering, batch));
                default:
                    throw new NotSupportedException("Native Ray-Bomb operation requires its actual immediate owner.");
            }
        }

        NativeBattleRayBomb10Frame AppendSprite(NativeBattleRayBomb10Frame current, NativeBattleModulePacketBatch batch) => Append(current, (process, cursor, ordering) => NativeBattleEffectPacketArena.Append(process, cursor, ordering, batch, NativeBattleEffectGouraudWrites.FullDword));

        NativeBattleRayBomb10Frame Append(NativeBattleRayBomb10Frame current, Func<NativeBattleEffectMemory, int, NativeBattleLogicalOrdering, NativeBattleEffectPacketWrite> write)
        {
            NativeBattleRayBomb10State state = current.Memory.RayBomb10 ?? throw new NotSupportedException("Native Ray-Bomb requires its selected packet arena.");
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Ray-Bomb requires its physical ordering table.");
            int cursor = checked((int)(state.PacketCursor - NativeBattleEffectNodeMemory.ArenaAddress));
            NativeBattleEffectPacketWrite written = write(current.Memory, cursor, primitive.Pending);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeEffectMemory = written.Memory with { RayBomb10 = state with { PacketCursor = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor) } };
            return current with { Memory = NativeEffectMemory };
        }
    }

    private bool TryWriteNativeRayBomb10Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint relative = unchecked(address - 0x24C91D8);
        if (relative >= 0x20 || relative % 0x10 != 14)
        {
            return false;
        }
        if (graphics.OriginalEffect is { Binding.Entry: 0x627E60 } effect)
        {
            updated = graphics with { OriginalEffect = NativeBattleRayBomb10Controller.WriteCompletionByte(effect, address, value) };
        }
        else if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x627E60 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleRayBomb10Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            throw new NotSupportedException("Native Ray-Bomb completion requires its retained physical root task bank.");
        }
        return true;
    }
}
