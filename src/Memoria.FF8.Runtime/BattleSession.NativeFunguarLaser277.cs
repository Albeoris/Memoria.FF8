using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleFunguarLaser277;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeFunguarLaser277(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFunguarLaser277Resources resources = effect.Binding.FunguarLaser277Resources ?? throw new NotSupportedException("Native Funguar Laser requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleFunguarLaser277State process = memory.FunguarLaser277 ?? throw new NotSupportedException("Native Funguar Laser requires its original module fields.");
        NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Funguar Laser requires its original TIM allocation.");
        NativeBattleEffectTextureState textures = memory.Textures!;
        (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
        textures = textures.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeEffectMemory = memory with { Textures = textures, FunguarLaser277 = process with { BufferOffset = memory.Cursor } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2508268, 0x2508210, 20, 1);
        (root, _) = root.Append(0x683FC0);
        root = root.WriteSlot(0, root.Slots[0].WriteDword(12, 0).WriteDword(16, 0));
        return graphics with { OriginalEffect = effect with { Pools = [root], ReturnedDescriptor = 0x2508268, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeFunguarLaser277(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFunguarLaser277Resources resources = effect.Binding.FunguarLaser277Resources ?? throw new NotSupportedException("Native Funguar Laser requires its original resources.");
        NativeBattleFunguarLaser277State process = NativeEffectMemory!.FunguarLaser277 ?? throw new NotSupportedException("Native Funguar Laser requires its original module fields.");
        NativeBattleEffectTaskPool[] pools = effect.Pools.ToArray();
        NativeBattleModulePoolTask root = pools[0].Slots[0];
        if (pools[0].DescriptorAddress != 0x2508268 || root.Callback != 0x683FC0)
        {
            throw new InvalidDataException("Native Funguar Laser lost its physical task bank.");
        }
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Funguar Laser requires its shared CRT stream.");
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Funguar Laser requires its physical contact pointer.");
        int buffer = process.BufferOffset;
        effect = effect with { DrawArenaOffset = checked(buffer + (root.ReadDword(16) != 0 ? 0x10AC : 0x110AC)) };
        root = root.WriteDword(16, root.ReadDword(16) != 0 ? 0u : 1u);
        if (LaserWord(root, 12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                uint address = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)buffer);
                pools = [pools[0], NativeBattleEffectTaskPool.Initialize(0x2508258, address + 0x8C0, 0x664, 1), NativeBattleEffectTaskPool.Initialize(0x2508248, address + 0x780, 0x14, 16), NativeBattleEffectTaskPool.Initialize(0x2508238, address + 0x540, 0x24, 16), NativeBattleEffectTaskPool.Initialize(0x2508228, address, 0xA8, 8)];
            }
            int action = root.ReadByte(14);
            if (action <= header.ReadByte(17))
            {
                NativeBattleActionHeaderSlot selected = nativeActionPackets.Headers[effect.Parameters.HeaderIndex7C + action];
                int contact = selected.FirstContactIndex ?? throw new InvalidDataException("Native Funguar Laser requires the selected action's first contact.");
                int target = nativeActionPackets.ReadContact(contact).Primary.ActorIndex;
                uint pointer = checked(0x1D972C0u + (uint)target * 156);
                NativeBattleModulePoolTask live = pools[1].Slots[0];
                if ((live.Flags & 1) != 0 && live.ReadDword(16) == pointer)
                {
                    root = root.WriteWord(12, 0);
                }
                else if (Allocate(1, 0x6841F0, out int slot))
                {
                    NativeBattleModulePoolTask beam = pools[1].Slots[slot].ClearPayload(0x658);
                    beam = LaserStore(beam.WriteWord(14, (ushort)action).WriteDword(16, pointer), 20, ReadNativeGraphicsBone(graphics, target, 0xF1, 0));
                    beam = Initialize(beam, 0x1C, 0x3B0, resources.Muzzle);
                    beam = Initialize(beam, 0x3CC, 0xE8, resources.Ray);
                    beam = Initialize(beam, 0x4B4, 0x1B0, resources.Impact);
                    Put(1, slot, beam);
                    root = root.WriteByte(14, unchecked((byte)(action + 1)));
                }
            }
        }
        int survivors = root.ReadByte(15) == 0 ? 1 : 0;
        if (root.ReadByte(15) != 0)
        {
            for (int bank = 1; bank < 5; bank++)
            {
                int current = bank;
                NativeBattleEffectTaskStep step = pools[bank].Advance((pool, slot) =>
                {
                    pools[current] = pool;
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    int result = task.Callback switch
                    {
                        0x6841F0 => Beam(ref task),
                        0x684D60 => Glow(ref task),
                        0x684CA0 => Spark(ref task),
                        0x684C40 => Hit(ref task),
                        _ => throw new NotSupportedException("Native Funguar Laser selected an unrelated callback.")
                    };
                    task = task.UpdateHeader(task.Flags, task.State, pools[current].Slots[slot].Next, task.Callback);
                    Put(current, slot, task);
                    return new(pools[current], result);
                });
                pools[bank] = step.Pool;
                survivors += step.Survivors;
            }
        }
        Put(0, 0, root);
        if (survivors == 0)
        {
            pools[0] = pools[0].Advance((pool, _) => new(pool, 2)).Pool;
        }
        else
        {
            Put(0, 0, root.WriteWord(12, unchecked((ushort)(LaserWord(root, 12) + 1))));
        }
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = pools.ToImmutableArray(), ReturnedDescriptor = survivors == 0 ? 0u : 0x2508268u } };

        int Beam(ref NativeBattleModulePoolTask task)
        {
            int counter = LaserWord(task, 12);
            uint relative = unchecked(task.ReadDword(16) - 0x1D972C0u);
            if (relative % 156 != 0 || relative / 156 >= 7)
            {
                throw new InvalidDataException("Native Funguar Laser target leaves its physical model bank.");
            }
            int target = (int)(relative / 156);
            NativeBattleVector origin = VertexPoint();
            NativeBattleModuleAnchor targetAnchor = ReadNativeModelAnchor(graphics, target);
            NativeBattleVector endpoint = new(targetAnchor.X, targetAnchor.Y, targetAnchor.Z);
            NativeBattleActionMatrix floor = Basis50CBA0(endpoint.X - origin.X, 0, origin.Z - endpoint.Z).Matrix;
            NativeBattleActionMatrix reverse = floor with { Rotation = floor.Rotation.Select((word, index) => index / 3 == 1 ? word : unchecked((short)-word)).ToImmutableArray() };
            if (counter == 0)
            {
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128 });
            }
            if ((uint)(counter - 5) < 40)
            {
                NativeBattleFullMacVertex point = NativeBattleGfMatrices.TransformMac460840(view, origin);
                NativeBattleActionMatrix muzzle = view with { X = point.X, Y = point.Y, Z = point.Z };
                Play(ref task, 0x1C, 0x3B0, resources.Muzzle, muzzle, 0);
                var ease = Ease68433C(counter, graphics.Models[target].Transform!.Scale38);
                endpoint = endpoint with { Y = unchecked((short)(endpoint.Y + ease.Lift)) };
                NativeBattleActionMatrix ray = Basis50CBA0(endpoint.X - origin.X, origin.Y - endpoint.Y, origin.Z - endpoint.Z).Matrix with { X = origin.X, Y = origin.Y, Z = origin.Z };
                Play(ref task, 0x3CC, 0xE8, resources.Ray, NativeBattleGfMatrices.Compose56C2F0(view, ray), 0);
                if ((uint)(counter - 15) < 40)
                {
                    if (endpoint.Y >= 0)
                    {
                        endpoint = Floor68449D(origin, endpoint);
                    }
                    var shape = Basis50CBA0(endpoint.X - origin.X, origin.Y - endpoint.Y, origin.Z - endpoint.Z);
                    NativeBattleActionMatrix matrix = shape.Matrix;
                    int length = shape.Length;
                    short top = graphics.Models[target].MinimumY146 ?? throw new NotSupportedException("Native Funguar Laser requires actual target WORD146.");
                    if (endpoint.Y < top)
                    {
                        length = unchecked(length << 1);
                    }
                    else if (endpoint.Y >= 0)
                    {
                        if (Allocate(4, 0x684C40, out int hitSlot))
                        {
                            NativeBattleModulePoolTask hit = StoreLaserMatrix(pools[4].Slots[hitSlot], reverse with { X = endpoint.X, Y = endpoint.Y, Z = endpoint.Z });
                            Put(4, hitSlot, Initialize(hit, 0x2C, 0x7C, resources.Hit));
                        }
                    }
                    else if (endpoint.Y >= top - (top >> 3) && (counter & 1) == 0 && Allocate(2, 0x684D60, out int glowSlot))
                    {
                        Put(2, glowSlot, LaserStore(pools[2].Slots[glowSlot].WriteWord(0x12, 0), 12, endpoint));
                    }
                    short width = unchecked((short)(NativeBattleCameraMath.Sine(ease.Width >> 1) >> 3));
                    matrix = NativeBattleGfMatrices.Multiply56C090(matrix, new([width, 0, 0, 0, width, 0, 0, 0, unchecked((short)length)], 0, 0, 0, 4096)) with { X = origin.X, Y = origin.Y, Z = origin.Z };
                    Append(NativeBattleModulePackets.EmitBatch572200(resources.Beam, 0, default, NativeBattleGfMatrices.Compose56C2F0(view, matrix), projection, new(ease.Fade == 0 ? 0x30u : 0xF0u, ease.Fade == 0 ? null : 0u, ease.Fade == 0 ? null : ease.Fade, null, null, null, 17, 2), colors));
                }
            }
            if ((uint)(counter - 33) < 24)
            {
                NativeBattleVector spawn = LaserVector(task, 20);
                reverse = reverse with { X = spawn.X, Y = (graphics.Models[target].MinimumY146 ?? throw new NotSupportedException("Native Funguar Laser requires actual target WORD146.")) / 2, Z = spawn.Z };
                Play(ref task, 0x4B4, 0x1B0, resources.Impact, NativeBattleGfMatrices.Compose56C2F0(view, reverse), -128);
            }
            if ((uint)(counter - 36) < 2)
            {
                for (int index = 0; index < 8; index++)
                {
                    if (!Allocate(3, 0x684CA0, out int slot))
                    {
                        continue;
                    }
                    NativeBattleModulePoolTask spark = LaserStore(pools[3].Slots[slot].WriteWord(0x12, 0), 12, LaserVector(task, 20));
                    NativeBattleVector velocity = new((short)((Roll() & 511) - 256), (short)((Roll() & 511) - 256), (short)((Roll() & 511) - 256));
                    spark = LaserStore(spark, 0x14, velocity);
                    spark = LaserStore(spark, 0x1C, new((short)(velocity.X / -12), (short)(velocity.Y / -12), (short)(velocity.Z / -12)));
                    Put(3, slot, spark);
                }
            }
            if (counter == 43)
            {
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first)] });
            }
            if (counter >= 50)
            {
                return 2;
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return 0;
        }

        int Glow(ref NativeBattleModulePoolTask task)
        {
            int counter = LaserWord(task, 0x12);
            Sprite(resources.Glow, counter, LaserVector(task, 12), 0xAF0, -0x400);
            task = task.WriteWord(0x12, unchecked((ushort)(counter + 1)));
            return counter + 1 >= 16 ? 2 : 0;
        }

        int Spark(ref NativeBattleModulePoolTask task)
        {
            int counter = LaserWord(task, 0x12);
            Sprite(resources.Spark, counter, LaserVector(task, 12), 0xC00, -0x300);
            task = task.WriteWord(0x12, unchecked((ushort)(counter + 1)));
            if (counter + 1 >= 16)
            {
                return 2;
            }
            NativeBattleVector point = LaserVector(task, 12);
            NativeBattleVector velocity = LaserVector(task, 0x14);
            task = LaserStore(task, 12, new(unchecked((short)(point.X + velocity.X)), unchecked((short)(point.Y + velocity.Y)), unchecked((short)(point.Z + velocity.Z))));
            if (counter + 1 < 12)
            {
                NativeBattleVector drag = LaserVector(task, 0x1C);
                task = LaserStore(task, 0x14, new(unchecked((short)(velocity.X + drag.X)), unchecked((short)(velocity.Y + drag.Y)), unchecked((short)(velocity.Z + drag.Z))));
            }
            return 0;
        }

        int Hit(ref NativeBattleModulePoolTask task)
        {
            return Play(ref task, 0x2C, 0x7C, resources.Hit, NativeBattleGfMatrices.Compose56C2F0(view, ReadLaserMatrix(task)), 0) == 0 ? 2 : 0;
        }

        NativeBattleVector VertexPoint()
        {
            int caster = effect.Parameters.Actor78;
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, caster);
            NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Funguar Laser requires its actual body skeleton.");
            NativeBattleRawMesh mesh = (resource.Geometry ?? throw new NotSupportedException("Native Funguar Laser requires its actual model geometry."))[0];
            if (mesh.Vertices.Length <= 123)
            {
                throw new NotSupportedException("Native Funguar Laser requires caster object zero vertex123.");
            }
            NativeBattleGraphicsModel actor = NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(graphics.Models[caster], source, NativeBattleCuragaMatrices.Identity8DD770());
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source);
            NativeBattleRawVertex vertex = mesh.Vertices[123];
            NativeBattleVector result = NativeBattleGfMatrices.Transform460840(bones[vertex.Bone], vertex.Position);
            graphics = graphics with { Models = graphics.Models.SetItem(caster, NativeBattleBodyBoneMatrices.Publish508C90(actor, source)) };
            return result;
        }

        int Play(ref NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleActionMatrix context, int depth)
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
                    if (count > 49)
                    {
                        throw new InvalidDataException("Native Funguar Laser leaves its original49-vertex morph scratch.");
                    }
                    NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
                    vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(buffer + 0xF24, 0x188).ToImmutableArray());
                    NativeEffectMemory = NativeEffectMemory with { Arena = arena.Write(buffer + 0xF24, vertices.AsSpan()) };
                    vertices = vertices.Slice(0, count * 8);
                }
                byte[] record = node.Data.ToArray();
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4), node.Header & ~0x1000u);
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose6C53A0(node with { Data = record.ToImmutableArray() }, context, null);
                bool cue = node.Scalar36 != 0;
                Append(NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, vertices, matrix, projection, new(cue ? 0x20F0u : 0x2030u, cue ? node.Color32 : 0, node.Scalar36, null, null, depth, 17, 2), colors));
                return current;
            });
            task = PublishWorkspace(task, offset, advance.Workspace);
            return advance.Remaining;
        }

        NativeBattleModulePoolTask Initialize(NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source)
        {
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(offset - 12, size));
            return PublishWorkspace(task, offset, workspace.Initialize7016B0(source, size));
        }
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        void Sprite(NativeBattleModuleResource resource, int frame, NativeBattleVector point, int scale, int depth)
        {
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(frame), Phase = unchecked((ushort)frame), Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, DepthOffset = depth, SpriteFlags = 0 };
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera.Roll, 17, 2);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.RgbBytes);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        bool Allocate(int bank, uint callback, out int slot)
        {
            if (!pools[bank].Slots.Any(candidate => (candidate.Flags & 1) == 0))
            {
                slot = -1;
                return false;
            }
            (pools[bank], slot) = pools[bank].Append(callback);
            return true;
        }
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        void Put(int bank, int slot, NativeBattleModulePoolTask task) => pools[bank] = pools[bank].WriteSlot(slot, task);
    }

    private static short LaserWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector LaserVector(NativeBattleModulePoolTask task, int offset) => new(LaserWord(task, offset), LaserWord(task, offset + 2), LaserWord(task, offset + 4));
    private static NativeBattleModulePoolTask LaserStore(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));

    private static NativeBattleModulePoolTask StoreLaserMatrix(NativeBattleModulePoolTask task, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(12 + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task.WriteDword(32, unchecked((uint)matrix.X)).WriteDword(36, unchecked((uint)matrix.Y)).WriteDword(40, unchecked((uint)matrix.Z));
    }

    private static NativeBattleActionMatrix ReadLaserMatrix(NativeBattleModulePoolTask task)
    {
        return new(Enumerable.Range(0, 9).Select(index => LaserWord(task, 12 + index * 2)).ToImmutableArray(), unchecked((int)task.ReadDword(32)), unchecked((int)task.ReadDword(36)), unchecked((int)task.ReadDword(40)), 4096);
    }
}
