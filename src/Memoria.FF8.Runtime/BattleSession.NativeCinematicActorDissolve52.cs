using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicActorDissolve52(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        NativeBattlePilotVisualIdentity visualIdentity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank));
        uint manager = CoreDword(frame, 0x27973EC);
        uint payload = Slot32(0xB8);
        byte actorIndex = ReadSlot(frame, 0x1B, 1)[0];
        uint actorAddress = CoreDword(frame, CoreDword(frame, 0x279744C) + 0x60u + actorIndex * 4u);
        if (actorIndex >= graphics.Models.Length || actorAddress != 0x1D972C0u + actorIndex * 156u)
        {
            throw new NotSupportedException("Native actor dissolve requires its actual physical actor directory.");
        }
        NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actorIndex);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Native actor dissolve requires its current body rig.");
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native actor dissolve requires its physical CPU resources.");
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native actor dissolve requires its current staging owner.");
        int physicalRow = actor.ActiveResourceRow100 ?? throw new NotSupportedException("Native actor dissolve requires its physical resource row.");
        NativeBattleModelCpuResourceRow row = cpu.Rows[physicalRow];
        NativeBattleModelCpuReference geometry = row.Require(16);
        List<ImmutableArray<ushort>> objectCounts = [];
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => NativeBattleModelEnemyHeap.Read(cpu, staging, geometry with { Offset = checked(geometry.Offset + offset) }, length), pose.BoneCount, (_, counts, _, _) => objectCounts.Add(counts));
        uint clone = Slot32(0xBC);
        if (clone == 0)
        {
            (frame, clone) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 0x80);
            if (visualIdentity.Effect == 265)
            {
                NativeBattlePilotVisualLifetimes.Allocated(frame.Arena, clone, 0);
            }
            SetSlot(0xBC, clone);
            Put32(clone + 0xC, actorAddress);
            RefreshClone();
            Put32(clone + 0x54, 0);
            Put32(clone + 0x58, 0xD00140);
            uint color = actor.Color40 ?? throw new NotSupportedException("Native actor dissolve clone requires its current actor color.");
            Put32(clone + 0x78, color);
            Put32(clone + 0x5C, color);
            Put32(clone + 0x60, actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native actor dissolve clone requires its current object mask."));
            Put16(clone + 0x64, 0);
            Put32(clone + 0x68, 0);
            Put32(clone + 0x50, uint.MaxValue);
            Put16(clone, 0);
            Put16(clone + 2, 0);
            Put(clone + 0x21, [0]);
            (frame, uint allocatedState) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 16);
            SetSlot(0xC0, allocatedState);
            int vertices = meshes.Sum(mesh => mesh.Vertices.Length);
            int triangles = objectCounts.Sum(counts => (int)unchecked((short)counts[0]));
            int quads = objectCounts.Sum(counts => (int)unchecked((short)counts[1]));
            frame = frame.WriteTemporaryDword(0xFC, unchecked((uint)triangles)).WriteTemporaryDword(0xF8, unchecked((uint)quads));
            Put16(allocatedState + 2, unchecked((ushort)vertices));
            ushort polygons = unchecked((ushort)(triangles + quads));
            Put16(allocatedState, polygons);
            uint accumulator = (unchecked((uint)vertices) & 0xFFFF0000u) | polygons;
            int counterBytes = checked((int)(unchecked(accumulator + 4) & 0xFFC));
            (frame, uint allocatedCounters) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, unchecked((uint)counterBytes));
            SetSlot(0xC4, allocatedCounters);
            if (counterBytes > 0)
            {
                Put(allocatedCounters, Enumerable.Repeat((byte)255, counterBytes).ToArray());
            }
            Put32(allocatedState + 4, unchecked(payload + (uint)(int)SignedWord(payload)));
            actor = actor with { Flags0 = (ushort)(actor.Flags0 | 4) };
            graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor) };
        }
        else if (Dword(clone + 0xC) != actorAddress)
        {
            throw new InvalidDataException("Native actor dissolve clone belongs to another physical actor.");
        }
        if (visualIdentity.Effect == 265)
        {
            visualIdentity = NativeBattleCinematic31Presentation.DissolveSource(visualIdentity, frame.Arena, clone, NativeBattleModelResourceRow.Address100(actor), geometry.PhysicalAddress);
        }
        uint state = Slot32(0xC0);
        uint counters = Slot32(0xC4);
        frame = frame.WriteTemporaryDword(0x80, payload).WriteTemporaryDword(0x84, clone).WriteTemporaryDword(0x88, state).WriteTemporaryDword(0x8C, counters);
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        int referenceIndex = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank), frame.Core.Read(0x2797454, 128).ToImmutableArray(), SignedWord(payload + 2));
        uint reference = unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, referenceIndex));
        RefreshClone();
        short boundary = unchecked((short)SlotWord(frame, 0x94));
        short translationY = unchecked((short)SlotWord(frame, 0x96));
        short clipHeight = unchecked((short)SlotWord(frame, 0x98));
        uint radiusAddress = CoreDword(frame, manager + 0x74);
        ImmutableArray<int> radius = NativeBattleCinematicActorDissolve52.BuildRadiusTable(boundary, SignedWord(reference + 0x8C), SignedWord(reference + 0x8E), SignedWord(reference + 0x90), SignedWord(reference + 0x94), SignedWord(reference + 0x96));
        for (int index = 0; index < radius.Length; index++)
        {
            Put32(radiusAddress + checked((uint)(index * 4)), unchecked((uint)radius[index]));
        }
        uint projectedWork = unchecked(radiusAddress + checked((uint)(radius.Length * 4)));
        frame = frame.WriteTemporaryDword(0x60, radiusAddress).WriteTemporaryDword(0x64, projectedWork).WriteTemporaryDword(0x6C, Dword(state + 4)).WriteTemporaryDword(0x70, unchecked((uint)(int)boundary)).WriteTemporaryDword(0x74, unchecked((uint)(int)translationY)).WriteTemporaryDword(0x78, unchecked((uint)(int)clipHeight));
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, pose);
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native actor dissolve requires its retained entity matrix.");
        NativeBattleActionMatrix basis = world with { X = 0, Y = translationY, Z = 0 };
        StoreTemporaryMatrix(0xE0, basis);
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, basis);
        graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor) };
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, pose);
        Put32(clone + 0x44, projectedWork);
        NativeBattleActionMatrix view = MonsterNodeMatrix(frame);
        NativeBattleVector position = actor.Transform?.Position28 ?? throw new NotSupportedException("Native actor dissolve requires its current model position.");
        PublishPackedInput(position);
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(view, position);
        frame = frame.WriteTemporaryDword(0xF4, unchecked((uint)translated.X)).WriteTemporaryDword(0xF8, unchecked((uint)translated.Y)).WriteTemporaryDword(0xFC, unchecked((uint)translated.Z)).WriteTemporaryDword(0xE0, translated.Flags);
        NativeBattleActionMatrix projectionMatrix = view with { X = translated.X, Y = translated.Y, Z = translated.Z };
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native actor dissolve requires its actual projection controls.");
        NativeBattleScriptedGraphicsMemory controls = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native actor dissolve requires its retained GTE controls.");
        for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
        {
            NativeBattleRawMesh mesh = meshes[meshIndex];
            ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Native actor dissolve requires its original packed vertex groups.");
            System.Numerics.Vector3[] worldVertices = new System.Numerics.Vector3[mesh.Vertices.Length];
            int vertexIndex = 0;
            foreach (NativeBattleRawVertexGroup group in groups)
            {
                if (group.Bone >= bones.Length)
                {
                    throw new InvalidDataException("Native actor dissolve references an absent current bone matrix.");
                }
                frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)(int)group.Count));
                for (int local = 0; local < group.Count; local++)
                {
                    NativeBattleRawVertex vertex = mesh.Vertices[vertexIndex + local];
                    PublishPackedInput(vertex.Position);
                    NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(bones[group.Bone], vertex.Position);
                    PublishMac(mac);
                    uint address = projectedWork + checked((uint)((vertexIndex + local) * 8));
                    PutVector(address, new(unchecked((short)mac.X), unchecked((short)mac.Y), unchecked((short)mac.Z)));
                }
                for (int local = 0; local < group.Count; local++)
                {
                    uint address = projectedWork + checked((uint)((vertexIndex + local) * 8));
                    NativeBattleVector captured = Vector(address);
                    NativeBattleCinematicActorDissolve52Deformation changed = NativeBattleCinematicActorDissolve52.DeformVertex(captured, boundary, clipHeight, index => unchecked((int)Dword(unchecked(radiusAddress + (uint)(index * 4)))), (x, z) =>
                    {
                        (frame, int angle) = NativeBattleCinematicRibbonBasis.Angle(frame, 0, 0, x, z);
                        return angle;
                    });
                    PutVector(address, changed.Vertex);
                    Put(address + 6, [changed.Clipped]);
                    PublishPackedInput(changed.Vertex);
                    NativeBattleModuleDepthCueVertex point = NativeBattleModuleProjection.ProjectWithCue45FAA0(projectionMatrix, changed.Vertex, projection, controls.Dqa, controls.Dqb);
                    NativeBattleEffectMemory memory = RequireWaterMemory();
                    NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor dissolve lost its packed GTE input owner.");
                    NativeEffectMemory = memory with { ScriptedColorRegisters = registers with { Ir = registers.Ir.SetItem(1, point.Ir2), Flags = point.Projected.Flags } };
                    worldVertices[vertexIndex + local] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(projectionMatrix, changed.Vertex), projection);
                    Put32(address, point.Projected.Screen);
                    Put16(address + 4, point.Projected.Depth);
                }
                vertexIndex += group.Count;
            }
            if (vertexIndex != mesh.Vertices.Length)
            {
                throw new InvalidDataException("Native actor dissolve traversed a different packed vertex owner.");
            }
            uint output = CoreDword(frame, manager + 0x7C);
            frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)(int)SignedWord(payload + 4)));
            int faceCount = objectCounts[meshIndex][0] + objectCounts[meshIndex][1];
            // AD9840 restarts its physical counter pointer for each object invocation.
            for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
            {
                NativeBattleRawPolygon face = mesh.Polygons[faceIndex];
                byte before = Read(counters + (uint)faceIndex, 1)[0];
                NativeBattleCinematicActorDissolve52Emission emitted = NativeBattleCinematicActorDissolve52.EmitFace(face.Bytes, face.Quad, before, SignedWord(payload + 4), index =>
                {
                    uint address = projectedWork + checked((uint)(index * 8));
                    return new(Dword(address), Word(address + 4), Read(address + 6, 1)[0]);
                }, index => Dword(unchecked(Dword(state + 4) + (uint)(index * 4))), frame.ReadTemporaryDword(0x14C), visualIdentity.Effect == 265 ? visualIdentity with { Primitive = unchecked(meshIndex * 65537 + faceIndex) } : null, visualIdentity.Effect == 265 ? index => worldVertices[index] : null);
                if (emitted.Counter != before)
                {
                    Put(counters + (uint)faceIndex, [emitted.Counter]);
                }
                NativeBattleGfGeometryBatch captured = emitted.Packets;
                if (visualIdentity.Effect != 265)
                {
                    int[] indices = Enumerable.Range(0, face.Quad ? 4 : 3).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(corner * 2)) & 4095).ToArray();
                    NativeBattlePilotVisualGeometry pilotGeometry = new(visualIdentity with { Primitive = unchecked(meshIndex * 65537 + faceIndex) }, worldVertices[indices[0]], worldVertices[indices[1]], worldVertices[indices[2]], face.Quad ? worldVertices[indices[3]] : default, checked((byte)indices.Length));
                    captured = emitted.Packets with { Attempts = emitted.Packets.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { PilotGeometry = pilotGeometry } } : attempt).ToImmutableArray() };
                }
                var appended = AppendNativeCinematicPackets(frame, output, emitted.OrderingAddress, captured, graphics);
                frame = appended.Frame;
                graphics = appended.Graphics;
                output = unchecked(output + (uint)emitted.Packets.AllocationBytes);
            }
            frame = WriteCoreDword(frame, manager + 0x7C, output);
        }
        // B67040 uses the shared current rig and restores that same skeleton through its descriptor.
        actor = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[actorIndex], pose);
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, world);
        graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor) };
        frame = DrawDissolveRing(frame);
        graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[actorIndex], pose)) };
        return (frame, graphics);

        NativeBattleWaterExecutionFrame DrawDissolveRing(NativeBattleWaterExecutionFrame current)
        {
            NativeBattleCinematicBrothersModelServices services = new(_ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (active, address) =>
            {
                NativeBattleEffectMemory memory = RequireWaterMemory();
                NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor dissolve ring requires its packed GTE input owner.");
                NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(active, address, registers) };
                return active;
            }, (active, matrix, address) =>
            {
                NativeBattleScriptedGraphicsMemory currentControls = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native actor dissolve ring requires its depth-cue controls.");
                NativeBattleVector input = NativeBattleScriptedMeshPreparation.Vector(active.ReadTemporary(checked((int)(address - active.TemporaryFrame.Address)), 6));
                NativeBattleModuleDepthCueVertex point = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, input, NativeCamera?.Projection ?? throw new NotSupportedException("Native actor dissolve ring requires its actual projection owner."), currentControls.Dqa, currentControls.Dqb);
                return new(active, point.Projected.Screen, point.Cue, point.Projected.Flags) { PilotWorld = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, input), projection) };
            }, () =>
            {
                _ = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native actor dissolve ring requires its current main ordering-table owner.");
                return 0;
            }, (active, output, ordering, batch) =>
            {
                var appended = AppendNativeCinematicPackets(active, output, ordering, batch, graphics);
                graphics = appended.Graphics;
                return appended.Frame;
            });
            services = services with { PilotIdentity = visualIdentity };
            ImmutableArray<NativeBattleActionMatrix> currentBones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actorIndex], pose);
            return NativeBattleCinematicBrothersModel3.DrawPreparedRing(current, currentBones.Length, currentBones[0].X, currentBones[0].Z, (_, bone) => NativeBattleBodyBoneMatrices.Require(graphics.Models[actorIndex], pose)[bone], (_, bone) => BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(0x12 + bone * 48, 2)), new(0, 0, 0), -1, 0, unchecked((short)SlotWord(current, 0x8C)), NativeBattleCameraMath.Sine(512), null, services);
        }

        void RefreshClone()
        {
            // Descriptor handle bytes remain symbolic; the typed current pose and CPU row own their resources.
            frame = frame with { Arena = frame.Arena.MarkUnknown(ArenaOffset(clone + 0x10, 12), 12) };
            foreach (int field in new[] { 12, 16, 20, 24 })
            {
                Put32(clone + 0x30u + (uint)(field - 12), row.Require(field).PhysicalAddress);
            }
        }

        void PublishPackedInput(NativeBattleVector input)
        {
            byte[] packed = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(packed, input.X);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(2), input.Y);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(4), input.Z);
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor dissolve requires its retained GTE input owner.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = registers with { NormalInput = packed.ToImmutableArray(), NormalInputKnown = null } };
        }

        void PublishMac(NativeBattleFullMacVertex mac)
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor dissolve requires its retained GTE registers.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = registers with { Mac = [mac.X, mac.Y, mac.Z], Ir = [Math.Clamp(mac.X, short.MinValue, short.MaxValue), Math.Clamp(mac.Y, short.MinValue, short.MaxValue), Math.Clamp(mac.Z, short.MinValue, short.MaxValue)], Flags = mac.Flags } };
        }

        void StoreTemporaryMatrix(int offset, NativeBattleActionMatrix matrix)
        {
            for (int index = 0; index < 9; index++)
            {
                frame = frame.WriteTemporaryWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
            }
            frame = frame.WriteTemporaryDword(offset + 20, unchecked((uint)matrix.X)).WriteTemporaryDword(offset + 24, unchecked((uint)matrix.Y)).WriteTemporaryDword(offset + 28, unchecked((uint)matrix.Z));
            frame.TemporaryFrame.MarkUnknown(offset + 18, 2);
        }

        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
        uint Slot32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        NativeBattleVector Vector(uint address) => new(SignedWord(address), SignedWord(address + 2), SignedWord(address + 4));
        void Put(uint address, ReadOnlySpan<byte> bytes) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, bytes.Length), bytes) };
        void Put16(uint address, ushort value) => Put(address, BitConverter.GetBytes(value));
        void Put32(uint address, uint value) => Put(address, BitConverter.GetBytes(value));
        void SetSlot(int offset, uint value) => frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 4), BitConverter.GetBytes(value)) };
        void PutVector(uint address, NativeBattleVector value)
        {
            Put16(address, unchecked((ushort)value.X));
            Put16(address + 2, unchecked((ushort)value.Y));
            Put16(address + 4, unchecked((ushort)value.Z));
        }
    }
}
