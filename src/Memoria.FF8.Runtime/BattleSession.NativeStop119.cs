using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeStop119(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleStop119Resources resources = effect.Binding.Stop119Resources ?? throw new NotSupportedException("Native Stop requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleEffectTextureState textures = memory.Textures!;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            textures = textures.Register5099A0(resources.Camera);
            NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
            NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Stop requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        }
        NativeEffectMemory = memory with { Textures = textures };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25215C8, 0x25215A0, 20, 1);
        (root, _) = root.Append(0x6BF070);
        root = root.WriteSlot(0, root.Slots[0].WriteDword(12, 0).WriteDword(16, 0));
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = 0x25215C8, DrawArenaOffset = null, Stop119 = new(0) } };
    }

    private NativeBattleGraphicsState AdvanceNativeStop119(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleStop119Resources resources = effect.Binding.Stop119Resources ?? throw new NotSupportedException("Native Stop requires its original resources.");
        int buffer = effect.Stop119?.BufferOffset ?? throw new NotSupportedException("Native Stop requires its executed initializer buffer.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Stop requires its actual shared CRT stream.");
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleEffectTaskPool? targets = effect.Pools.Length > 1 ? effect.Pools[1] : null;
        NativeBattleModulePoolTask root = roots.Slots[0];
        if (roots.DescriptorAddress != 0x25215C8 || root.Callback != 0x6BF070)
        {
            throw new InvalidDataException("Native Stop lost its original root bank.");
        }
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 0x866C : 0x2066C)) };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        if (unchecked((short)root.ReadWord(12)) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                targets = NativeBattleEffectTaskPool.Initialize(0x25215B8, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer), 0x504, 3);
            }
            int action = root.ReadByte(14);
            if (action <= nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C].ReadByte(17))
            {
                NativeBattleEffectTaskPool pool = targets ?? throw new InvalidDataException("Native Stop lost its target pool.");
                (pool, int slot) = pool.Append(0x6BF1B0);
                NativeBattleModulePoolTask task = pool.Slots[slot].ClearPayload(0x4F8);
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C + action];
                int contact = header.FirstContactIndex ?? throw new InvalidDataException("Native Stop requires the original selected contact.");
                int target = nativeActionPackets.ReadContact(contact).Primary.ActorIndex;
                task = WindStore(task.WriteWord(14, (ushort)action).WriteDword(16, checked(0x1D972C0u + (uint)target * 156)), 20, ReadNativeDefaultPosition571400(graphics, target));
                NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x1C - 12, 0x4E8));
                task = PublishWorkspace(task, 0x1C, workspace.Initialize7016B0(resources.Clock, 0x4E8));
                targets = pool.WriteSlot(slot, task);
                root = root.WriteByte(14, unchecked((byte)(action + 1)));
            }
        }
        int survivors = 1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep step = (targets ?? throw new InvalidDataException("Native Stop has no active target pool.")).Advance((pool, slot) =>
            {
                targets = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                if (task.Callback != 0x6BF1B0)
                {
                    throw new NotSupportedException("Native Stop selected an unrelated target callback.");
                }
                int result = Target(ref task);
                task = task.UpdateHeader(task.Flags, task.State, targets.Slots[slot].Next, task.Callback);
                targets = targets.WriteSlot(slot, task);
                return new(targets, result);
            });
            targets = step.Pool;
            survivors = step.Survivors;
        }
        roots = roots.WriteSlot(0, root);
        if (survivors == 0)
        {
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        else
        {
            int counter = unchecked((short)root.ReadWord(12)) + 1;
            roots = roots.WriteSlot(0, root.WriteWord(12, unchecked((ushort)(counter >= 20 ? 0 : counter))));
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = targets is null ? [roots] : [roots, targets], ReturnedDescriptor = survivors == 0 ? 0u : 0x25215C8u } };

        int Target(ref NativeBattleModulePoolTask task)
        {
            uint relative = unchecked(task.ReadDword(16) - 0x1D972C0u);
            if (relative % 156 != 0 || relative / 156 >= 7)
            {
                throw new InvalidDataException("Native Stop target leaves its physical model bank.");
            }
            int actor = (int)(relative / 156);
            int counter = unchecked((short)task.ReadWord(12));
            NativeBattleVector anchor = WindVector(task, 20);
            if (counter == 0)
            {
                PublishNativeModuleSound(resources.Sound, 1, new(anchor.X, anchor.Y, anchor.Z, 0));
            }
            if (counter >= 0)
            {
                NativeBattleGraphicsModel model = graphics.Models[actor];
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Stop requires its target physical scale WORD26.");
                short angle = model.Rotation14 ?? throw new NotSupportedException("Native Stop requires its target physical facing WORD0E.");
                int size = transform.Scale38 + 4096;
                int x = unchecked(anchor.X - (unchecked(NativeBattleCameraMath.Sine(angle) * size) >> 15));
                int z = unchecked(anchor.Z - (unchecked(NativeBattleCameraMath.Cosine(angle) * size) >> 15));
                NativeBattleVector position = new(unchecked((short)x), anchor.Y, unchecked((short)z));
                NativeBattleActionMatrix world = NativeBattlePandemona291Restoration.Rotation701270(unchecked((short)-angle)) with { X = x, Y = anchor.Y, Z = z };
                Play(ref task, NativeBattleGfMatrices.Compose56C2F0(view, world), world, position);
                if (counter == 50)
                {
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 0, 128) };
                }
                if (counter >= 53)
                {
                    ImmutableArray<byte> shards = NativeBattleStop119Geometry.Advance(ReadShards());
                    WriteShards(shards);
                    Append(NativeBattleStop119Geometry.Draw(shards, resources.Clock, view, projection, colors));
                }
            }
            if (counter == 53)
            {
                int contact = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C + WindWord(task, 14)].FirstContactIndex ?? throw new InvalidDataException("Native Stop requires its contact at clock break.");
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            }
            if (counter >= 70)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return 0;
        }

        void Play(ref NativeBattleModulePoolTask task, NativeBattleActionMatrix context, NativeBattleActionMatrix world, NativeBattleVector anchor)
        {
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x1C - 12, 0x4E8));
            int frame = workspace.Frame;
            NativeBattleGfAnimationSource source = resources.Clock;
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
                    if (count > 222)
                    {
                        throw new InvalidDataException("Native Stop exceeds its original222-vertex morph scratch.");
                    }
                    NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                    vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(buffer + 0xF0C, 0x6F0).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(buffer + 0xF0C, vertices.AsSpan()) };
                    vertices = vertices.Slice(0, count * 8);
                }
                int group = NativeBattleStop119Geometry.Group(node.NodeIndex);
                if ((node.Header & 0x200) == 0 && frame >= 53 && group >= 0)
                {
                    if (frame == 53)
                    {
                        if (node.GeometryIndex != NativeBattleStop119Geometry.GeometryIndex(group))
                        {
                            throw new InvalidDataException("Native Stop clock piece changed its original model owner.");
                        }
                        NativeBattleVector center = new(unchecked((short)(anchor.X + node.Vector8.X)), unchecked((short)(anchor.Y + node.Vector8.Y)), unchecked((short)(anchor.Z + node.Vector8.Z)));
                        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(world, NativeBattleStop119Geometry.Rotation(node)) with { X = anchor.X + node.Vector8.X, Y = anchor.Y + node.Vector8.Y, Z = anchor.Z + node.Vector8.Z };
                        WriteShards(NativeBattleStop119Geometry.Partition(ReadShards(), group, source.Data, geometry, vertices, matrix, center, ref random));
                    }
                    return current;
                }
                uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
                flags |= (node.Header & 0x2000) != 0 ? 12u : 0;
                flags |= node.Scalar36 != 0 ? 0xC0u : 0;
                Append(NativeBattleModulePackets.EmitBatch572200(source.Data, geometry, vertices, NativeBattleStop119Geometry.Compose(node, context), projection, new(flags, node.Scalar36 != 0 ? node.Color32 : null, node.Scalar36, null, null, null, 17, 2), colors));
                return current;
            });
            task = PublishWorkspace(task, 0x1C, advance.Workspace);
        }

        ImmutableArray<byte> ReadShards() => NativeEffectMemory!.Arena!.Read(buffer + 0x15FC, NativeBattleStop119Geometry.ShardBytes).ToImmutableArray();
        void WriteShards(ImmutableArray<byte> shards) => NativeEffectMemory = NativeEffectMemory! with { Arena = NativeEffectMemory.Arena!.Write(buffer + 0x15FC, shards.AsSpan()) };
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
    }
}
