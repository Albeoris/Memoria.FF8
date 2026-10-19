using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBadBreath113;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private const uint BadBreath113Arena = 0x20DFAB8;

    private NativeBattleGraphicsState EnterNativeBadBreath113(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBadBreath113Resources resources = effect.Binding.BadBreath113Resources ?? throw new NotSupportedException("Native Bad Breath requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures!;
        NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Bad Breath requires its original TIM allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
        textures = textures.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25218C8, 0x2521880, 20, 1);
        (root, _) = root.Append(0x6CACD0);
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = root.DescriptorAddress, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeBadBreath113(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBadBreath113Resources resources = effect.Binding.BadBreath113Resources ?? throw new NotSupportedException("Native Bad Breath requires its original resources.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Bad Breath requires its shared CRT stream.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        if (pools[0].DescriptorAddress != 0x25218C8 || root.Callback != 0x6CACD0)
        {
            throw new InvalidDataException("Native Bad Breath lost its original task descriptor.");
        }
        effect = effect with { DrawArenaOffset = root.ReadDword(16) != 0 ? 0x14C8 : 0x114C8 };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int firstContact = header.FirstContactIndex ?? throw new InvalidDataException("Native Bad Breath requires its physical target records.");
        if (BadWord(root, 12) == 1)
        {
            root = root.WriteByte(15, 1);
            pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x25218B8, BadBreath113Arena + 0x10B4, 0x104, 1), NativeBattleEffectTaskPool.Initialize(0x25218A8, BadBreath113Arena + 0x2F4, 0x2C, 80), NativeBattleEffectTaskPool.Initialize(0x2521898, BadBreath113Arena, 0xFC, 3)];
            if (Allocate(1, 0x6CAE10, out int slot))
            {
                Put(1, slot, Clear(1, slot).WriteByte(14, unchecked((byte)effect.Parameters.TargetMask7A)));
            }
        }
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            survivors = 0;
            for (int bank = 1; bank < 4; bank++)
            {
                int current = bank;
                NativeBattleEffectTaskStep step = pools[bank].Advance((pool, slot) =>
                {
                    pools[current] = pool;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    int result = task.Callback switch
                    {
                        0x6CAE10 => Main(ref task),
                        0x6CB260 => Particle(ref task),
                        0x6CB340 => Target(ref task),
                        _ => throw new NotSupportedException("Native Bad Breath selected an unrelated callback.")
                    };
                    task = task with { Next = pools[current].Slots[slot].Next };
                    Put(current, slot, task);
                    return new(pools[current], result);
                });
                pools[bank] = step.Pool;
                survivors += step.Survivors;
            }
        }
        if (survivors != 0)
        {
            root = root.WriteWord(12, unchecked((ushort)(BadWord(root, 12) + 1)));
        }
        pools[0] = pools[0] with { Slots = pools[0].Slots.SetItem(0, root) };
        if (survivors == 0)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = survivors == 0 ? 0u : 0x25218C8u } };

        int Main(ref NativeBattleModulePoolTask task)
        {
            int frame = BadWord(task, 12);
            NativeBattleVector mouth = ReadNativeGraphicsBone(graphics, effect.Parameters.Actor78, 9, 0);
            if (frame <= 44)
            {
                if (frame == 0)
                {
                    NativeBattleVector total = default;
                    int count = 0;
                    for (int actor = 0; actor < 3; actor++)
                    {
                        if ((effect.Parameters.TargetMask7A & (1 << actor)) == 0)
                        {
                            continue;
                        }
                        NativeBattleVector anchor = ReadNativeGraphicsBone(graphics, actor, 0xF0, 4096);
                        total = new(unchecked((short)(total.X + anchor.X)), unchecked((short)(total.Y + anchor.Y)), unchecked((short)(total.Z + anchor.Z)));
                        count++;
                    }
                    if (count == 0)
                    {
                        throw new InvalidDataException("Native Bad Breath requires its original party target mask.");
                    }
                    task = BadStore(task, 16, new((short)(total.X / count), (short)(total.Y / count), unchecked((short)(total.Z / count + 2000)))).WriteWord(22, 0xF000);
                }
                else if (frame is 10 or 16 or 26)
                {
                    if ((task.ReadByte(14) & 1) != 0 && Allocate(3, 0x6CB340, out int targetSlot))
                    {
                        NativeBattleModulePoolTask targetTask = Clear(3, targetSlot).WriteWord(14, task.ReadByte(15));
                        NativeBattleGfAnimationWorkspace workspace = new(new byte[0xEC].ToImmutableArray());
                        workspace = workspace.Initialize7016B0(resources.Cloud, 0xEC);
                        byte[] payload = targetTask.Payload.ToArray();
                        workspace.Data.AsSpan().CopyTo(payload.AsSpan(4));
                        Put(3, targetSlot, targetTask with { Payload = payload.ToImmutableArray() });
                    }
                    task = task.WriteByte(14, (byte)(task.ReadByte(14) >> 1)).WriteByte(15, unchecked((byte)(task.ReadByte(15) + 1)));
                }
                if (NewParticle(out int slot, out NativeBattleModulePoolTask puff))
                {
                    int aimX = NativeBattleCameraMath.Sine(BadWord(task, 22) >> 2) * 2000 >> 12;
                    task = task.WriteWord(22, unchecked((ushort)(BadWord(task, 22) + 0x174)));
                    NativeBattleVector aim = BadVector(task, 16);
                    NativeBattleVector velocity = new(unchecked((short)((Roll() & 63) + (aimX - mouth.X) / 16 - 32)), unchecked((short)((Roll() & 63) + (aim.Y - mouth.Y) / 16 - 32)), unchecked((short)((Roll() & 63) + (aim.Z - mouth.Z) / 16 - 32)));
                    puff = BadStore(BadStore(puff, 20, mouth), 28, velocity).WriteDword(16, 0x12C118C).WriteWord(14, 20).WriteWord(34, 2048).WriteWord(26, (ushort)((Roll() >> 4) + 3072));
                    Put(2, slot, BadStore(puff, 36, Decelerate(velocity, 5)));
                }
                short pitch = (short)((Roll() & 127) + 600);
                short yaw = graphics.Models[effect.Parameters.Actor78].Rotation14 ?? throw new NotSupportedException("Native Bad Breath requires its caster yaw WORD14.");
                short roll = (short)Roll();
                for (int birth = 0; birth < 2; birth++)
                {
                    if (!NewParticle(out int spraySlot, out NativeBattleModulePoolTask spray))
                    {
                        continue;
                    }
                    NativeBattleVector direction = Direction6CBB40(pitch, yaw, roll);
                    NativeBattleVector velocity = new((short)(direction.X >> 6), (short)(direction.Y >> 6), (short)(direction.Z >> 6));
                    spray = BadStore(BadStore(spray, 20, mouth), 28, velocity).WriteDword(16, 0x12C1418).WriteWord(14, 14).WriteWord(26, (ushort)((Roll() >> 4) + 2048));
                    Put(2, spraySlot, BadStore(spray, 36, Decelerate(velocity, 4)));
                    roll = unchecked((short)(roll + 2048));
                }
            }
            if (frame is >= 44 and <= 59 && NewParticle(out int fallingSlot, out NativeBattleModulePoolTask falling))
            {
                short x = (short)((Roll() & 31) - 16);
                short z = (short)((Roll() & 31) - 16);
                short y = (short)(-16 - (Roll() & 15));
                short scale = (short)((Roll() >> 4) + 2048);
                NativeBattleVector point = new(unchecked((short)(mouth.X + (x << 4))), mouth.Y, unchecked((short)(mouth.Z + (z << 4))));
                falling = BadStore(BadStore(falling, 28, new(x, y, z)), 20, point).WriteDword(16, 0x12C1418).WriteWord(14, 14).WriteWord(26, (ushort)scale).WriteWord(34, (ushort)scale);
                Put(2, fallingSlot, falling);
            }
            if (frame == 0)
            {
                NativeBattleVector anchor = ReadNativeGraphicsBone(graphics, effect.Parameters.Actor78, 0xF0, 4096);
                PublishNativeModuleSound(resources.Sound, 0x100, new(anchor.X, anchor.Y, anchor.Z, 0));
            }
            if (frame >= 59)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return 0;
        }

        int Particle(ref NativeBattleModulePoolTask task)
        {
            int frame = BadWord(task, 12);
            uint resourceAddress = task.ReadDword(16);
            NativeBattleModuleResource resource = resources.Sprites.Single(sprite => sprite.Address == resourceAddress);
            NativeBattleVector point = BadVector(task, 20);
            int depth = resource.Address == 0x12C118C && frame < 4 ? 0 : -(BadWord(task, 34) >> 2);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(frame), Phase = task.ReadWord(12), Anchor = new(point.X, point.Y, point.Z, 0), Scale = BadWord(task, 26), DepthOffset = depth, Direction = 4096, SpriteFlags = 0 };
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            frame++;
            task = task.WriteWord(12, unchecked((ushort)frame));
            if (frame >= BadWord(task, 14))
            {
                return 2;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                short velocity = unchecked((short)(BadWord(task, 28 + axis * 2) + BadWord(task, 36 + axis * 2)));
                task = task.WriteWord(28 + axis * 2, unchecked((ushort)velocity)).WriteWord(20 + axis * 2, unchecked((ushort)(BadWord(task, 20 + axis * 2) + velocity)));
            }
            return 0;
        }

        int Target(ref NativeBattleModulePoolTask task)
        {
            int frame = BadWord(task, 12);
            int actor = BadWord(task, 14);
            if (frame <= 10 && NewParticle(out int slot, out NativeBattleModulePoolTask particle))
            {
                NativeBattleVector anchor = ReadNativeGraphicsBone(graphics, actor, 0xF0, 4096);
                short x = (short)((Roll() & 64) - 32);
                short z = (short)((Roll() & 64) - 32);
                short y = (short)(-32 - (Roll() & 31));
                short height = unchecked((short)RiseHeight(Roll(), anchor.Y));
                short scale = (short)((Roll() >> 4) + 2048);
                NativeBattleVector point = new(unchecked((short)(anchor.X + (x << 2))), height, unchecked((short)(anchor.Z + (z << 2))));
                particle = BadStore(BadStore(particle, 20, point), 28, new(x, y, z)).WriteDword(16, 0x12C1014).WriteWord(14, 14).WriteWord(26, (ushort)scale).WriteWord(34, (ushort)scale);
                Put(2, slot, particle);
            }
            int step = frame - 10;
            if ((uint)step < 28)
            {
                int sine = NativeBattleCameraMath.Sine(step * 2048 / 28);
                DrawOverlay(actor, (short)(sine >> 2), unchecked((short)(step << 8)));
                graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = (ushort)(graphics.Models[actor].Flags0 | 4) }) };
                PlayCloud(ref task, actor);
            }
            else
            {
                graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = (ushort)(graphics.Models[actor].Flags0 & 0xFFFB) }) };
            }
            if (frame == 27)
            {
                for (int index = 0; index < header.ReadByte(16); index++)
                {
                    if (nativeActionPackets.ReadContact(firstContact + index).Primary.ActorIndex == actor)
                    {
                        graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(firstContact + index)] });
                    }
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame >= 38 ? 2 : 0;
        }

        void PlayCloud(ref NativeBattleModulePoolTask task, int actor)
        {
            NativeBattleVector anchor = ReadNativeGraphicsBone(graphics, actor, 0xF1, 0);
            anchor = anchor with { Y = graphics.Models[actor].Transform!.Angle36 };
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(4, 0xEC));
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Cloud, 0, (current, node) =>
            {
                if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
                {
                    return current;
                }
                ImmutableArray<byte> source = resources.Cloud.Data;
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(8 + node.GeometryIndex * 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(geometry + 4));
                ImmutableArray<byte> vertices;
                if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
                {
                    int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                    vertices = source.Slice(geometry + 12 + shape * count * 8, count * 8);
                }
                else
                {
                    NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                    vertices = NativeBattleGfMorphGeometry.Morph701390(source, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(0x11B8, 0x310).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(0x11B8, vertices.AsSpan()) };
                    vertices = vertices.Slice(0, count * 8);
                }
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(node.Vector16) with { X = node.Vector8.X + anchor.X, Y = node.Vector8.Y + anchor.Y, Z = node.Vector8.Z + anchor.Z };
                uint flag = resources.Flags[node.NodeIndex];
                if ((flag & 0x10000000) != 0)
                {
                    var translation = NativeBattleGfMatrices.RotateLong56C600(view, matrix.X, matrix.Y, matrix.Z);
                    matrix = matrix with { X = unchecked(translation.X + view.X), Y = unchecked(translation.Y + view.Y), Z = unchecked(translation.Z + view.Z) };
                }
                else
                {
                    matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
                }
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
                uint flags = (flag & 0xDFFF) | 0x2000u | (node.Scalar36 == 0 ? 0u : 0xC0u);
                Append(NativeBattleModulePackets.EmitBatch572200(source, geometry, vertices, matrix, projection, new(flags, node.Scalar36 == 0 ? 0u : node.Color32, node.Scalar36, null, null, null, 17, 2), colors));
                return current;
            });
            byte[] payload = task.Payload.ToArray();
            advanced.Workspace.Data.AsSpan().CopyTo(payload.AsSpan(4));
            task = task with { Payload = payload.ToImmutableArray() };
        }

        void DrawOverlay(int actorIndex, short amplitude, short phase)
        {
            NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actorIndex);
            NativeBattleActionMatrix main = NativeBattleGfMatrices.Compose56C2F0(view, actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Bad Breath requires its retained actor world matrix."));
            NativeBattleModuleProjectedVertex origin = NativeBattleModuleProjection.Project45FE10(main, default, projection);
            if (origin.Depth == 0)
            {
                return;
            }
            graphics = SubmitNativeShadowPackets5088A0(graphics, actor, projection, 4112, false);
            NativeBattlePoseSource source = resource.PoseSource!;
            actor = NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(actor, source, view);
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source);
            ImmutableArray<NativeBattleRawMesh> body = resource.Geometry!.Value;
            ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry!.Value;
            bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Bad Breath requires linked skeleton ownership."));
            for (int index = 0; index < body.Length; index++)
            {
                if ((actor.SequenceState!.MeshMask124 & (1u << (index & 31))) != 0)
                {
                    Submit(shared && index == 1 ? linked.Single() : body[index], bones, shared && index == 1 ? actor.LinkedTextureSlots! : actor.BodyTextureSlots!);
                }
            }
            actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
            if (!shared && !linked.IsEmpty)
            {
                ImmutableArray<NativeBattleActionMatrix> weaponBones = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(actor, resource.LinkedPoseSource!, view);
                foreach (NativeBattleRawMesh mesh in linked)
                {
                    Submit(mesh, weaponBones, actor.LinkedTextureSlots!);
                }
            }
            graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor) };

            void Submit(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> matrices, NativeBattleModelTextureBinding binding)
            {
                ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Bad Breath requires its actual projected scratch capacity.");
                uint address = checked(0x196B018u - (uint)maximum * 8);
                byte[] output = (NativeFileStaging ?? throw new NotSupportedException("Native Bad Breath requires its retained shared projection bytes.")).Read(address, mesh.Vertices.Length * 8);
                NativeBattleProjectedVertex[] projected = new NativeBattleProjectedVertex[mesh.Vertices.Length];
                for (int index = 0; index < mesh.Vertices.Length; index++)
                {
                    NativeBattleRawVertex vertex = mesh.Vertices[index];
                    NativeBattleModuleDepthCueVertex point = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrices[vertex.Bone], vertex.Position, projection, 0, 0);
                    NativeBattleProjectedVertex retained = new(BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(index * 8)), BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(index * 8 + 4)));
                    projected[index] = Sway6CB7D0(point.Projected, point.Ir2, unchecked((short)origin.Screen), amplitude, phase, resources.Wave, retained);
                    BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(index * 8), projected[index].Screen);
                    BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(index * 8 + 4), projected[index].DepthAndClip);
                }
                NativeFileStaging = NativeFileStaging!.Write(address, output);
                NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.Emit6BCB50(mesh.Polygons, projected, actor.Color40!.Value, resource.GeometryRelocated507550 ? null : binding);
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeEffectMemory!.PrimitiveProcess!, graphics.PrimitiveFrame!.Pending, batch);
                NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = written.Process };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            }
        }

        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        bool Allocate(int bank, uint callback, out int slot)
        {
            if (pools[bank].Slots.All(task => (task.Flags & 1) != 0))
            {
                slot = -1;
                return false;
            }
            (pools[bank], slot) = pools[bank].Append(callback);
            return true;
        }
        bool NewParticle(out int slot, out NativeBattleModulePoolTask task)
        {
            if (!Allocate(2, 0x6CB260, out slot))
            {
                task = null!;
                return false;
            }
            task = Clear(2, slot);
            return true;
        }
        NativeBattleModulePoolTask Clear(int bank, int slot) => pools[bank].Slots[slot] with { Payload = new byte[pools[bank].Slots[slot].Payload.Length].ToImmutableArray() };
        void Put(int bank, int slot, NativeBattleModulePoolTask task) => pools[bank] = pools[bank] with { Slots = pools[bank].Slots.SetItem(slot, task) };
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        static NativeBattleVector Decelerate(NativeBattleVector velocity, int shift) => new((short)(-velocity.X >> shift), (short)(-velocity.Y >> shift), (short)(-velocity.Z >> shift));
    }

    private static short BadWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector BadVector(NativeBattleModulePoolTask task, int offset) => new(BadWord(task, offset), BadWord(task, offset + 2), BadWord(task, offset + 4));
    private static NativeBattleModulePoolTask BadStore(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
}
