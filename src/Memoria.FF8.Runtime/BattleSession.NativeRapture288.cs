using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRapture288(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Rapture requires its selected effect.");
        NativeBattleRapture288Resources resources = effect.Binding.Rapture288Resources ?? throw new NotSupportedException("Native Rapture requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Rapture requires retained memory.");
        NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Rapture requires its loaded TIM.");
        (NativeBattleEffectTextureState textures, _) = (memory.Textures ?? throw new NotSupportedException("Native Rapture requires its texture queue.")).EnqueueTim505E30(new(texture.Name, texture.Data));
        textures = textures.Register5099A0(resources.Camera);
        NativeEffectMemory = memory with { Textures = textures };
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Rapture requires its camera.")).RegisterControlResource(textures.Animation);
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x2507C78, 0x2507C88, 24, 1);
        (roots, int slot) = roots.Append(0x679290);
        NativeBattleModulePoolTask task = roots.Slots[slot].WriteDword(12, 0).WriteDword(16, 0).WriteDword(20, 0);
        roots = roots.WriteSlot(slot, task);
        return graphics with { OriginalEffect = effect with { Pools = [roots], ReturnedDescriptor = 0x2507C78, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeRapture288(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Rapture requires its selected effect.");
        NativeBattleRapture288Resources resources = effect.Binding.Rapture288Resources ?? throw new NotSupportedException("Native Rapture requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Rapture requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Rapture requires its camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Rapture requires original interpolation constants.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        effect = effect with { DrawArenaOffset = root.ReadDword(20) != 0 ? 9312 : 107616 };
        root = root.WriteDword(20, root.ReadDword(20) != 0 ? 0u : 1u);
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Rapture requires its original target contact list.");
        if (root.ReadWord(12) == 1)
        {
            if (root.ReadByte(17) == 0)
            {
                root = root.WriteByte(17, 1);
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x2507C68, 0x20DFAB8, 1056, 6), NativeBattleEffectTaskPool.Initialize(0x2507C58, 0x20DFAB8 + 6336, 36, 48), NativeBattleEffectTaskPool.Initialize(0x2507C48, 0x20DFAB8 + 8064, 20, 48)];
            }
            int ordinal = root.ReadByte(16);
            if (ordinal < header.ReadByte(16))
            {
                if (Allocate(1, 0x679510, out int slot))
                {
                    NativeBattleContactPacket contact = nativeActionPackets.ReadContact(first + ordinal);
                    int actor = contact.Primary.ActorIndex;
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    NativeBattleModulePoolTask target = pools[1].Slots[slot].ClearPayload(1044);
                    target = target.WriteWord(14, unchecked((ushort)(model.Transform ?? throw new NotSupportedException("Native Rapture requires target WORD30.")).Position28.Y)).WriteByte(16, (byte)(contact.Primary.HitFlags3 & 4)).WriteDword(20, checked(0x1D972C0u + (uint)actor * 156));
                    target = Init(target, 24, 252, resources.First);
                    Put(1, slot, Init(target, 276, 780, resources.Second));
                }
                root = root.WriteByte(16, unchecked((byte)(ordinal + 1)));
            }
        }
        var flash = NativeBattleRapture288Geometry.Flash(unchecked((short)root.ReadWord(14)), resources.Colors);
        int targetSurvivors = 0;
        if (root.ReadByte(17) != 0)
        {
            NativeBattleEffectTaskStep targets = pools[1].Advance((pool, slot) =>
            {
                pools[1] = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result = Target(ref task);
                return new(pool with { Slots = pool.Slots.SetItem(slot, task) }, result);
            });
            pools[1] = targets.Pool;
            targetSurvivors = targets.Survivors;
            pools[2] = pools[2].Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                NativeBattleVector position = WindVector(task, 16);
                int scale = WindWord(task, 14);
                NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(WindVector(task, 24)), scale, scale, scale) with { X = position.X, Y = position.Y, Z = position.Z };
                matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
                NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, 88);
                NativeBattleModuleMeshTemporaryResult drawn = NativeBattleModulePackets.EmitRetainedBatch572200(temporary.Staging.Write(temporary.Address, BitConverter.GetBytes(resources.Debris.Address)).Write(temporary.Address + 28, new byte[4]), temporary.Address, resources.Debris.Address, resources.Debris.Bytes, matrix, projection, new(0, null, null, null, null, null, 17, 2), colors);
                NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 88);
                Append(drawn.Batch);
                task = task.WriteWord(24, unchecked((ushort)(WindWord(task, 24) + (sbyte)task.ReadByte(32)))).WriteWord(26, unchecked((ushort)(WindWord(task, 26) + (sbyte)task.ReadByte(33)))).WriteWord(28, unchecked((ushort)(WindWord(task, 28) + (sbyte)task.ReadByte(34)))).WriteWord(18, unchecked((ushort)(WindWord(task, 18) + WindWord(task, 22)))).WriteWord(12, unchecked((ushort)(WindWord(task, 12) + 1)));
                if (WindWord(task, 12) >= 10)
                {
                    task = task.WriteWord(14, unchecked((ushort)(scale - 256)));
                }
                return new(pool with { Slots = pool.Slots.SetItem(slot, task) }, WindWord(task, 14) < 256 ? 2 : 0);
            }).Pool;
            pools[3] = pools[3].Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int phase = WindWord(task, 18);
                NativeBattleVector point = WindVector(task, 12);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Spark, SpriteFrame = resources.Spark.ProjectedFrame571C80(phase), Phase = unchecked((ushort)phase), Anchor = new(point.X, point.Y, point.Z, 0), Scale = 4096, DepthOffset = -1024, SpriteFlags = 0 };
                NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, 180);
                NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, resources.Spark.Address, 0).Write(temporary.Address + 4, BitConverter.GetBytes((ushort)phase));
                NativeBattleModuleSpriteTemporaryResult drawn = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary.Address, draw, projection, NativeCamera!.Roll, 17, 2);
                NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 180);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, drawn.Batch, NativeBattleEffectGouraudWrites.FullDword);
                NativeEffectMemory = written.Memory;
                effect = effect with { DrawArenaOffset = written.Cursor };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
                task = task.WriteWord(14, unchecked((ushort)(point.Y + 30))).WriteWord(18, unchecked((ushort)(phase + 1)));
                return new(pool with { Slots = pool.Slots.SetItem(slot, task) }, phase + 1 >= 12 ? 2 : 0);
            }).Pool;
        }
        bool released = root.ReadByte(17) != 0 && targetSurvivors == 0;
        NativeStage = (NativeStage ?? throw new NotSupportedException("Native Rapture requires its current stage.")) with { Groups = NativeStage!.Groups.Select(group => group with { ColorDepth = unchecked((short)(released ? 0 : flash.Strength)), ColorFlags = released ? 0u : flash.Color }).ToImmutableArray() };
        if (!released)
        {
            root = root.WriteWord(12, unchecked((ushort)((WindWord(root, 12) + 1) % 10))).WriteWord(14, unchecked((ushort)(WindWord(root, 14) + 1)));
        }
        pools[0] = pools[0].WriteSlot(0, root);
        if (released)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = released ? 0u : 0x2507C78u } };

        int Target(ref NativeBattleModulePoolTask task)
        {
            int age = WindWord(task, 12);
            int actor = NativeBattleStatusMagicController.ModelIndex(task.ReadDword(20));
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Rapture requires its target transform.");
            NativeBattleVector anchor = ReadNativeDefaultPosition571400(graphics, actor);
            int scale = ((transform.Scale38 >> 1) + 2048) >> 2;
            if ((uint)(age - 4) < 26)
            {
                if (age < 8)
                {
                    for (int birth = 0; birth < 2; birth++)
                    {
                        if (!Allocate(2, 0x679C80, out int slot))
                        {
                            continue;
                        }
                        NativeBattleModulePoolTask child = pools[2].Slots[slot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)((Random() & 2047) + 2048)));
                        int angle = Random();
                        int radius = unchecked(scale * (Random() + 65536)) >> 16;
                        child = child.WriteWord(16, unchecked((ushort)(anchor.X + (radius * NativeBattleCameraMath.Sine(angle) >> 12)))).WriteWord(18, 0).WriteWord(20, unchecked((ushort)(anchor.Z + (radius * NativeBattleCameraMath.Cosine(angle) >> 12)))).WriteWord(22, unchecked((ushort)(-unchecked(scale * (Random() + 32768)) >> 18))).WriteWord(24, Random()).WriteWord(26, Random()).WriteWord(28, Random()).WriteByte(32, unchecked((byte)(Random() + 128))).WriteByte(33, unchecked((byte)(Random() + 128))).WriteByte(34, unchecked((byte)(Random() + 128)));
                        Put(2, slot, child);
                    }
                }
                NativeBattleVector point = ReadNativeGraphicsBone(graphics, actor, 241, 0);
                NativeBattleActionMatrix world = NativeBattlePandemona291Restoration.Rotation701270(-(model.Rotation14 ?? throw new NotSupportedException("Native Rapture requires target yaw."))) with { X = point.X, Y = transform.Angle36, Z = point.Z };
                Play(ref task, 24, 252, resources.First, NativeBattleGfMatrices.Compose56C2F0(view, world), scale, 0);
            }
            if ((uint)(age - 8) < 66 && (model.Flags0 & 6) == 2)
            {
                NativeBattleVector point = ReadNativeGraphicsBone(graphics, actor, 240, 0);
                NativeBattleActionMatrix world = NativeBattlePandemona291Restoration.Rotation701270(-model.Rotation14!.Value) with { X = point.X, Y = point.Y, Z = point.Z };
                NativeBattleActionMatrix context = NativeBattleGfMatrices.Compose56C2F0(view, world);
                NativeBattleVector depthPoint = NativeBattleGfMatrices.Transform460840(context, resources.DepthVector);
                Play(ref task, 276, 780, resources.Second, context, scale, unchecked(depthPoint.Z * (transform.Scale38 + 2048)) >> 15);
            }
            if (age == 0)
            {
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 });
            }
            if (age < 16 && (age & 1) == 0 && Allocate(3, 0x679D90, out int spark))
            {
                int angle = Random();
                int radius = unchecked(scale * (Random() + 65536)) >> 16;
                NativeBattleModulePoolTask child = pools[3].Slots[spark].WriteWord(12, unchecked((ushort)(anchor.X + (radius * NativeBattleCameraMath.Sine(angle) >> 12)))).WriteWord(16, unchecked((ushort)(anchor.Z + (radius * NativeBattleCameraMath.Cosine(angle) >> 12)))).WriteWord(14, unchecked((ushort)(-unchecked(scale * (Random() + 32768)) >> 15))).WriteWord(18, 0);
                Put(3, spark, child);
            }
            if ((uint)(age - 23) < 40)
            {
                short y = NativeBattleRapture288Geometry.TargetY(age, WindWord(task, 14), scale, task.ReadByte(16) != 0);
                model = model with { Transform = transform with { Position28 = transform.Position28 with { Y = y } }, Flags0 = (ushort)(model.Flags0 | 0x1000) };
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            }
            else if (age == 63)
            {
                model = model with { Transform = transform with { Position28 = transform.Position28 with { Y = WindWord(task, 14) } }, Flags0 = (ushort)(model.Flags0 & 0xEFFF) };
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                for (int index = 0; index < header.ReadByte(16); index++)
                {
                    NativeBattleContactPacket contact = nativeActionPackets.ReadContact(first + index);
                    if (contact.Primary.ActorIndex == actor)
                    {
                        graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [contact] });
                    }
                }
                graphics = graphics with { PendingContacts = pending };
                if (task.ReadByte(16) == 0)
                {
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = (ushort)(graphics.Models[actor].Flags0 | 4) }) };
                }
            }
            if (age >= 74)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return 0;
        }

        void Play(ref NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleActionMatrix context, int scale, int depth)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (offset - 12), size);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (owner, node) =>
            {
                NativeBattleRapture288Draw draw = NativeBattleRapture288Part.Draw(NativeEffectMemory!, NativeFileStaging!, source, node, context, scale, depth, projection, colors);
                NativeEffectMemory = draw.Memory;
                NativeFileStaging = draw.Staging;
                Append(draw.Batch);
                return owner;
            });
            task = Store(task, offset, advanced.Workspace);
        }
        NativeBattleModulePoolTask Init(NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source) => Store(task, offset, NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (offset - 12), size).Initialize7016B0(source, size));
        NativeBattleModulePoolTask Store(NativeBattleModulePoolTask task, int offset, NativeBattleGfAnimationWorkspace workspace)
        {
            return workspace.CopyTo(task, offset, retainUnknown: true);
        }
        ushort Random()
        {
            nativeCrtRandom = (nativeCrtRandom ?? throw new NotSupportedException("Native Rapture requires the actual CRT random stream.")).Draw(out ushort value);
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
        void Put(int bank, int slot, NativeBattleModulePoolTask task)
        {
            pools[bank] = pools[bank].WriteSlot(slot, task);
        }
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
    }
}
