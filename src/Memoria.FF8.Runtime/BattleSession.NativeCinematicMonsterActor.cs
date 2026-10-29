using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record CinematicMonsterMesh(NativeBattleRawMesh Mesh, ImmutableArray<NativeBattleActionMatrix> Bones, int ObjectIndex);

    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicMonsterActor(NativeBattleWaterExecutionFrame frame, bool glow, int resourceLength, NativeBattleCinematicResources resources, NativeBattleCinematicModelServices modelServices, NativeBattleGraphicsState graphics)
    {
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic monster drawing requires its current projection owner.");
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        NativeBattlePilotVisualIdentity visualIdentity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank));
        uint manager = CoreDword(frame, 0x27973EC);
        uint clone = SlotDword(0xBC);
        uint payload = SlotDword(0xB8);
        byte actorIndex = ReadSlot(frame, 0x1B, 1)[0];
        uint header = CoreDword(frame, 0x279744C);
        uint actorAddress = CoreDword(frame, header + 0x60u + 4u * actorIndex);
        if (actorIndex >= graphics.Models.Length || actorAddress != 0x1D972C0u + 156u * actorIndex)
        {
            throw new NotSupportedException("Native cinematic monster drawing requires its actual physical actor directory.");
        }
        bool embedded = !glow && (clone == 0 ? (Word(payload) & 1) != 0 : (Word(SlotDword(0xC0) + 2) & 1) != 0);
        if (clone == 0)
        {
            if (!glow)
            {
                (frame, uint work) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 16);
                SlotStore(0xC0, work);
                StoreWord(work, 0);
                StoreWord(work + 2, Word(payload));
                StoreWord(work + 6, Word(payload + 4));
            }
            if (embedded)
            {
                (frame, clone) = NativeBattleCinematicModel.Allocate(frame, unchecked((short)Word(payload + 2)), modelServices);
            }
            else
            {
                NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
                graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor with { Flags0 = (ushort)(actor.Flags0 | 4) }) };
                (frame, clone) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 0x80);
                StoreDword(clone + 0xC, actorAddress);
                StoreDword(clone + 0x54, 0);
                StoreDword(clone + 0x58, 0x00D00140);
                uint color = actor.Color40 ?? throw new NotSupportedException("Native cinematic monster clone requires its current actor color.");
                StoreDword(clone + 0x78, color);
                StoreDword(clone + 0x5C, color);
                StoreDword(clone + 0x60, actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native cinematic monster clone requires its actual object mask."));
                StoreWord(clone + 0x64, 0);
                StoreDword(clone + 0x68, 0);
                StoreDword(clone + 0x50, uint.MaxValue);
                StoreWord(clone, 0);
                StoreWord(clone + 2, 0);
                StoreByte(clone + 0x21, 0);
            }
            SlotStore(0xBC, clone);
            if (glow)
            {
                NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actorIndex);
                NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native cinematic glow requires its original pose resource.");
                NativeBattleGraphicsModel actor = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[actorIndex], source);
                NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native cinematic glow requires its actual entity matrix.");
                actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, world);
                graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor) };
                InitializeGlow(CinematicActorMeshes(actor, resource, NativeBattleBodyBoneMatrices.Require(actor, source), world, false));
            }
        }
        ImmutableArray<CinematicMonsterMesh> meshes;
        if (embedded)
        {
            _ = modelServices.ResolveShape(frame, unchecked((short)Word(clone + 8)));
            NativeBattleWaterSlotBank slotBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
            NativeBattleWaterMeshPrepared prepared = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, slotBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, slotBank), 2, unchecked((short)(projection.FocalLength ?? throw new NotSupportedException("Native cinematic monster model requires its current focal WORD."))));
            frame = NativeBattleCinematicModel.AdvanceAndCompose(prepared.Frame, clone, modelServices);
            uint table = Dword(clone + 0x14);
            uint skeleton = Dword(table);
            uint geometry = Dword(table + 4);
            int boneCount = frame.Arena.Read(ArenaOffset(skeleton, 1), 1)[0];
            ImmutableArray<NativeBattleRawMesh> geometryMeshes = NativeBattleRawGeometryReader.Read((offset, length) => frame.Arena.Read(ArenaOffset(geometry + (uint)offset, length), length), boneCount);
            ImmutableArray<NativeBattleActionMatrix> bones = Enumerable.Range(0, boneCount).Select(index => NativeBattleArenaPose.ReadMatrix(frame.Arena, ArenaOffset(skeleton + 0x20u + 0x30u * (uint)index, 32))).ToImmutableArray();
            meshes = geometryMeshes.Select((mesh, index) => new CinematicMonsterMesh(mesh, bones, index)).ToImmutableArray();
        }
        else
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(ArenaOffset(clone + 0x10, 12), 12).MarkUnknown(ArenaOffset(clone + 0x30, 16), 16) };
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actorIndex);
            NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native cinematic monster drawing requires its body pose resource.");
            NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
            if (glow)
            {
                actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
                actor = NativeBattlePoseMatrices.PublishShadowBounds5086F0(actor, source);
            }
            NativeBattleActionMatrix node = MonsterNodeMatrix(frame);
            NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native cinematic monster drawing requires its retained entity matrix.");
            NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(node, world);
            actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, basis);
            graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor) };
            meshes = CinematicActorMeshes(actor, resource, NativeBattleBodyBoneMatrices.Require(actor, source), node, !glow);
            if (!glow)
            {
                Shadow();
            }
        }
        uint mask = Dword(clone + 0x60);
        uint projectedWork;
        ImmutableArray<short> wobble = default;
        if (glow)
        {
            uint state = SlotDword(0xC0);
            uint reference = ResolveNode(unchecked((short)Word(state + 8)));
            NativeBattleVector offset = Vector(reference + 0x94);
            int count = unchecked((short)Word(state));
            uint colors = CoreDword(frame, manager + 0x74);
            uint rampAddress = Dword(state + 12);
            ImmutableArray<uint> ramp = Enumerable.Range(0, 256).Select(index =>
            {
                byte[] rgb = frame.Arena.Read(ArenaOffset(rampAddress + 4u * (uint)index, 3), 3);
                return (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
            }).ToImmutableArray();
            uint actorColor = graphics.Models[actorIndex].Color40 ?? throw new NotSupportedException("Native cinematic glow requires its current actor color.");
            for (int index = 0; index < count; index++)
            {
                uint color = NativeBattleCinematicMonsterVertices.Glow(Word(Dword(state + 4) + 2u * (uint)index), unchecked((short)SlotWord(frame, 0x8C)), unchecked((short)SlotWord(frame, 0x8E)), unchecked((short)SlotWord(frame, 0x90)), actorColor, offset, ramp);
                uint target = colors + 4u * (uint)index;
                short distance = unchecked((short)Word(Dword(state + 4) + 2u * (uint)index));
                if (distance >= unchecked((short)SlotWord(frame, 0x8C)))
                {
                    StoreDword(target, 0);
                }
                else
                {
                    frame = frame with { Arena = frame.Arena.Write(ArenaOffset(target, 3), [(byte)color, (byte)(color >> 8), (byte)(color >> 16)]) };
                }
            }
            projectedWork = unchecked(colors + 4u * (uint)count);
        }
        else
        {
            uint reference = ResolveNode(unchecked((short)Word(SlotDword(0xC0) + 6)));
            NativeBattleVector controls = Vector(reference + 0x94);
            wobble = NativeBattleCinematicMonsterVertices.WobbleTable(controls.X, controls.Y, controls.Z);
            uint work = CoreDword(frame, manager + 0x74);
            for (int index = 0; index < 256; index++)
            {
                StoreWord(work + 2u * (uint)index, unchecked((ushort)wobble[index]));
                StoreWord(work + 0x400u + 2u * (uint)index, unchecked((ushort)wobble[512 + index]));
            }
            projectedWork = work + 0x800;
        }
        StoreDword(clone + 0x44, projectedWork);
        int drawn = 0;
        foreach (CinematicMonsterMesh item in meshes)
        {
            if ((mask & (1u << (item.ObjectIndex & 31))) == 0)
            {
                continue;
            }
            System.Numerics.Vector3[] worldVertices = new System.Numerics.Vector3[item.Mesh.Vertices.Length];
            NativeBattlePilotVisualIdentity meshIdentity = visualIdentity with { Primitive = unchecked((int)clone * 397 + item.ObjectIndex) };
            NativeBattleProjectedVertex[] projected = new NativeBattleProjectedVertex[item.Mesh.Vertices.Length];
            for (int index = 0; index < projected.Length; index++)
            {
                NativeBattleRawVertex vertex = item.Mesh.Vertices[index];
                NativeBattleActionMatrix basis = item.Bones[vertex.Bone];
                if (glow)
                {
                    projected[index] = NativeBattleGeometryProjection.Project50F900(basis, vertex.Position, projection, new(0, 0, 320, 208));
                    worldVertices[index] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(basis, vertex.Position), projection);
                }
                else
                {
                    NativeBattleVector transformed = NativeBattleGfMatrices.Transform460840(basis, vertex.Position);
                    NativeBattleVector changed = NativeBattleCinematicMonsterVertices.Wobble(transformed, wobble);
                    NativeBattleActionMatrix billboard = MonsterBillboardMatrix(frame);
                    NativeBattleModuleProjectedVertex point = NativeBattleModuleProjection.Project45FE10(billboard, changed, projection);
                    worldVertices[index] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(billboard, changed), projection);
                    projected[index] = new(point.Screen, (uint)(point.Depth >> 2 << 2));
                }
                StoreDword(projectedWork + 8u * (uint)index, projected[index].Screen);
                StoreDword(projectedWork + 8u * (uint)index + 4, projected[index].DepthAndClip);
            }
            NativeBattleGfGeometryBatch batch;
            if (glow)
            {
                uint state = SlotDword(0xC0);
                int groupOffset = unchecked((short)Word(state + 0x10u + 2u * (uint)drawn));
                uint colorAddress = unchecked(CoreDword(frame, manager + 0x74) + (uint)(groupOffset * 2));
                var snapshots = Enumerable.Range(0, projected.Length).Select(index => frame.Arena.ReadSnapshot(ArenaOffset(colorAddress + 4u * (uint)index, 4), 4)).ToArray();
                if (snapshots.Any(value => value.Known.Take(3).Any(known => !known)))
                {
                    throw new NotSupportedException("Native cinematic glow reached an unknown RGB source lane.");
                }
                uint[] colors = snapshots.Select(value => BinaryPrimitives.ReadUInt32LittleEndian(value.Data)).ToArray();
                batch = NativeBattleCinematicMonsterPackets.Glow(item.Mesh, projected, colors, OtBucket(frame.ReadTemporaryDword(0x14C)), snapshots.Select(value => value.Known[3]).ToArray(), worldVertices, meshIdentity);
            }
            else
            {
                batch = NativeBattleCinematicMonsterPackets.Pack(NativeBattleRawGeometryPackets.EmitProjected5099D0(item.Mesh.Polygons, projected, Dword(clone + 0x5C), OtBucket(frame.ReadTemporaryDword(0x14C)), 4, unchecked((short)Word(clone + 0x64)), null, definition.NativeGraphics!.Rules.SummonColors, pilotWorldVertices: worldVertices, pilotIdentity: meshIdentity));
            }
            uint output = CoreDword(frame, manager + 0x7C);
            var written = AppendNativeCinematicPackets(frame, output, null, batch, graphics);
            frame = WriteCoreDword(written.Frame, manager + 0x7C, unchecked(output + (uint)batch.AllocationBytes));
            graphics = written.Graphics;
            drawn++;
        }
        if (glow)
        {
            Shadow();
        }
        return (frame, graphics);

        void InitializeGlow(ImmutableArray<CinematicMonsterMesh> initial)
        {
            uint state = CoreDword(frame, manager + 0x74);
            SlotStore(0xC0, state);
            StoreDword(state + 4, state + 0x200);
            uint cursor = state + 0x200;
            int groupIndex = 0;
            NativeBattleVector origin = new(unchecked((short)SlotWord(frame, 0x94)), unchecked((short)SlotWord(frame, 0x96)), unchecked((short)SlotWord(frame, 0x98)));
            foreach (CinematicMonsterMesh item in initial)
            {
                ImmutableArray<NativeBattleRawVertexGroup> groups = item.Mesh.PackedVertexGroups ?? throw new NotSupportedException("Native cinematic glow requires its exact original vertex groups.");
                foreach (NativeBattleRawVertexGroup group in groups)
                {
                    StoreWord(state + 0x10u + 2u * (uint)groupIndex, unchecked((ushort)(cursor - Dword(state + 4))));
                    groupIndex++;
                    for (int vertex = 0; vertex < group.Count; vertex++)
                    {
                        NativeBattleVector point = NativeBattleScriptedMeshPreparation.Vector(group.Bytes.AsSpan().Slice(vertex * 6, 6));
                        StoreWord(cursor, NativeBattleCinematicMonsterVertices.Distance(NativeBattleGfMatrices.TransformMac460840(item.Bones[group.Bone], point), origin));
                        cursor += 2;
                    }
                }
            }
            StoreWord(state, unchecked((ushort)((cursor - Dword(state + 4)) / 2)));
            cursor = unchecked((cursor + 4) & ~3u);
            StoreDword(state + 12, cursor);
            StoreWord(state + 8, Word(payload + 2));
            NativeBattleCinematicTextureSelection selection = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, unchecked((short)Word(payload)));
            ushort[] palette = Enumerable.Range(0, 256).Select(index => Word(selection.SourceAddress + 2u * (uint)index)).ToArray();
            ImmutableArray<uint> ramp = NativeBattleCinematicMonsterVertices.Palette(palette);
            for (int index = 0; index < ramp.Length; index++)
            {
                uint rgb = ramp[index];
                frame = frame with { Arena = frame.Arena.Write(ArenaOffset(cursor + 4u * (uint)index, 3), [(byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16)]) };
            }
            (frame, _) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, unchecked(cursor + 1024 - state));
        }

        void Shadow()
        {
            NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
            if ((actor.Flags0 & 0x20) != 0)
            {
                return;
            }
            NativeBattleGraphicsDefinition native = definition.NativeGraphics ?? throw new NotSupportedException("Native cinematic monster shadow requires its graphics owner.");
            bool gouraud = native.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Native cinematic monster shadow requires its current device capability.");
            NativeBattleShadowSource source = native.Rules.ShadowSource ?? throw new NotSupportedException("Native cinematic monster shadow requires its original seed matrix/vector.");
            NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(actor, actor.GeometryBounds52 ?? throw new NotSupportedException("Native cinematic monster shadow requires its current model bounds."), projection.View ?? throw new NotSupportedException("Native cinematic monster shadow requires its current camera matrix."), source, gouraud);
            NativeBattleModuleProjectedVertex[] vertices = shape.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shape.CameraMatrix, vertex, projection)).ToArray();
            NativeBattleGfGeometryBatch batch = NativeBattleShadowPackets.EmitBatch5088A0(vertices, actor.Color44 ?? throw new NotSupportedException("Native cinematic monster shadow requires its current shadow color."), gouraud, 4121, 31, shape.Vertices.Select(vertex => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(shape.CameraMatrix, vertex), projection)).ToArray(), visualIdentity with { Primitive = unchecked((int)0x80000000) });
            uint output = CoreDword(frame, manager + 0x7C);
            var written = AppendNativeCinematicPackets(frame, output, null, batch, graphics);
            frame = WriteCoreDword(written.Frame, manager + 0x7C, unchecked(output + (uint)batch.AllocationBytes));
            graphics = written.Graphics;
        }

        uint ResolveNode(short selector)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
            int selected = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank), frame.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, selected));
        }
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        NativeBattleVector Vector(uint address) => new(unchecked((short)Word(address)), unchecked((short)Word(address + 2)), unchecked((short)Word(address + 4)));
        void StoreByte(uint address, byte value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 1), [value]) };
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
        void StoreDword(uint address, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        void SlotStore(int offset, uint value) => frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 4), BitConverter.GetBytes(value)) };
    }

    private static NativeBattleActionMatrix MonsterBillboardMatrix(NativeBattleWaterExecutionFrame frame)
    {
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(frame.Core.Read(0x2797968u + (uint)(index * 2), 2))).ToImmutableArray();
        return new(rotation, unchecked((int)CoreDword(frame, 0x27971E4)), unchecked((int)CoreDword(frame, 0x27971E8)), unchecked((int)CoreDword(frame, 0x27971EC)), 4096);
    }

    private static NativeBattleActionMatrix MonsterNodeMatrix(NativeBattleWaterExecutionFrame frame)
    {
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(frame.Core.Read(0x27979E8u + (uint)(index * 2), 2))).ToImmutableArray();
        return new(rotation, unchecked((int)CoreDword(frame, 0x27979FC)), unchecked((int)CoreDword(frame, 0x2797A00)), unchecked((int)CoreDword(frame, 0x2797A04)), 4096);
    }

    private static ImmutableArray<CinematicMonsterMesh> CinematicActorMeshes(NativeBattleGraphicsModel actor, NativeBattleGraphicsResource resource, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleActionMatrix node, bool includeWeapon)
    {
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native cinematic monster drawing requires its original body geometry.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native cinematic monster drawing requires its actual weapon-presence owner.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native cinematic monster drawing requires its actual weapon skeleton ownership."));
        ImmutableArray<CinematicMonsterMesh>.Builder result = ImmutableArray.CreateBuilder<CinematicMonsterMesh>();
        for (int index = 0; index < geometry.Length; index++)
        {
            NativeBattleRawMesh mesh = shared && index == 1 ? linked.Single() : geometry[index];
            NativeBattleModelTextureBinding? binding = shared && index == 1 ? actor.LinkedTextureSlots : resource.GeometryRelocated507550 ? null : actor.BodyTextureSlots;
            if (binding is not null)
            {
                mesh = mesh with { Polygons = mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, binding) }).ToImmutableArray() };
            }
            result.Add(new(mesh, bones, index));
        }
        if (includeWeapon && !shared && !linked.IsEmpty)
        {
            ImmutableArray<NativeBattleActionMatrix> weapon = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(actor, resource.LinkedPoseSource ?? throw new NotSupportedException("Native cinematic monster drawing requires its independent weapon rig."), node);
            for (int index = 0; index < linked.Length; index++)
            {
                NativeBattleRawMesh mesh = linked[index];
                NativeBattleModelTextureBinding binding = actor.LinkedTextureSlots ?? throw new NotSupportedException("Native cinematic monster drawing requires its weapon materials.");
                result.Add(new(mesh with { Polygons = mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, binding) }).ToImmutableArray() }, weapon, index));
            }
        }
        return result.ToImmutable();
    }
}
