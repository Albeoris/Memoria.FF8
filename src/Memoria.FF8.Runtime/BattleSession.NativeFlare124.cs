using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeFlare124(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFlare124Resources resources = effect.Binding.Flare124Resources ?? throw new NotSupportedException("Native Flare requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures!;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            textures = textures.Register5099A0(resources.Camera);
            NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
            NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Flare requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        }
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x2521438, 0x25213E0, 20, 1);
        (roots, _) = roots.Append(0x6BBE40);
        roots = roots with { Slots = roots.Slots.SetItem(0, roots.Slots[0].WriteDword(12, 0).WriteDword(16, 0)) };
        return graphics with { OriginalEffect = effect with { Flare124 = new(0), Pools = [roots], ReturnedDescriptor = 0x2521438, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeFlare124(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFlare124Resources resources = effect.Binding.Flare124Resources!;
        int buffer = effect.Flare124?.BufferOffset ?? throw new NotSupportedException("Native Flare requires its executed initializer.");
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleActionMatrix billboard = NativeBattleGfMatrices.Normalize67AAE0(view);
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Flare requires its shared CRT stream.");
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 0x30BC : 0x1B0BC)) };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        int flash = 0;
        if (WindWord(root, 12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x2521428, Address(0), 0xB44, 3), NativeBattleEffectTaskPool.Initialize(0x2521418, Address(0x21CC), 44, 64)];
            }
            int action = root.ReadByte(14);
            if (action <= nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C].ReadByte(17))
            {
                int first = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C + action].FirstContactIndex ?? throw new InvalidDataException("Native Flare requires its action contact pointer.");
                int actor = nativeActionPackets.ReadContact(first).Primary.ActorIndex;
                uint entity = checked(0x1D972C0u + (uint)actor * 156);
                uint color = graphics.Models[actor].Color40 ?? throw new NotSupportedException("Native Flare requires its target color.");
                bool busy = false;
                foreach (NativeBattleModulePoolTask previous in pools[1].Slots)
                {
                    if ((previous.Flags & 1) != 0 && previous.ReadDword(16) == entity)
                    {
                        if (WindWord(previous, 12) <= 60)
                        {
                            busy = true;
                            break;
                        }
                        color = previous.ReadDword(36);
                    }
                }
                if (busy)
                {
                    root = root.WriteWord(12, 0);
                }
                else
                {
                    (pools[1], int slot) = pools[1].Append(0x6BC040);
                    NativeBattleModulePoolTask target = pools[1].Slots[slot] with { Payload = new byte[0xB38].ToImmutableArray() };
                    NativeBattleModuleAnchor position = ReadNativeModelAnchor(graphics, actor);
                    target = WindStore(WindStore(target.WriteWord(14, (ushort)action).WriteDword(16, entity).WriteDword(36, color).WriteWord(26, unchecked((ushort)position.W)), 20, new(position.X, position.Y, position.Z)), 28, ReadNativeGraphicsBone(graphics, actor, 0xF0, 0));
                    target = Store(target, 44, new NativeBattleGfAnimationWorkspace(target.Payload.Slice(32, 0xA70)).Initialize7016B0(resources.Burst, 0xA70));
                    target = Store(target, 0xA9C, new NativeBattleGfAnimationWorkspace(target.Payload.Slice(0xA90, 0xA8)).Initialize7016B0(resources.Ring, 0xA8));
                    pools[1] = pools[1] with { Slots = pools[1].Slots.SetItem(slot, target) };
                    root = root.WriteByte(14, unchecked((byte)(action + 1)));
                }
            }
        }
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            survivors = 0;
            for (int bank = 1; bank <= 2; bank++)
            {
                int current = bank;
                NativeBattleEffectTaskStep step = pools[current].Advance((pool, slot) =>
                {
                    pools[current] = pool;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    int result = current == 1 ? Target(ref task) : Particle(ref task);
                    task = task with { Next = pools[current].Slots[slot].Next };
                    pools[current] = pools[current] with { Slots = pools[current].Slots.SetItem(slot, task) };
                    return new(pools[current], result);
                });
                pools[current] = step.Pool;
                survivors += step.Survivors;
            }
        }
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Flare requires its actual stage.");
        NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)flash), ColorFlags = 0 }).ToImmutableArray() };
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
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = survivors == 0 ? 0u : 0x2521438u } };

        int Target(ref NativeBattleModulePoolTask task)
        {
            int age = WindWord(task, 12);
            int actor = checked((int)((task.ReadDword(16) - 0x1D972C0u) / 156));
            NativeBattleVector anchor = WindVector(task, 20);
            if (age == 0)
            {
                PublishNativeModuleSound(resources.Sound, 1, new(anchor.X, anchor.Y, anchor.Z, 0));
            }
            flash = Math.Max(flash, NativeBattleFlare124Geometry.Flash((short)age));
            if (age == 39 || age == 41)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, age == 39 ? (short)0 : (short)1, age == 39 ? (short)96 : (short)192) };
                if (age == 41)
                {
                    flash = 1024;
                }
            }
            NativeBattleGraphicsModel model = graphics.Models[actor];
            model = model with { Flags0 = (ushort)(model.Flags0 | 0x800), Color40 = NativeBattleFlare124Geometry.Color((short)age, task.ReadDword(36), resources.Red, model.Color40!.Value) };
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            int size = model.Transform!.Scale38;
            int scale = (size >> 1) + 2048;
            task = Play(task, 44, 0xA70, resources.Burst, anchor, scale, (-2048 - size) >> 3, out int burst);
            task = Play(task, 0xA9C, 0xA8, resources.Ring, anchor with { Y = model.Transform.Angle36 }, scale, 0, out int ring);
            int elapsed = age - 40;
            if (elapsed == 40)
            {
                Visibility(actor, false);
            }
            else if ((uint)elapsed < 40)
            {
                NativeBattleModuleProjectedVertex center = NativeBattleModuleProjection.Project45FE10(view, anchor, projection);
                if (center.Depth != 0)
                {
                    int step = elapsed >= 20 ? 39 - elapsed : elapsed;
                    int amplitude = NativeBattleCameraMath.Sine((elapsed << 11) / 40);
                    graphics = DrawNativeFlare124Actor(graphics, actor, unchecked((short)(unchecked((short)center.Screen) >> 3)), unchecked((short)(unchecked((short)(center.Screen >> 16)) >> 3)), unchecked((short)task.ReadDword(40)), step, amplitude, elapsed << 9, resources, out short radius);
                    task = task.WriteDword(40, unchecked((uint)radius));
                    Visibility(actor, true);
                }
                else
                {
                    Visibility(actor, false);
                }
            }
            if (age == 80)
            {
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C + task.ReadWord(14)];
                int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Flare lost its action contacts.");
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(0, header.ReadByte(16)).Select(index => nativeActionPackets.ReadContact(first + index)).ToImmutableArray() });
                graphics = graphics with { PendingContacts = pending };
            }
            if ((uint)age < 8)
            {
                for (int index = 0; index < 6; index++)
                {
                    if (!pools[2].Slots.Any(candidate => (candidate.Flags & 1) == 0))
                    {
                        break;
                    }
                    (pools[2], int slot) = pools[2].Append(0x6BD020);
                    NativeBattleModulePoolTask particle = pools[2].Slots[slot] with { Payload = new byte[32].ToImmutableArray() };
                    short radius = unchecked((short)(scale - (scale >> 2)));
                    particle = WindStore(particle.WriteWord(12, unchecked((ushort)radius)).WriteWord(14, unchecked((ushort)radius)).WriteWord(16, unchecked((ushort)(((Random() * 20) >> 15) - age + 4))).WriteWord(24, 1200), 18, anchor);
                    NativeBattleVector angles = new(unchecked((short)Random()), unchecked((short)Random()), unchecked((short)Random()));
                    NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CD50(angles), new(0, 0, 50));
                    particle = WindStore(particle, 34, velocity);
                    particle = WindStore(particle, 26, new(unchecked((short)Random()), unchecked((short)Random()), unchecked((short)Random())));
                    pools[2] = pools[2] with { Slots = pools[2].Slots.SetItem(slot, particle) };
                }
            }
            if ((burst | ring) == 0)
            {
                model = graphics.Models[actor];
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = (ushort)(model.Flags0 & 0xF7FF) }) };
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return 0;
        }

        int Particle(ref NativeBattleModulePoolTask task)
        {
            (NativeBattleModulePoolTask prepared, int mode) = NativeBattleFlare124Geometry.PrepareParticle(task);
            task = prepared;
            if (mode == -2)
            {
                return 2;
            }
            if (mode >= 0)
            {
                NativeBattleVector direction = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CD50(WindVector(task, 26)), new(0, 0, WindWord(task, 12)));
                NativeBattleVector anchor = WindVector(task, 18);
                anchor = new(unchecked((short)(anchor.X + direction.X)), unchecked((short)(anchor.Y + direction.Y)), unchecked((short)(anchor.Z + direction.Z)));
                uint alpha = task.ReadByte(32);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Particle, SpriteFrame = resources.Particle.ProjectedFrame571C80(0), Phase = 0, Anchor = new(anchor.X, anchor.Y, anchor.Z, 0), Scale = WindWord(task, 24), DepthOffset = -(WindWord(task, 24) >> 2), SpriteFlags = (ushort)mode, SpriteColor = mode == 4 ? alpha * 0x10101u : 0x808080u };
                NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, 180);
                NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, resources.Particle.Address, (ushort)mode).Write(temporary.Address + 4, BitConverter.GetBytes((ushort)0));
                if (mode == 4)
                {
                    staging = staging.Write(temporary.Address + 28, [(byte)alpha, (byte)alpha, (byte)alpha]);
                }
                NativeBattleModuleSpriteTemporaryResult drawn = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary.Address, draw, projection, NativeCamera.Roll, 17, 2);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, drawn.Batch, NativeBattleEffectGouraudWrites.FullDword);
                NativeEffectMemory = written.Memory;
                effect = effect with { DrawArenaOffset = written.Cursor };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
                NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 180);
            }
            NativeBattleVector angles = WindVector(task, 26);
            NativeBattleVector velocity = WindVector(task, 34);
            task = WindStore(task, 26, new(unchecked((short)(angles.X + velocity.X)), unchecked((short)(angles.Y + velocity.Y)), unchecked((short)(angles.Z + velocity.Z)))).WriteWord(40, unchecked((ushort)(task.ReadWord(40) + 1)));
            return 0;
        }

        NativeBattleModulePoolTask Play(NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleVector anchor, int scale, int depth, out int alive)
        {
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(offset - 12, size));
            NativeBattleGfAnimationAdvance advance = workspace.Advance701970(source, 0, (current, node) =>
            {
                if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
                {
                    return current;
                }
                int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
                int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
                ImmutableArray<byte> vertices;
                if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
                {
                    int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                    vertices = source.Data.Slice(geometry + 12 + shape * count * 8, count * 8);
                }
                else
                {
                    NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                    vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(buffer + 0x2CCC, count * 8).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(buffer + 0x2CCC, vertices.AsSpan()) };
                    vertices = vertices.Slice(0, count * 8);
                }
                NativeBattleActionMatrix matrix = NativeBattleFlare124Geometry.Compose(node, view, billboard, anchor, scale);
                int offsetDepth = depth != 0 && node.GeometryIndex is 2 or 3 ? depth >> 2 : depth;
                NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, vertices, matrix, projection, new(node.Scalar36 == 0 ? 0x2030u : 0x20F0u, node.Color32, node.Scalar36, null, null, offsetDepth, 17, 2), definition.NativeGraphics!.Rules.SummonColors);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
                NativeEffectMemory = written.Memory;
                effect = effect with { DrawArenaOffset = written.Cursor };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
                return current;
            });
            alive = advance.Remaining;
            return Store(task, offset, advance.Workspace);
        }

        void Visibility(int actor, bool hidden)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            model = model with { Flags0 = hidden ? (ushort)(model.Flags0 | 4) : (ushort)(model.Flags0 & 0xFFFB) };
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            foreach (int linked in NativeBattleModelRings.Walk(graphics.Models, actor))
            {
                model = graphics.Models[linked];
                graphics = graphics with { Models = graphics.Models.SetItem(linked, model with { Flags0 = hidden ? (ushort)(model.Flags0 | 8) : (ushort)(model.Flags0 & 0xFFF7) }) };
            }
        }
        uint Address(int offset) => checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)(buffer + offset));
        ushort Random()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        static NativeBattleModulePoolTask Store(NativeBattleModulePoolTask task, int offset, NativeBattleGfAnimationWorkspace workspace)
        {
            byte[] bytes = task.Payload.ToArray();
            workspace.Data.AsSpan().CopyTo(bytes.AsSpan(offset - 12));
            return task with { Payload = bytes.ToImmutableArray() };
        }
    }
}
