using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private const uint Effect311CompletionAddress = 0x20DFAB8 + 0x700 + 0x12;

    private NativeBattleGraphicsState EnterNativeEffect311(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleEffect311Resources resources = effect.Binding.Effect311Resources ?? throw new NotSupportedException("Native6652C0 requires its actual Gravija executable resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native6652F0 requires its original texture queue.");
        NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native6652D0 requires its actual mag213-310.tim allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
        textures = textures.Register5099A0(resources.Animation);
        NativeEffectMemory = memory with { Textures = textures };
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native6652F0 requires its actual camera owner.")).RegisterControlResource(textures.Animation);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25073F8, 0x25073C0, 20, 1);
        (root, _) = root.Append(0x665380);
        root = root.WriteSlot(0, root.Slots[0].ClearPayload(8));
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = root.DescriptorAddress, Gravija = new(resources.Read(0x101B0E8, 0x1DD0)), DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeEffect311(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleEffect311Resources resources = effect.Binding.Effect311Resources ?? throw new NotSupportedException("Native665380 requires the authenticated Gravija resources.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native665550 requires its actual55CBD2 random owner.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        effect = effect with { DrawArenaOffset = root.ReadDword(16) != 0 ? 0x1994 : 0x11994 };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        if (Effect107Word(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            effect = effect with { Gravija = new(NativeBattleEffect311Geometry.InitializeMesh(resources)) };
            root = root.WriteByte(15, 1);
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x25073E8, 0x20DFAB8 + 0x700, 840, 1);
            (main, _) = main.Append(0x665550);
            NativeBattleModulePoolTask task = main.Slots[0].ClearPayload(828);
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (72), 756);
            workspace = workspace.Initialize7016B0(resources.Hierarchy, 756);
            task = StoreEffect311Workspace(task, workspace);
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
            int count = header.ReadByte(16);
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native665380 requires its actual first contact.");
            if (count == 0 || count > 14)
            {
                throw new InvalidDataException("Native665380 requires its original nonempty bounded model-pointer target bank.");
            }
            task = task.WriteWord(16, (ushort)count);
            short x = 0;
            short y = 0;
            short z = 0;
            for (int index = 0; index < count; index++)
            {
                int model = nativeActionPackets.ReadContact(first + index).Primary.ActorIndex;
                task = task.WriteDword(20 + index * 4, checked(0x1D972C0u + (uint)model * 156));
                NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, model);
                x = unchecked((short)(x + anchor.X));
                y = unchecked((short)(y + anchor.Y));
                z = unchecked((short)(z + anchor.Z));
            }
            task = task.WriteWord(76, unchecked((ushort)(x / count))).WriteWord(78, unchecked((ushort)(y / count))).WriteWord(80, unchecked((ushort)(z / count)));
            main = main.WriteSlot(0, task);
            pools = [pools[0], main, NativeBattleEffectTaskPool.Initialize(0x25073D8, 0x20DFAB8, 28, 64)];
        }
        int mainSurvivors = -1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep mainStep = pools[1].Advance((pool, slot) =>
            {
                pools[1] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result = AdvanceMain(ref task);
                return new(pools[1].WriteSlot(slot, task), result);
            });
            pools[1] = mainStep.Pool;
            mainSurvivors = mainStep.Survivors;
            NativeBattleEffectTaskStep particles = pools[2].Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                DrawOrbit(ref task);
                return new(pool.WriteSlot(slot, task), Effect107Word(task, 12) >= Effect107Word(task, 14) ? 2 : 0);
            });
            pools[2] = particles.Pool;
        }
        bool complete = root.ReadByte(15) != 0 && mainSurvivors == 0;
        if (!complete)
        {
            root = root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)));
        }
        pools[0] = pools[0].WriteSlot(0, root);
        if (complete)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = complete ? 0u : 0x25073F8u } };

        int AdvanceMain(ref NativeBattleModulePoolTask task)
        {
            short frame = Effect107Word(task, 12);
            int intensity = NativeBattleEffect311Timing.StageIntensity665550(frame);
            NativeStage = (NativeStage ?? throw new NotSupportedException("Native5713E0 requires its actual stage groups.")) with { Groups = NativeStage!.Groups.Select(group => group with { ColorDepth = checked((short)intensity), ColorFlags = 0x808080 }).ToImmutableArray() };
            NativeBattleVector center = Effect107Vector(task, 76);
            if (frame < 74)
            {
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (72), 756);
                NativeBattlePilotVisualIdentity hierarchyIdentity = NativeBattlePilotVisualLifetimes.Identity(311, task);
                NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Hierarchy, 0, (current, node) =>
                {
                    DrawNativeEffect311Hierarchy(ref graphics, ref effect, resources, node, center, hierarchyIdentity with { Primitive = node.ResourceOffset, ResourceGeneration = unchecked((((long)resources.Hierarchy.Address << 32) | 84u) * 397 ^ node.GeometryIndex) });
                    return current;
                });
                task = StoreEffect311Workspace(task, advanced.Workspace);
            }
            int phase = frame - 18;
            if ((uint)phase < 56)
            {
                DrawScreen(task, phase, center);
            }
            if (frame == 0)
            {
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128 });
            }
            if ((uint)phase < 50)
            {
                if (phase == 0)
                {
                    NativeBattleEffect311ParticleInitialization seeded = NativeBattleEffect311Particles.Initialize665848(pools[2], random);
                    pools[2] = seeded.Pool;
                    random = seeded.Random;
                }
                task = task.WriteWord(14, unchecked((ushort)NativeBattleEffect311Timing.Radius6658B3(phase)));
                if (phase == 49)
                {
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
                    int first = header.FirstContactIndex ?? throw new InvalidDataException("Native506BA0 requires its actual all-target contact pointer.");
                    var pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(0, header.ReadByte(16)).Select(index => nativeActionPackets.ReadContact(first + index)).ToImmutableArray() });
                    graphics = graphics with { PendingContacts = pending };
                }
            }
            if (frame >= 74)
            {
                if (frame == 74)
                {
                    byte completion = task.ReadByte(18);
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(0x20DFAB8 + 0x1994, Effect311CompletionAddress, (address, value) =>
                    {
                        if (address != Effect311CompletionAddress)
                        {
                            throw new InvalidDataException("Native665937 changed its physical task completion BYTE.");
                        }
                        completion = value;
                    });
                    task = task.WriteByte(18, completion).WriteWord(12, 75);
                    graphics = graphics with { Auxiliary = auxiliary };
                }
                NativeStage = NativeStage! with { Groups = NativeStage!.Groups.Select(group => group with { ColorDepth = 0, ColorFlags = 0 }).ToImmutableArray() };
                return task.ReadByte(18) != 0 ? 2 : 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return 0;
        }

        void DrawScreen(NativeBattleModulePoolTask main, int phase, NativeBattleVector center)
        {
            NativeBattleCameraProjection projection = NativeCamera!.Projection!;
            NativeBattleEffect311Bounds bounds = new(short.MaxValue, short.MaxValue, -32767, -32767, 65536, false);
            for (int index = 0; index < main.ReadWord(16); index++)
            {
                bounds = NativeBattleEffect311Geometry.IncludeBounds6661D0(bounds, graphics.Models[Effect107Model(main.ReadDword(20 + index * 4))], projection.View!, projection);
            }
            int depth = unchecked(bounds.MinimumDepth - 128);
            if (!bounds.Visible || depth <= 0)
            {
                return;
            }
            short width = unchecked((short)(bounds.MaxX - bounds.MinX));
            short height = unchecked((short)(bounds.MaxY - bounds.MinY));
            short x = unchecked((short)(bounds.MinX + width / 2));
            short y = unchecked((short)(bounds.MinY + height / 2));
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(projection.View!, center, projection);
            short divisor = unchecked((short)projected.Depth);
            short minimum = unchecked((short)(98304 / divisor));
            width = Math.Max(width, minimum);
            height = Math.Max(height, minimum);
            if (phase < 8)
            {
                width = unchecked((short)((uint)(width * phase) >> 3));
                height = unchecked((short)((uint)(height * phase) >> 3));
            }
            NativeBattleActionMatrix matrix = new([width, 0, 0, 0, height, 0, 0, 0, 0], x, y, 0, 4096);
            var segments = NativeBattleEffect311Geometry.Emit665D50(effect.Gravija!.Mesh, matrix, (depth >> 2) + 17, NativeBattleEffect311Timing.ScreenColor6657B7(phase), definition.NativeGraphics!.Rules.SummonColors!, NativeBattlePilotVisualLifetimes.Identity(311, main, 0x10000) with { ResourceGeneration = 0x101B0E8 });
            foreach (var segment in segments.Segments)
            {
                Append(segment.Batch, segment.Flags20);
            }
        }

        void DrawOrbit(ref NativeBattleModulePoolTask task)
        {
            NativeBattleModulePoolTask main = pools[1].Slots[0];
            int radius = Effect107Word(main, 14) * Effect107Word(task, 18) >> 12;
            int angle = Effect107Word(task, 16);
            NativeBattleVector local = new(unchecked((short)(NativeBattleCameraMath.Cosine(angle) * radius >> 12)), 0, unchecked((short)(NativeBattleCameraMath.Sine(angle) * radius >> 12)));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CE30(Effect107Vector(task, 20)), local);
            NativeBattleVector origin = Effect107Vector(main, 76);
            NativeBattleVector point = new(unchecked((short)(rotated.X + origin.X)), unchecked((short)(rotated.Y + origin.Y)), unchecked((short)(rotated.Z + origin.Z)));
            NativeBattleModuleResource sprite = resources.Sprite;
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = sprite, SpriteFrame = sprite.ProjectedFrame571C80(1), Phase = 1, Anchor = new(point.X, point.Y, point.Z, 0), Scale = 2048, DepthOffset = -512, Direction = 4096, PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(311, task) with { ResourceGeneration = resources.Sprite.Address }, PilotContinuous = true };
            var batch = NativeBattleModulePackets.EmitSprite571DC0(draw, NativeCamera!.Projection!, NativeCamera.Roll, 17, 2);
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, frame.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = frame with { Pending = written.Ordering } };
            task = task.WriteWord(16, unchecked((ushort)(task.ReadWord(16) + 8))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        }
        void Append(NativeBattleGfGeometryBatch batch, ushort flags = 0)
        {
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, frame.Pending, batch, flags);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = frame with { Pending = written.Ordering } };
        }

    }

    private static NativeBattleModulePoolTask StoreEffect311Workspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        return workspace.CopyTo(task, 12 + (72), retainUnknown: true);
    }

    private bool TryWriteNativeEffect311Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        if (address != Effect311CompletionAddress)
        {
            return false;
        }
        NativeBattleOriginalEffectState? detached = NativeEffectMemory?.DetachedReturnedTask;
        if (detached is { Binding.Entry: 0x6652C0 })
        {
            NativeEffectMemory = NativeEffectMemory! with { DetachedReturnedTask = Write(detached) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x6652C0 } current ? current : throw new NotSupportedException("Native508660 completion lost its physical Gravija target task.");
            updated = graphics with { OriginalEffect = Write(effect) };
        }
        return true;

        NativeBattleOriginalEffectState Write(NativeBattleOriginalEffectState effect)
        {
            NativeBattleEffectTaskPool main = effect.Pools[1];
            return effect with { Pools = effect.Pools.SetItem(1, main.WriteSlot(0, main.Slots[0].WriteByte(18, value))) };
        }
    }
}
