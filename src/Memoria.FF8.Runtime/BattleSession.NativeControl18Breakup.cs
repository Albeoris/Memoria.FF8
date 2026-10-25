using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeControl18Breakup(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        uint manager = CoreDword(frame, 0x27973EC);
        uint payload = Slot32(0xB8);
        byte actorIndex = ReadSlot(frame, 0x1B, 1)[0];
        uint actorPointer = CoreDword(frame, CoreDword(frame, 0x279744C) + 0x60u + actorIndex * 4u);
        if (actorIndex >= graphics.Models.Length || actorPointer != 0x1D972C0u + actorIndex * 156u)
        {
            throw new NotSupportedException("Native breakup drawing requires its actual physical actor directory.");
        }
        NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actorIndex);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Native breakup drawing requires its current body rig.");
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native breakup drawing requires its physical CPU resources.");
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native breakup drawing requires its current shared staging owner.");
        int physicalRow = actor.ActiveResourceRow100 ?? throw new NotSupportedException("Native breakup drawing requires its physical resource row.");
        NativeBattleModelCpuResourceRow row = cpu.Rows[physicalRow];
        NativeBattleModelCpuReference geometry = row.Require(16);
        uint geometryAddress = geometry.PhysicalAddress;
        var objectRecords = new List<(ImmutableArray<ushort> Counts, int Offset)>();
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => ReadGeometry(checked(geometryAddress + (uint)offset), length), pose.BoneCount, (_, counts, offset, _) => objectRecords.Add((counts, offset)));
        ushort mode = Word(payload + 8);
        if (mode is not (1 or 2 or 5 or 7))
        {
            throw new NotSupportedException($"Native breakup drawing requires its selected control program: mode {Word(payload + 8)}, hook {Word(payload + 0xE)}, auxiliary capacity {Word(payload + 0x2E)}, emissions {Word(payload + 0x30)}.");
        }
        bool initializeNormals = false;
        bool initializeRadialCache = false;
        uint clone = Slot32(0xBC);
        if (clone == 0)
        {
            (frame, clone) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 0x80);
            SetSlot(0xBC, clone);
            Put32(clone + 0xC, actorPointer);
            Put32(clone + 0x54, 0);
            Put32(clone + 0x58, 0xD00140);
            uint color = actor.Color40 ?? throw new NotSupportedException("Native breakup clone requires its current body color.");
            Put32(clone + 0x78, color);
            Put32(clone + 0x5C, color);
            Put32(clone + 0x60, actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native breakup clone requires its current object mask."));
            Put16(clone + 0x64, 0);
            Put32(clone + 0x68, 0);
            Put32(clone + 0x50, uint.MaxValue);
            Put16(clone, 0);
            Put16(clone + 2, 0);
            Put(clone + 0x21, [0]);
            int capacity = Word(payload + 0xA);
            (frame, uint allocatedWork) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, checked((uint)(32 + capacity * 68)));
            SetSlot(0xC0, allocatedWork);
            Put16(allocatedWork, 0);
            Put16(allocatedWork + 2, unchecked((ushort)(capacity - 1)));
            Put16(allocatedWork + 6, 0);
            for (int index = 0; index < 3; index++)
            {
                Put32(allocatedWork + 8u + 4u * (uint)index, unchecked(payload + (uint)(short)Word(payload + (uint)(index * 2))));
            }
            if (Word(payload + 0x2E) != 0)
            {
                frame = NativeBattleCinematicBreakupAuxiliary.Initialize(frame, allocatedWork, payload);
            }
            int faceCount = objectRecords.Sum(item => item.Counts[0] + item.Counts[1]);
            Put16(allocatedWork + 4, unchecked((ushort)faceCount));
            int flagBytes = (faceCount + 4) & ~3;
            (frame, uint allocatedFlags) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, unchecked((uint)flagBytes));
            SetSlot(0xC4, allocatedFlags);
            Put(allocatedFlags, new byte[flagBytes]);
            for (int index = 0; index < capacity; index++)
            {
                Put32(allocatedWork + 32u + (uint)(index * 68), 0);
            }
            if (mode == 5)
            {
                (frame, uint allocatedAnchors) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, checked((uint)(faceCount * 4)));
                Put32(allocatedWork + 0x18, allocatedAnchors);
                initializeRadialCache = true;
            }
            Put32(allocatedWork + 0x14, 0);
            if (ReadSlot(frame, 0xE1, 1)[0] != 0)
            {
                (frame, uint allocatedNormals) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, checked((uint)(faceCount * 8)));
                Put32(allocatedWork + 0x14, allocatedNormals);
                initializeNormals = true;
            }
            actor = actor with { Flags0 = (ushort)(actor.Flags0 | 4) };
        }
        uint work = Slot32(0xC0);
        uint states = Slot32(0xC4);
        int recordsLength = (Word(work + 2) + 1) * 68;
        int polygonCount = Word(work + 4);
        frame = frame.WriteTemporaryDword(0x90, payload).WriteTemporaryDword(0x80, clone).WriteTemporaryDword(0x84, work).WriteTemporaryDword(0x88, states).WriteTemporaryDword(0x8C, Slot32(0xA0)).WriteTemporaryDword(0x48, Dword(work + 8)).WriteTemporaryDword(0x4C, Dword(work + 12));
        frame = frame with { Arena = frame.Arena.MarkUnknown(ArenaOffset(clone + 0x10, 12), 12) };
        foreach (int field in new[] { 12, 16, 20, 24 })
        {
            Put32(clone + 0x30u + (uint)(field - 12), row.Require(field).PhysicalAddress);
        }
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, pose);
        ImmutableArray<NativeBattleActionMatrix> localBones = NativeBattleBodyBoneMatrices.Require(actor, pose);
        if (initializeRadialCache)
        {
            byte[] globalBasis = frame.Core.Read(0x2797968, 32);
            NativeBattleActionMatrix anchorBasis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(globalBasis.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(globalBasis.AsSpan(20)), BinaryPrimitives.ReadInt32LittleEndian(globalBasis.AsSpan(24)), BinaryPrimitives.ReadInt32LittleEndian(globalBasis.AsSpan(28)), 4096);
            NativeBattleWaterActorAnchorResult anchor = CreateNativeActorAnchorServices().EvaluateB66B80(frame, pose.Skeleton, localBones, 0xF1, 4096, anchorBasis);
            frame = anchor.Frame;
            List<NativeBattleControl18Face> radialFaces = BuildFaces();
            NativeBattleCinematicBreakupMode5Step initialized = NativeBattleCinematicBreakupMode5.Initialize(frame, new(unchecked((short)anchor.Anchor.X), unchecked((short)anchor.Anchor.Y), unchecked((short)anchor.Anchor.Z)), radialFaces);
            frame = initialized.Frame;
            Put(Dword(work + 0x18), initialized.Anchors.AsSpan());
        }
        if (initializeNormals)
        {
            uint normalPointer = Dword(work + 0x14);
            uint scratch = CoreDword(frame, manager + 0x74) + checked((uint)(meshes.Sum(mesh => mesh.Vertices.Length) * 8));
            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                NativeBattleRawMesh mesh = meshes[meshIndex];
                NativeBattleVector[] points = mesh.Vertices.Select((vertex, index) => TransformLocal(localBones[vertex.Bone], vertex.Position, scratch + (uint)(index * 8), true)).ToArray();
                int faces = objectRecords[meshIndex].Counts[0] + objectRecords[meshIndex].Counts[1];
                for (int faceIndex = 0; faceIndex < faces; faceIndex++)
                {
                    ReadOnlySpan<byte> face = mesh.Polygons[faceIndex].Bytes.AsSpan();
                    NativeBattleVector normal = NativeBattleCinematicBreakupLighting.FaceNormal(points[BinaryPrimitives.ReadUInt16LittleEndian(face) & 0xFFF], points[BinaryPrimitives.ReadUInt16LittleEndian(face[2..]) & 0xFFF], points[BinaryPrimitives.ReadUInt16LittleEndian(face[4..]) & 0xFFF]);
                    Put16(normalPointer, unchecked((ushort)normal.X));
                    Put16(normalPointer + 2, unchecked((ushort)normal.Y));
                    Put16(normalPointer + 4, unchecked((ushort)normal.Z));
                    normalPointer += 8;
                }
            }
        }
        byte[] recordBytes = frame.Arena.ReadSnapshot(ArenaOffset(work + 32, recordsLength), recordsLength).Data;
        byte[] polygonStates = Read(states, polygonCount);
        if (CoreDword(frame, 0x27973B8) != frame.TemporaryFrame.Address + 0x100)
        {
            throw new NotSupportedException("Native breakup requires its actual current callback control binding.");
        }
        if (frame.ReadTemporaryByte(0x145) == 0)
        {
            short controlX = unchecked((short)SlotWord(frame, 0x94));
            short controlZ = unchecked((short)SlotWord(frame, 0x98));
            int limit = controlZ == 0 ? 0x7FFF : polygonStates.Count(value => value == 0) / (controlZ + 1);
            if (limit == 0)
            {
                limit = 1;
            }
            frame = frame.WriteTemporaryDword(0x50, mode).WriteTemporaryDword(0x54, unchecked((uint)controlX)).WriteTemporaryDword(0x58, unchecked((uint)controlZ)).WriteTemporaryDword(0x5C, 0).WriteTemporaryDword(0x40, unchecked((uint)limit)).WriteTemporaryDword(0x44, unchecked((uint)limit)).WriteTemporaryDword(0xF0, unchecked((uint)(short)SlotWord(frame, 0x96)));
            List<NativeBattleControl18Face> faces = BuildFaces();
            NativeBattleControl18SpawnServices? spawnServices = mode is 1 or 5 or 7 || Word(payload + 0xE) != 0 || Word(payload + 0x2E) != 0 || Word(payload + 0x30) != 0 ? new(limit, (random, x, y, z, count) =>
            {
                frame = frame with { Random = random };
                for (int index = 0; index < count; index++)
                {
                    frame = NativeBattleCinematicBreakupAuxiliary.Emit(frame, x, y, z);
                }
                return frame.Random;
            })
            {
                EmitFragmentHook = (random, x, y, z) =>
                {
                    frame = frame with { Random = random };
                    frame = NativeBattleCinematicBreakupParticleHook.Emit(frame, x, y, z, ResolveHookOwner);
                    return frame.Random;
                }
            } : null;
            NativeBattleControl18SpawnStep spawn = mode == 5 ? NativeBattleCinematicBreakupMode5.Advance(recordBytes, polygonStates, Word(work + 6), Read(payload, 0x4C), faces, unchecked((short)SlotWord(frame, 0x96)), Read(Dword(work + 0x18), polygonCount * 4), frame.Random, spawnServices!) : NativeBattleControl18Spawn.Advance(recordBytes, polygonStates, Word(work + 6), Read(payload, spawnServices is not null ? 0x4C : 0x32), faces, unchecked((short)SlotWord(frame, 0x96)), frame.Random, spawnServices);
            frame = frame with { Random = spawn.Random };
            if (spawn.FallbackCountdown is int countdown)
            {
                frame = frame.WriteTemporaryDword(0x44, unchecked((uint)countdown));
            }
            PublishRecords(recordBytes, spawn.Records.AsSpan(), true);
            Put(states, spawn.PolygonStates.AsSpan());
            Put16(work + 6, spawn.LiveCount);
            if (spawn.LastDistance is int distance)
            {
                frame = frame.WriteTemporaryDword(0xFC, unchecked((uint)distance));
                for (int index = 0; index < spawn.LastVertices.Length; index++)
                {
                    NativeBattleVector point = spawn.LastVertices[index];
                    frame = frame.WriteTemporaryWord(0xC0 + index * 8, unchecked((ushort)point.X)).WriteTemporaryWord(0xC2 + index * 8, unchecked((ushort)point.Y)).WriteTemporaryWord(0xC4 + index * 8, unchecked((ushort)point.Z));
                }
            }
            NativeBattleControl18PhysicsStep physics = NativeBattleControl18Physics.Advance(spawn.Records.AsSpan(), spawn.PolygonStates.AsSpan(), spawn.LiveCount, mode);
            PublishRecords(spawn.Records.AsSpan(), physics.Records.AsSpan(), false);
            Put(states, physics.PolygonStates.AsSpan());
            Put16(work + 6, physics.LiveCount);
            recordBytes = physics.Records.ToArray();
            polygonStates = physics.PolygonStates.ToArray();
        }
        NativeBattleWaterSlotBank matrixBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        uint matrixAddress = NativeBattleWaterMatrixCommands.ResolveB65520(frame, matrixBank, SlotWord(frame, 0x9C));
        byte[] matrixBytes = frame.Core.Read(matrixAddress, 32);
        NativeBattleActionMatrix parentMatrix = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(matrixBytes.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(matrixBytes.AsSpan(20)), BinaryPrimitives.ReadInt32LittleEndian(matrixBytes.AsSpan(24)), BinaryPrimitives.ReadInt32LittleEndian(matrixBytes.AsSpan(28)), 4096);
        NativeBattleEffectMemory parentMemory = RequireWaterMemory();
        NativeBattleScriptedColorRegisters parentRegisters = NativeBattleCinematicRibbonInput.Capture(frame, 0x27971E4, parentMemory.ScriptedColorRegisters ?? throw new NotSupportedException("Native breakup parent composition requires its current packed input owner."));
        NativeBattleVector parentInput = new(unchecked((short)CoreWord(frame, 0x27971E4)), unchecked((short)CoreWord(frame, 0x27971E6)), unchecked((short)CoreWord(frame, 0x27971E8)));
        NativeEffectMemory = parentMemory with { ScriptedColorRegisters = TransformRegisters(parentRegisters, NativeBattleGfMatrices.TransformMac460840(parentMatrix, parentInput)) };
        NativeBattleCinematicScaledBasisResult prepared = NativeBattleCinematicScaledBasis.Prepare(frame, resourceLength);
        frame = prepared.Frame;
        byte[] rawBasis = frame.ReadTemporary(0xE0, 32);
        NativeBattleActionMatrix bodyBasis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rawBasis.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(rawBasis.AsSpan(20)), BinaryPrimitives.ReadInt32LittleEndian(rawBasis.AsSpan(24)), BinaryPrimitives.ReadInt32LittleEndian(rawBasis.AsSpan(28)), 4096);
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, bodyBasis);
        graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor) };
        Put32(clone + 0x44, CoreDword(frame, manager + 0x74));
        int highestLife = Enumerable.Range(0, recordBytes.Length / 68).Where(index => BinaryPrimitives.ReadUInt32LittleEndian(recordBytes.AsSpan(index * 68)) != 0).Select(index => (int)unchecked((sbyte)recordBytes[index * 68 + 0x2E])).DefaultIfEmpty(0).Max();
        DrawBody();
        if (Word(payload + 0x2E) != 0)
        {
            frame = NativeBattleCinematicBreakupAuxiliary.AdvanceDraw(frame, new((current, position) =>
            {
                frame = current;
                NativeBattleModuleProjectedVertex point = Project(position);
                return new(frame, point.Screen, point.Depth >> 2, point.Flags);
            }, (current, values) =>
            {
                NativeBattleEffectMemory memory = RequireWaterMemory();
                int effectId = graphics.OriginalEffect?.Binding.Effect ?? throw new NotSupportedException("Native breakup depth publication requires its current module identity.");
                NativeEffectMemory = memory with { CinematicBreakupPayload4 = (memory.CinematicBreakupPayload4 ?? ImmutableDictionary<int, ImmutableArray<int>>.Empty).SetItem(effectId, values) };
                return current;
            }, (current, output, ordering, batch) =>
            {
                var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
                graphics = written.Graphics;
                return written.Frame;
            }));
        }
        Put16(clone, unchecked((ushort)(Word(clone) + 1)));
        return (frame, graphics);

        void DrawBody()
        {
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native breakup body requires its current camera projection.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, pose);
            int globalFace = 0;
            frame = frame.WriteTemporaryDword(0x60, 0);
            uint mask = Dword(clone + 0x60);
            uint bodyColor = Dword(clone + 0x5C);
            NativeBattleEffectMemory colorMemory = RequireWaterMemory();
            NativeBattleModelColorOverride colorOverride = (colorMemory.ModelColorOverrides ?? throw new NotSupportedException("Native breakup requires its actual global model color owner.")).Slots[0];
            if ((colorOverride.Actor ?? throw new NotSupportedException("Native breakup requires its retained color override selector.")) != 255)
            {
                bodyColor = colorOverride.Color ?? throw new NotSupportedException("Native breakup requires its retained override RGB.");
                Put32(clone + 0x5C, bodyColor & 0xFFFFFF);
            }
            NativeBattlePrimitiveProcess renderer = colorMemory.PrimitiveProcess ?? throw new NotSupportedException("Native breakup body requires its actual color register owner.");
            NativeEffectMemory = colorMemory with { PrimitiveProcess = renderer.PublishModelColor(bodyColor) };
            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                NativeBattleRawMesh mesh = meshes[meshIndex];
                var directory = objectRecords[meshIndex];
                int faces = directory.Counts[0] + directory.Counts[1];
                if ((mask & (1u << (meshIndex & 31))) != 0)
                {
                    byte[] projected = new byte[mesh.Vertices.Length * 8];
                    for (int index = 0; index < mesh.Vertices.Length; index++)
                    {
                        NativeBattleRawVertex vertex = mesh.Vertices[index];
                        NativeBattleEffectMemory projectionMemory = RequireWaterMemory();
                        NativeBattleScriptedGraphicsMemory cue = projectionMemory.ScriptedGraphics ?? throw new NotSupportedException("Native breakup body projection requires its depth-cue owner.");
                        NativeBattleModuleDepthCueVertex point = NativeBattleControl18BodyProjection.Project50FD40(bones[vertex.Bone], vertex.Position, projection, cue.Dqa, cue.Dqb);
                        NativeBattleScriptedColorRegisters registers = projectionMemory.ScriptedColorRegisters ?? throw new NotSupportedException("Native breakup body projection requires its packed input owner.");
                        int packedIndex = index;
                        ImmutableArray<byte> packedInput = default;
                        foreach (NativeBattleRawVertexGroup group in mesh.PackedVertexGroups ?? throw new NotSupportedException("Native breakup body projection requires its original packed vertex groups."))
                        {
                            if (packedIndex < group.Count)
                            {
                                packedInput = group.Bytes.AsSpan().Slice(packedIndex * 6, 8).ToArray().ToImmutableArray();
                                break;
                            }
                            packedIndex -= group.Count;
                        }
                        if (packedInput.IsDefault)
                        {
                            throw new InvalidDataException("Native breakup body projection leaves its original vertex groups.");
                        }
                        NativeEffectMemory = projectionMemory with { ScriptedColorRegisters = registers with { NormalInput = packedInput, NormalInputKnown = null, Ir = registers.Ir.SetItem(1, point.Ir2), Flags = point.Projected.Flags } };
                        BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(index * 8), point.Projected.Screen);
                        BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(index * 8 + 4), (point.Projected.Flags & 0xFFFF0000) | point.Projected.Depth);
                    }
                    Put(CoreDword(frame, manager + 0x74), projected);
                    Put16(clone + 0x48, directory.Counts[0]);
                    Put16(clone + 0x4A, directory.Counts[1]);
                    Put16(clone + 0x4C, directory.Counts[2]);
                    Put16(clone + 0x4E, directory.Counts[3]);
                    Put32(clone + 0x40, geometryAddress + (uint)directory.Offset);
                    Func<int, ImmutableArray<uint>, uint, uint>? light = ReadSlot(frame, 0xE1, 1)[0] != 0 ? ShadeFace : null;
                    NativeBattleGfGeometryBatch body = NativeBattleControl18Body.Emit(ReadGeometry(geometryAddress + (uint)directory.Offset, directory.Counts[0] * 16), ReadGeometry(geometryAddress + (uint)directory.Offset + (uint)(directory.Counts[0] * 16), directory.Counts[1] * 20), projected, polygonStates.AsSpan(globalFace, faces), bodyColor, light, OtBucket(frame.ReadTemporaryDword(0x14C)));
                    Append(body);
                    globalFace += faces;
                    frame = frame.WriteTemporaryDword(0x60, unchecked((uint)globalFace));
                    uint shadeAddress = Dword(work + 8);
                    uint[] shades = Enumerable.Range(0, highestLife + 1).Select(index => Dword(shadeAddress + 4u * (uint)index)).ToArray();
                    NativeBattleGfGeometryBatch shards = NativeBattleControl18Shards.Emit(recordBytes, (address, length) => ReadGeometry(address, length).ToImmutableArray(), shades, Project, OtBucket(frame.ReadTemporaryDword(0x14C)));
                    Append(shards);
                }
            }
            uint ShadeFace(int faceIndex, ImmutableArray<uint> screen, uint material)
            {
                uint address = Dword(work + 0x14) + checked((uint)((globalFace + faceIndex) * 8));
                NativeBattleEffectMemory memory = RequireWaterMemory();
                NativeBattleScriptedColorRegisters registers = NativeBattleCinematicRibbonInput.Capture(frame, address, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native breakup lighting requires its current packed input owner."));
                NativeBattleGeometryRegisters geometryRegisters = memory.GeometryRegisters ?? throw new NotSupportedException("Native breakup lighting requires its current geometry registers.");
                NativeBattleGeometryRegisterStep clipped = NativeBattleGeometry.Clip45EE10(new(geometryRegisters with { Sxy = [screen[0], screen[1], screen[2]] }, registers));
                registers = NativeBattleCinematicBreakupLighting.Shade(memory.ScriptedLighting ?? throw new NotSupportedException("Native breakup lighting requires its current light controls."), registers.NormalInput!.Value.AsSpan(), Dword(clone + 0x5C), clipped.Colors);
                NativeEffectMemory = memory with { ScriptedColorRegisters = registers, GeometryRegisters = clipped.Geometry };
                uint color = registers.Rgb[2] ?? throw new NotSupportedException("Native breakup lighting requires its current RGB output.");
                frame = frame.WriteTemporaryDword(0xFC, color);
                return color;
            }
        }
        List<NativeBattleControl18Face> BuildFaces()
        {
            var faces = new List<NativeBattleControl18Face>();
            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                NativeBattleRawMesh mesh = meshes[meshIndex];
                NativeBattleVector[] points = mesh.Vertices.Select((vertex, index) => TransformLocal(localBones[vertex.Bone], vertex.Position, CoreDword(frame, manager + 0x74) + (uint)(index * 8))).ToArray();
                uint cursor = geometryAddress + (uint)objectRecords[meshIndex].Offset;
                int plain = objectRecords[meshIndex].Counts[0] + objectRecords[meshIndex].Counts[1];
                for (int faceIndex = 0; faceIndex < plain; faceIndex++)
                {
                    NativeBattleRawPolygon face = mesh.Polygons[faceIndex];
                    int corners = face.Quad ? 4 : 3;
                    ImmutableArray<NativeBattleVector> vertices = Enumerable.Range(0, corners).Select(index => points[BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(index * 2)) & 0xFFF]).ToImmutableArray();
                    faces.Add(new(cursor, face.Quad, vertices));
                    cursor += face.Quad ? 20u : 16u;
                }
            }
            return faces;
        }
        NativeBattleVector TransformLocal(NativeBattleActionMatrix matrix, NativeBattleVector input, uint destination, bool wrap = false)
        {
            frame = frame.WriteTemporaryWord(0xF0, unchecked((ushort)input.X)).WriteTemporaryWord(0xF2, unchecked((ushort)input.Y)).WriteTemporaryWord(0xF4, unchecked((ushort)input.Z));
            if (wrap)
            {
                frame = frame.WriteTemporaryWord(0xF6, 0);
            }
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = NativeBattleCinematicRibbonInput.Capture(frame, frame.TemporaryFrame.Address + 0xF0, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native breakup local projection requires its packed input owner."));
            NativeBattleFullMacVertex result = NativeBattleGfMatrices.TransformMac460840(matrix, input);
            NativeEffectMemory = memory with { ScriptedColorRegisters = TransformRegisters(registers, result) };
            frame = frame.WriteTemporaryDword(0xFC, result.Flags);
            NativeBattleVector transformed = wrap ? new(unchecked((short)result.X), unchecked((short)result.Y), unchecked((short)result.Z)) : new((short)Math.Clamp(result.X, short.MinValue, short.MaxValue), (short)Math.Clamp(result.Y, short.MinValue, short.MaxValue), (short)Math.Clamp(result.Z, short.MinValue, short.MaxValue));
            byte[] packed = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(packed, transformed.X);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(2), transformed.Y);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(4), transformed.Z);
            Put(destination, packed);
            return transformed;
        }
        NativeBattleModuleProjectedVertex Project(NativeBattleVector input)
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native breakup projection requires its actual depth-cue controls.");
            NativeBattleModuleDepthCueVertex point = NativeBattleModuleProjection.ProjectWithCue45FAA0(prepared.Matrix, input, NativeCamera?.Projection ?? throw new NotSupportedException("Native breakup projection requires its camera owner."), controls.Dqa, controls.Dqb);
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native breakup projection requires its actual packed-input owner.");
            frame = frame.WriteTemporaryWord(0xF0, unchecked((ushort)input.X)).WriteTemporaryWord(0xF2, unchecked((ushort)input.Y)).WriteTemporaryWord(0xF4, unchecked((ushort)input.Z));
            registers = NativeBattleCinematicRibbonInput.Capture(frame, frame.TemporaryFrame.Address + 0xF0, registers);
            NativeEffectMemory = memory with { ScriptedColorRegisters = registers with { Ir = registers.Ir.SetItem(1, point.Ir2), Flags = point.Projected.Flags } };
            frame = frame.WriteTemporaryDword(0xFC, point.Projected.Flags);
            return point.Projected;
        }
        void Append(NativeBattleGfGeometryBatch batch)
        {
            uint output = CoreDword(frame, manager + 0x7C);
            var appended = AppendNativeCinematicPackets(frame, output, null, batch, graphics);
            frame = WriteCoreDword(appended.Frame, manager + 0x7C, unchecked(output + (uint)batch.AllocationBytes));
            graphics = appended.Graphics;
        }
        void PublishRecords(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after, bool spawn)
        {
            for (int offset = 0; offset < after.Length; offset += 68)
            {
                uint oldPointer = BinaryPrimitives.ReadUInt32LittleEndian(before.Slice(offset));
                uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(after.Slice(offset));
                uint address = work + 32u + (uint)offset;
                if (spawn && oldPointer == 0 && pointer != 0)
                {
                    foreach ((int start, int length) in new (int, int)[] { (0, 16), (0x10, 6), (0x16, 8), (0x1E, 9), (0x2E, 1), (0x30, 8), (0x3C, 8) })
                    {
                        Put(address + (uint)start, after.Slice(offset + start, length));
                    }
                    if (after[offset + 0x26] != 0)
                    {
                        Put(address + 0x28, after.Slice(offset + 0x28, 6));
                    }
                }
                else if (!spawn && oldPointer != 0)
                {
                    Put(address, after.Slice(offset, 16));
                    Put(address + 0x16, after.Slice(offset + 0x16, 2));
                    Put(address + 0x2E, after.Slice(offset + 0x2E, 1));
                    if (mode is 5 or 7)
                    {
                        Put(address + 0x32, after.Slice(offset + 0x32, 2));
                    }
                    if (mode == 1 && BinaryPrimitives.ReadInt32LittleEndian(after.Slice(offset + 8)) > 0)
                    {
                        foreach (int acceleration in new[] { 0x1E, 0x3E, 0x42 })
                        {
                            Put(address + (uint)acceleration, after.Slice(offset + acceleration, 2));
                        }
                    }
                    Put(address + 0x3C, after.Slice(offset + 0x3C, 2));
                    Put(address + 0x40, after.Slice(offset + 0x40, 2));
                }
            }
        }
        byte[] ReadGeometry(uint address, int length)
        {
            int offset = checked(geometry.Offset + (int)((long)address - geometryAddress));
            return NativeBattleModelEnemyHeap.Read(cpu, staging, geometry with { Offset = offset }, length);
        }
        uint ResolveHookOwner(NativeBattleWaterExecutionFrame current, short selector)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int currentIndex = NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank);
            int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, currentIndex, current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }
        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
        uint Slot32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        void Put(uint address, ReadOnlySpan<byte> bytes) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, bytes.Length), bytes) };
        void Put16(uint address, ushort value) => Put(address, BitConverter.GetBytes(value));
        void Put32(uint address, uint value) => Put(address, BitConverter.GetBytes(value));
        void SetSlot(int offset, uint value) => frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 4), BitConverter.GetBytes(value)) };
    }
}
