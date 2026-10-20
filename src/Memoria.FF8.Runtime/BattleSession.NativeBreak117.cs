using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeBreak117(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBreak117Resources resources = effect.Binding.Break117Resources ?? throw new NotSupportedException("Native Break requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures!;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            textures = textures.Register5099A0(resources.Camera);
            NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
            NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Break requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        }
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25216A8, 0x2521660, 20, 1);
        (root, _) = root.Append(0x6C1740);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteDword(12, 0).WriteDword(16, 0)) };
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = 0x25216A8, DrawArenaOffset = null, Break117 = new(0) } };
    }

    private NativeBattleGraphicsState AdvanceNativeBreak117(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBreak117Resources resources = effect.Binding.Break117Resources ?? throw new NotSupportedException("Native Break requires its original resources.");
        int buffer = effect.Break117?.BufferOffset ?? throw new NotSupportedException("Native Break requires its executed initializer buffer.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Break requires its shared CRT stream.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        if (pools.Length is not (1 or 4) || pools[0].DescriptorAddress != 0x25216A8 || pools[0].Slots[0].Callback != 0x6C1740)
        {
            throw new InvalidDataException("Native Break lost its original task banks.");
        }
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleActionMatrix effectCamera = NativeBattleGfMatrices.Normalize67AAE0(view);
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 0x5EF0 : 0x1DEF0)) };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        if (WindWord(root, 12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x2521698, Address(0xAA0), 0x1570, 3), NativeBattleEffectTaskPool.Initialize(0x2521688, Address(0x500), 0x30, 30), NativeBattleEffectTaskPool.Initialize(0x2521678, Address(0), 0x28, 32)];
                byte[] words = new byte[512];
                for (int index = 0; index < 256; index++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(index * 2), Random());
                }
                NativeEffectMemory = NativeEffectMemory! with { Arena = NativeEffectMemory.Arena!.Write(buffer + 0x5CF0, words) };
            }
            int action = root.ReadByte(14);
            if (action <= nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C].ReadByte(17))
            {
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C + action];
                int contact = header.FirstContactIndex ?? throw new InvalidDataException("Native Break requires its original selected contact.");
                int actor = nativeActionPackets.ReadContact(contact).Primary.ActorIndex;
                uint entity = checked(0x1D972C0u + (uint)actor * 156);
                if (pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.ReadDword(24) == entity))
                {
                    root = root.WriteWord(12, 0);
                }
                else if (Allocate(1, 0x6C1950, out int slot))
                {
                    NativeBattleVector position = ReadNativeGraphicsBone(graphics, actor, 0xF1, 0);
                    NativeBattleModelTransform transform = graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Break requires its actor heightWORD24.");
                    position = position with { Y = transform.Angle36 };
                    NativeBattleModulePoolTask task = WindStore(pools[1].Slots[slot] with { Payload = new byte[0x1564].ToImmutableArray() }, 16, position).WriteWord(14, (ushort)action).WriteDword(24, entity);
                    NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x1C - 12, 0x1554));
                    Put(1, slot, PublishWorkspace(task, 0x1C, workspace.Initialize7016B0(resources.Shell, 0x1554)));
                    root = root.WriteByte(14, unchecked((byte)(action + 1)));
                }
            }
        }
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            survivors = 0;
            for (int bank = 1; bank < 4; bank++)
            {
                int selectedBank = bank;
                NativeBattleEffectTaskStep step = pools[bank].Advance((pool, slot) =>
                {
                    pools[selectedBank] = pool;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    int result = task.Callback switch
                    {
                        0x6C1950 when selectedBank == 1 => Target(ref task),
                        0x6C3440 when selectedBank == 2 => Debris(ref task),
                        0x6C3390 when selectedBank == 3 => Spark(ref task),
                        _ => throw new NotSupportedException("Native Break selected an unrelated callback.")
                    };
                    task = task with { Next = pools[selectedBank].Slots[slot].Next };
                    Put(selectedBank, slot, task);
                    return new(pools[selectedBank], result);
                });
                pools[bank] = step.Pool;
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
            int age = WindWord(root, 12) + 1;
            pools[0] = pools[0] with { Slots = pools[0].Slots.SetItem(0, root.WriteWord(12, unchecked((ushort)(age >= 20 ? 0 : age)))) };
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = survivors == 0 ? 0u : 0x25216A8u } };

        int Target(ref NativeBattleModulePoolTask task)
        {
            int actor = Actor(task.ReadDword(24));
            int age = WindWord(task, 12);
            NativeBattleVector position = WindVector(task, 16);
            int scale = ((graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Break requires its actor reachWORD26.")).Scale38 >> 2) + 1024;
            Play(ref task, position, scale);
            bool hidden = (uint)(age - 20) < 64;
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if (!hidden)
            {
                model = model with { Flags0 = (ushort)(model.Flags0 & 0xFFFB) };
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            }
            foreach (int index in NativeBattleModelRings.Walk(graphics.Models, actor))
            {
                model = graphics.Models[index];
                graphics = graphics with { Models = graphics.Models.SetItem(index, model with { Flags0 = hidden ? (ushort)(model.Flags0 | 8) : (ushort)(model.Flags0 & 0xFFF7) }) };
            }
            if ((uint)(age - 24) < 60)
            {
                model = graphics.Models[actor];
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = (ushort)(model.Flags0 | 4) }) };
                graphics = DrawNativeBreak117Body(graphics, actor, NativeBattleBreak117Body.StoneLevel(age - 24), NativeEffectMemory!.Arena!.Read(buffer + 0x5CF0, 512).ToImmutableArray());
            }
            if (age == 0)
            {
                NativeBattleVector soundPosition = ReadNativeDefaultPosition571400(graphics, actor);
                PublishNativeModuleSound(resources.Sound, 0x101, new(soundPosition.X, soundPosition.Y, soundPosition.Z, 0));
            }
            int elapsed = age - 50;
            if ((uint)elapsed < 30 && (elapsed & 1) == 0 && Allocate(2, 0x6C3440, out int debrisSlot))
            {
                NativeBattleModulePoolTask debris = pools[2].Slots[debrisSlot] with { Payload = new byte[0x24].ToImmutableArray() };
                int distance = unchecked((int)((uint)unchecked((elapsed + 40) * scale) >> 10));
                int angle = Random();
                short vx = unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * distance) >> 12));
                short vz = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * distance) >> 12));
                debris = WindStore(debris, 24, new(vx, 0, vz));
                debris = WindStore(debris, 16, new(unchecked((short)(position.X + vx)), unchecked((short)-((Random() & 255) + (distance << 5))), unchecked((short)(position.Z + vz))));
                for (int axis = 0; axis < 3; axis++)
                {
                    debris = debris.WriteWord(32 + axis * 2, Random());
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    debris = debris.WriteWord(40 + axis * 2, unchecked((ushort)((Random() & 127) - 64)));
                }
                Put(2, debrisSlot, debris);
            }
            if ((uint)elapsed < 20 && Allocate(3, 0x6C3390, out int sparkSlot))
            {
                NativeBattleModulePoolTask spark = pools[3].Slots[sparkSlot] with { Payload = new byte[0x1C].ToImmutableArray() };
                int speed = (Random() & 127) + 64;
                int angle = Random();
                short vx = unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12));
                short vz = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12));
                spark = WindStore(WindStore(WindStore(spark, 16, new(unchecked((short)(position.X + vx)), position.Y, unchecked((short)(position.Z + vz)))), 24, new(vx, 0, vz)), 32, new((short)(-vx >> 4), 0, (short)(-vz >> 4)));
                Put(3, sparkSlot, spark);
            }
            if (age == 78)
            {
                int contact = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C + WindWord(task, 14)].FirstContactIndex ?? throw new InvalidDataException("Native Break requires its completing contact.");
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            }
            if (age >= 84)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return 0;
        }

        int Debris(ref NativeBattleModulePoolTask task)
        {
            NativeBattleVector position = WindVector(task, 16);
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(WindVector(task, 32)) with { X = position.X, Y = position.Y, Z = position.Z };
            Append(NativeBattleModulePackets.EmitBatch572200(resources.Debris, 0, default, NativeBattleGfMatrices.Compose56C2F0(view, matrix), projection, new(0, null, null, null, null, null, 17, 2), colors));
            task = NativeBattleBreak117Geometry.DebrisMove(task);
            int age = WindWord(task, 12) + 1;
            task = task.WriteWord(12, unchecked((ushort)age));
            return age < 30 ? 0 : 2;
        }

        int Spark(ref NativeBattleModulePoolTask task)
        {
            NativeBattleVector position = WindVector(task, 16);
            int frame = WindWord(task, 12);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Spark, SpriteFrame = resources.Spark.ProjectedFrame571C80(frame), Phase = unchecked((ushort)frame), Anchor = new(position.X, position.Y, position.Z, 0), Scale = 4096, DepthOffset = -1024, SpriteFlags = 0 };
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, 0xB4);
            NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, resources.Spark.Address, 0).Write(temporary.Address + 4, BitConverter.GetBytes((ushort)frame));
            NativeBattleModuleSpriteTemporaryResult drawn = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary.Address, draw, projection, NativeCamera.Roll, 17, 2);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 0xB4);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, drawn.Batch, NativeBattleEffectGouraudWrites.FullDword);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            frame++;
            task = task.WriteWord(12, unchecked((ushort)frame));
            if (frame >= 14)
            {
                return 2;
            }
            task = NativeBattleBreak117Geometry.SparkMove(task);
            return 0;
        }

        void Play(ref NativeBattleModulePoolTask task, NativeBattleVector anchor, int scale)
        {
            NativeBattleGfAnimationSource source = resources.Shell;
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x1C - 12, 0x1554));
            int frame = workspace.Frame;
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) =>
            {
                if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
                {
                    return current;
                }
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
                ImmutableArray<byte> selected;
                if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
                {
                    int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                    selected = source.Data.Slice(geometry + 12 + shape * count * 8, count * 8);
                }
                else
                {
                    if (count > 64)
                    {
                        throw new InvalidDataException("Native Break exceeds its original64-vertex morph scratch.");
                    }
                    selected = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, NativeEffectMemory!.Arena!.Read(buffer + 0x4AF0, 512).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory! with { Arena = NativeEffectMemory.Arena!.Write(buffer + 0x4AF0, selected.AsSpan()) };
                    selected = selected.Slice(0, count * 8);
                }
                NativeBattleActionMatrix matrix = NativeBattleBreak117Geometry.Compose(node, anchor, scale, view, effectCamera);
                if (node.NodeIndex <= 27 && frame < 50)
                {
                    NativeBattleBreak117CutResult drawn = NativeBattleBreak117Cut.Draw(source.Data, geometry, selected, matrix, projection, -(node.Vector8.Y / 2), NativeEffectMemory!.Arena!.Read(buffer + 0x4CF0, 4096).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory! with { Arena = NativeEffectMemory.Arena!.Write(buffer + 0x4CF0, drawn.Projected.AsSpan()) };
                    Append(drawn.Packets);
                }
                else
                {
                    uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
                    flags |= (node.Header & 0x2000) != 0 ? 12u : 0;
                    flags |= node.Scalar36 != 0 ? 0xC0u : 0;
                    Append(NativeBattleModulePackets.EmitBatch572200(source.Data, geometry, selected, matrix, projection, new(flags, node.Scalar36 == 0 ? null : node.Color32, node.Scalar36, null, null, null, 17, 2), colors));
                }
                return current;
            });
            task = PublishWorkspace(task, 0x1C, step.Workspace);
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
            slot = -1;
            if (!pools[bank].Slots.Any(task => (task.Flags & 1) == 0))
            {
                return false;
            }
            (pools[bank], slot) = pools[bank].Append(callback);
            return true;
        }
        void Put(int bank, int slot, NativeBattleModulePoolTask task) => pools[bank] = pools[bank] with { Slots = pools[bank].Slots.SetItem(slot, task) };
        uint Address(int offset) => checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer + (uint)offset);
        int Actor(uint address)
        {
            uint delta = unchecked(address - 0x1D972C0u);
            return delta % 156 == 0 && delta / 156 < 7 ? (int)(delta / 156) : throw new InvalidDataException("Native Break physical actor pointer leaves its original bank.");
        }
        ushort Random()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }
}
