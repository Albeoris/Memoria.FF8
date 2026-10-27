using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice169;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeGastricJuice169(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleGastricJuice169Resources resources = effect.Binding.GastricJuice169Resources ?? throw new NotSupportedException("Native Gastric Juice requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleGastricJuice169State process = memory.GastricJuice169 ?? throw new NotSupportedException("Native Gastric Juice requires its retained process arrays.");
        byte[] drops = process.Drops.ToArray();
        for (int index = 0; index < 100; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(drops.AsSpan(index * 24), 0);
        }
        NativeBattleEffectTextureState textures = memory.Textures!.Register5099A0(resources.Camera);
        NativeCamera = NativeCamera!.RegisterControlResource(textures.Animation);
        NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Gastric Juice requires its original TIM allocation.");
        (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
        NativeEffectMemory = memory with { Textures = textures, GastricJuice169 = process with { Drops = drops.ToImmutableArray() } };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x23B1388, 0x23B1378, 16, 1);
        (root, _) = root.Append(0x5E16E0);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23B1CF8, 0x23B1D08, 36, 100);
        (children, _) = children.Append(0x5DEF20);
        return graphics with { OriginalEffect = effect with { Pools = [root, children], ReturnedDescriptor = root.DescriptorAddress, DrawArenaOffset = null } };
    }

    private NativeBattleGraphicsState AdvanceNativeGastricJuice169(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleGastricJuice169Resources resources = effect.Binding.GastricJuice169Resources ?? throw new NotSupportedException("Native Gastric Juice requires its original resources.");
        NativeBattleGastricJuice169State process = NativeEffectMemory!.GastricJuice169 ?? throw new NotSupportedException("Native Gastric Juice requires its retained process arrays.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Gastric Juice requires its shared CRT random stream.");
        byte[] streams = process.Streams.ToArray();
        byte[] drops = process.Drops.ToArray();
        byte[] meshVertices = process.MeshVertices.ToArray();
        NativeBattleEffectTaskPool rootPool = effect.Pools[0];
        NativeBattleEffectTaskPool children = effect.Pools[1];
        NativeBattleModulePoolTask root = rootPool.Slots[0];
        if (root.Callback != 0x5E16E0 || rootPool.DescriptorAddress != 0x23B1388 || children.DescriptorAddress != 0x23B1CF8)
        {
            throw new InvalidDataException("Native Gastric Juice lost its physical task banks.");
        }
        effect = effect with { DrawArenaOffset = (root.ReadWord(12) & 1) != 0 ? 0x8000 : 0 };
        bool paused = (graphics.Flags & 0x201) != 0;
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C];
        int firstContact = header.FirstContactIndex ?? throw new InvalidDataException("Native Gastric Juice requires its actual first target contact.");
        int target = nativeActionPackets.ReadContact(firstContact).Primary.ActorIndex;
        NativeBattleEffectTaskStep advanced = children.Advance((pool, slot) =>
        {
            children = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            int result = task.Callback switch
            {
                0x5DEF20 => Director(ref task),
                0x5DFD40 => Stream(ref task),
                0x5E00B0 => Blob(ref task),
                0x5E01A0 => Landing(ref task),
                0x5E1200 => Puddle(ref task),
                0x5E0FE0 => Droplets(ref task, false),
                0x5E1330 => Droplets(ref task, true),
                0x5DF030 => Tint(ref task),
                _ => throw new NotSupportedException("Native Gastric Juice selected an unrelated task callback.")
            };
            task = task.UpdateHeader(task.Flags, task.State, children.Slots[slot].Next, task.Callback);
            return new(children.WriteSlot(slot, task), result);
        });
        children = advanced.Pool;
        root = root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)));
        rootPool = rootPool.WriteSlot(0, root);
        if (advanced.Survivors == 0)
        {
            rootPool = rootPool.Advance((pool, _) => new(pool, 2)).Pool;
        }
        NativeEffectMemory = NativeEffectMemory! with { GastricJuice169 = process with { Streams = streams.ToImmutableArray(), Drops = drops.ToImmutableArray(), MeshVertices = meshVertices.ToImmutableArray() } };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = effect with { Pools = [rootPool, children], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : 0x23B1388u } };

        int Director(ref NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            int counter = JuiceWord(task, 12);
            if (counter == 6)
            {
                for (int index = 0; index < 5; index++)
                {
                    (children, int slot) = children.Append(0x5DFD40);
                    NativeBattleModulePoolTask stream = children.Slots[slot].WriteWord(12, 0).WriteWord(14, task.ReadWord(14)).WriteWord(24, 0).WriteWord(26, (ushort)(Roll() % 4));
                    Put(slot, stream);
                    task = task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) + 1)));
                }
                (children, int splash) = children.Append(0x5E1330);
                Put(splash, children.Slots[splash].WriteWord(12, 0).WriteWord(14, 0));
            }
            if (counter == 30)
            {
                (children, int slot) = children.Append(0x5DF030);
                Put(slot, JuiceStoreAnchor(children.Slots[slot].WriteWord(12, 0), ReadNativeModelAnchor(graphics, target)));
            }
            if (counter == 35)
            {
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(firstContact)] });
                graphics = graphics with { PendingContacts = pending };
            }
            if (counter == 1)
            {
                PublishNativeModuleSound(new(resources.Sound, 0, null) { Pan = 128 });
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return counter >= 50 ? 2 : 0;
        }

        int Stream(ref NativeBattleModulePoolTask task)
        {
            if (paused)
            {
                return 0;
            }
            int index = JuiceWord(task, 14);
            int start = index * 0x110;
            if (JuiceWord(task, 24) == 0)
            {
                NativeBattleVector source = ReadNativeGraphicsBone(graphics, effect.Parameters!.Actor78, 0xF0, 0);
                NativeBattleModuleAnchor destination = ReadNativeModelAnchor(graphics, target);
                JuiceWriteVector(streams, start, source);
                JuiceWriteVector(streams, start + 8, new(unchecked((short)(destination.X + Roll() % 1400 - 700)), 0, unchecked((short)(destination.Z + Roll() % 1400 - 700))));
                JuicePutWord(streams, start + 14, destination.W);
                streams.AsSpan(start, 8).CopyTo(streams.AsSpan(start + 16, 8));
                streams.AsSpan(start + 8, 8).CopyTo(streams.AsSpan(start + 40, 8));
                NativeBattleVector end = JuiceReadVector(streams, start + 8);
                int dx = (end.X - source.X) / 3;
                int dz = (end.Z - source.Z) / 3;
                short firstY = unchecked((short)(source.Y - (short)(Roll() % 500) - 2200));
                JuiceWriteVector(streams, start + 24, new(unchecked((short)(source.X + dx)), firstY, unchecked((short)(source.Z + dz))));
                JuiceWriteVector(streams, start + 32, new(unchecked((short)(source.X + dx * 2)), unchecked((short)(firstY - (short)(Roll() % 600) - 200)), unchecked((short)(source.Z + dz * 2))));
                NativeBattleEffectArena memory = NativeBattleEffectArena.Clear().Write(0, streams);
                NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(4, memory, start + 16, NativeBattleEffectArena.Clear());
                for (int sample = 0; sample < 28; sample++)
                {
                    memory = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(4, work, (sample << 12) / 27, memory, start + 48 + sample * 8);
                }
                streams = memory.Read(0, streams.Length);
                task = task.WriteWord(24, 1);
            }
            if (JuiceWord(task, 26) > 0)
            {
                task = task.WriteWord(26, unchecked((ushort)(JuiceWord(task, 26) - 1)));
                return 0;
            }
            int counter = JuiceWord(task, 12);
            if (counter == 1)
            {
                (children, int slot) = children.Append(0x5E00B0);
                Put(slot, children.Slots[slot].WriteWord(12, 0).WriteWord(14, (ushort)index).WriteWord(24, 0).WriteWord(26, 4).WriteWord(28, (ushort)(Roll() % 384 + 320)));
            }
            if (counter == 22)
            {
                (children, int slot) = children.Append(0x5E01A0);
                NativeBattleModulePoolTask mesh = JuiceCopyPosition(children.Slots[slot], streams, start + 8).WriteWord(12, 0).WriteWord(14, (ushort)index).WriteWord(24, (ushort)(Roll() % 4096)).WriteWord(26, (ushort)(Roll() % 6 + 16)).WriteWord(28, (ushort)(Roll() % 1536 + 1024));
                Put(slot, mesh);
                (children, int drip) = children.Append(0x5E0FE0);
                Put(drip, JuiceCopyPosition(children.Slots[drip], streams, start + 8).WriteWord(12, 0).WriteWord(14, unchecked((ushort)(1 << index))));
            }
            if (counter == 21)
            {
                (children, int slot) = children.Append(0x5E1200);
                NativeBattleModulePoolTask puddle = JuiceCopyPosition(children.Slots[slot], streams, start + 8).WriteWord(12, 0).WriteWord(24, (ushort)(Roll() % 4096));
                ushort growth = (ushort)((Roll() % 2048 + 3328) / 10);
                Put(slot, puddle.WriteWord(30, growth).WriteWord(28, growth));
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return counter >= 39 ? 2 : 0;
        }

        int Blob(ref NativeBattleModulePoolTask task)
        {
            int frame = JuiceWord(task, 12);
            NativeBattleVector point = JuiceReadVector(streams, JuiceWord(task, 14) * 0x110 + 48 + JuiceWord(task, 24) * 8);
            Sprite(resources.Sprites[0], frame & 7, point, JuiceWord(task, 28), 3, frame < 1 ? (ushort)4 : (ushort)0, 0);
            if (paused)
            {
                return 0;
            }
            int speed = JuiceWord(task, 26);
            int sample = unchecked((short)(JuiceWord(task, 24) + speed));
            task = task.WriteWord(24, unchecked((ushort)sample));
            if (speed > 1 && sample < 14)
            {
                task = task.WriteWord(26, (ushort)(speed - 1));
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return sample >= 27 ? 2 : 0;
        }

        int Landing(ref NativeBattleModulePoolTask task)
        {
            int frame = JuiceWord(task, 12);
            int offset = JuiceWord(task, 14) * 0x248;
            Morph(resources.Mesh, resources.SecondShape, meshVertices.AsSpan(offset, 0x248), frame);
            NativeBattleActionMatrix matrix = JuiceModelMatrix(task);
            if (frame < 16)
            {
                Append(NativeBattleModulePackets.EmitBatch572200(resources.Mesh, 0, meshVertices.AsSpan(offset, 0x248).ToArray().ToImmutableArray(), matrix, projection, new(frame < 8 ? 0x2033u : 0x20F3u, 0, MeshFade(frame), null, null, 0, 17, 2), colors));
            }
            else if (frame < 32)
            {
                Append(Emit5E0450(resources.Mesh, meshVertices.AsSpan(offset, 0x248).ToArray().ToImmutableArray(), matrix, projection, MeshFade(frame), colors));
            }
            if (paused)
            {
                return 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame >= 31 ? 2 : 0;
        }

        int Puddle(ref NativeBattleModulePoolTask task)
        {
            int frame = JuiceWord(task, 12);
            Append(NativeBattleModulePackets.EmitBatch572200(resources.Puddle, 0, default, JuiceModelMatrix(task), projection, new(0xF3, 0, frame < 8 ? 2048 : frame << 8, null, null, 0, 17, 2), colors));
            if (paused)
            {
                return 0;
            }
            task = task.WriteWord(28, unchecked((ushort)(JuiceWord(task, 28) + JuiceWord(task, 30)))).WriteWord(12, unchecked((ushort)(frame + 1)));
            return frame >= 15 ? 2 : 0;
        }

        int Droplets(ref NativeBattleModulePoolTask task, bool splash)
        {
            int counter = JuiceWord(task, 12);
            int mask = splash ? 0x100 : JuiceWord(task, 14);
            int alive = 0;
            NativeBattleActionMatrix light = NativeBattleCuragaMatrices.Identity8DD770();
            if (splash)
            {
                int start = JuiceWord(task, 14) * 0x110 + 48;
                NativeBattleVector first = JuiceReadVector(streams, start);
                NativeBattleVector second = JuiceReadVector(streams, start + 16);
                NativeBattleVector anchor = JuiceReadVector(streams, start + 8);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(second.X - first.X, second.Y - first.Y, second.Z - first.Z);
                NativeBattleModuleAxisAngle align = NativeBattleModuleAlignment.Between571480(0, -4096, 0, direction.X, direction.Y, direction.Z);
                light = NativeBattleGfMatrices.Compose56C2F0(view, new(NativeBattleModuleAlignment.Rotation5714F0(align.Angle, align.Axis), anchor.X, anchor.Y, anchor.Z, 4096));
            }
            NativeBattleModuleResource resource = resources.Sprites[splash ? 1 : 2];
            for (int index = 0; index < 100; index++)
            {
                int offset = index * 24;
                if ((BinaryPrimitives.ReadInt32LittleEndian(drops.AsSpan(offset)) & mask) == 0)
                {
                    continue;
                }
                int frame = JuiceRawWord(drops, offset + 4);
                int scale = JuiceRawWord(drops, offset + 6);
                NativeBattleVector point = JuiceReadVector(drops, offset + 8);
                if (splash)
                {
                    NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(light, point);
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(transformed.X, transformed.Y, transformed.Z);
                    int distance = -(scale >> 4);
                    int x = unchecked(transformed.X + (distance * direction.X >> 12));
                    int y = unchecked(transformed.Y + (distance * direction.Y >> 12));
                    int z = unchecked(transformed.Z + (distance * direction.Z >> 12));
                    process = process with { SplashTranslation = new(x, y, z, 0) };
                    Sprite(resource, frame, default, scale, 0, 0, 0, new(x, y, z, 0));
                }
                else
                {
                    Sprite(resource, frame, point, scale, 4, 0, 0);
                }
                if (paused)
                {
                    continue;
                }
                JuicePutWord(drops, offset + 4, unchecked((short)(frame + 1)));
                if (resource.SpriteFrame(frame).NextOffset < 0)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(drops.AsSpan(offset), 0);
                    continue;
                }
                if (splash)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        short velocity = JuiceRawWord(drops, offset + 16 + axis * 2);
                        JuicePutWord(drops, offset + 8 + axis * 2, unchecked((short)(JuiceRawWord(drops, offset + 8 + axis * 2) + velocity)));
                        JuicePutWord(drops, offset + 16 + axis * 2, unchecked((short)(velocity - (short)(velocity >> 2))));
                    }
                }
                else
                {
                    short velocity = JuiceRawWord(drops, offset + 18);
                    JuicePutWord(drops, offset + 10, unchecked((short)(point.Y + velocity)));
                    JuicePutWord(drops, offset + 18, unchecked((short)(velocity - (short)(velocity >> 4))));
                }
                alive++;
            }
            if (paused)
            {
                return 0;
            }
            int births = splash ? counter is 1 or 2 ? 12 : 0 : counter is >= 0 and <= 11 ? resources.DripCounts[counter] : 0;
            for (int birth = 0; birth < births; birth++)
            {
                int free = Enumerable.Range(0, 100).FirstOrDefault(index => BinaryPrimitives.ReadInt32LittleEndian(drops.AsSpan(index * 24)) == 0, -1);
                if (free < 0)
                {
                    break;
                }
                int offset = free * 24;
                BinaryPrimitives.WriteInt32LittleEndian(drops.AsSpan(offset), mask);
                JuicePutWord(drops, offset + 4, 0);
                if (splash)
                {
                    JuicePutWord(drops, offset + 6, (short)(Roll() % 384 + 512));
                    JuiceWriteVector(drops, offset + 8, new((short)(Roll() % 300 - 150), 0, (short)(Roll() % 300 - 150)));
                    int angle = Roll() % 4096;
                    int radius = Roll() % 100 + 80;
                    JuicePutWord(drops, offset + 16, (short)(NativeBattleCameraMath.Cosine(angle) * radius >> 12));
                    JuicePutWord(drops, offset + 18, (short)(-100 - Roll() % 120));
                    JuicePutWord(drops, offset + 20, (short)(NativeBattleCameraMath.Sine(angle) * radius >> 12));
                }
                else
                {
                    JuicePutWord(drops, offset + 6, (short)(Roll() % 2304 + 1536));
                    NativeBattleVector point = JuiceVector(task, 16);
                    JuiceWriteVector(drops, offset + 8, new(unchecked((short)(point.X + Roll() % 700 - 350)), unchecked((short)(point.Y - 100 - Roll() % 300)), unchecked((short)(point.Z + Roll() % 700 - 350))));
                    JuicePutWord(drops, offset + 14, JuiceWord(task, 22));
                    JuicePutWord(drops, offset + 18, (short)(-40 - Roll() % 70));
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            return JuiceWord(task, 12) >= 4 && alive == 0 ? 2 : 0;
        }

        int Tint(ref NativeBattleModulePoolTask task)
        {
            int counter = JuiceWord(task, 12);
            if (counter < 22)
            {
                graphics = graphics with { Models = graphics.Models.SetItem(target, graphics.Models[target] with { Flags0 = (ushort)(graphics.Models[target].Flags0 | 4) }) };
                DrawTint(task, counter);
            }
            if (paused)
            {
                return 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
            if (counter < 22)
            {
                return 0;
            }
            graphics = graphics with { Models = graphics.Models.SetItem(target, graphics.Models[target] with { Flags0 = (ushort)(graphics.Models[target].Flags0 & 0xFFFB) }) };
            return 2;
        }

        void DrawTint(NativeBattleModulePoolTask task, int frame)
        {
            NativeBattleGraphicsModel actor = graphics.Models[target];
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, target);
            graphics = SubmitNativeShadowPackets5088A0(graphics, actor, projection, 4121, false);
            NativeBattlePoseSource source = resource.PoseSource!;
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.ModelGroupMatrices50F190(actor, source, identity);
            ImmutableArray<NativeBattleRawMesh> body = resource.Geometry!.Value;
            ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry!.Value;
            bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Gastric Juice requires its actual linked model ownership."));
            if (shared)
            {
                body = body.SetItem(1, linked.Single());
            }
            uint mask = actor.SequenceState!.MeshMask124;
            ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Gastric Juice tint requires its actual scratch maximum.");
            uint address = checked(0x196B018u - (uint)(maximum * 8));
            for (int index = 0; index < body.Length; index++)
            {
                if ((mask & (1u << (index & 31))) == 0)
                {
                    continue;
                }
                NativeBattleRawMesh mesh = body[index];
                byte[] output = (NativeFileStaging ?? throw new NotSupportedException("Native Gastric Juice tint requires its shared projected byte owner.")).Read(address, checked(mesh.Vertices.Length * 8));
                for (int vertex = 0; vertex < mesh.Vertices.Length; vertex++)
                {
                    NativeBattleRawVertex raw = mesh.Vertices[vertex];
                    NativeBattleVector world = NativeBattleGfMatrices.Transform460840(matrices[raw.Bone], raw.Position);
                    JuiceWriteVector(output, vertex * 8, world);
                }
                NativeFileStaging = NativeFileStaging!.Write(address, output);
                NativeBattleModelTextureBinding texture = shared && index == 1 ? actor.LinkedTextureSlots! : actor.BodyTextureSlots!;
                ImmutableArray<NativeBattleRawPolygon> polygons = resource.GeometryRelocated507550 ? mesh.Polygons : mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, texture) }).ToImmutableArray();
                Common(Tint5DF540(polygons, output.ToImmutableArray(), view, projection, JuiceVector(task, 16), actor.Color40!.Value, frame, colors));
            }
            if (!shared && !linked.IsEmpty && (actor.Flags0 & 0x200) == 0 && ResetAndEnterNativeModelRenderer5099D0(actor.Color40!.Value))
            {
                ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(actor, resource.LinkedPoseSource!, view);
                ImmutableArray<NativeBattleRawMesh> geometry = linked.Select(mesh => resource.GeometryRelocated507550 ? mesh : mesh with { Polygons = mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, actor.LinkedTextureSlots!) }).ToImmutableArray() }).ToImmutableArray();
                ImmutableArray<NativeBattleGeometryPrimitive> primitives = NativeBattleRawGeometryPackets.Emit5099D0(geometry, bones, projection, actor.Color40!.Value, uint.MaxValue, null, 17, 4, null, colors, PublishNativeBodyProjectionScratch);
                Common(NativeActorPrimitiveBatch(primitives));
            }
        }

        NativeBattleActionMatrix JuiceModelMatrix(NativeBattleModulePoolTask task)
        {
            NativeBattleVector point = JuiceVector(task, 16);
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(0, JuiceWord(task, 24), 0)) with { X = point.X, Y = point.Y, Z = point.Z };
            int scale = JuiceWord(task, 28);
            return NativeBattleGfMatrices.Compose56C2F0(view, NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale));
        }
        void Sprite(NativeBattleModuleResource resource, int frame, NativeBattleVector point, int scale, int shift, ushort flags, uint color, NativeBattleFullMacVertex? camera = null)
        {
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(frame), Phase = (ushort)frame, Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, DepthOffset = camera is null ? -(scale >> shift) : 0, Direction = 4096, SpriteFlags = flags, SpriteColor = color };
            NativeBattleCameraProjection selected = projection;
            short roll = NativeCamera.Roll;
            if (camera is { } position)
            {
                draw = draw with { SpriteCameraXOffset = position.X, SpriteCameraYOffset = position.Y, SpriteCameraZOffset = position.Z };
                selected = projection with { View = NativeBattleCuragaMatrices.Identity8DD770() };
                roll = 0;
            }
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, selected, roll, 17, 2);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        void Common(NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveProcess packetProcess = NativeEffectMemory!.PrimitiveProcess!;
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(packetProcess, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = written.Process.Publish50FDF0(unchecked((uint)written.Process.ByteCursor!.Value), null) };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        void Put(int slot, NativeBattleModulePoolTask task) => children = children.WriteSlot(slot, task);
    }

    private static NativeBattleGfGeometryBatch NativeActorPrimitiveBatch(ImmutableArray<NativeBattleGeometryPrimitive> primitives)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        foreach (NativeBattleGeometryPrimitive primitive in primitives)
        {
            byte[] bytes = new byte[primitive.Words.Length * 4];
            for (int index = 0; index < primitive.Words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), primitive.Words[index]);
            }
            attempts.Add(new(allocated, bytes.ToImmutableArray(), primitive.PhysicalWriteMask ?? Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive));
            allocated += bytes.Length;
        }
        return new(attempts.ToImmutable(), allocated);
    }
    private static short JuiceWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector JuiceVector(NativeBattleModulePoolTask task, int offset) => new(JuiceWord(task, offset), JuiceWord(task, offset + 2), JuiceWord(task, offset + 4));
    private static short JuiceRawWord(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static void JuicePutWord(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    private static NativeBattleVector JuiceReadVector(byte[] bytes, int offset) => new(JuiceRawWord(bytes, offset), JuiceRawWord(bytes, offset + 2), JuiceRawWord(bytes, offset + 4));
    private static void JuiceWriteVector(byte[] bytes, int offset, NativeBattleVector point)
    {
        JuicePutWord(bytes, offset, point.X);
        JuicePutWord(bytes, offset + 2, point.Y);
        JuicePutWord(bytes, offset + 4, point.Z);
    }
    private static NativeBattleModulePoolTask JuiceCopyPosition(NativeBattleModulePoolTask task, byte[] bytes, int offset) => task.WriteWord(16, unchecked((ushort)JuiceRawWord(bytes, offset))).WriteWord(18, unchecked((ushort)JuiceRawWord(bytes, offset + 2))).WriteWord(20, unchecked((ushort)JuiceRawWord(bytes, offset + 4))).WriteWord(22, unchecked((ushort)JuiceRawWord(bytes, offset + 6)));
    private static NativeBattleModulePoolTask JuiceStoreAnchor(NativeBattleModulePoolTask task, NativeBattleModuleAnchor point) => task.WriteWord(16, unchecked((ushort)point.X)).WriteWord(18, unchecked((ushort)point.Y)).WriteWord(20, unchecked((ushort)point.Z)).WriteWord(22, unchecked((ushort)point.W));
}
