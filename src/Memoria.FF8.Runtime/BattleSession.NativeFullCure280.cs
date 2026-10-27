using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeFullCure280(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFullCure280Resources resources = effect.Binding.FullCure280Resources ?? throw new NotSupportedException("Native Full-cure requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleFullCure280State process = memory.FullCure280 ?? throw new NotSupportedException("Native Full-cure requires its retained process state.");
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Full-cure requires its original TIM allocation.");
        (NativeBattleEffectTextureState textures, _) = memory.Textures!.EnqueueTim505E30(new(asset.Name, asset.Data));
        textures = textures.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        nativeCrtRandom = (nativeCrtRandom ?? throw new NotSupportedException("Native Full-cure requires its CRT process.")).Draw(out ushort random);
        NativeEffectMemory = memory with { Textures = textures, FullCure280 = process with { EntryRandom = random } };
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x2508018, 0x2508028, 20, 1);
        (roots, int slot) = roots.Append(0x67E7F0);
        roots = roots.WriteSlot(slot, roots.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        return graphics with { OriginalEffect = effect with { FullCure280 = new(0), Pools = [roots], ReturnedDescriptor = 0x2508018, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeFullCure280(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFullCure280Resources resources = effect.Binding.FullCure280Resources ?? throw new NotSupportedException("Native Full-cure requires its original resources.");
        NativeBattleFullCure280State process = NativeEffectMemory!.FullCure280 ?? throw new NotSupportedException("Native Full-cure requires its retained process state.");
        int buffer = effect.FullCure280?.BufferOffset ?? throw new NotSupportedException("Native Full-cure requires its executed initializer.");
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleModulePoolTask root = roots.Slots[0];
        NativeBattleEffectTaskPool? directors = effect.Pools.Length > 1 ? effect.Pools[1] : null;
        NativeBattleEffectTaskPool? targets = effect.Pools.Length > 2 ? effect.Pools[2] : null;
        NativeBattleEffectTaskPool? sparks = effect.Pools.Length > 3 ? effect.Pools[3] : null;
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 7664 : 73200)) };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        int flash = 0;
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            root = root.WriteByte(15, 1);
            directors = NativeBattleEffectTaskPool.Initialize(0x2508008, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)(buffer + 7124)), 28, 1);
            targets = NativeBattleEffectTaskPool.Initialize(0x2507FF8, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)(buffer + 320)), 2268, 3);
            sparks = NativeBattleEffectTaskPool.Initialize(0x2507FE8, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer), 20, 16);
            (directors, int slot) = directors.Append(0x67E980);
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
            int count = header.ReadByte(16);
            if (count > 3)
            {
                throw new NotSupportedException("Native Full-cure target count exceeds its physical three-pointer director.");
            }
            NativeBattleModulePoolTask director = directors.Slots[slot].WriteDword(12, 0).WriteDword(16, 0).WriteDword(20, 0).WriteDword(24, 0).WriteWord(14, (ushort)count);
            int contact = header.FirstContactIndex ?? throw new InvalidDataException("Native Full-cure requires its current target contacts.");
            for (int index = 0; index < count; index++)
            {
                director = director.WriteDword(16 + index * 4, checked(0x1D972C0u + (uint)nativeActionPackets.ReadContact(contact + index).Primary.ActorIndex * 156));
            }
            directors = directors.WriteSlot(slot, director);
        }
        int directorSurvivors = 1;
        int sparkSurvivors = 1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep directorStep = directors!.Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int age = WindWord(task, 12);
                flash = NativeBattleFullCure280Geometry.Flash((short)age);
                int index = age / 20;
                if ((uint)age < 63 && age % 20 == 0 && index < WindWord(task, 14))
                {
                    (targets, int targetSlot) = targets!.Append(0x67EA80);
                    uint actorPointer = task.ReadDword(16 + index * 4);
                    int actor = checked((int)((actorPointer - 0x1D972C0) / 156));
                    NativeBattleModulePoolTask target = targets.Slots[targetSlot].WriteDword(12, actorPointer);
                    target = WindStore(target, 16, ReadNativeGraphicsBone(graphics, actor, 0xF1, 0)).WriteWord(22, 0);
                    NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(target, 12 + (12), 2244);
                    workspace = workspace.Initialize7016B0(resources.Layout, 2244);
                    target = StoreWorkspace(target, workspace);
                    targets = targets.WriteSlot(targetSlot, target);
                    PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 });
                }
                task = task.WriteWord(12, unchecked((ushort)(age + 1)));
                bool complete = age + 1 >= 65;
                if (complete)
                {
                    flash = 0;
                }
                return new(pool with { Slots = pool.Slots.SetItem(slot, task) }, complete ? 2 : 0);
            });
            directors = directorStep.Pool;
            directorSurvivors = directorStep.Survivors;
            targets = targets!.Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (12), 2244);
                NativeBattleVector anchor = WindVector(task, 16);
                NativeBattleGfAnimationAdvance played = workspace.Advance701970(resources.Layout, 0, (current, node) =>
                {
                    DrawMesh(node, anchor);
                    return current;
                });
                task = StoreWorkspace(task, played.Workspace);
                int actor = checked((int)((task.ReadDword(12) - 0x1D972C0) / 156));
                int age = WindWord(task, 22);
                if (age >= 19 && (age & 1) == 0 && sparks!.Slots.Any(candidate => (candidate.Flags & 1) == 0))
                {
                    (sparks, int sparkSlot) = sparks.Append(0x67EF50);
                    NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Full-cure sparks require their target skeleton.");
                    NativeBattleModulePoolTask spark = WindStore(sparks.Slots[sparkSlot], 12, ReadNativeGraphicsBone(graphics, actor, checked((byte)(age % source.BoneCount)), 0)).WriteWord(18, 0);
                    sparks = sparks.WriteSlot(sparkSlot, spark);
                }
                task = task.WriteWord(22, unchecked((ushort)(age + 1)));
                if (age + 1 >= 30)
                {
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
                    int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Full-cure lost its contact owner.");
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
                }
                return new(pool with { Slots = pool.Slots.SetItem(slot, task) }, age + 1 >= 30 ? 2 : 0);
            }).Pool;
            NativeBattleEffectTaskStep sparkStep = sparks!.Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int age = WindWord(task, 18);
                NativeBattleVector anchor = WindVector(task, 12);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Spark, SpriteFrame = resources.Spark.ProjectedFrame571C80(age), Phase = (ushort)age, Anchor = new(anchor.X, anchor.Y, anchor.Z, 0), Scale = 2048, DepthOffset = -512, SpriteFlags = 0 };
                NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, 180);
                NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, resources.Spark.Address, 0).Write(temporary.Address + 4, BitConverter.GetBytes((ushort)age));
                NativeBattleModuleSpriteTemporaryResult drawn = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary.Address, draw, projection, NativeCamera.Roll, 17, 2);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, drawn.Batch, NativeBattleEffectGouraudWrites.FullDword);
                NativeEffectMemory = written.Memory;
                effect = effect with { DrawArenaOffset = written.Cursor };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
                NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 180);
                task = task.WriteWord(18, unchecked((ushort)(age + 1)));
                return new(pool with { Slots = pool.Slots.SetItem(slot, task) }, age + 1 >= 8 ? 2 : 0);
            });
            sparks = sparkStep.Pool;
            sparkSurvivors = sparkStep.Survivors;
        }
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Full-cure requires its current stage.");
        NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)flash), ColorFlags = 0 }).ToImmutableArray() };
        bool released = root.ReadByte(15) != 0 && directorSurvivors == 0 && sparkSurvivors == 0;
        if (!released)
        {
            root = root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)));
        }
        roots = roots.WriteSlot(0, root);
        if (released)
        {
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        return graphics with { OriginalEffect = effect with { Pools = directors is null ? [roots] : [roots, directors, targets!, sparks!], ReturnedDescriptor = released ? 0u : 0x2508018u } };

        NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
        {
            return workspace.CopyTo(task, 12 + (12), retainUnknown: true);
        }

        void DrawMesh(NativeBattleGfAnimationNode node, NativeBattleVector anchor)
        {
            if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
            {
                return;
            }
            int geometry = BinaryPrimitives.ReadInt32LittleEndian(resources.Layout.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
            int count = BinaryPrimitives.ReadInt32LittleEndian(resources.Layout.Data.AsSpan().Slice(geometry + 4));
            ImmutableArray<byte> vertices;
            if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
            {
                int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                vertices = resources.Layout.Data.Slice(checked(geometry + 12 + shape * count * 8), checked(count * 8));
            }
            else
            {
                NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                vertices = NativeBattleGfMorphGeometry.Morph701390(resources.Layout.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(buffer + 7152, checked(count * 8)).ToImmutableArray());
                NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(buffer + 7152, vertices.AsSpan()) };
                vertices = vertices.Slice(0, checked(count * 8));
            }
            NativeBattleActionMatrix matrix = NativeBattleFullCure280Geometry.Compose(node, view, process.Billboard, anchor);
            uint flags = node.Scalar36 == 0 ? 0x2030u : 0x20F0u;
            NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(resources.Layout.Data, geometry, vertices, matrix, projection, new(flags, node.Color32, node.Scalar36, null, null, -128, 17, 2), definition.NativeGraphics!.Rules.SummonColors);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
    }
}
