using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeMadRush273(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Mad Rush requires its selected effect.");
        NativeBattleMadRush273Resources resources = effect.Binding.MadRush273Resources ?? throw new NotSupportedException("Mad Rush requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Mad Rush requires its retained process memory.");
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Mad Rush requires its original TIM allocation.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Mad Rush requires its texture owner.");
        (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        textures = textures.Register5099A0(resources.Camera);
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Mad Rush requires its camera.")).RegisterControlResource(textures.Animation);
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2508530, 0x25084F8, 20, 1);
        (root, int slot) = root.Append(0x68D700);
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = 0x2508530, DrawArenaOffset = null, MadRush273 = new(memory.Cursor) } };
    }

    private NativeBattleGraphicsState AdvanceNativeMadRush273(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Mad Rush requires its selected effect.");
        NativeBattleMadRush273Resources resources = effect.Binding.MadRush273Resources ?? throw new NotSupportedException("Mad Rush requires its original resources.");
        int buffer = effect.MadRush273?.BufferOffset ?? throw new NotSupportedException("Mad Rush requires its retained initializer cursor.");
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleModulePoolTask root = roots.Slots[0];
        if (roots.DescriptorAddress != 0x2508530 || root.Callback != 0x68D700)
        {
            throw new InvalidDataException("Mad Rush lost its original root pool.");
        }
        NativeBattleEffectTaskPool? targets = effect.Pools.Length > 1 ? effect.Pools[1] : null;
        NativeBattleEffectTaskPool? particles = effect.Pools.Length > 2 ? effect.Pools[2] : null;
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Mad Rush requires its projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Mad Rush requires its view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 0x1250 : 0x11250)) };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        if (unchecked((short)root.ReadWord(12)) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                targets = NativeBattleEffectTaskPool.Initialize(0x2508520, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer), 0x310, 3);
                particles = NativeBattleEffectTaskPool.Initialize(0x2508510, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer + 0x930), 0x18, 0x40);
            }
            int first = header.FirstContactIndex ?? throw new InvalidDataException("Mad Rush requires its original contact directory.");
            for (int contact = 0; contact < header.ReadByte(16); contact++)
            {
                NativeBattleEffectTaskPool pool = targets ?? throw new InvalidDataException("Mad Rush lost its target pool.");
                if (pool.Slots.All(task => (task.Flags & 1) != 0))
                {
                    continue;
                }
                (pool, int slot) = pool.Append(0x68D8B0);
                NativeBattleModulePoolTask task = pool.Slots[slot].ClearPayload(0x304);
                int actor = nativeActionPackets.ReadContact(first + contact).Primary.ActorIndex;
                NativeBattleVector anchor = ReadNativeGraphicsBone(graphics, actor, 0xF1, 0);
                task = WindStore(task.WriteWord(12, unchecked((ushort)(-contact * 2))).WriteWord(14, (ushort)contact).WriteDword(16, checked(0x1D972C0u + (uint)actor * 156)), 20, anchor);
                task = task.WriteWord(22, unchecked((ushort)(graphics.Models[actor].Transform?.Angle36 ?? throw new NotSupportedException("Mad Rush requires its target height."))));
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x10), 0x2F4);
                task = PublishWorkspace(task, 0x1C, workspace.Initialize7016B0(resources.Animation, 0x2F4));
                targets = pool.WriteSlot(slot, task);
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128, RawPan = 128 });
            }
        }
        int targetSurvivors = 1;
        int particleSurvivors = 1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep targetsStep = (targets ?? throw new InvalidDataException("Mad Rush has no target pool.")).Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                if (task.Callback != 0x68D8B0)
                {
                    throw new NotSupportedException("Mad Rush selected an unsupported target callback.");
                }
                int counter = unchecked((short)task.ReadWord(12));
                if (counter < 0)
                {
                    task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
                    return new(pool.WriteSlot(slot, task), 0);
                }
                NativeBattleVector anchor = WindVector(task, 20);
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x10), 0x2F4);
                NativeBattleGfAnimationAdvance advance = workspace.Advance701970(resources.Animation, 0, (current, node) =>
                {
                    DrawMesh(node, anchor, NativeBattleMadRush273Presentation.MeshIdentity(task, resources.Animation.Address, node.ResourceOffset));
                    return current;
                });
                task = PublishWorkspace(task, 0x1C, advance.Workspace);
                int count = NativeBattleMadRush273Particles.BirthCount(counter);
                for (int birth = 0; birth < count; birth++)
                {
                    NativeBattleEffectTaskPool current = particles ?? throw new InvalidDataException("Mad Rush lost its particle pool.");
                    if (current.Slots.All(candidate => (candidate.Flags & 1) != 0))
                    {
                        continue;
                    }
                    (current, int child) = current.Append(0x68DBE0);
                    (NativeCrtRandomState random, NativeBattleModulePoolTask particle) = NativeBattleMadRush273Particles.Initialize(nativeCrtRandom ?? throw new NotSupportedException("Mad Rush requires its current CRT stream."), current.Slots[child], anchor);
                    nativeCrtRandom = random;
                    particles = current.WriteSlot(child, particle);
                }
                int result = 0;
                if (advance.Remaining == 0)
                {
                    int first = header.FirstContactIndex ?? throw new InvalidDataException("Mad Rush lost its contact directory.");
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first + unchecked((short)task.ReadWord(14)))] });
                    result = 2;
                }
                else
                {
                    task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
                }
                return new(pool.WriteSlot(slot, task), result);
            });
            targets = targetsStep.Pool;
            targetSurvivors = targetsStep.Survivors;
            NativeBattleEffectTaskStep particlesStep = (particles ?? throw new InvalidDataException("Mad Rush has no particle pool.")).Advance((pool, slot) =>
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                if (task.Callback != 0x68DBE0)
                {
                    throw new NotSupportedException("Mad Rush selected an unsupported particle callback.");
                }
                DrawParticle(task);
                (task, int result) = NativeBattleMadRush273Particles.Advance(task);
                return new(pool.WriteSlot(slot, task), result);
            });
            particles = particlesStep.Pool;
            particleSurvivors = particlesStep.Survivors;
        }
        bool completed = root.ReadByte(15) != 0 && targetSurvivors == 0 && particleSurvivors == 0;
        roots = roots.WriteSlot(0, completed ? root : root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1))));
        if (completed)
        {
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        return graphics with { OriginalEffect = effect with { Pools = targets is null || particles is null ? [roots] : [roots, targets, particles], ReturnedDescriptor = completed ? 0u : 0x2508530u } };

        void DrawMesh(NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattlePilotVisualIdentity identity)
        {
            if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
            {
                return;
            }
            NativeBattleGfAnimationSource source = resources.Animation;
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Mad Rush requires its geometry temporary."), 88);
            NativeFileStaging = allocation.Staging;
            int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + unchecked((short)node.GeometryIndex) * 4));
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
            ImmutableArray<byte> vertices;
            uint vertexAddress;
            if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
            {
                int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                vertices = source.Data.Slice(geometry + 12 + shape * count * 8, count * 8);
                vertexAddress = checked(source.Address + (uint)(geometry + 12 + shape * count * 8));
            }
            else
            {
                NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(buffer + 0xF30, count * 8).ToImmutableArray());
                NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(buffer + 0xF30, vertices.AsSpan()) };
                vertices = vertices.Slice(0, count * 8);
                vertexAddress = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer + 0xF30);
            }
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(node.Vector16) with { X = node.Vector8.X + anchor.X, Y = node.Vector8.Y + anchor.Y, Z = node.Vector8.Z + anchor.Z };
            matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
            NativeBattleVector scale = node.Vector24;
            if (scale is not { X: 4096, Y: 4096, Z: 4096 })
            {
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
            }
            uint flags = node.Scalar36 != 0 ? 0x20C0u : 0x2000u;
            uint meshAddress = checked(source.Address + (uint)geometry);
            Store(0, meshAddress);
            Store(4, vertexAddress);
            Store(12, unchecked((uint)(int)node.Scalar36));
            Store(28, flags);
            if (node.Scalar36 != 0)
            {
                Store(8, node.Color32);
            }
            NativeBattleModuleMeshTemporaryResult drawn = NativeBattleModulePackets.EmitRetainedBatch572200(NativeFileStaging, allocation.Address, meshAddress, source.Data.Slice(geometry, source.Data.Length - geometry), matrix, projection, new(flags, node.Scalar36 != 0 ? node.Color32 : null, node.Scalar36, null, null, null, 17, 2), colors, vertices, vertexAddress, pilotIdentity: identity);
            NativeFileStaging = drawn.Staging;
            Append(drawn.Batch);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging, 88);

            void Store(uint offset, uint value)
            {
                byte[] bytes = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
                NativeFileStaging = NativeFileStaging!.Write(allocation.Address + offset, bytes);
            }
        }

        void DrawParticle(NativeBattleModulePoolTask task)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Mad Rush requires its temporary memory."), 180);
            NativeFileStaging = allocation.Staging;
            NativeEffectMemory = NativeBattleModuleSpriteSetup.Setup571BC0(NativeEffectMemory!, NativeCamera!.CapturePackedView1D97778(), WindVector(task, 16), unchecked((short)task.ReadWord(22)), unchecked((short)task.ReadWord(14)), unchecked((short)-(unchecked((short)task.ReadWord(14)) >> 2)));
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(NativeFileStaging, allocation.Address, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            byte[] source = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(source, resources.Particle.Address);
            temporary = temporary.Write(0, source);
            byte[] phase = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(phase, task.ReadWord(12));
            temporary = temporary.Write(4, phase).Write(36, new byte[2]);
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(NativeEffectMemory!, temporary, address => address == resources.Particle.Address ? resources.Particle : throw new NotSupportedException("Mad Rush selected an unsupported particle sprite."), projection, NativeCamera.Roll, pilotIdentity: NativeBattleMadRush273Presentation.ParticleIdentity(task, resources.Particle.Address));
            NativeEffectMemory = drawn.Memory;
            NativeFileStaging = NativeBattleTemporaryMemory.PublishView(NativeFileStaging, allocation.Address, drawn.Temporary180, 180);
            Append(drawn.Batch);
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging, 180);
        }

        void Append(NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleMadRush273Presentation.PreserveNativeDepths(batch);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
    }
}
