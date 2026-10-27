using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeLevel(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native level effect requires its selected owner.");
        NativeBattleLevelResources resources = effect.Binding.LevelResources ?? throw new NotSupportedException("Native level effect requires its original resources.");
        NativeBattleLevelProfile profile = NativeBattleLevelProfile.ForEffect(effect.Binding.Effect);
        if (resources.Effect != profile.Effect || resources.Animation.Address != profile.ProgrammeAddress || resources.Animation.Data.Length != profile.ProgrammeBytes)
        {
            throw new InvalidDataException("Native level effect resources differ from its selected binding.");
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native level effect requires its retained process memory.");
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native level effect requires its original TIM.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native level effect requires its texture owner.");
        (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        textures = textures.Register5099A0(resources.Camera);
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native level effect requires its camera.")).RegisterControlResource(textures.Animation);
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(profile.RootDescriptor, profile.RootBank, 20, 1);
        (root, int slot) = root.Append(profile.RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = profile.RootDescriptor, DrawArenaOffset = null, Level = new(memory.Cursor) } };
    }

    private NativeBattleGraphicsState AdvanceNativeLevel(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native level effect requires its selected owner.");
        NativeBattleLevelResources resources = effect.Binding.LevelResources ?? throw new NotSupportedException("Native level effect requires its original resources.");
        NativeBattleLevelProfile profile = NativeBattleLevelProfile.ForEffect(effect.Binding.Effect);
        if (resources.Effect != profile.Effect || resources.Animation.Address != profile.ProgrammeAddress || resources.Animation.Data.Length != profile.ProgrammeBytes)
        {
            throw new InvalidDataException("Native level effect resources differ from its selected binding.");
        }
        int buffer = effect.Level?.BufferOffset ?? throw new NotSupportedException("Native level effect requires its retained initializer cursor.");
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleModulePoolTask root = roots.Slots[0];
        if (roots.DescriptorAddress != profile.RootDescriptor || root.Callback != profile.RootCallback)
        {
            throw new InvalidDataException("Native level effect lost its actual root callback.");
        }
        NativeBattleEffectTaskPool? targets = effect.Pools.Length > 1 ? effect.Pools[1] : null;
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native level effect requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native level effect requires its current camera matrix.");
        NativeBattleActionMatrix billboard = NativeBattleGfMatrices.Normalize67AAE0(view);
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        effect = effect with { DrawArenaOffset = checked(buffer + profile.TaskBytes + 8 + (root.ReadDword(16) != 0 ? 0 : 0x10000)) };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        if (unchecked((short)root.ReadWord(12)) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                targets = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer), checked((ushort)profile.TaskBytes), 1);
            }
            NativeBattleEffectTaskPool pool = targets ?? throw new InvalidDataException("Native level effect lost its child pool.");
            (pool, int slot) = pool.Append(profile.ChildCallback);
            NativeBattleModulePoolTask task = pool.Slots[slot].ClearPayload(profile.TaskBytes - 12);
            int first = header.FirstContactIndex ?? throw new InvalidDataException("Native level effect requires its captured contact directory.");
            int contactIndex = root.ReadByte(14);
            int actor = nativeActionPackets.ReadContact(first + contactIndex).Primary.ActorIndex;
            task = task.WriteWord(14, checked((ushort)contactIndex)).WriteDword(16, checked(0x1D972C0u + (uint)actor * 156));
            NativeBattleLevelBoundsStep bounds = MeasureLevelBounds(actor);
            NativeBattleGraphicsResource targetResource = NativeGraphicsPoseResource(graphics, actor);
            NativeBattlePoseSource pose = targetResource.PoseSource ?? throw new NotSupportedException("Native level effect requires its actual target rig.");
            NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(graphics.Models[actor], pose);
            short middle = unchecked((short)((bounds.Minimum.Y + bounds.Maximum.Y) >> 1));
            task = WindStore(task, 20, new(anchor.X, middle, anchor.Z)).WriteWord(26, unchecked((ushort)anchor.W));
            NativeBattleLevelScale scale = NativeBattleLevelNodeMatrix.BoundsScale(bounds.Minimum, bounds.Maximum);
            task = task.WriteDword(28, unchecked((uint)scale.X)).WriteDword(32, unchecked((uint)scale.Y)).WriteDword(36, unchecked((uint)scale.Z));
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x20), profile.WorkspaceBytes);
            task = PublishWorkspace(task, 0x2C, workspace.Initialize7016B0(resources.Animation, profile.WorkspaceBytes));
            targets = pool.WriteSlot(slot, task);
            root = root.WriteByte(14, unchecked((byte)(root.ReadByte(14) + 1)));
            PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 });
        }
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep children = (targets ?? throw new InvalidDataException("Native level effect has no current child pool.")).Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                if (task.Callback != profile.ChildCallback)
                {
                    throw new NotSupportedException("Native level effect selected a different child callback.");
                }
                uint pointer = task.ReadDword(16);
                uint actorOffset = unchecked(pointer - 0x1D972C0u);
                if (actorOffset % 156 != 0 || actorOffset / 156 >= graphics.Models.Length)
                {
                    throw new InvalidDataException("Native level effect child lost its actual physical actor pointer.");
                }
                int actor = checked((int)(actorOffset / 156));
                short age = unchecked((short)task.ReadWord(12));
                short scroll = profile.Effect == 272 ? unchecked((short)-age) : age;
                short scale38 = graphics.Models[actor].Transform?.Scale38 ?? throw new NotSupportedException("Native level effect requires its target's current scale WORD38.");
                int depthBias = -(int)scale38 >> 4;
                NativeBattleVector anchor = WindVector(task, 20);
                NativeBattleLevelScale scale = new(unchecked((int)task.ReadDword(28)), unchecked((int)task.ReadDword(32)), unchecked((int)task.ReadDword(36)));
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x20), profile.WorkspaceBytes);
                NativeBattleGfAnimationAdvance advance = workspace.Advance701970(resources.Animation, 0, (current, node) =>
                {
                    DrawLevelNode(node, anchor, scale, scroll, depthBias);
                    return current;
                });
                task = PublishWorkspace(task, 0x2C, advance.Workspace);
                int result = 0;
                if (advance.Remaining == 0)
                {
                    int first = header.FirstContactIndex ?? throw new InvalidDataException("Native level effect lost its captured first contact.");
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first)] });
                    result = 2;
                }
                else
                {
                    task = task.WriteWord(12, unchecked((ushort)(age + 1)));
                }
                return new(pool.WriteSlot(slot, task), result);
            });
            targets = children.Pool;
            survivors = children.Survivors;
        }
        bool completed = root.ReadByte(15) != 0 && survivors == 0;
        roots = roots.WriteSlot(0, completed ? root : root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1))));
        if (completed)
        {
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        return graphics with { OriginalEffect = effect with { Pools = targets is null ? [roots] : [roots, targets], ReturnedDescriptor = completed ? 0u : profile.RootDescriptor } };

        NativeBattleLevelBoundsStep MeasureLevelBounds(int actor)
        {
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
            NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Native level bounds require the actual body rig.");
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native level bounds require the target's physical world matrix.");
            model = NativeBattleBodyBoneMatrices.Compose5095B0(model, world);
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
            ImmutableArray<NativeBattleRawMesh> meshes = resource.Geometry ?? throw new NotSupportedException("Native level bounds require all actual body objects.");
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Native level bounds require their retained temporary."), 32);
            NativeFileStaging = allocation.Staging;
            NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(NativeFileStaging, allocation.Address, 32);
            NativeBattleLevelBoundsStep measured = NativeBattleLevelBounds.Measure(NativeEffectMemory!, temporary, bones, meshes);
            NativeEffectMemory = measured.Memory;
            NativeFileStaging = NativeBattleTemporaryMemory.PublishView(NativeFileStaging, allocation.Address, measured.Temporary32, 32);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging, 32);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, NativeBattleBodyBoneMatrices.Publish508C90(model, pose)) };
            return measured;
        }

        void DrawLevelNode(NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattleLevelScale scale, short scroll, int depthBias)
        {
            if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
            {
                return;
            }
            NativeBattleGfAnimationSource source = resources.Animation;
            int geometryIndex = unchecked((short)node.GeometryIndex);
            int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + geometryIndex * 4));
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
            ImmutableArray<byte> vertices;
            uint vertexAddress;
            if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
            {
                int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                int offset = checked(geometry + 12 + shape * count * 8);
                vertices = source.Data.Slice(offset, count * 8);
                vertexAddress = checked(source.Address + (uint)offset);
            }
            else
            {
                NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                int scratch = checked(buffer + profile.TaskBytes);
                vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(scratch, count * 8).ToImmutableArray());
                NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(scratch, vertices.AsSpan()) };
                vertices = vertices.Slice(0, count * 8);
                vertexAddress = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)scratch);
            }
            NativeBattleActionMatrix matrix = NativeBattleLevelNodeMatrix.Compose(node, anchor, scale, true, view, billboard);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Native level mesh requires its retained temporary."), 88);
            NativeFileStaging = allocation.Staging;
            NativeBattleEffectArena context = NativeBattleTemporaryMemory.ReadView(NativeFileStaging, allocation.Address, 88);
            uint meshAddress = checked(source.Address + (uint)geometry);
            context = LevelPut(context, 0, meshAddress);
            context = LevelPut(context, 4, vertexAddress);
            context = LevelPut(context, 12, unchecked((uint)(int)node.Scalar36));
            context = LevelPut(context, 28, node.Scalar36 != 0 ? 0x20F0u : 0x2030u);
            if (node.Scalar36 != 0)
            {
                context = LevelPut(context, 8, node.Color32);
            }
            NativeBattleEffectMemory memory = NativeEffectMemory!;
            memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters!, matrix, true, true, null) };
            NativeBattleStaticMeshResource mesh = new(0, meshAddress, source.Data.Slice(geometry, source.Data.Length - geometry));
            NativeBattleGfGeometryBatch batch;
            if (geometryIndex <= 1)
            {
                if (profile.Effect == 272)
                {
                    context = LevelPut(context, 24, unchecked((uint)depthBias));
                }
                context = context.Write(0x22, BitConverter.GetBytes(unchecked((ushort)((unchecked((byte)scroll) << (geometryIndex + 3)) & 0x7F))));
                context = context.Write(0x24, new byte[4]).Write(0x28, new byte[] { 0, 1, 0, 1 }).Write(0x2C, new byte[4]).Write(0x30, new byte[] { 0, 1, 0x80, 0 });
                NativeBattleLevelGlyphPacketsStep drawn = NativeBattleLevelGlyphPackets.DrawLoaded(memory, mesh, projection, context, colors, 17, 2, PackedVertex, profile.Effect == 272);
                memory = drawn.Memory;
                context = drawn.Context88;
                batch = drawn.Batch;
            }
            else
            {
                int bias = profile.Effect == 271 && geometryIndex is 2 or 3 || profile.Effect == 272 && geometryIndex == 2 ? unchecked(depthBias * 2) : profile.Effect == 272 && geometryIndex == 3 ? unchecked(depthBias * 4) : depthBias;
                context = LevelPut(context, 24, unchecked((uint)bias));
                if (profile.Effect == 272 && geometryIndex is 2 or 4)
                {
                    NativeBattleLevelPlainPacketsStep drawn = NativeBattleLevelPlainPackets.DrawLoaded(memory, mesh, projection, context, colors, 17, 2, PackedVertex);
                    memory = drawn.Memory;
                    context = drawn.Context88;
                    batch = drawn.Batch;
                }
                else
                {
                    NativeFileStaging = NativeBattleTemporaryMemory.PublishView(NativeFileStaging, allocation.Address, context, 88);
                    NativeBattleModuleMeshTemporaryResult drawn = NativeBattleModulePackets.EmitRetainedBatch7043B0(NativeFileStaging, allocation.Address, meshAddress, mesh.Bytes, matrix, projection, new(node.Scalar36 != 0 ? 0x20F0u : 0x2030u, node.Color32, node.Scalar36, null, null, bias, 17, 2), colors, offset => NativeBattleScriptedMeshPreparation.Vector(vertices.AsSpan().Slice(offset, 6).ToArray()));
                    NativeFileStaging = drawn.Staging;
                    context = NativeBattleTemporaryMemory.ReadView(NativeFileStaging, allocation.Address, 88);
                    batch = drawn.Batch;
                }
            }
            NativeEffectMemory = memory;
            NativeFileStaging = NativeBattleTemporaryMemory.PublishView(NativeFileStaging, allocation.Address, context, 88);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging, 88);

            NativeBattleEffectArena PackedVertex(int offset) => NativeBattleEffectArena.Clear().Write(0, vertices.AsSpan().Slice(offset, 8));
        }
    }

    private static NativeBattleEffectArena LevelPut(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));
}
