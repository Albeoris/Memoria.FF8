using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeShot195(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Canister Shot requires its loaded module.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Canister Shot requires retained effect memory.");
        NativeBattleShot195Frame frame = NativeBattleShot195.Enter(new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Native Canister Shot requires its original CRT random owner.")), nativeActionPackets);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        if (effect.Parameters?.Parameter80 == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native5095F0 requires the actual camera owner.")) with { ForceCamera = false, Mask = 0, BlendProgress = 4096 };
            NativeBattleEffectResourcePointer tim = effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x22711C4);
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Canister Shot requires its actual TIM upload owner.");
            (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Native Canister Shot requires its current loaded TIM bytes."), tim.ArenaOffset);
            frame = frame with { Memory = frame.Memory with { Textures = textures } };
            NativeEffectMemory = frame.Memory;
        }
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeShot195(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Canister Shot requires its current module.");
        NativeBattleShot195Resources resources = effect.Binding.Shot195Resources ?? throw new NotSupportedException("Native Canister Shot requires its original executable resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Canister Shot requires its current published camera projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Canister Shot requires its actual view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Canister Shot requires its original interpolation tables.");
        NativeBattleShot195Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Canister Shot requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Canister Shot requires its CRT random state."));
        frame = NativeBattleShot195.Advance(frame, new(graphics.Flags, nativeActionPackets, Muzzle, BoneCount, (actor, bone, fraction) => ReadNativeGraphicsBone(graphics, actor, checked((byte)bone), unchecked((short)fraction)), actor => graphics.Models[actor].Transform?.Scale38 ?? throw new NotSupportedException("Native Canister Shot requires its target scale WORD38."), Center, LinkedBones, Execute), resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        int BoneCount(int actor) => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Canister Shot requires its current target skeleton.")).BoneCount;

        NativeBattleVector Center(int actor)
        {
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(anchor.X, anchor.Y, anchor.Z);
        }

        NativeBattleShot195Anchor Muzzle(int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            ImmutableArray<NativeBattleActionMatrix> bones = LinkedBones(actor).Matrices;
            if (bones.Length <= 2)
            {
                throw new InvalidDataException("Native5B31A0 muzzle requires its actual bone2.");
            }
            var local = NativeBattleGfMatrices.RotateLong56C600(bones[2], 0, 0, -4096);
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(local.X, local.Y, local.Z);
            NativeBattleActionMatrix world = LinkedWorld(model);
            var rotated = NativeBattleGfMatrices.RotateLong56C600(world, direction.X, direction.Y, direction.Z);
            direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(rotated.X, rotated.Y, rotated.Z);
            NativeBattleActionMatrix position = NativeBattleGfMatrices.Compose56C2F0(world, bones[2]);
            NativeBattleVector point = new(unchecked((short)position.X), unchecked((short)position.Y), unchecked((short)position.Z));
            point = new(unchecked((short)(point.X + (350 * direction.X >> 12))), unchecked((short)(point.Y + (350 * direction.Y >> 12))), unchecked((short)(point.Z + (350 * direction.Z >> 12))));
            NativeBattleVector axis = resources.ReferenceAxes[0];
            NativeBattleModuleAxisAngle angle = NativeBattleModuleAlignment.Between571480(axis.X, axis.Y, axis.Z, direction.X, direction.Y, direction.Z);
            return new(point, new(unchecked((short)direction.X), unchecked((short)direction.Y), unchecked((short)direction.Z)), new(NativeBattleModuleAlignment.Rotation5714F0(angle.Angle, angle.Axis), 0, 0, 0, 4096));
        }

        NativeBattleBodyBoneMatrices LinkedBones(int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).LinkedPoseSource ?? throw new NotSupportedException("Native Canister Shot requires its actual linked weapon skeleton.");
            if (source.BoneCount > 4)
            {
                throw new NotSupportedException("Native Canister Shot linked pose copy crosses its original128-byte echo history into adjacent module globals.");
            }
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Canister Shot requires its current linked pose owner.");
            NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, source);
            return new(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source.Skeleton.AsSpan())), NativeBattlePoseMatrices.StageBoneMatrices(source, pose));
        }

        static NativeBattleActionMatrix LinkedWorld(NativeBattleGraphicsModel model)
        {
            NativeBattleActionMatrix parent = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Canister Shot requires its original parent world matrix.");
            NativeBattleLinkedModelTransform linked = model.LinkedTransform ?? throw new NotSupportedException("Native Canister Shot requires its actual linked weapon transform.");
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(linked.Rotation) with { X = linked.Position.X, Y = linked.Position.Y, Z = linked.Position.Z };
            return NativeBattleGfMatrices.Compose56C2F0(parent, local);
        }

        NativeBattleShot195Frame Execute(NativeBattleShot195Frame current, NativeBattleShot195Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleShot195Bind bind:
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(0xD07400, resources.ActorOverride.Data.AsSpan());
                    NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(staging, NativeModelCpuResources ?? throw new NotSupportedException("Native Canister Shot requires its actual CPU resource rows."), actor, 0x226DF00, 0xD07400);
                    NativeFileStaging = binding.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, binding.Model) };
                    return current;
                case NativeBattleShot195Sequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Actor, sequence.Sequence);
                    return current;
                case NativeBattleShot195Stage stage:
                    NativeBattleStageState actual = NativeStage ?? throw new NotSupportedException("Native Canister Shot requires its retained stage groups.");
                    NativeStage = actual with { Groups = actual.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = 0x808080 }).ToImmutableArray() };
                    return current;
                case NativeBattleShot195Contact contact:
                    if (contact.All)
                    {
                        graphics = ApplyNativeHeaderContacts(graphics, current.Effect.Parameters!.HeaderIndex7C);
                    }
                    else
                    {
                        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C];
                        NativeBattleContactPacket first = nativeActionPackets.ReadContact(header.FirstContactIndex ?? throw new InvalidDataException("Native Canister Shot requires its physical first contact pointer."));
                        NativeBattleGraphicsState applied = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [first] });
                        graphics = applied with { PendingContacts = graphics.PendingContacts };
                    }
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleShot195Sound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, unchecked((uint)sound.Channel), null) { RawPan = unchecked((uint)sound.Volume) });
                    return current;
                case NativeBattleShot195Mesh mesh:
                    NativeBattleStaticMeshResource source = resources.Meshes.Single(resource => resource.Address == mesh.Address);
                    NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, mesh.World);
                    if (mesh.CameraAnchor is { } anchor)
                    {
                        NativeBattleModuleDraw billboard = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = new(anchor.X, anchor.Y, anchor.Z, 0), Scale = 4096, DepthOffset = mesh.Depth, SpriteFlags = 0 };
                        NativeBattleActionMatrix translation = NativeBattlePoseMatrices.ModuleSpriteCamera(billboard, view, 0);
                        matrix = matrix with { X = translation.X, Y = translation.Y, Z = translation.Z };
                    }
                    NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(source.Bytes, 0, source.Bytes.Slice(8, source.Bytes.Length - 8), matrix, projection, new(mesh.Flags, 0, mesh.Fade, null, null, null, 17, 2), colors);
                    return AppendMesh(current, batch, false);
                case NativeBattleShot195Sprite sprite:
                    NativeBattleSearchSpriteResource spriteSource = resources.Sprites.Single(resource => resource.Address == sprite.Address);
                    NativeBattleModuleResource resource = new(0, sprite.Address, 0, 0, spriteSource.Bytes);
                    NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(sprite.Phase), Phase = sprite.Phase, Anchor = new(sprite.Position.X, sprite.Position.Y, sprite.Position.Z, 0), Scale = sprite.Scale, DepthOffset = sprite.Depth, SpriteFlags = sprite.Flags };
                    NativeBattleCameraProjection spriteProjection = projection;
                    if (sprite.World is { } spriteWorld)
                    {
                        spriteProjection = projection with { View = NativeBattleGfMatrices.Compose56C2F0(view, spriteWorld) };
                        draw = draw with { Anchor = new(0, 0, 0, 0), Scale = 4096, SpriteWorldRotation = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096] };
                    }
                    NativeBattleModulePacketBatch sprites = NativeBattleModulePackets.EmitSprite571DC0(draw, spriteProjection, NativeCamera!.Roll, 17, 2);
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Canister Shot requires its current ordering table.");
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, sprites, NativeBattleEffectGouraudWrites.FullDword);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
                case NativeBattleShot195Echo echo:
                    return DrawEcho(current, echo);
                default:
                    throw new NotSupportedException("Native Canister Shot operation has no original service owner.");
            }
        }

        NativeBattleShot195Frame DrawEcho(NativeBattleShot195Frame current, NativeBattleShot195Echo echo)
        {
            NativeBattleGraphicsModel model = graphics.Models[echo.Actor];
            if ((model.Flags0 & 0x200) != 0)
            {
                return current;
            }
            if ((uint)echo.Geometry >= resources.EchoColors.Length)
            {
                throw new InvalidDataException("Native Canister Shot echo geometry exceeds its original five pointers.");
            }
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Canister Shot echo requires its actual renderer process.");
            bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Canister Shot echo requires its selected renderer capability.");
            var gate = process.Enter5099D0(hardware);
            current = current with { Memory = current.Memory with { PrimitiveProcess = gate.Process } };
            if (!gate.Draw)
            {
                return current;
            }
            NativeBattleActionMatrix world = LinkedWorld(model);
            NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(view, world);
            ImmutableArray<NativeBattleActionMatrix> bones = echo.Bones.Matrices.Select(bone => NativeBattleGfMatrices.Compose56C2F0(basis, bone)).ToImmutableArray();
            ImmutableArray<NativeBattleRawMesh> meshes = NativeGraphicsPoseResource(graphics, echo.Actor).LinkedGeometry ?? throw new NotSupportedException("Native Canister Shot echo requires its actual linked weapon geometry.");
            NativeBattleModelTextureBinding textures = model.LinkedTextureSlots ?? throw new NotSupportedException("Native Canister Shot echo requires its executed linked weapon texture relocation.");
            meshes = meshes.Select(mesh => mesh with { Polygons = mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, textures) }).ToImmutableArray() }).ToImmutableArray();
            int length = meshes.Select(mesh => checked(mesh.Vertices.Length * 8)).DefaultIfEmpty().Max();
            uint rgb = resources.EchoColors[echo.Geometry];
            NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, bones, projection, rgb, uint.MaxValue, new byte[length].ToImmutableArray(), colors, 4, new(0, 0, 320, 216), 17, null);
            current = current with { Memory = current.Memory with { PrimitiveProcess = gate.Process.PublishModelColor(rgb) } };
            return AppendMesh(current, draw.Packets, true);
        }

        NativeBattleShot195Frame AppendMesh(NativeBattleShot195Frame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Canister Shot requires its current ordering table.");
            if (common)
            {
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Canister Shot echo requires its common packet owner."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }
            NativeBattleEffectPacketWrite packet = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = packet.Ordering } };
            return current with { Memory = packet.Memory, Effect = current.Effect with { DrawArenaOffset = packet.Cursor } };
        }
    }
}
