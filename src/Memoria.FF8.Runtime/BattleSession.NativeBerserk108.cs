using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeBerserk108(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBerserk108Resources resources = effect.Binding.Berserk108Resources ?? throw new NotSupportedException("Native Berserk requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures!;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            textures = textures.Register5099A0(resources.Camera);
            NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
            NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Berserk requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        }
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521B18, 0x2521AD0, 20, 1);
        (root, _) = root.Append(0x6CFD70);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteDword(12, 0).WriteDword(16, 0)) };
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = 0x2521B18, DrawArenaOffset = null, Berserk108 = new(memory.Cursor) } };
    }

    private NativeBattleGraphicsState AdvanceNativeBerserk108(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBerserk108Resources resources = effect.Binding.Berserk108Resources ?? throw new NotSupportedException("Native Berserk requires its original resources.");
        int buffer = effect.Berserk108?.BufferOffset ?? throw new NotSupportedException("Native Berserk requires its executed initializer.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Berserk requires its shared CRT stream.");
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 0x554C : 0x1D54C)) };
        root = root.WriteDword(16, root.ReadDword(16) == 0 ? 1u : 0u);
        if (WindWord(root, 12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x2521B08, Address(0), 0x8C4, 3), NativeBattleEffectTaskPool.Initialize(0x2521AF8, Address(0x1A4C), 0x420, 12), NativeBattleEffectTaskPool.Initialize(0x2521AE8, Address(0x4BCC), 0x1C, 64)];
            }
            int action = root.ReadByte(14);
            if (action <= nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C].ReadByte(17))
            {
                int contact = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C + action].FirstContactIndex ?? throw new InvalidDataException("Native Berserk requires its selected first contact.");
                int actor = nativeActionPackets.ReadContact(contact).Primary.ActorIndex;
                uint entity = checked(0x1D972C0u + (uint)actor * 156);
                if (pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.ReadDword(20) == entity && WindWord(task, 12) <= 43))
                {
                    root = root.WriteWord(12, 0);
                }
                else if (Allocate(1, 0x6CFF60, out int slot))
                {
                    NativeBattleModulePoolTask task = pools[1].Slots[slot] with { Payload = new byte[0x8B8].ToImmutableArray() };
                    NativeBattleVector spawn = ReadNativeGraphicsBone(graphics, actor, 0xF1, 0) with { Y = graphics.Models[actor].Transform!.Angle36 };
                    task = WindStore(WindStore(task.WriteWord(14, (ushort)action).WriteDword(20, entity), 24, spawn), 32, ReadNativeDefaultPosition571400(graphics, actor));
                    NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x628 - 12, 0x29C));
                    task = PublishWorkspace(task, 0x628, workspace.Initialize7016B0(resources.Burst, 0x29C));
                    pools[1] = pools[1] with { Slots = pools[1].Slots.SetItem(slot, task) };
                    root = root.WriteByte(14, unchecked((byte)(action + 1)));
                }
            }
        }
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            survivors = 0;
            for (int bank = 1; bank <= 3; bank++)
            {
                int current = bank;
                NativeBattleEffectTaskStep step = pools[current].Advance((pool, slot) =>
                {
                    pools[current] = pool;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    int result;
                    if (current == 1 && task.Callback == 0x6CFF60)
                    {
                        result = Main(ref task, slot);
                    }
                    else if (current == 2 && task.Callback == 0x6D0C20)
                    {
                        uint owner = task.ReadDword(28);
                        uint delta = unchecked(owner - Address(0));
                        if (delta % 0x8C4 != 0 || delta / 0x8C4 >= 3)
                        {
                            throw new InvalidDataException("Native Berserk swirl lost its main-node owner.");
                        }
                        NativeBattleBerserk108SwirlStep swirl = NativeBattleBerserk108Swirl.Advance(task, WindVector(pools[1].Slots[(int)(delta / 0x8C4)], 32), projection, resources.TextureWindows);
                        task = swirl.Task;
                        Append(swirl.Packets, 1);
                        result = swirl.Result;
                    }
                    else if (current == 3 && task.Callback == 0x6D1250)
                    {
                        result = Particle(ref task);
                    }
                    else
                    {
                        throw new NotSupportedException("Native Berserk selected an unrelated callback.");
                    }
                    task = task with { Next = pools[current].Slots[slot].Next };
                    pools[current] = pools[current] with { Slots = pools[current].Slots.SetItem(slot, task) };
                    return new(pools[current], result);
                });
                pools[current] = step.Pool;
                survivors += step.Survivors;
            }
        }
        pools[0] = pools[0] with { Slots = pools[0].Slots.SetItem(0, root) };
        if (survivors == 0)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        else
        {
            int counter = WindWord(root, 12) + 1;
            pools[0] = pools[0] with { Slots = pools[0].Slots.SetItem(0, root.WriteWord(12, unchecked((ushort)(counter >= 10 ? 0 : counter)))) };
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = survivors == 0 ? 0u : 0x2521B18u } };

        int Main(ref NativeBattleModulePoolTask task, int slot)
        {
            int counter = WindWord(task, 12);
            NativeBattleVector anchor = WindVector(task, 32);
            NativeBattleVector spawn = WindVector(task, 24);
            if (counter < 4 && Allocate(2, 0x6D0C20, out int swirlSlot))
            {
                NativeBattleModulePoolTask swirl = pools[2].Slots[swirlSlot] with { Payload = new byte[0x414].ToImmutableArray() };
                swirl = swirl.WriteWord(16, unchecked((ushort)((counter << 10) + 0x200))).WriteWord(22, 0x800).WriteDword(28, Address(slot * 0x8C4));
                pools[2] = pools[2] with { Slots = pools[2].Slots.SetItem(swirlSlot, swirl) };
            }
            int elapsed = counter - 17;
            if ((uint)elapsed < 6)
            {
                Sprite(0, elapsed, anchor, 0x2000, -0x800);
            }
            if ((uint)elapsed < 15)
            {
                int scale = 1024 + NativeBattleCameraMath.Sine((elapsed << 10) / 15);
                int fade = 2048 + (elapsed << 11) / 15;
                (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
                NativeBattleActionMatrix matrix = new([unchecked((short)scale), 0, 0, 0, unchecked((short)scale), 0, 0, 0, unchecked((short)scale)], unchecked(x + view.X), unchecked(y + view.Y), unchecked(z + view.Z), 4096);
                Append(NativeBattleModulePackets.EmitBatch572200(resources.Glow, 0, ImmutableArray<byte>.Empty, matrix, projection, new(0xF0, 0, fade, null, null, null, 17, 2), colors));
                int angle = Random();
                for (int index = 0; index < 3; index++)
                {
                    int distance = (Random() & 255) + 150;
                    if (!Allocate(3, 0x6D1250, out int particleSlot))
                    {
                        continue;
                    }
                    NativeBattleModulePoolTask particle = pools[3].Slots[particleSlot] with { Payload = new byte[16].ToImmutableArray() };
                    NativeBattleVector point = new(unchecked((short)((NativeBattleCameraMath.Sine(angle) * distance >> 12) + spawn.X)), unchecked((short)(spawn.Y - (Random() >> 5))), unchecked((short)((NativeBattleCameraMath.Cosine(angle) * distance >> 12) + spawn.Z)));
                    particle = WindStore(particle.WriteWord(14, 0x800).WriteByte(13, unchecked((byte)(0xFE - (Random() & 15)))).WriteDword(24, 0x12E6E98), 16, point);
                    pools[3] = pools[3] with { Slots = pools[3].Slots.SetItem(particleSlot, particle) };
                    angle += 0x555;
                }
            }
            elapsed = counter - 9;
            if ((uint)elapsed < 8 && (elapsed & 1) == 0 && Allocate(3, 0x6D1250, out int sparkSlot))
            {
                NativeBattleModulePoolTask spark = pools[3].Slots[sparkSlot] with { Payload = new byte[16].ToImmutableArray() };
                spark = WindStore(spark.WriteWord(14, 0x1000).WriteDword(24, 0x12E6C48), 16, new(unchecked((short)((Random() & 511) + anchor.X - 0x100)), unchecked((short)((Random() & 511) + anchor.Y - 0x100)), unchecked((short)((Random() & 511) + anchor.Z - 0x100))));
                pools[3] = pools[3] with { Slots = pools[3].Slots.SetItem(sparkSlot, spark) };
            }
            if ((uint)(counter - 10) < 8)
            {
                for (int birth = 0; birth < 8; birth++)
                {
                    int ray = -1;
                    for (int index = 0; index < 32; index++)
                    {
                        if (task.ReadWord(0x28 + index * 48 + 6) == 0)
                        {
                            ray = 0x28 + index * 48;
                            break;
                        }
                    }
                    if (ray < 0)
                    {
                        break;
                    }
                    task = task.WriteWord(ray + 2, Random()).WriteWord(ray + 4, unchecked((ushort)((Random() & 0x7FF) - 0x400))).WriteWord(ray + 6, 1);
                    task = task.WriteWord(ray + 32, (Random() & 1) != 0 ? (ushort)100 : unchecked((ushort)-100));
                    for (int color = 0; color < 4; color++)
                    {
                        task = task.WriteDword(ray + 8 + color * 4, resources.RayColors[color]);
                    }
                    task = task.WriteWord(ray + 34, 160).WriteWord(ray + 36, 2880).WriteWord(ray + 38, unchecked((ushort)-32)).WriteWord(ray + 40, 160).WriteWord(ray + 42, 64).WriteWord(ray + 44, unchecked((ushort)-20));
                }
            }
            if (counter == 17)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 1, 128) };
            }
            if ((uint)(counter - 17) < 36)
            {
                task = Burst(task, spawn);
            }
            (int tx, int ty, int tz) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            NativeBattleActionMatrix rayMatrix = view with { X = unchecked(tx + view.X), Y = unchecked(ty + view.Y), Z = unchecked(tz + view.Z) };
            NativeBattleBerserk108RayStep rays = NativeBattleBerserk108Geometry.Rays(task.Payload.Slice(0x28 - 12, 32 * 48), point => NativeBattleModuleProjection.Project45FE10(rayMatrix, point, projection));
            byte[] payload = task.Payload.ToArray();
            rays.Rays.AsSpan().CopyTo(payload.AsSpan(0x28 - 12));
            task = task with { Payload = payload.ToImmutableArray() };
            Append(rays.Packets, 1);
            if (counter == 0)
            {
                int actor = Actor(task.ReadDword(20));
                NativeBattleVector currentAnchor = ReadNativeDefaultPosition571400(graphics, actor);
                PublishNativeModuleSound(resources.Sound, 0x101, new(currentAnchor.X, currentAnchor.Y, currentAnchor.Z, 0));
            }
            if (counter == 43)
            {
                int contact = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C + WindWord(task, 14)].FirstContactIndex ?? throw new InvalidDataException("Native Berserk lost its selected contact.");
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            }
            if (counter >= 53 && rays.Alive == 0)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return 0;
        }

        int Particle(ref NativeBattleModulePoolTask task)
        {
            int frame = unchecked((sbyte)task.ReadByte(12));
            bool ember = task.ReadDword(24) == 0x12E6E98;
            if (!ember && task.ReadDword(24) != 0x12E6C48)
            {
                throw new InvalidDataException("Native Berserk particle lost its original flipbook.");
            }
            Sprite(ember ? 1 : 2, frame, WindVector(task, 16), WindWord(task, 14), -(WindWord(task, 14) >> 2));
            if (ember)
            {
                short velocity = unchecked((short)(WindWord(task, 22) + unchecked((sbyte)task.ReadByte(13))));
                task = task.WriteWord(22, unchecked((ushort)velocity)).WriteWord(18, unchecked((ushort)(WindWord(task, 18) + velocity)));
            }
            frame = unchecked((sbyte)(frame + 1));
            task = task.WriteByte(12, unchecked((byte)frame));
            return frame >= (ember ? 14 : 15) ? 2 : 0;
        }

        NativeBattleModulePoolTask Burst(NativeBattleModulePoolTask task, NativeBattleVector spawn)
        {
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x628 - 12, 0x29C));
            NativeBattleGfAnimationAdvance advance = workspace.Advance701970(resources.Burst, 0, (current, node) =>
            {
                if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
                {
                    return current;
                }
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(resources.Burst.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(resources.Burst.Data.AsSpan().Slice(geometry + 4));
                ImmutableArray<byte> vertices;
                if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
                {
                    int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                    vertices = resources.Burst.Data.Slice(geometry + 12 + shape * count * 8, count * 8);
                }
                else
                {
                    if (count > 80)
                    {
                        throw new InvalidDataException("Native Berserk exceeds its80-vertex morph bank.");
                    }
                    NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                    vertices = NativeBattleGfMorphGeometry.Morph701390(resources.Burst.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(buffer + 0x52CC, 0x280).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(buffer + 0x52CC, vertices.AsSpan()) };
                    vertices = vertices.Slice(0, count * 8);
                }
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(node.Vector16);
                int x = node.Vector8.X + spawn.X;
                int y = node.Vector8.Y + spawn.Y;
                int z = node.Vector8.Z + spawn.Z;
                uint part = resources.PartFlags[node.NodeIndex];
                if ((part & 0x10000000) != 0)
                {
                    (x, y, z) = NativeBattleGfMatrices.RotateLong56C600(view, x, y, z);
                    matrix = matrix with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
                }
                else
                {
                    matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix with { X = x, Y = y, Z = z });
                }
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
                uint flags = (part & 0xDFFF) | 0x2000u;
                flags |= node.Scalar36 != 0 ? 0xC0u : 0;
                Append(NativeBattleModulePackets.EmitBatch572200(resources.Burst.Data, geometry, vertices, matrix, projection, new(flags, node.Scalar36 != 0 ? node.Color32 : null, node.Scalar36, null, null, null, 17, 2), colors));
                return current;
            });
            return PublishWorkspace(task, 0x628, advance.Workspace);
        }

        uint Address(int offset) => checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer + (uint)offset);
        ushort Random()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        bool Allocate(int bank, uint callback, out int slot)
        {
            if (!pools[bank].Slots.Any(candidate => (candidate.Flags & 1) == 0))
            {
                slot = -1;
                return false;
            }
            (pools[bank], slot) = pools[bank].Append(callback);
            return true;
        }
        int Actor(uint address)
        {
            uint delta = unchecked(address - 0x1D972C0u);
            if (delta % 156 != 0 || delta / 156 >= 7)
            {
                throw new InvalidDataException("Native Berserk target leaves its physical entity bank.");
            }
            return (int)(delta / 156);
        }
        void Append(NativeBattleGfGeometryBatch batch, ushort flags = 0)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, flags);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        void Sprite(int index, int frame, NativeBattleVector point, int scale, int depth)
        {
            NativeBattleModuleResource resource = resources.Sprites[index];
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(frame), Phase = unchecked((ushort)frame), Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, DepthOffset = depth, SpriteFlags = 0 };
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.RgbBytes);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
    }
}
