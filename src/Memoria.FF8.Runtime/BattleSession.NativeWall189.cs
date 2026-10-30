using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeWall189(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleWall189Resources resources = effect.Binding.Wall189Resources ?? throw new NotSupportedException("Native Wall requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleWall189State state = memory.Wall189 ?? throw new NotSupportedException("Native Wall requires its retained module state.");
        state.Validate();
        state = state with { Sprites = NativeBattleWall189Motion.ClearFlags(state.Sprites, 150), Meshes = NativeBattleWall189Motion.ClearFlags(state.Meshes, 100) };
        NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Wall requires its original loaded TIM.");
        NativeBattleEffectTextureState textures = memory.Textures!.Register5099A0(resources.Camera);
        (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeEffectMemory = memory with { Wall189 = state, Textures = textures };
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x2293F80, 0x2293F60, 16, 2);
        NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(0x2295C00, 0x2295C10, 36, 100);
        (roots, int root) = roots.Append(0x5BE290);
        roots = roots.WriteSlot(root, roots.Slots[root].WriteWord(12, 0));
        (particles, int child) = particles.Append(0x5BCA20);
        particles = particles.WriteSlot(child, particles.Slots[child].WriteWord(12, 0));
        return graphics with { OriginalEffect = effect with { Pools = [roots, particles], ReturnedDescriptor = 0x2293F80, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeWall189(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleWall189Resources resources = effect.Binding.Wall189Resources!;
        NativeBattleWall189State state = NativeEffectMemory!.Wall189!;
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleEffectTaskPool particles = effect.Pools[1];
        int cursor = effect.DrawArenaOffset ?? -1;
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        bool frozen = (graphics.Flags & 0x201) != 0;
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Wall requires its current target contact list.");
        int firstActor = nativeActionPackets.ReadContact(first).Primary.ActorIndex;
        NativeBattleEffectTaskStep stepped = roots.Advance((pool, slot) =>
        {
            roots = pool;
            NativeBattleModulePoolTask root = roots.Slots[slot];
            int age = WindWord(root, 12);
            int result;
            if (root.Callback == 0x5BE290)
            {
                cursor = (age & 1) == 0 ? 0 : 0xE000;
                NativeBattleEffectTaskStep children = particles.Advance((current, index) =>
                {
                    particles = current;
                    NativeBattleModulePoolTask task = particles.Slots[index];
                    int finished = Child(ref task);
                    task = task.UpdateHeader(task.Flags, task.State, particles.Slots[index].Next, task.Callback);
                    return new(particles.WriteSlot(index, task), finished);
                });
                particles = children.Pool;
                if (children.Survivors == 0)
                {
                    (roots, int outro) = roots.Append(0x5BE2F0);
                    roots = roots.WriteSlot(outro, roots.Slots[outro].WriteWord(12, 0).WriteWord(14, 0));
                }
                root = root.WriteWord(12, unchecked((ushort)(age + 1)));
                result = children.Survivors == 0 ? 2 : 0;
            }
            else if (root.Callback == 0x5BE2F0)
            {
                if (age == 1)
                {
                    uint completionAddress = checked(roots.BankAddress + (uint)slot * roots.Stride + 14);
                    byte completion = root.ReadByte(14);
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(0x2296A30, completionAddress, (address, value) =>
                    {
                        if (address != completionAddress)
                        {
                            throw new InvalidDataException("Native Wall common texture changed its task completion address.");
                        }
                        completion = value;
                    });
                    graphics = graphics with { Auxiliary = auxiliary };
                    root = root.WriteByte(14, completion);
                }
                result = root.ReadWord(14) != 0 ? 2 : 0;
                root = root.WriteWord(12, unchecked((ushort)(age + 1)));
            }
            else
            {
                throw new NotSupportedException("Native Wall root callback is unknown.");
            }
            root = root.UpdateHeader(root.Flags, root.State, roots.Slots[slot].Next, root.Callback);
            roots = roots.WriteSlot(slot, root);
            return new(roots, result);
        });
        roots = stepped.Pool;
        NativeEffectMemory = NativeEffectMemory with { Wall189 = state };
        return graphics with { OriginalEffect = effect with { Pools = [roots, particles], DrawArenaOffset = cursor, ReturnedDescriptor = stepped.Survivors == 0 ? 0u : 0x2293F80u } };

        int Child(ref NativeBattleModulePoolTask task)
        {
            return task.Callback switch
            {
                0x5BCA20 => Generator(ref task),
                0x5BDB60 => Burst(ref task),
                0x5BDCC0 => Sprites(ref task),
                0x5BDF60 => Meshes(ref task),
                0x5BCF40 => Shield(ref task),
                _ => throw new NotSupportedException("Native Wall particle callback is unknown.")
            };
        }

        int Generator(ref NativeBattleModulePoolTask task)
        {
            if (frozen)
            {
                return 0;
            }
            int age = WindWord(task, 12);
            if (age == 0)
            {
                NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(index => (effect.Parameters.TargetMask7A & (1 << index)) != 0).Select(index => graphics.Models[index].Transform?.Position28 ?? throw new NotSupportedException("Native Wall center requires every actual target position.")).ToArray());
                NativeBattleActionMatrix world = graphics.Models[firstActor].GeometryWorldMatrix ?? throw new NotSupportedException("Native Wall requires its target's actual model matrix.");
                NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(world, new(0, 0, -1500));
                center = new(unchecked((short)(center.X + offset.X)), unchecked((short)(center.Y + offset.Y)), unchecked((short)(center.Z + offset.Z)));
                task = WindStore(task, 16, center);
                (state, graphics, cursor) = CaptureNativeWall189(state, graphics, center, cursor, NativeBattlePilotVisualLifetimes.Identity(189, task));
            }
            if (age == 1)
            {
                int angle = Random() % 4096;
                NativeBattleVector center = WindVector(task, 16);
                for (int index = 0; index < 24; index++)
                {
                    (int slot, NativeBattleModulePoolTask child) = Add(0x5BDB60);
                    int direction = (angle + index * 4096 / 24) & 4095;
                    child = child.WriteWord(12, 0).WriteWord(14, unchecked((ushort)(index + Random() % (32 - index))));
                    child = WindStore(child, 16, new(unchecked((short)(center.X + (2300 * NativeBattleCameraMath.Cosine(direction) >> 12))), center.Y, unchecked((short)(center.Z + (1280 * (2300 * NativeBattleCameraMath.Sine(direction) >> 12) >> 12)))));
                    child = child.WriteWord(24, unchecked((ushort)(3072 - direction))).WriteWord(28, unchecked((ushort)(Random() % 4096 + 2048))).WriteWord(32, 0).WriteWord(34, 1450);
                    Put(slot, child);
                }
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 });
            }
            if (age == 14)
            {
                foreach (uint callback in new uint[] { 0x5BDCC0, 0x5BDF60 })
                {
                    (int slot, NativeBattleModulePoolTask child) = Add(callback);
                    Put(slot, child.WriteWord(12, 0).WriteDword(16, task.ReadDword(16)).WriteDword(20, task.ReadDword(20)));
                }
            }
            if (age == 16)
            {
                (int slot, NativeBattleModulePoolTask child) = Add(0x5BCF40);
                Put(slot, child.WriteWord(12, 0).WriteWord(16, task.ReadWord(16)).WriteWord(18, 2013).WriteWord(20, task.ReadWord(20)).WriteWord(28, 4096).WriteWord(34, 100));
            }
            if (age == 65)
            {
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                for (int index = 0; index < header.ReadByte(16); index++)
                {
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first + index)] });
                }
                graphics = graphics with { PendingContacts = pending };
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 > 70 ? 2 : 0;
        }

        int Burst(ref NativeBattleModulePoolTask task)
        {
            int delay = WindWord(task, 14);
            if (delay > 0)
            {
                if (!frozen)
                {
                    task = task.WriteWord(14, unchecked((ushort)(delay - 1)));
                }
                return 0;
            }
            short age = WindWord(task, 12);
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(0, WindWord(task, 24), 0));
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, WindWord(task, 28), WindWord(task, 32), WindWord(task, 28));
            NativeBattleVector point = WindVector(task, 16);
            matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix with { X = point.X, Y = point.Y, Z = point.Z });
            Mesh(resources.Burst, matrix, age >= 16 ? 243u : 51u, age >= 16 ? (age - 16) << 9 : null, NativeBattlePilotVisualLifetimes.Identity(189, task));
            if (frozen)
            {
                return 0;
            }
            if (age < 6)
            {
                task = task.WriteWord(32, unchecked((ushort)(WindWord(task, 32) + WindWord(task, 34))));
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 24 ? 2 : 0;
        }

        int Sprites(ref NativeBattleModulePoolTask task)
        {
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, 180);
            NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, resources.Spark.Address, 8);
            int alive = 0;
            for (int index = 0; index < 150; index++)
            {
                ImmutableArray<byte> row = Row(state.Sprites, index);
                if ((row[0] & 1) == 0)
                {
                    continue;
                }
                short frame = S(row, 4);
                short scale = S(row, 6);
                NativeBattleVector point = V(row, 8);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Spark, SpriteFrame = resources.Spark.ProjectedFrame571C80(frame), Phase = unchecked((ushort)frame), Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, DepthOffset = -(scale >> 4), SpriteFlags = 8, PilotIdentity = NativeBattleWall189Presentation.Row(task, 0x5BDCC0, index, resources.Spark.Address) };
                staging = staging.Write(temporary.Address + 4, BitConverter.GetBytes(frame));
                NativeBattleModuleSpriteTemporaryResult drawn = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary.Address, draw, projection, NativeCamera!.Roll, 17, 2);
                staging = drawn.Staging;
                Emit(NativeBattleModulePackets.CaptureSprite571DC0(drawn.Batch));
                if (!frozen)
                {
                    bool ended = BinaryPrimitives.ReadInt16LittleEndian(staging.Read(temporary.Address + 40, 2)) < 0;
                    row = NativeBattleWall189Motion.Sprite(row, ended);
                    state = state with { Sprites = SetRow(state.Sprites, index, row) };
                    if (!ended)
                    {
                        alive++;
                    }
                }
            }
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 180);
            if (frozen)
            {
                return 0;
            }
            if (task.ReadWord(12) <= 56)
            {
                for (int birth = 0; birth < 4; birth++)
                {
                    int index = Empty(state.Sprites);
                    if (index < 0)
                    {
                        break;
                    }
                    byte[] row = Row(state.Sprites, index).ToArray();
                    I(row, 0, 1);
                    W(row, 4, 0);
                    W(row, 6, Random() % 2560 + 2560);
                    CopyCenter(row, task);
                    int angle = (Random() % 24 << 12) / 24;
                    W(row, 8, S(row.ToImmutableArray(), 8) + (1800 * NativeBattleCameraMath.Cosine(angle) >> 12));
                    W(row, 12, S(row.ToImmutableArray(), 12) + (1280 * (1800 * NativeBattleCameraMath.Sine(angle) >> 12) >> 12));
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(S(row.ToImmutableArray(), 8) - WindWord(task, 16), 0, S(row.ToImmutableArray(), 12) - WindWord(task, 20));
                    int speed = Random() % 25 + 20;
                    W(row, 16, speed * direction.X >> 12);
                    W(row, 18, -100);
                    W(row, 20, speed * direction.Z >> 12);
                    state = state with { Sprites = SetRow(state.Sprites, index, row.ToImmutableArray()) };
                    NativeBattleWall189Presentation.Allocated(task, 0x5BDCC0, index);
                }
            }
            int age = WindWord(task, 12) + 1;
            task = task.WriteWord(12, unchecked((ushort)age));
            return age < 4 || alive != 0 ? 0 : 2;
        }

        int Meshes(ref NativeBattleModulePoolTask task)
        {
            NativeBattleTemporaryAllocation meshHeader = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, 88);
            NativeBattleTemporaryAllocation scratch = NativeBattleTemporaryMemory.Allocate5082B0(meshHeader.Staging, 136);
            NativeBattleFileStaging retained = PublishMatrix(scratch.Staging, scratch.Address + 40, view, false).Write(scratch.Address + 2, new byte[2]);
            int alive = 0;
            for (int index = 0; index < 100; index++)
            {
                ImmutableArray<byte> row = Row(state.Meshes, index);
                if ((row[0] & 1) == 0)
                {
                    continue;
                }
                NativeBattleVector point = V(row, 8);
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(S(row, 18), 0, S(row, 22)));
                short scale = S(row, 6);
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale) with { X = point.X, Y = point.Y, Z = point.Z };
                matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
                retained = retained.Write(scratch.Address, BitConverter.GetBytes(S(row, 18))).Write(scratch.Address + 4, BitConverter.GetBytes(S(row, 22)));
                retained = PublishMatrix(retained, scratch.Address + 8, matrix, true).Write(scratch.Address + 72, BitConverter.GetBytes((int)scale)).Write(scratch.Address + 76, BitConverter.GetBytes((int)scale)).Write(scratch.Address + 80, BitConverter.GetBytes((int)scale));
                retained = MeshAt(retained, meshHeader.Address, resources.Debris, matrix, 0, null, NativeBattleWall189Presentation.Row(task, 0x5BDF60, index, resources.Debris.Address));
                if (!frozen)
                {
                    row = NativeBattleWall189Motion.Mesh(row);
                    state = state with { Meshes = SetRow(state.Meshes, index, row) };
                    if ((row[0] & 1) != 0)
                    {
                        alive++;
                    }
                }
            }
            retained = NativeBattleTemporaryMemory.Release5082D0(retained, 136);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(retained, 88);
            if (frozen)
            {
                return 0;
            }
            if (task.ReadWord(12) <= 24)
            {
                for (int birth = 0; birth < 3; birth++)
                {
                    int index = Empty(state.Meshes);
                    if (index < 0)
                    {
                        break;
                    }
                    byte[] row = Row(state.Meshes, index).ToArray();
                    I(row, 0, 1);
                    W(row, 4, 0);
                    W(row, 6, Random() % 704 + 128);
                    CopyCenter(row, task);
                    int angle = (Random() % 24 << 12) / 24;
                    W(row, 8, S(row.ToImmutableArray(), 8) + (1800 * NativeBattleCameraMath.Cosine(angle) >> 12));
                    W(row, 10, -(Random() % 200));
                    W(row, 12, S(row.ToImmutableArray(), 12) + (1280 * (1800 * NativeBattleCameraMath.Sine(angle) >> 12) >> 12));
                    W(row, 14, -25 - Random() % 200);
                    int x = Random() % 120 + 60;
                    W(row, 16, (x & 1) != 0 ? -x : x);
                    int z = Random() % 60 + 30;
                    W(row, 20, (z & 1) != 0 ? -z : z);
                    int rotation = Random() % 4096;
                    W(row, 18, rotation);
                    W(row, 22, rotation >> 1);
                    state = state with { Meshes = SetRow(state.Meshes, index, row.ToImmutableArray()) };
                    NativeBattleWall189Presentation.Allocated(task, 0x5BDF60, index);
                }
            }
            int age = WindWord(task, 12) + 1;
            task = task.WriteWord(12, unchecked((ushort)age));
            return age < 4 || alive != 0 ? 0 : 2;
        }

        int Shield(ref NativeBattleModulePoolTask task)
        {
            short age = WindWord(task, 12);
            NativeBattleVector point = WindVector(task, 16);
            short scale = WindWord(task, 28);
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), scale, scale, scale) with { X = point.X, Y = point.Y, Z = point.Z };
            matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
            int size = age >= 20 ? 96 : 424;
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, size);
            NativeBattleModuleMeshTemporaryResult drawn;
            if (age >= 20)
            {
                drawn = NativeBattleWall189ShieldPackets.Emit64F6D0(temporary.Staging, temporary.Address, resources.Shield, matrix, projection, age, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(189, task) with { ResourceGeneration = resources.Shield.Address });
            }
            else
            {
                NativeBattlePrimitiveProcess process = NativeEffectMemory!.PrimitiveProcess!;
                uint table = checked(0x1D8E058u + (uint)process.Buffer80!.Value * 17544 + 68);
                drawn = NativeBattleWall189ClipPackets.Emit5BD1C0(temporary.Staging, temporary.Address, resources, matrix, projection, age, table, checked(0x2296A30u + (uint)cursor), NativeEffectMemory.ScriptedGraphics!, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(189, task) with { ResourceGeneration = resources.Shield.Address });
            }
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, size);
            Emit(drawn.Batch);
            if (frozen)
            {
                return 0;
            }
            task = task.WriteWord(18, age >= 20 ? (ushort)0 : unchecked((ushort)(WindWord(task, 18) - WindWord(task, 34))));
            if (age < 26)
            {
                int impulse = 40 * NativeBattleCameraMath.Sine((age << 11) / 26) >> 12;
                NativeCamera = NativeCamera!.AddViewTranslationY61B8D5(unchecked((short)((age & 1) == 0 ? -impulse : impulse)));
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 60 ? 2 : 0;
        }

        void Mesh(NativeBattleStaticMeshResource resource, NativeBattleActionMatrix matrix, uint flags, int? fade, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, 88);
            NativeBattleFileStaging staging = MeshAt(temporary.Staging, temporary.Address, resource, matrix, flags, fade, pilotIdentity);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
        }
        NativeBattleFileStaging MeshAt(NativeBattleFileStaging staging, uint address, NativeBattleStaticMeshResource resource, NativeBattleActionMatrix matrix, uint flags, int? fade, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            staging = staging.Write(address, BitConverter.GetBytes(resource.Address)).Write(address + 8, new byte[4]).Write(address + 28, BitConverter.GetBytes(flags));
            if (fade is int value)
            {
                staging = staging.Write(address + 12, BitConverter.GetBytes(value));
            }
            NativeBattleModuleMeshTemporaryResult drawn = NativeBattleModulePackets.EmitRetainedBatch572200(staging, address, resource.Address, resource.Bytes, matrix, projection, new(flags, 0, fade, null, null, null, 17, 2), colors, pilotIdentity: pilotIdentity is { } identity ? identity with { ResourceGeneration = unchecked(identity.ResourceGeneration * 397 ^ resource.Address) } : null);
            Emit(drawn.Batch);
            return drawn.Staging;
        }
        static NativeBattleFileStaging PublishMatrix(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix, bool identityPadding)
        {
            byte[] bytes = new byte[32];
            for (int index = 0; index < 9; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), matrix.Rotation[index]);
            }
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), matrix.X);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), matrix.Y);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), matrix.Z);
            staging = staging.Write(address, bytes.AsSpan(0, 18)).Write(address + 20, bytes.AsSpan(20, 12));
            return identityPadding ? staging.Write(address + 18, new byte[2]) : staging.MarkUnknown(address + 18, 2);
        }
        void Emit(NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleWall189Presentation.Depths(batch);
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(state.Packets, "2296A30", cursor, 0x1C000, graphics.PrimitiveFrame!.Pending, batch);
            state = state with { Packets = written.Arena };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            cursor = written.Cursor;
        }
        int Random()
        {
            nativeCrtRandom = (nativeCrtRandom ?? throw new NotSupportedException("Native Wall requires its actual CRT random stream.")).Draw(out ushort value);
            return value;
        }
        (int Slot, NativeBattleModulePoolTask Task) Add(uint callback)
        {
            (particles, int slot) = particles.Append(callback);
            return (slot, particles.Slots[slot]);
        }
        void Put(int slot, NativeBattleModulePoolTask task)
        {
            particles = particles.WriteSlot(slot, task);
        }
        static ImmutableArray<byte> Row(ImmutableArray<byte> bytes, int index) => bytes.Slice(index * 24, 24);
        static ImmutableArray<byte> SetRow(ImmutableArray<byte> bytes, int index, ImmutableArray<byte> row)
        {
            byte[] result = bytes.ToArray();
            row.AsSpan().CopyTo(result.AsSpan(index * 24));
            return result.ToImmutableArray();
        }
        static int Empty(ImmutableArray<byte> bytes)
        {
            for (int index = 0; index < bytes.Length / 24; index++)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(index * 24)) == 0)
                {
                    return index;
                }
            }
            return -1;
        }
        static short S(ImmutableArray<byte> row, int offset) => BinaryPrimitives.ReadInt16LittleEndian(row.AsSpan().Slice(offset));
        static NativeBattleVector V(ImmutableArray<byte> row, int offset) => new(S(row, offset), S(row, offset + 2), S(row, offset + 4));
        static void W(byte[] row, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(offset), unchecked((short)value));
        static void I(byte[] row, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(offset), value);
        static void CopyCenter(byte[] row, NativeBattleModulePoolTask task)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(8), task.ReadDword(16));
            BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(12), task.ReadDword(20));
        }
    }
}
