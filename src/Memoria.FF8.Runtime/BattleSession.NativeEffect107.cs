using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private const uint Effect107Arena = 0x20DFAB8;

    private NativeBattleGraphicsState EnterNativeEffect107(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleEffect107Resources resources = effect.Binding.Effect107Resources ?? throw new NotSupportedException("Native6D1310 requires its authenticated original resources.");
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521BA8, 0x2521B40, 20, 1);
        (root, _) = root.Append(0x6D13B0);
        root = root.WriteSlot(0, root.Slots[0].ClearPayload(8));
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory!;
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native6D1340 requires its real texture upload queue.");
            textures = textures.Register5099A0(resources.Animation);
            NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native6D1320 requires its actual mag106.tim allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
            NativeEffectMemory = memory with { Textures = textures };
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native6D1340 requires its actual camera owner.")).RegisterControlResource(textures.Animation);
        }
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = root.DescriptorAddress, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeEffect107(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleEffect107Resources resources = effect.Binding.Effect107Resources ?? throw new NotSupportedException("Native6D13B0 requires its authenticated effect bank.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native effect107 requires the original55CBD2 CRT random owner.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        if (pools.Length is not (1 or 5) || pools[0].DescriptorAddress != 0x2521BA8 || pools[0].Stride != 20 || pools[0].Slots.Length != 1)
        {
            throw new InvalidDataException("Native6D13B0 lost its actual task banks.");
        }
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        if (root.Callback != 0x6D13B0)
        {
            throw new InvalidDataException("Native effect107 root lost its actual callback.");
        }
        effect = effect with { DrawArenaOffset = root.ReadDword(16) != 0 ? 0x21C8 : 0x121C8 };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        bool canComplete = true;
        if (Effect107Word(root, 12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x2521B98, Effect107Arena, 664, 3), NativeBattleEffectTaskPool.Initialize(0x2521B78, Effect107Arena + 0x7C8, 28, 64), NativeBattleEffectTaskPool.Initialize(0x2521B88, Effect107Arena + 0xEC8, 32, 64), NativeBattleEffectTaskPool.Initialize(0x2521B68, Effect107Arena + 0x16C8, 28, 64)];
            }
            int sequence = root.ReadByte(14);
            if (sequence <= nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C].ReadByte(17))
            {
                NativeBattleActionHeaderSlot header = Effect107Header(sequence);
                int contact = header.FirstContactIndex ?? throw new InvalidDataException("Native6D13B0 requires its physical contact pointer.");
                int model = nativeActionPackets.ReadContact(contact).Primary.ActorIndex;
                uint modelAddress = checked(0x1D972C0u + (uint)model * 156);
                if (pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.ReadDword(20) == modelAddress))
                {
                    canComplete = false;
                    root = root.WriteWord(12, 0);
                }
                else
                {
                    (pools[1], int slot) = pools[1].Append(0x6D1640);
                    NativeBattleModulePoolTask target = pools[1].Slots[slot].ClearPayload(652);
                    target = target.WriteWord(14, (ushort)sequence).WriteDword(20, modelAddress);
                    NativeBattleVector first = ReadNativeGraphicsBone(graphics, model, 241, 0);
                    NativeBattleVector second = ReadNativeGraphicsBone(graphics, model, 240, 0);
                    target = target.WriteWord(24, unchecked((ushort)((first.X + second.X) >> 1))).WriteWord(26, unchecked((ushort)((first.Y + second.Y) >> 1))).WriteWord(28, unchecked((ushort)((first.Z + second.Z) >> 1)));
                    NativeBattleGfAnimationWorkspace workspace = new(target.ReadRawPayloadSpan().Slice(20, 632).ToArray().ToImmutableArray());
                    workspace = workspace.Initialize7016B0(resources.Hierarchy, 632);
                    target = StoreWorkspace(target, workspace);
                    pools[1] = pools[1].WriteSlot(slot, target);
                    root = root.WriteByte(14, unchecked((byte)(sequence + 1)));
                }
            }
        }
        int survivors = 0;
        if (root.ReadByte(15) != 0)
        {
            for (int index = 1; index < 5; index++)
            {
                int current = index;
                NativeBattleEffectTaskStep step = pools[index].Advance((pool, slot) =>
                {
                    pools[current] = pool;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    int result = task.Callback switch
                    {
                        0x6D1640 => AdvanceTarget(slot, ref task),
                        0x6D1E10 => AdvanceOrbit(ref task),
                        0x6D2990 => AdvanceBurst(ref task),
                        0x6D2910 => AdvanceSpray(ref task),
                        _ => throw new NotSupportedException("Native effect107 task selected an unrelated callback.")
                    };
                    return new(pools[current].WriteSlot(slot, task), result);
                });
                pools[index] = step.Pool;
                survivors += step.Survivors;
            }
        }
        bool complete = canComplete && root.ReadByte(15) != 0 && survivors == 0;
        if (!complete)
        {
            root = root.WriteWord(12, unchecked((ushort)((Effect107Word(root, 12) + 1) % 45)));
        }
        pools[0] = pools[0].WriteSlot(0, root);
        if (complete)
        {
            NativeBattleEffectTaskStep finished = pools[0].Advance((pool, _) => new(pool, 2));
            pools[0] = finished.Pool;
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = complete ? 0u : 0x2521BA8u } };

        NativeBattleActionHeaderSlot Effect107Header(int sequence)
        {
            int index = effect.Parameters!.HeaderIndex7C + sequence;
            if ((uint)index >= NativeBattleActionPacketsState.HeaderCapacity)
            {
                throw new InvalidDataException("Native6D13B0 sequence leaves its physical header bank.");
            }
            return nativeActionPackets.Headers[index];
        }

        int AdvanceTarget(int targetIndex, ref NativeBattleModulePoolTask task)
        {
            short frame = Effect107Word(task, 12);
            int model = Effect107Model(task.ReadDword(20));
            NativeBattleVector anchor = Effect107Vector(task, 24);
            if (frame < 13)
            {
                int angle = Roll();
                for (int particle = 0; particle < 4; particle++)
                {
                    if (Append(2, 0x6D1E10, out int slot))
                    {
                        short radius = (short)((Roll() & 511) + 2048);
                        NativeBattleModulePoolTask child = pools[2].Slots[slot].ClearPayload(16);
                        child = child.WriteDword(16, Effect107Arena + (uint)(targetIndex * 664)).WriteWord(20, unchecked((ushort)angle)).WriteWord(22, (ushort)radius).WriteWord(24, unchecked((ushort)(-radius >> 4))).WriteWord(26, unchecked((ushort)(radius >> 10)));
                        Put(2, slot, child);
                        angle += 1024;
                    }
                }
            }
            if ((uint)(frame - 20) < 6)
            {
                DrawSprite(0x12EC358, frame - 20, anchor, 8192, -2048);
            }
            if ((uint)(frame - 23) < 6)
            {
                DrawSprite(0x12EC8F8, frame - 23, anchor, 8192, -2048);
            }
            if (frame == 23)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 1, 128) };
            }
            if ((uint)(frame - 30) < 60)
            {
                int phase = frame - 30;
                int intensity = phase < 15 ? 16 - phase * 16 / 15 : phase < 30 ? (phase - 15) * 16 / 15 : (phase - 30) * 16 / 15 + 16;
                if (phase >= 30 && intensity > 31)
                {
                    intensity = 64 - intensity;
                }
                intensity = Math.Min(intensity, 31);
                NativeBattleCameraProjection projection = NativeCamera!.Projection!;
                NativeBattleModuleProjectedVertex center = NativeBattleModuleProjection.Project45FE10(projection.View!, anchor, projection);
                if (center.Depth > 0)
                {
                    short x = unchecked((short)((short)center.Screen >> 3));
                    short y = unchecked((short)((short)(center.Screen >> 16) >> 3));
                    (NativeBattleGraphicsState drawn, short radius) = DrawNativeEffect107Model(graphics with { OriginalEffect = effect }, model, x, y, Effect107Word(task, 16), intensity);
                    graphics = drawn;
                    effect = graphics.OriginalEffect!;
                    task = task.WriteWord(16, unchecked((ushort)radius));
                    Visibility(true);
                }
                else
                {
                    Visibility(false);
                }
            }
            else
            {
                Visibility(false);
            }
            if (frame >= 10)
            {
                NativeBattleGfAnimationWorkspace workspace = new(task.ReadRawPayloadSpan().Slice(20, 632).ToArray().ToImmutableArray());
                NativeBattleGfAnimationAdvance step = workspace.Advance701970(resources.Hierarchy, 0, (current, node) => AdvanceHierarchy(current, node, anchor, Effect107Arena + (uint)(targetIndex * 664 + 32)));
                task = StoreWorkspace(task, step.Workspace);
            }
            if (frame == 0)
            {
                NativeBattleModuleAnchor soundAnchor = ReadNativeModelAnchor(graphics, model);
                PublishNativeModuleSound(resources.Sound, 0x101, soundAnchor);
            }
            if (frame == 86)
            {
                int contact = Effect107Header(task.ReadWord(14)).FirstContactIndex ?? throw new InvalidDataException("Native6D1640 lost its physical contact.");
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
                graphics = graphics with { PendingContacts = pending };
            }
            if (frame >= 91)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return 0;


            void Visibility(bool hidden)
            {
                int linked = model;
                NativeBattleGraphicsModel first = graphics.Models[linked];
                graphics = graphics with { Models = graphics.Models.SetItem(linked, first with { Flags0 = hidden ? (ushort)(first.Flags0 | 4) : (ushort)(first.Flags0 & ~4) }) };
                HashSet<int> seen = [];
                while (seen.Add(linked))
                {
                    NativeBattleGraphicsModel current = graphics.Models[linked];
                    graphics = graphics with { Models = graphics.Models.SetItem(linked, current with { Flags0 = hidden ? (ushort)(current.Flags0 | 8) : (ushort)(current.Flags0 & ~8) }) };
                    int? next = current.ReadLinkedModelIndex140();
                    if (next is not int child)
                    {
                        break;
                    }
                    linked = child;
                }
            }
        }

        int AdvanceOrbit(ref NativeBattleModulePoolTask task)
        {
            NativeBattleModulePoolTask parent = Target(task.ReadDword(16));
            NativeBattleVector anchor = Effect107Vector(parent, 24);
            NativeBattleActionMatrix view = NativeCamera!.Projection!.View!;
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            int angle = Effect107Word(task, 20);
            int radius = Effect107Word(task, 22);
            (int X, int Y, int Z) cameraPoint = (unchecked(x + view.X + (NativeBattleCameraMath.Sine(angle) * radius >> 12)), unchecked(y + view.Y + (NativeBattleCameraMath.Cosine(angle) * radius >> 12)), unchecked(z + view.Z));
            DrawSprite(0x12EC7CC, task.ReadWord(12), default, 4096, 0, 1, unchecked((short)angle), cameraPoint);
            short velocity = unchecked((short)(Effect107Word(task, 24) + Effect107Word(task, 26)));
            radius = unchecked((short)(radius + velocity));
            task = task.WriteWord(24, unchecked((ushort)velocity)).WriteWord(22, unchecked((ushort)radius));
            if (radius <= 0)
            {
                return 2;
            }
            task = task.WriteWord(12, (ushort)((task.ReadWord(12) + 1) % 11));
            return 0;
        }

        int AdvanceBurst(ref NativeBattleModulePoolTask task)
        {
            NativeBattleModulePoolTask parent = TargetWorkspace(task.ReadDword(16));
            int radius = Effect107Word(parent, 32 + 18) * Effect107Word(task, 22) >> 12;
            int angle = Effect107Word(task, 20);
            NativeBattleVector local = new(unchecked((short)(NativeBattleCameraMath.Cosine(angle) * radius >> 12)), 0, unchecked((short)(NativeBattleCameraMath.Sine(angle) * radius >> 12)));
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(Effect107Vector(task, 24));
            NativeBattleVector position = NativeBattleGfMatrices.RotateShort56C4F0(rotation, local);
            NativeBattleVector origin = Effect107Vector(parent, 24);
            position = new(unchecked((short)(position.X + origin.X)), unchecked((short)(position.Y + origin.Y)), unchecked((short)(position.Z + origin.Z)));
            DrawSprite(0x12EC6D4, 1, position, 4096, -1024);
            task = task.WriteWord(20, unchecked((ushort)(angle + 8)));
            if (Effect107Word(parent, 50) < 512)
            {
                return 2;
            }
            task = task.WriteWord(12, (ushort)((task.ReadWord(12) + 1) % 9));
            return 0;
        }

        int AdvanceSpray(ref NativeBattleModulePoolTask task)
        {
            int scale = Effect107Word(task, 26);
            DrawSprite(task.ReadDword(16), task.ReadWord(12), Effect107Vector(task, 20), scale, -(scale >> 2));
            task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            return Effect107Word(task, 12) < Effect107Word(task, 14) ? 0 : 2;
        }

        NativeBattleGfAnimationWorkspace AdvanceHierarchy(NativeBattleGfAnimationWorkspace workspace, NativeBattleGfAnimationNode node, NativeBattleVector anchor, uint workspacePointer)
        {
            return AdvanceNativeEffect107Hierarchy(ref graphics, ref effect, pools, ref random, resources, workspace, node, anchor, workspacePointer);
        }

        bool Append(int pool, uint callback, out int slot)
        {
            if (pools[pool].Slots.All(task => (task.Flags & 1) != 0))
            {
                slot = -1;
                return false;
            }
            (pools[pool], slot) = pools[pool].Append(callback);
            return true;
        }
        void Put(int pool, int slot, NativeBattleModulePoolTask task) => pools[pool] = pools[pool].WriteSlot(slot, task);
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        NativeBattleModulePoolTask Target(uint address)
        {
            uint offset = unchecked(address - Effect107Arena);
            if (offset % 664 != 0 || offset / 664 >= 3)
            {
                throw new InvalidDataException("Native effect107 child lost its physical target pointer.");
            }
            return pools[1].Slots[(int)(offset / 664)];
        }
        NativeBattleModulePoolTask TargetWorkspace(uint address) => Target(checked(address - 32));
        void DrawSprite(uint address, int phase, NativeBattleVector anchor, int scale, int depth, ushort flags = 0, short rotation = 0, (int X, int Y, int Z)? cameraTranslation = null)
        {
            NativeBattleModuleResource resource = resources.Sprites.Single(sprite => sprite.Address == address);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(phase), Phase = checked((ushort)phase), Anchor = new(anchor.X, anchor.Y, anchor.Z, 0), Scale = scale, DepthOffset = depth, Direction = 4096, SpriteFlags = flags, Rotation = rotation };
            NativeBattleCameraProjection projection = NativeCamera!.Projection!;
            if (cameraTranslation is { } translated)
            {
                draw = draw with { SpriteCameraXOffset = translated.X, SpriteCameraYOffset = translated.Y, SpriteCameraZOffset = translated.Z };
                projection = projection with { View = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096) };
            }
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, frame.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = frame with { Pending = written.Ordering } };
        }
    }

    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask target, NativeBattleGfAnimationWorkspace workspace)
    {
        return target.WriteRetainedBytes(12 + (20), workspace.Data.AsSpan());
    }
    private static short Effect107Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector Effect107Vector(NativeBattleModulePoolTask task, int offset) => new(Effect107Word(task, offset), Effect107Word(task, offset + 2), Effect107Word(task, offset + 4));
    private static int Effect107Model(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0u);
        return offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Native effect107 target leaves the seven physical model rows.");
    }
}
