using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleStorm11Motion;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private const uint Storm11Arena = 0x20DFAB8;

    private NativeBattleGraphicsState EnterNativeStorm11(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleStorm11Resources resources = effect.Binding.Storm11Resources ?? throw new NotSupportedException("Native Storm Breath requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleStorm11State process = memory.Storm11 ?? throw new NotSupportedException("Native Storm Breath requires its process-retained executable owner.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Storm Breath requires its texture upload queue.");
        NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Storm Breath requires its original mag010.tim allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
        textures = textures.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeEffectMemory = memory with { Textures = textures, Storm11 = process.StreamVertices is null ? process with { StreamVertices = resources.Stream.Slice(8, 308 * 8) } : process };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25561A8, 0x2556150, 20, 1);
        (root, _) = root.Append(0x6EA5B0);
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = root.DescriptorAddress, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeStorm11(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleStorm11Resources resources = effect.Binding.Storm11Resources ?? throw new NotSupportedException("Native Storm Breath requires its original resources.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Storm Breath requires the shared CRT random stream.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        if (pools.Length is not (1 or 5) || pools[0].DescriptorAddress != 0x25561A8 || root.Callback != 0x6EA5B0)
        {
            throw new InvalidDataException("Native Storm Breath lost its physical task banks.");
        }
        effect = effect with { DrawArenaOffset = root.ReadDword(16) != 0 ? 0x26B8 : 0x126B8 };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int firstContact = header.FirstContactIndex ?? throw new InvalidDataException("Native Storm Breath requires its actual first contact.");
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        if (Word(root, 12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x2556198, Storm11Arena + 0x1614, 0x504, 1), NativeBattleEffectTaskPool.Initialize(0x2556188, Storm11Arena + 0x614, 0x20, 128), NativeBattleEffectTaskPool.Initialize(0x2556178, Storm11Arena + 0x4D4, 0x14, 16), NativeBattleEffectTaskPool.Initialize(0x2556168, Storm11Arena + 0x1D4, 0x30, 16)];
                ImmutableArray<byte> vertices = NativeEffectMemory!.Storm11!.StreamVertices!.Value;
                byte[] retained = vertices.ToArray();
                int min = short.MaxValue;
                int max = short.MinValue;
                for (int index = 0; index < 308; index++)
                {
                    short z = BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(index * 8 + 4));
                    min = Math.Min(min, z);
                    max = Math.Max(max, z);
                }
                for (int index = 0; index < 308; index++)
                {
                    short z = BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(index * 8 + 4));
                    BinaryPrimitives.WriteInt16LittleEndian(retained.AsSpan(index * 8 + 6), unchecked((short)((z << 12) / (max - min))));
                }
                NativeEffectMemory = NativeEffectMemory with { Arena = NativeEffectMemory.Arena!.Write(0x1B18, retained) };
            }
            if (Allocate(1, 0x6EA920, out int slot))
            {
                int count = header.ReadByte(16);
                if (count is < 1 or > 3)
                {
                    throw new InvalidDataException("Native Storm Breath requires one to three original target records.");
                }
                NativeBattleModulePoolTask target = pools[1].Slots[slot].ClearPayload(0x4F8);
                target = target.WriteWord(14, (ushort)count);
                int[] ordered = Enumerable.Range(0, count).ToArray();
                for (int a = 0; a < count - 1; a++)
                {
                    for (int b = a + 1; b < count; b++)
                    {
                        int ia = nativeActionPackets.ReadContact(firstContact + ordered[a]).Primary.ActorIndex;
                        int ib = nativeActionPackets.ReadContact(firstContact + ordered[b]).Primary.ActorIndex;
                        short za = graphics.Models[ia].Transform!.Position28.X;
                        short zb = graphics.Models[ib].Transform!.Position28.X;
                        if (za < zb)
                        {
                            (ordered[a], ordered[b]) = (ordered[b], ordered[a]);
                        }
                    }
                }
                for (int index = 0; index < count; index++)
                {
                    int actor = nativeActionPackets.ReadContact(firstContact + ordered[index]).Primary.ActorIndex;
                    target = target.WriteDword(16 + index * 4, checked(0x1D972C0u + (uint)actor * 156)).WriteByte(0x1C + index, (byte)ordered[index]);
                }
                target = target.WriteWord(0x22, count > 1 ? (ushort)1 : (ushort)0).WriteWord(0x26, (ushort)((count << 12) / 40)).WriteWord(0x2C, unchecked((ushort)CountVertices(Model(target.ReadDword(16)))));
                Put(1, slot, target);
            }
        }
        int survivors = 0;
        if (root.ReadByte(15) != 0)
        {
            for (int bank = 1; bank < 5; bank++)
            {
                int selected = bank;
                NativeBattleEffectTaskStep step = pools[bank].Advance((pool, slot) =>
                {
                    pools[selected] = pool;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    int result = task.Callback == 0x6EA920 ? Target(ref task) : Particle(ref task);
                    return new(pools[selected].WriteSlot(slot, task), result);
                });
                pools[bank] = step.Pool;
                survivors += step.Survivors;
            }
        }
        bool completed = root.ReadByte(15) != 0 && survivors == 0;
        root = root.WriteWord(12, (ushort)Math.Min(Word(root, 12) + 1, 10));
        pools[0] = pools[0].WriteSlot(0, root);
        if (completed)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = completed ? 0u : 0x25561A8u } };

        int Target(ref NativeBattleModulePoolTask task)
        {
            int frame = Word(task, 12);
            int caster = effect.Parameters!.Actor78;
            NativeBattleVector mouth = ReadNativeGraphicsBone(graphics, caster, 0x1D, 0x400);
            task = Store(task, 0x34, mouth);
            if (frame == 8)
            {
                PublishNativeModuleSound(resources.Sound, 0, new(mouth.X, mouth.Y, mouth.Z, 0));
            }
            NativeBattleActionMatrix streamWorld = NativeBattleCuragaMatrices.Identity8DD770();
            int dx = 0;
            int dz = 0;
            int distance = unchecked((int)task.ReadDword(0x30));
            if ((uint)(frame - 25) < 50)
            {
                ImmutableArray<byte> current = NativeEffectMemory!.Storm11!.StreamVertices!.Value;
                ImmutableArray<byte> retained = NativeEffectMemory.Arena!.Read(0x1B18, 308 * 8).ToImmutableArray();
                current = Wobble(retained, current, Word(task, 0x28), Word(task, 0x2A));
                NativeEffectMemory = NativeEffectMemory with { Storm11 = NativeEffectMemory.Storm11 with { StreamVertices = current } };
                int cur = Word(task, 0x20);
                int next = Word(task, 0x22);
                NativeBattleVector from = ReadNativeGraphicsBone(graphics, Model(task.ReadDword(16 + cur * 4)), 0xF1, 0);
                NativeBattleVector to = ReadNativeGraphicsBone(graphics, Model(task.ReadDword(16 + next * 4)), 0xF1, 0);
                int t = Word(task, 0x24);
                NativeBattleVector aim = new(unchecked((short)(from.X + ((to.X - from.X) * t >> 12))), unchecked((short)(from.Y + ((to.Y - from.Y) * t >> 12))), unchecked((short)(from.Z + ((to.Z - from.Z) * t >> 12))));
                if (frame >= 30)
                {
                    t = unchecked((short)(t + Word(task, 0x26)));
                    if (t >= 4096)
                    {
                        int contact = firstContact + task.ReadByte(0x1C + cur);
                        ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                        graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
                        graphics = graphics with { PendingContacts = pending };
                        if (cur != next)
                        {
                            task = task.WriteWord(0x2C, unchecked((ushort)CountVertices(Model(task.ReadDword(16 + next * 4)))));
                        }
                        t -= 4096;
                        task = task.WriteWord(0x20, (ushort)next).WriteWord(0x22, (ushort)Math.Min(next + 1, Word(task, 14) - 1));
                    }
                    task = task.WriteWord(0x24, unchecked((ushort)t));
                }
                dx = aim.X - mouth.X;
                int dy = aim.Y - mouth.Y;
                dz = aim.Z - mouth.Z;
                int square = unchecked(dx * dx + dy * dy + dz * dz);
                distance = unchecked((int)Math.Sqrt(square));
                NativeBattleActionMatrix basis = NativeBattlePandemona291Restoration.Basis50CBA0(-dx, -dy, dz, new(0, 0, 0));
                task = WriteBasis(task, basis).WriteDword(0x30, unchecked((uint)distance));
                int phase = frame - 25;
                short scale = unchecked((short)distance);
                if (phase < 8)
                {
                    scale = unchecked((short)(scale * NativeBattleCameraMath.Sine(phase << 7) >> 12));
                }
                streamWorld = NativeBattlePoseMatrices.Scale56BEF0(basis, 4096, 4096, scale) with { X = mouth.X, Y = mouth.Y, Z = mouth.Z };
                Append(NativeBattleStorm11Geometry.Emit6EB6A0(resources.Stream, current, NativeBattleGfMatrices.Compose56C2F0(view, streamWorld), projection, phase, colors));
            }
            if ((uint)frame < 24)
            {
                NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(-128, unchecked((short)(graphics.Models[caster].Rotation14!.Value + 2048)), 0)) with { X = mouth.X, Y = mouth.Y, Z = mouth.Z };
                Play(ref task, resources.Mouth, 0x4A0, resources.MouthFlags, NativeBattleGfMatrices.Compose56C2F0(view, world), frame == 0);
                Roll();
                int angle = Roll();
                for (int birth = 0; birth < 2; birth++)
                {
                    if (!Allocate(2, 0x6EBB20, out int slot))
                    {
                        continue;
                    }
                    NativeBattleVector point = NativeBattleGfMatrices.Transform460840(world, new((short)(NativeBattleCameraMath.Cosine(angle) >> 2), (short)(NativeBattleCameraMath.Sine(angle) >> 2), (short)((Roll() & 1023) + 1024)));
                    NativeBattleModulePoolTask child = Clear(2, slot);
                    child = Store(child, 16, point).WriteWord(0x16, (ushort)((Roll() & 1023) + 2048));
                    Put(2, slot, child);
                    angle += 2048;
                }
            }
            if (frame >= 29)
            {
                if (frame == 29)
                {
                    task = Store(task, 0x3C, mouth);
                }
                NativeBattleVector frozen = Vector(task, 0x3C);
                NativeBattleActionMatrix cone = ReadBasis(task) with { X = frozen.X, Y = frozen.Y, Z = frozen.Z };
                Play(ref task, resources.Cone, 0x140, resources.ConeFlags, NativeBattleGfMatrices.Compose56C2F0(view, cone), frame == 29);
            }
            if ((uint)(frame - 35) < 30)
            {
                int vertex = unchecked(Roll() * Word(task, 0x2C)) >> 16;
                for (int birth = 0; birth < 2; birth++)
                {
                    if (!Allocate(2, 0x6EBCB0, out int slot))
                    {
                        continue;
                    }
                    NativeBattleModulePoolTask child = Clear(2, slot);
                    NativeBattleVector point = SampleVertex(Model(task.ReadDword(16 + Word(task, 0x20) * 4)), vertex);
                    child = Store(child, 16, point).WriteWord(0x16, (ushort)((Roll() & 2047) + 3072)).WriteWord(0x18, unchecked((ushort)((dx << 7) / distance))).WriteWord(0x1C, unchecked((ushort)((dz << 7) / distance)));
                    Put(2, slot, child);
                    vertex += Word(task, 0x2C) >> 1;
                }
            }
            if ((uint)(frame - 30) < 40)
            {
                ImmutableArray<byte> stream = NativeEffectMemory!.Storm11!.StreamVertices!.Value;
                for (int index = 0; index < 308; index++)
                {
                    ReadOnlySpan<byte> bytes = stream.AsSpan().Slice(index * 8, 6);
                    NativeBattleVector local = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
                    NativeBattleVector point = NativeBattleGfMatrices.Transform460840(streamWorld, local);
                    if (point.Y < 0)
                    {
                        continue;
                    }
                    if (Roll() < 2048 && Allocate(2, 0x6EBA80, out int spark))
                    {
                        NativeBattleModulePoolTask child = Store(Clear(2, spark), 16, point).WriteWord(0x16, (ushort)((Roll() & 2047) + 2048)).WriteWord(0x18, unchecked((ushort)((dx << 7) / distance))).WriteWord(0x1A, unchecked((ushort)-50)).WriteWord(0x1C, unchecked((ushort)((dz << 7) / distance)));
                        Put(2, spark, child);
                    }
                    if (frame >= 60 || Roll() >= 2048 || !Allocate(4, 0x6EB790, out int debris))
                    {
                        continue;
                    }
                    NativeBattleModulePoolTask fragment = Store(Clear(4, debris), 16, point).WriteWord(0x16, (ushort)((Roll() & 2047) + 2048)).WriteWord(0x18, unchecked((ushort)((dx << 6) / distance))).WriteWord(0x1A, unchecked((ushort)(-128 - (Roll() & 127)))).WriteWord(0x1C, unchecked((ushort)((dz << 6) / distance)));
                    for (int axis = 0; axis < 3; axis++)
                    {
                        fragment = fragment.WriteWord(0x28 + axis * 2, unchecked((ushort)((Roll() & 1023) - 512)));
                    }
                    Put(4, debris, fragment);
                }
            }
            if ((uint)(frame - 20) < 45)
            {
                NativeBattleActionMatrix yaw = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), graphics.Models[caster].Rotation14!.Value) with { X = mouth.X, Y = mouth.Y, Z = mouth.Z };
                int angle = Roll();
                for (int birth = 0; birth < 2; birth++)
                {
                    if (!Allocate(2, 0x6EBC10, out int slot))
                    {
                        continue;
                    }
                    short x = (short)(NativeBattleCameraMath.Cosine(angle) >> 2);
                    short y = (short)(NativeBattleCameraMath.Sine(angle) >> 2);
                    NativeBattleVector velocity = NativeBattleGfMatrices.Transform460840(yaw, new(x, y, frame < 25 ? (short)256 : (short)1024));
                    NativeBattleVector point = NativeBattleGfMatrices.Transform460840(yaw, new((short)(x >> 3), (short)(y >> 3), 0));
                    NativeBattleModulePoolTask child = Store(Clear(2, slot), 16, point).WriteWord(0x16, (ushort)((Roll() & 2047) + 2048));
                    child = Store(child, 0x18, new((short)((point.X - velocity.X) >> 4), (short)((point.Y - velocity.Y) >> 4), (short)((point.Z - velocity.Z) >> 4)));
                    Put(2, slot, child);
                    angle += 2048;
                }
            }
            int ring = frame - 25;
            if ((uint)ring < 17 && (ring & 3) == 0 && Allocate(3, 0x6EB900, out int ringSlot))
            {
                Put(3, ringSlot, Clear(3, ringSlot).WriteDword(16, Storm11Arena + 0x1614));
            }
            task = task.WriteWord(0x28, unchecked((ushort)(Word(task, 0x28) - 100))).WriteWord(0x2A, unchecked((ushort)(Word(task, 0x2A) + 100)));
            if (frame >= 75)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return 0;
        }

        int Particle(ref NativeBattleModulePoolTask task)
        {
            if (task.Callback == 0x6EB790)
            {
                int frame = Word(task, 12);
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(Vector(task, 0x20));
                if (frame >= 22)
                {
                    int scale = 4096 + ((22 - frame) << 12) / 12;
                    matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale);
                }
                NativeBattleVector point = Vector(task, 16);
                matrix = matrix with { X = point.X, Y = point.Y, Z = point.Z };
                Mesh(resources.Debris, matrix, 0);
                task = MoveDebris6EB790(task, out bool ended);
                return ended ? 2 : 0;
            }
            if (task.Callback == 0x6EB900)
            {
                NativeBattleModulePoolTask target = pools[1].Slots[0];
                int growth = Word(task, 14);
                int angle = unchecked((short)(Word(target, 0x2A) + growth));
                int radius = NativeBattleCameraMath.Sine(growth >> 1) >> 3;
                NativeBattleVector local = new((short)(NativeBattleCameraMath.Cosine(angle) * radius >> 12), (short)(-NativeBattleCameraMath.Sine(angle) * radius >> 12), unchecked((short)((unchecked((int)target.ReadDword(0x30)) * growth) >> 12)));
                NativeBattleActionMatrix basis = ReadBasis(target);
                NativeBattleVector mouth = Vector(target, 0x34);
                NativeBattleFullMacVertex point = NativeBattleGfMatrices.TransformMac460840(basis with { X = mouth.X, Y = mouth.Y, Z = mouth.Z }, local);
                NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(basis, unchecked((short)(growth + 1024)), unchecked((short)(growth + 1024)), 4096) with { X = point.X, Y = point.Y, Z = point.Z };
                Mesh(resources.Ring, matrix, 0x30);
                task = task.WriteWord(14, unchecked((ushort)(growth + 128)));
                return Word(task, 14) >= 4096 ? 2 : 0;
            }
            uint address = task.Callback switch { 0x6EBA80 => 0x1368200, 0x6EBB20 => 0x1368580, 0x6EBC10 => 0x136843C, 0x6EBCB0 => 0x1368360, _ => throw new NotSupportedException("Native Storm Breath selected an unrelated child callback.") };
            NativeBattleVector anchor = Vector(task, 16);
            if (task.Callback == 0x6EBB20)
            {
                NativeBattleVector mouth = Vector(pools[1].Slots[0], 0x34);
                int ratio = (Word(task, 12) << 12) / 12;
                short Blend(short first, short second) => (short)Math.Clamp(unchecked(NativePrecision53.RoundInteger(first * ((4096 - ratio) / 4096d)) + NativePrecision53.RoundInteger(second * (ratio / 4096d))), short.MinValue, short.MaxValue);
                anchor = new(Blend(anchor.X, mouth.X), Blend(anchor.Y, mouth.Y), Blend(anchor.Z, mouth.Z));
            }
            NativeBattleModuleResource resource = resources.Sprites.Single(sprite => sprite.Address == address);
            int scaleSprite = Word(task, 0x16);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(Word(task, 12)), Phase = task.ReadWord(12), Anchor = new(anchor.X, anchor.Y, anchor.Z, 0), Scale = scaleSprite, DepthOffset = -(scaleSprite >> 2), Direction = 4096, SpriteFlags = 0 };
            NativeBattleModulePacketBatch spriteBatch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
            NativeBattleEffectPacketWrite spriteWritten = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, spriteBatch, NativeBattleEffectGouraudWrites.FullDword);
            NativeEffectMemory = spriteWritten.Memory;
            effect = effect with { DrawArenaOffset = spriteWritten.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = spriteWritten.Ordering } };
            int life = task.Callback == 0x6EBA80 ? 13 : task.Callback == 0x6EBCB0 ? 8 : 12;
            if (task.Callback == 0x6EBB20)
            {
                task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + 1)));
                return Word(task, 12) >= life ? 2 : 0;
            }
            task = MoveSprite(task, life, out bool spriteEnded);
            return spriteEnded ? 2 : 0;
        }

        void Play(ref NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, int size, ImmutableArray<uint> flags, NativeBattleActionMatrix context, bool initialize)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x64 - 12), 0x4A0);
            if (initialize)
            {
                workspace = workspace.Initialize7016B0(source, size);
            }
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
            {
                if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
                {
                    return current;
                }
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
                ImmutableArray<byte> vertices;
                int ratio = node.Scalar38;
                if (node.Word40 == node.Word42 || ratio is 0 or 4096)
                {
                    int shape = node.Word40 == node.Word42 || ratio == 0 ? node.Word40 : node.Word42;
                    vertices = source.Data.Slice(checked(geometry + 12 + shape * count * 8), checked(count * 8));
                }
                else
                {
                    if (count > 64)
                    {
                        throw new InvalidDataException("Native Storm Breath morph leaves its original64-vertex scratch.");
                    }
                    NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                    vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, ratio, arena.Read(0x24B8, 0x200).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(0x24B8, vertices.AsSpan()) };
                    vertices = vertices.Slice(0, count * 8);
                }
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(node.Vector16) with { X = node.Vector8.X, Y = node.Vector8.Y, Z = node.Vector8.Z };
                matrix = NativeBattleGfMatrices.Compose56C2F0(context, matrix);
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
                uint render = (flags[node.NodeIndex] & 0xDFFFu) | 0x2000u;
                if (node.Scalar36 != 0)
                {
                    render |= 0xC0;
                }
                Append(NativeBattleModulePackets.EmitBatch572200(source.Data, geometry, vertices, matrix, projection, new(render, node.Scalar36 != 0 ? node.Color32 : 0u, node.Scalar36, null, null, null, 17, 2), colors));
                return current;
            });
            task = advanced.Workspace.CopyTo(task, 0x64, retainUnknown: true);
        }

        void Mesh(ImmutableArray<byte> source, NativeBattleActionMatrix world, uint flags)
        {
            Append(NativeBattleModulePackets.EmitBatch572200(source, 0, default, NativeBattleGfMatrices.Compose56C2F0(view, world), projection, new(flags, 0, 0, null, null, 0, 17, 2), colors));
        }
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        int CountVertices(int actor)
        {
            var data = VertexInputs(actor);
            return NativeBattleStorm11Vertices.Count69EEE0(data.Body, data.Mask) + NativeBattleStorm11Vertices.Count69EEE0(data.Linked, uint.MaxValue);
        }
        NativeBattleVector SampleVertex(int actor, int vertex)
        {
            var data = VertexInputs(actor);
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            ImmutableArray<NativeBattleActionMatrix> body = NativeBattlePoseMatrices.ModelGroupMatrices50F190(graphics.Models[actor], data.Source, identity);
            if (NativeBattleStorm11Vertices.Find6CE880(data.Body, data.Mask, body, ref vertex, out NativeBattleVector point))
            {
                return point;
            }
            if (data.Linked.IsEmpty)
            {
                throw new InvalidDataException("Native Storm Breath vertex selection exhausted its actual model groups.");
            }
            ImmutableArray<NativeBattleActionMatrix> linked = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(graphics.Models[actor], data.LinkedSource!, identity);
            if (!NativeBattleStorm11Vertices.Find6CE880(data.Linked, uint.MaxValue, linked, ref vertex, out point))
            {
                throw new InvalidDataException("Native Storm Breath vertex selection exhausted its actual linked model groups.");
            }
            return point;
        }
        (ImmutableArray<NativeBattleRawMesh> Body, ImmutableArray<NativeBattleRawMesh> Linked, uint Mask, NativeBattlePoseSource Source, NativeBattlePoseSource? LinkedSource) VertexInputs(int actor)
        {
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
            ImmutableArray<NativeBattleRawMesh> body = resource.Geometry ?? throw new NotSupportedException("Native Storm Breath requires actual body geometry.");
            ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Storm Breath requires observed weapon geometry presence.");
            NativeBattlePoseSource source = resource.PoseSource!;
            bool shared = !linked.IsEmpty && resource.LinkedGeometryUsesBodySkeleton == true;
            if (shared)
            {
                body = body.SetItem(1, linked.Single());
                linked = [];
            }
            return (body, linked, graphics.Models[actor].SequenceState!.MeshMask124, source, resource.LinkedPoseSource);
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
        NativeBattleModulePoolTask Clear(int bank, int slot) => pools[bank].Slots[slot].ClearPayload(pools[bank].Stride - 12);
        void Put(int bank, int slot, NativeBattleModulePoolTask task) => pools[bank] = pools[bank].WriteSlot(slot, task);
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private static int Model(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0u);
        return offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Native Storm Breath model pointer exceeds its actual bank.");
    }
    private static NativeBattleModulePoolTask WriteBasis(NativeBattleModulePoolTask task, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x44 + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task;
    }
    private static NativeBattleActionMatrix ReadBasis(NativeBattleModulePoolTask task)
    {
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => Word(task, 0x44 + index * 2)).ToImmutableArray();
        return new(rotation, 0, 0, 0, 4096);
    }
}
