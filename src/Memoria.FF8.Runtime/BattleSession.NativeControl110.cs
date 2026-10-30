using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeControl110(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleControl110Resources resources = effect.Binding.Control110Resources ?? throw new NotSupportedException("Native control110 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleControl110State retained = memory.Control110 ?? throw new NotSupportedException("Native control110 requires its cold process owner.");
        if (resources.StaticFrame.Length != 20 || resources.StaticFrame.AsSpan().Slice(8).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new NotSupportedException("Native control110 requires its original immutable zero translation frame.");
        }
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native control110 requires its original TIM allocation.");
        (NativeBattleEffectTextureState textures, _) = memory.Textures!.EnqueueTim505E30(new(asset.Name, asset.Data));
        NativeEffectMemory = memory with { Textures = textures, Control110 = retained with { Vertices = retained.Vertices ?? resources.Stream.Slice(8, 308 * 8) } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521A40, 0x25219D8, 20, 1);
        (root, _) = root.Append(0x6CE2A0);
        root = root.WriteSlot(0, root.Slots[0].WriteDword(12, 0).WriteDword(16, 0));
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = 0x2521A40, DrawArenaOffset = null, Control110 = new(memory.Cursor) } };
    }

    private NativeBattleGraphicsState AdvanceNativeControl110(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleControl110Resources resources = effect.Binding.Control110Resources ?? throw new NotSupportedException("Native control110 requires its original resources.");
        int buffer = effect.Control110?.BufferOffset ?? throw new NotSupportedException("Native control110 requires its executed initializer.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native control110 requires its shared CRT stream.");
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 0x1E9C : 0x11E9C)) };
        root = root.WriteDword(16, root.ReadDword(16) == 0 ? 1u : 0u);
        if (WindWord(root, 12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                ImmutableArray<byte> source = NativeEffectMemory!.Control110!.Vertices ?? throw new NotSupportedException("Native control110 lost its retained EXE stream vertices.");
                ImmutableArray<byte> normalized = NativeBattleControl110Motion.Normalize(source);
                NativeEffectMemory = NativeEffectMemory with { Arena = NativeEffectMemory.Arena!.Write(buffer + 0x14FC, normalized.AsSpan()) };
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x25219F0, Address(0), 44, 2), NativeBattleEffectTaskPool.Initialize(0x2521A00, Address(0x58), 48, 16), NativeBattleEffectTaskPool.Initialize(0x2521A10, Address(0x358), 20, 16), NativeBattleEffectTaskPool.Initialize(0x2521A20, Address(0x498), 32, 128), NativeBattleEffectTaskPool.Initialize(0x2521A30, Address(0x1498), 100, 1)];
            }
            if (Allocate(5, 0x6CEA30, out int slot))
            {
                NativeBattleModulePoolTask director = Clear(5, slot);
                int headerIndex = effect.Parameters!.HeaderIndex7C;
                int count = nativeActionPackets.Headers[headerIndex].ReadByte(16);
                int contact = nativeActionPackets.Headers[headerIndex].FirstContactIndex ?? throw new InvalidDataException("Native control110 requires its original target array.");
                if (count is < 1 or > 3)
                {
                    throw new InvalidDataException("Native control110 target pointers leave their three physical slots.");
                }
                director = director.WriteWord(14, (ushort)count).WriteWord(30, 1).WriteWord(34, unchecked((ushort)((count << 12) / 60)));
                for (int index = 0; index < count; index++)
                {
                    int actor = nativeActionPackets.ReadContact(contact + index).Primary.ActorIndex;
                    director = director.WriteDword(16 + index * 4, checked(0x1D972C0u + (uint)actor * 156));
                }
                Put(5, slot, director);
                BeginTarget(slot, 0);
            }
        }
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            survivors = 0;
            for (int bank = 5; bank >= 1; bank--)
            {
                int selected = bank;
                NativeBattleEffectTaskStep step = pools[selected].Advance((pool, slot) =>
                {
                    pools[selected] = pool;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    NativeBattlePilotVisualIdentity pilotIdentity = NativeBattlePilotVisualLifetimes.Identity(110, task);
                    int result = task.Callback switch
                    {
                        0x6CEA30 => Director(ref task, slot, pilotIdentity),
                        0x6CE610 => Target(ref task),
                        0x6CE990 => SpriteTask(ref task, 0, 8, pilotIdentity),
                        0x6CF400 => SpriteTask(ref task, 1, 13, pilotIdentity),
                        0x6CF110 => Debris(ref task, pilotIdentity),
                        0x6CF280 => Ring(ref task, pilotIdentity),
                        _ => throw new NotSupportedException("Native control110 selected an unrelated callback.")
                    };
                    task = task.UpdateHeader(task.Flags, task.State, pools[selected].Slots[slot].Next, task.Callback);
                    Put(selected, slot, task);
                    return new(pools[selected], result);
                });
                pools[selected] = step.Pool;
                survivors += step.Survivors;
            }
        }
        bool completed = root.ReadByte(15) != 0 && survivors == 0;
        if (!completed)
        {
            root = root.WriteWord(12, unchecked((ushort)Math.Min(10, WindWord(root, 12) + 1)));
        }
        Put(0, 0, root);
        if (completed)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = completed ? 0u : 0x2521A40u } };

        int Director(ref NativeBattleModulePoolTask task, int slot, NativeBattlePilotVisualIdentity pilotIdentity)
        {
            int frame = WindWord(task, 12);
            task = task.WriteWord(54, unchecked((ushort)-4000)).WriteWord(56, unchecked((ushort)-4000));
            NativeBattleActionMatrix world = ReadBasis110(task);
            if ((uint)frame < 70)
            {
                if (frame == 0)
                {
                    NativeBattleVector sound = ReadVector110(resources.StaticFrame, 0);
                    PublishNativeModuleSound(resources.Sound, 0, new(sound.X, sound.Y, sound.Z, BinaryPrimitives.ReadInt16LittleEndian(resources.StaticFrame.AsSpan().Slice(6))));
                }
                ImmutableArray<byte> normalized = NativeEffectMemory!.Arena!.Read(buffer + 0x14FC, 308 * 8).ToImmutableArray();
                ImmutableArray<byte> current = NativeEffectMemory.Control110!.Vertices!.Value;
                current = NativeBattleStorm11Motion.Wobble(normalized, current, WindWord(task, 36), WindWord(task, 38));
                NativeEffectMemory = NativeEffectMemory with { Control110 = NativeEffectMemory.Control110 with { Vertices = current } };
                if (frame >= 15)
                {
                    task = task.WriteWord(32, unchecked((ushort)(WindWord(task, 32) + WindWord(task, 34))));
                    if (WindWord(task, 32) >= 4096)
                    {
                        task = task.WriteWord(32, unchecked((ushort)(WindWord(task, 32) - 4096)));
                        int next = WindWord(task, 30);
                        if (WindWord(task, 28) != next)
                        {
                            NativeBattleVector saved = Position(Actor(task.ReadDword(16 + next * 4)));
                            task = WindStore(task, 60, saved);
                            Put(5, slot, task);
                            BeginTarget(slot, next);
                        }
                        task = task.WriteWord(28, unchecked((ushort)next)).WriteWord(30, unchecked((ushort)Math.Min(next + 1, WindWord(task, 14) - 1)));
                    }
                }
                NativeBattleVector aim;
                int nextActor = Actor(task.ReadDword(16 + WindWord(task, 30) * 4));
                NativeBattleVector to = Position(nextActor);
                if (WindWord(task, 28) == 1)
                {
                    NativeBattleVector saved = WindVector(task, 60);
                    aim = new(unchecked((short)((saved.X + to.X) >> 1)), unchecked((short)((saved.Y + to.Y) >> 1)), unchecked((short)((saved.Z + to.Z) >> 1)));
                }
                else
                {
                    NativeBattleVector from = Position(Actor(task.ReadDword(16 + WindWord(task, 28) * 4)));
                    int fraction = WindWord(task, 32);
                    aim = new(unchecked((short)(from.X + ((to.X - from.X) * fraction >> 12))), unchecked((short)(from.Y + ((to.Y - from.Y) * fraction >> 12))), unchecked((short)(from.Z + ((to.Z - from.Z) * fraction >> 12))));
                }
                task = task.WriteWord(52, unchecked((ushort)aim.X));
                int dy = aim.Y - WindWord(task, 54);
                int dz = aim.Z - WindWord(task, 56);
                int distance = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dy * dy + dz * dz));
                task = task.WriteDword(40, unchecked((uint)distance)).WriteDword(44, 0).WriteDword(48, unchecked((uint)dz));
                NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(0, -dy, dz).Matrix;
                task = StoreBasis110(task, basis);
                world = NativeBattleControl110Motion.StreamMatrix(basis, distance, frame, WindVector(task, 52));
                Append(NativeBattleStorm11Geometry.Emit6CF0C0(resources.Stream, current, NativeBattleGfMatrices.Compose56C2F0(view, world), projection, frame, colors, pilotIdentity with { ResourceGeneration = 0x12E0650 }));
            }
            int elapsed = frame - 5;
            if ((uint)elapsed < 60)
            {
                ImmutableArray<byte> current = NativeEffectMemory!.Control110!.Vertices!.Value;
                int distance = unchecked((int)task.ReadDword(40));
                if (distance == 0)
                {
                    throw new DivideByZeroException("Native control110 particle velocities require the executed stream length.");
                }
                for (int index = 0; index < 308; index++)
                {
                    NativeBattleVector local = ReadVector110(current, index * 8);
                    NativeBattleVector point = NativeBattleGfMatrices.Transform460840(world, local);
                    if (point.Y < 0)
                    {
                        continue;
                    }
                    if (Random() < 0x800 && Allocate(4, 0x6CF400, out int particleSlot))
                    {
                        NativeBattleModulePoolTask particle = WindStore(Clear(4, particleSlot), 16, point).WriteWord(22, unchecked((ushort)((Random() & 0x7FF) + 0x800))).WriteWord(24, unchecked((ushort)((unchecked((int)task.ReadDword(44)) << 7) / distance))).WriteWord(26, unchecked((ushort)-50)).WriteWord(28, unchecked((ushort)((unchecked((int)task.ReadDword(48)) << 7) / distance)));
                        Put(4, particleSlot, particle);
                    }
                    if (elapsed < 50 && Random() < 0x800 && Allocate(2, 0x6CF110, out int debrisSlot))
                    {
                        NativeBattleModulePoolTask debris = WindStore(Clear(2, debrisSlot), 16, point).WriteWord(22, unchecked((ushort)((Random() & 0x7FF) + 0x800))).WriteWord(24, unchecked((ushort)((unchecked((int)task.ReadDword(44)) << 6) / distance)));
                        debris = debris.WriteWord(26, unchecked((ushort)(-128 - (Random() & 127)))).WriteWord(28, unchecked((ushort)((unchecked((int)task.ReadDword(48)) << 6) / distance)));
                        for (int axis = 0; axis < 3; axis++)
                        {
                            debris = debris.WriteWord(40 + axis * 2, unchecked((ushort)((Random() & 0x3FF) - 0x200)));
                        }
                        Put(2, debrisSlot, debris);
                    }
                }
            }
            if ((uint)frame < 37 && (frame & 3) == 0 && Allocate(3, 0x6CF280, out int ringSlot))
            {
                Put(3, ringSlot, Clear(3, ringSlot).WriteDword(16, Address(0x1498 + slot * 100)));
            }
            task = task.WriteWord(36, unchecked((ushort)(WindWord(task, 36) - 100))).WriteWord(38, unchecked((ushort)(WindWord(task, 38) + 100)));
            if (frame >= 70)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return 0;
        }

        int Target(ref NativeBattleModulePoolTask task)
        {
            if (WindWord(task, 34) != 0)
            {
                task = task.WriteWord(34, unchecked((ushort)(WindWord(task, 34) - 1)));
                return 0;
            }
            int actor = Actor(task.ReadDword(28));
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if (WindWord(task, 12) == 0)
            {
                int ordinal = WindWord(task, 32);
                if (ordinal is 0 or 1)
                {
                    graphics = SelectNativeHeaderSequence(graphics, (byte)actor, 5);
                    task = task.WriteWord(14, 10);
                }
                if (actor is 3 or 4)
                {
                    BattleCombatant combatant = GraphicsActor(graphics, (byte)actor);
                    NativeBattleAiDefinition program = combatant.NativeAi ?? throw new NotSupportedException("Native control110 actor message requires its installed enemy message bank.");
                    QueueNativeText(combatant, program, actor == 3 ? 10 : 1, 0);
                }
            }
            model = graphics.Models[actor];
            if (WindWord(task, 20) == 0)
            {
                model = model with { Flags0 = (ushort)(model.Flags0 | 8) };
            }
            else
            {
                task = task.WriteWord(20, unchecked((ushort)(WindWord(task, 20) - 1)));
            }
            if (WindWord(task, 14) == 0)
            {
                model = model with { Rotation12 = unchecked((short)((model.Rotation12 ?? throw new NotSupportedException("Native control110 requires target rotationWORD12.")) + 160)) };
            }
            else
            {
                int index = unchecked(Random() * WindWord(task, 22)) >> 16;
                int directorSlot = DirectorSlot(task.ReadDword(16));
                NativeBattleModulePoolTask director = pools[5].Slots[directorSlot];
                int distance = unchecked((int)director.ReadDword(40));
                for (int birth = 0; birth < 2; birth++)
                {
                    if (!Allocate(4, 0x6CE990, out int particleSlot))
                    {
                        continue;
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                    NativeBattleVector point = Sample110(actor, index);
                    NativeBattleModulePoolTask particle = WindStore(Clear(4, particleSlot), 16, point).WriteWord(22, unchecked((ushort)((Random() & 0x7FF) + 0xC00))).WriteWord(24, unchecked((ushort)((unchecked((int)director.ReadDword(44)) << 7) / distance))).WriteWord(26, 0).WriteWord(28, unchecked((ushort)((unchecked((int)director.ReadDword(48)) << 7) / distance)));
                    Put(4, particleSlot, particle);
                    index += WindWord(task, 22) >> 1;
                }
                task = task.WriteWord(14, unchecked((ushort)(WindWord(task, 14) - 1)));
                if (WindWord(task, 14) == 0)
                {
                    task = task.WriteWord(40, 256).WriteWord(38, unchecked((ushort)-200));
                    if (WindWord(task, 32) is 0 or 1)
                    {
                        task = task.WriteWord(36, WindWord(task, 32) == 0 ? (ushort)100 : unchecked((ushort)-100));
                    }
                }
            }
            NativeBattleVector position = model.Transform!.Position28;
            position = new(unchecked((short)(position.X + WindWord(task, 36))), unchecked((short)(position.Y + WindWord(task, 38))), unchecked((short)(position.Z + WindWord(task, 40))));
            model = model with { Transform = model.Transform with { Position28 = position }, Flags0 = (ushort)(model.Flags0 | 0x1000) };
            int counter = WindWord(task, 12) + 1;
            task = task.WriteWord(12, unchecked((ushort)counter));
            if (counter >= 50)
            {
                model = model with { Flags0 = (ushort)((model.Flags0 | 4) & 0xEFFF) };
            }
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            return counter >= 50 ? 2 : 0;
        }

        int SpriteTask(ref NativeBattleModulePoolTask task, int resource, int lifetime, NativeBattlePilotVisualIdentity pilotIdentity)
        {
            int frame = WindWord(task, 12);
            int scale = WindWord(task, 22);
            Sprite(resource, frame, WindVector(task, 16), scale, -(scale >> 2), pilotIdentity);
            task = NativeBattleStorm11Motion.MoveSprite(task, lifetime, out bool ended);
            return ended ? 2 : 0;
        }
        int Debris(ref NativeBattleModulePoolTask task, NativeBattlePilotVisualIdentity pilotIdentity)
        {
            int frame = WindWord(task, 12);
            NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(WindVector(task, 32));
            if (frame >= 22)
            {
                int scale = 4096 + ((22 - frame) << 12) / 12;
                world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, scale, scale);
            }
            NativeBattleVector position = WindVector(task, 16);
            world = world with { X = position.X, Y = position.Y, Z = position.Z };
            Append(NativeBattleModulePackets.EmitBatch572200(resources.Debris, 0, default, NativeBattleGfMatrices.Compose56C2F0(view, world), projection, new(0, null, null, null, null, null, 17, 2), colors, pilotIdentity: pilotIdentity with { ResourceGeneration = 0x12E2F40 }));
            task = NativeBattleStorm11Motion.MoveDebris6EB790(task, out bool ended);
            return ended ? 2 : 0;
        }
        int Ring(ref NativeBattleModulePoolTask task, NativeBattlePilotVisualIdentity pilotIdentity)
        {
            NativeBattleModulePoolTask director = pools[5].Slots[DirectorSlot(task.ReadDword(16))];
            short phase = WindWord(task, 14);
            short angle = unchecked((short)(WindWord(director, 38) + phase));
            short radius = unchecked((short)(NativeBattleCameraMath.Sine(phase >> 1) >> 3));
            NativeBattleVector point = new(unchecked((short)(NativeBattleCameraMath.Cosine(angle) * radius >> 12)), unchecked((short)(unchecked(-NativeBattleCameraMath.Sine(angle) * radius) >> 12)), unchecked((short)(unchecked((int)director.ReadDword(40) * phase) >> 12)));
            NativeBattleActionMatrix basis = ReadBasis110(director) with { X = WindWord(director, 52), Y = WindWord(director, 54), Z = WindWord(director, 56) };
            NativeBattleVector position = NativeBattleGfMatrices.Transform460840(basis, point);
            int scale = unchecked((short)(phase + 1024));
            NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(ReadBasis110(director), new([unchecked((short)scale), 0, 0, 0, unchecked((short)scale), 0, 0, 0, 4096], 0, 0, 0, 4096)) with { X = position.X, Y = position.Y, Z = position.Z };
            Append(NativeBattleModulePackets.EmitBatch572200(resources.Ring, 0, default, NativeBattleGfMatrices.Compose56C2F0(view, scaled), projection, new(0x30, null, null, null, null, null, 17, 2), colors, pilotIdentity: pilotIdentity with { ResourceGeneration = 0x12E2BD8 }));
            task = task.WriteWord(14, unchecked((ushort)(phase + 128)));
            return WindWord(task, 14) >= 4096 ? 2 : 0;
        }
        void BeginTarget(int directorSlot, int ordinal)
        {
            if (!Allocate(1, 0x6CE610, out int slot))
            {
                return;
            }
            NativeBattleModulePoolTask director = pools[5].Slots[directorSlot];
            uint entity = director.ReadDword(16 + ordinal * 4);
            int actor = Actor(entity);
            NativeBattleModulePoolTask task = Clear(1, slot).WriteDword(16, Address(0x1498 + directorSlot * 100)).WriteDword(28, entity).WriteWord(32, unchecked((ushort)ordinal)).WriteWord(22, unchecked((ushort)Count110(actor))).WriteWord(40, 8);
            if (ordinal is 0 or 1)
            {
                task = task.WriteWord(34, ordinal == 0 ? (ushort)10 : (ushort)2).WriteWord(20, 15);
            }
            Put(1, slot, task);
        }
        NativeBattleVector Position(int actor) => graphics.Models[actor].Transform!.Position28;
        uint Address(int offset) => checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer + (uint)offset);
        int Actor(uint address)
        {
            uint delta = unchecked(address - 0x1D972C0u);
            return delta % 156 == 0 && delta / 156 < 7 ? (int)(delta / 156) : throw new InvalidDataException("Native control110 actor pointer leaves its actual model bank.");
        }
        int DirectorSlot(uint address)
        {
            uint delta = unchecked(address - Address(0x1498));
            return delta == 0 ? 0 : throw new InvalidDataException("Native control110 child lost its singleton director address.");
        }
        ushort Random()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        bool Allocate(int bank, uint callback, out int slot)
        {
            if (!pools[bank].Slots.Any(task => (task.Flags & 1) == 0))
            {
                slot = -1;
                return false;
            }
            (pools[bank], slot) = pools[bank].Append(callback);
            return true;
        }
        NativeBattleModulePoolTask Clear(int bank, int slot) => pools[bank].Slots[slot].ClearPayload(pools[bank].Stride - 12);
        void Put(int bank, int slot, NativeBattleModulePoolTask task) => pools[bank] = pools[bank].WriteSlot(slot, task);
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        void Sprite(int index, int frame, NativeBattleVector point, int scale, int depth, NativeBattlePilotVisualIdentity pilotIdentity)
        {
            NativeBattleModuleResource resource = resources.Sprites[index];
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(frame), Phase = unchecked((ushort)frame), Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, DepthOffset = depth, SpriteFlags = 0, PilotIdentity = pilotIdentity with { ResourceGeneration = resource.Address } };
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.RgbBytes);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        int Count110(int actor)
        {
            var data = Inputs110(actor);
            return NativeBattleStorm11Vertices.Count69EEE0(data.Body, data.Mask) + NativeBattleStorm11Vertices.Count69EEE0(data.Linked, uint.MaxValue);
        }
        NativeBattleVector Sample110(int actor, int vertex)
        {
            var data = Inputs110(actor);
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.ModelGroupMatrices50F190(graphics.Models[actor], data.Source, identity);
            if (NativeBattleStorm11Vertices.Find6CE880(data.Body, data.Mask, matrices, ref vertex, out NativeBattleVector point))
            {
                return point;
            }
            if (data.Linked.IsEmpty)
            {
                throw new InvalidDataException("Native control110 exhausted its actual vertex bank.");
            }
            matrices = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(graphics.Models[actor], data.LinkedSource!, identity);
            if (!NativeBattleStorm11Vertices.Find6CE880(data.Linked, uint.MaxValue, matrices, ref vertex, out point))
            {
                throw new InvalidDataException("Native control110 exhausted its actual linked vertex bank.");
            }
            return point;
        }
        (ImmutableArray<NativeBattleRawMesh> Body, ImmutableArray<NativeBattleRawMesh> Linked, uint Mask, NativeBattlePoseSource Source, NativeBattlePoseSource? LinkedSource) Inputs110(int actor)
        {
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
            ImmutableArray<NativeBattleRawMesh> body = resource.Geometry ?? throw new NotSupportedException("Native control110 requires actual body geometry.");
            ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native control110 requires observed linked-geometry presence.");
            if (!linked.IsEmpty && resource.LinkedGeometryUsesBodySkeleton == true)
            {
                body = body.SetItem(1, linked.Single());
                linked = [];
            }
            return (body, linked, graphics.Models[actor].SequenceState!.MeshMask124, resource.PoseSource!, resource.LinkedPoseSource);
        }
    }

    private static NativeBattleVector ReadVector110(ImmutableArray<byte> bytes, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset + 4)));
    private static NativeBattleModulePoolTask StoreBasis110(NativeBattleModulePoolTask task, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(68 + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task;
    }
    private static NativeBattleActionMatrix ReadBasis110(NativeBattleModulePoolTask task) => new(Enumerable.Range(0, 9).Select(index => WindWord(task, 68 + index * 2)).ToImmutableArray(), 0, 0, 0, 4096);
}
