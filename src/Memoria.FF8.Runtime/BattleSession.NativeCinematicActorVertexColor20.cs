using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicActorVertexColor20(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native actor glow requires its current CPU resources.");
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native actor glow requires its current staging owner.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native actor glow requires its current projection.");
        uint manager = CoreDword(frame, 0x27973EC);
        byte actorIndex = ReadSlot(frame, 0x1B, 1)[0];
        uint actorAddress = CoreDword(frame, CoreDword(frame, 0x279744C) + 0x60u + 4u * actorIndex);
        if (actorIndex >= graphics.Models.Length || actorAddress != 0x1D972C0u + 156u * actorIndex)
        {
            throw new NotSupportedException("Native actor glow requires its actual physical actor row.");
        }
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actorIndex);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Native actor glow requires its current physical rig.");
        NativeBattleResourcePointer[] fields = Enumerable.Range(0, 4).Select(ReadResource).ToArray();
        NativeBattleResourcePointer skeleton = fields[0];
        NativeBattleResourcePointer geometry = fields[1];
        uint geometryAddress = Address(geometry);
        Dictionary<int, (ImmutableArray<ushort> Counts, int Payload)> objectMetadata = [];
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => ReadKnown(geometry, offset, length), pose.BoneCount, (index, counts, payload, _) => objectMetadata.Add(index, (counts, payload)));
        uint clone = SlotDword(0xBC);
        NativeBattleVector origin = new(unchecked((short)SlotWord(frame, 0x94)), unchecked((short)SlotWord(frame, 0x96)), unchecked((short)SlotWord(frame, 0x98)));
        frame = frame.WriteTemporaryDword(0x60, unchecked((uint)(int)origin.X)).WriteTemporaryDword(0x64, unchecked((uint)(int)origin.Y)).WriteTemporaryDword(0x68, unchecked((uint)(int)origin.Z));
        if (clone == 0)
        {
            NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
            graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor with { Flags0 = (ushort)(actor.Flags0 | 4) }) };
            (frame, clone) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 0x80);
            StoreDword(clone + 0xC, actorAddress);
            RefreshDescriptors();
            StoreDword(clone + 0x54, 0);
            StoreDword(clone + 0x58, 0x00D00140);
            uint color = actor.Color40 ?? throw new NotSupportedException("Native actor glow requires its current actor color.");
            StoreDword(clone + 0x78, color);
            StoreDword(clone + 0x5C, color);
            StoreDword(clone + 0x60, actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native actor glow requires its current object mask."));
            StoreWord(clone + 0x64, 0);
            StoreDword(clone + 0x68, 0);
            StoreDword(clone + 0x50, uint.MaxValue);
            StoreWord(clone, 0);
            StoreWord(clone + 2, 0);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(clone + 0x21, 1), [0]) };
            SlotStore(0xBC, clone);
            RestoreLocal();
            Compose(graphics.Models[actorIndex].GeometryWorldMatrix ?? throw new NotSupportedException("Native actor glow requires its current world matrix."));
            InitializeDistances();
        }
        uint state = SlotDword(0xC0);
        short selector = unchecked((short)Word(state + 8));
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        int referenceIndex = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank), frame.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
        uint referenceAddress = unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, referenceIndex));
        NativeBattleVector referenceVector = NativeBattleScriptedMeshPreparation.Vector(frame.Arena.Read(ArenaOffset(referenceAddress + 0x94, 6), 6));
        uint colorBase = CoreDword(frame, manager + 0x74);
        frame = frame.WriteTemporaryDword(0x70, colorBase);
        frame = NativeBattleCinematicActorVertexColor20.UpdateColors(frame, state, colorBase, unchecked((short)SlotWord(frame, 0x8C)), unchecked((short)SlotWord(frame, 0x8E)), unchecked((short)SlotWord(frame, 0x90)), graphics.Models[actorIndex].Color40 ?? throw new NotSupportedException("Native actor glow requires its actual base color."), referenceVector);
        int colorCount = unchecked((short)Word(state));
        uint projectedWork = unchecked(colorBase + (uint)(colorCount * 4));
        StoreDword(clone + 0x44, projectedWork);
        RefreshDescriptors();
        RestoreLocal();
        NativeBattleGraphicsModel bounded = NativeBattlePoseMatrices.PublishShadowBounds5086F0(graphics.Models[actorIndex], pose);
        graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, bounded) };
        NativeBattleActionMatrix world = bounded.GeometryWorldMatrix ?? throw new NotSupportedException("Native actor glow requires its actual world matrix.");
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(MonsterNodeMatrix(frame), world);
        frame = WriteTemporaryMatrix(frame, 0xE0, basis);
        Compose(basis);
        NativeBattleEffectMemory colorMemory = RequireWaterMemory();
        NativeEffectMemory = colorMemory with { PrimitiveProcess = (colorMemory.PrimitiveProcess ?? throw new NotSupportedException("Native actor glow requires its retained renderer color owner.")).PublishModelColor(Dword(clone + 0x5C)) };
        uint objectMask = Dword(clone + 0x60);
        for (int objectIndex = 0; objectIndex < meshes.Length; objectIndex++)
        {
            if ((objectMask & (1u << (objectIndex & 31))) == 0)
            {
                continue;
            }
            int objectOffset = BinaryPrimitives.ReadInt32LittleEndian(ReadKnown(geometry, 4 + objectIndex * 4, 4));
            int groups = BinaryPrimitives.ReadInt16LittleEndian(ReadKnown(geometry, objectOffset, 2));
            uint sourceCursor = geometryAddress + (uint)objectOffset + 2;
            uint scratchCursor = projectedWork;
            for (int group = 0; group < groups; group++)
            {
                ushort bone = BinaryPrimitives.ReadUInt16LittleEndian(ReadKnown(geometry, checked((int)(sourceCursor - geometryAddress)), 2));
                sourceCursor += 2;
                LoadBone(bone);
                NativeBattleScriptedGraphicsMemory controls = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native actor glow requires its depth cue controls.");
                NativeBattleCinematicPackedProjectionStep step = NativeBattleCinematicPackedProjection50FD40.Execute(frame, sourceCursor, scratchCursor, Registers(), projection, controls.Dqa, controls.Dqb, ReadGeometry);
                frame = step.Frame;
                PublishRegisters(step.Registers);
                sourceCursor = step.SourceCursor;
                scratchCursor = step.ScratchCursor;
            }
            int countsOffset = checked((int)(((sourceCursor + 3) & ~3u) - geometryAddress));
            byte[] counts = ReadKnown(geometry, countsOffset, 8);
            for (int index = 0; index < 4; index++)
            {
                StoreWord(clone + 0x48u + 2u * (uint)index, BinaryPrimitives.ReadUInt16LittleEndian(counts.AsSpan(index * 2)));
            }
            StoreDword(clone + 0x40, geometryAddress + (uint)countsOffset + 12);
            var metadata = objectMetadata[objectIndex];
            if (metadata.Payload != countsOffset + 12 || !metadata.Counts.SequenceEqual(Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(counts.AsSpan(index * 2)))))
            {
                throw new NotSupportedException("Native actor glow reached an unsupported advancing geometry layout.");
            }
            NativeBattleRawMesh mesh = meshes[objectIndex];
            if (scratchCursor != projectedWork + (uint)(mesh.Vertices.Length * 8))
            {
                throw new NotSupportedException("Native actor glow projected stream does not match its current object extent.");
            }
            NativeBattleProjectedVertex[] projected = Enumerable.Range(0, mesh.Vertices.Length).Select(index =>
            {
                uint address = projectedWork + (uint)(index * 8);
                return new NativeBattleProjectedVertex(Dword(address), Dword(address + 4));
            }).ToArray();
            NativeBattleCinematicVertexColors colors = NativeBattleCinematicActorVertexColor20.ReadObjectColors(frame, state, colorBase, objectIndex, mesh.Vertices.Length);
            Append(NativeBattleCinematicMonsterPackets.Glow(mesh, projected, colors.Colors, OtBucket(frame.ReadTemporaryDword(0x14C)), colors.HighKnown));
        }
        Shadow();
        NativeModelCpuResources = cpu;
        NativeFileStaging = staging;
        return (frame, graphics);

        void RefreshDescriptors()
        {
            NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
            NativeBattleEffectArena captured = CaptureNativeEffect301Model(actor).Write(0x64, BitConverter.GetBytes(NativeBattleModelResourceRow.Address100(actor)));
            (byte[] bytes, bool[] known) = captured.ReadSnapshot(0x60, 12);
            int target = ArenaOffset(clone + 0x10, 12);
            NativeBattleEffectArena arena = frame.Arena.Write(target, bytes);
            for (int index = 0; index < 12; index++)
            {
                if (!known[index])
                {
                    arena = arena.MarkUnknown(target + index, 1);
                }
            }
            frame = frame with { Arena = arena };
            for (int index = 0; index < fields.Length; index++)
            {
                uint address = clone + 0x30u + 4u * (uint)index;
                if (Address(fields[index]) == 0)
                {
                    StoreDword(address, 0);
                }
                else
                {
                    frame = frame with { Arena = frame.Arena.WriteResourcePointer(ArenaOffset(address, 4), fields[index]) };
                }
            }
        }

        void RestoreLocal()
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
            NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actorIndex], pose, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
            staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
            graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, prepared.Model) };
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(prepared.Model.SequenceState ?? throw new NotSupportedException("Native actor glow requires its current decoded pose."), pose);
            if (!decoded.HasScale)
            {
                for (int bone = 0; bone < pose.BoneCount; bone++)
                {
                    if (BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(16 + bone * 48, 2)) < 0)
                    {
                        WriteResource(skeleton, 32 + bone * 48 + 18, new byte[2]);
                    }
                }
            }
        }

        void Compose(NativeBattleActionMatrix matrix)
        {
            NativeBattleGraphicsModel composed = NativeBattleBodyBoneMatrices.Compose5095B0(graphics.Models[actorIndex], matrix);
            graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, composed) };
        }

        void LoadBone(ushort bone)
        {
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actorIndex], pose);
            if (bone >= bones.Length)
            {
                throw new InvalidDataException("Native actor glow selected an absent current bone.");
            }
            (byte[] pad, bool[] known) = ReadSnapshot(skeleton, checked(32 + bone * 48 + 18), 2);
            NativeBattleGeometryRegisterStep step = Registers();
            NativeBattleGeometryRegisters loaded = NativeBattleGeometry.LoadMatrix(step.Geometry, bones[bone], true, true, null);
            loaded = loaded with { Rotation20 = loaded.Rotation20.SetItem(18, known[0] ? pad[0] : null).SetItem(19, known[1] ? pad[1] : null) };
            PublishRegisters(step with { Geometry = loaded });
        }

        void InitializeDistances()
        {
            uint stateAddress = CoreDword(frame, manager + 0x74);
            SlotStore(0xC0, stateAddress);
            StoreDword(stateAddress + 4, stateAddress + 0x200);
            uint cursor = stateAddress + 0x200;
            int groupIndex = 0;
            foreach (NativeBattleRawMesh mesh in meshes)
            {
                foreach (NativeBattleRawVertexGroup group in mesh.PackedVertexGroups ?? throw new NotSupportedException("Native actor glow requires packed group order."))
                {
                    if (0x10 + groupIndex * 2 >= 0x200)
                    {
                        throw new NotSupportedException("Native actor glow offset directory overlaps its distance owner.");
                    }
                    StoreWord(stateAddress + 0x10u + 2u * (uint)groupIndex++, unchecked((ushort)(cursor - Dword(stateAddress + 4))));
                    LoadBone(group.Bone);
                    for (int vertex = 0; vertex < group.Count; vertex++)
                    {
                        frame = frame.WriteTemporary(0xD0, group.Bytes.AsSpan().Slice(vertex * 6, 6));
                        NativeBattleGeometryRegisterStep step = Registers();
                        step = step with { Colors = NativeBattleCinematicRibbonInput.Capture(frame, frame.TemporaryFrame.Address + 0xD0, step.Colors) };
                        step = NativeBattleGeometry.Transform(step);
                        PublishRegisters(step);
                        NativeBattleFullMacVertex transformed = new(step.Colors.Mac[0]!.Value, step.Colors.Mac[1]!.Value, step.Colors.Mac[2]!.Value, step.Colors.Flags!.Value);
                        frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)transformed.X)).WriteTemporaryDword(0xF4, unchecked((uint)transformed.Y)).WriteTemporaryDword(0xF8, unchecked((uint)transformed.Z)).WriteTemporaryDword(0xFC, transformed.Flags);
                        StoreWord(cursor, NativeBattleCinematicMonsterVertices.Distance(transformed, origin));
                        cursor += 2;
                    }
                }
            }
            StoreWord(stateAddress, unchecked((ushort)((cursor - Dword(stateAddress + 4)) / 2)));
            cursor = unchecked((cursor + 4) & ~3u);
            StoreDword(stateAddress + 12, cursor);
            uint payload = SlotDword(0xB8);
            StoreWord(stateAddress + 8, Word(payload + 2));
            NativeBattleCinematicTextureSelection selection = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, resources, unchecked((short)Word(payload)));
            frame = frame.WriteTemporaryDword(0xF0, selection.RectangleAddress).WriteTemporaryDword(0xFC, selection.SourceAddress);
            ushort[] palette = Enumerable.Range(0, 256).Select(index => Word(selection.SourceAddress + 2u * (uint)index)).ToArray();
            ImmutableArray<uint> ramp = NativeBattleCinematicMonsterVertices.Palette(palette);
            for (int index = 0; index < 256; index++)
            {
                uint rgb = ramp[index];
                frame = frame with { Arena = frame.Arena.Write(ArenaOffset(cursor + 4u * (uint)index, 3), [(byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16)]) };
            }
            (frame, _) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, unchecked(cursor + 1024 - stateAddress));
        }

        void Shadow()
        {
            NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
            if ((actor.Flags0 & 0x20) != 0)
            {
                return;
            }
            NativeBattleGraphicsDefinition native = definition.NativeGraphics ?? throw new NotSupportedException("Native actor glow shadow requires its current graphics definition.");
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(staging, Registers(), actor, projection, memory.ScriptedGraphics ?? throw new NotSupportedException("Native actor glow shadow requires depth controls."), native.Rules.ShadowSource ?? throw new NotSupportedException("Native actor glow shadow requires its original vector source."), native.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Native actor glow shadow requires its current primitive capability."), 0x4064 / 4, 31);
            staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x78);
            PublishRegisters(shadow.Registers);
            Append(shadow.Batch);
        }

        void Append(NativeBattleGfGeometryBatch batch)
        {
            uint output = CoreDword(frame, manager + 0x7C);
            var written = AppendNativeCinematicPackets(frame, output, null, batch, graphics);
            frame = WriteCoreDword(written.Frame, manager + 0x7C, unchecked(output + (uint)batch.AllocationBytes));
            graphics = written.Graphics;
        }

        NativeBattleResourcePointer ReadResource(int index)
        {
            NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
            if (actor.ActiveStagedResourceRow100 is uint row)
            {
                uint address = row + 4u * (uint)index;
                return (IsNativeEffectArenaAddress(address) ? frame.Arena.ReadResourcePointer(ArenaOffset(address, 4)) : staging.ReadResourcePointer(address)) ?? throw new NotSupportedException("Native actor glow requires its current staged resource pointer.");
            }
            int rowIndex = actor.ActiveResourceRow100 ?? throw new NotSupportedException("Native actor glow requires its current CPU resource row.");
            NativeBattleModelCpuResourceRow current = cpu.Rows[rowIndex];
            int field = new[] { 12, 16, 20, 44 }[index];
            if (!current.Fields.TryGetValue(field, out NativeBattleModelCpuReference? reference))
            {
                throw new NotSupportedException("Native actor glow reached an unknown resource pointer.");
            }
            return reference is null ? new(null, 0) : new(reference, null, current.Lengths.TryGetValue(field, out int length) ? length : null);
        }

        (byte[] Data, bool[] Known) ReadGeometry(uint address, int length) => ReadSnapshot(geometry, checked((int)(address - geometryAddress)), length);

        (byte[] Data, bool[] Known) ReadSnapshot(NativeBattleResourcePointer pointer, int offset, int length)
        {
            if (offset < 0 || length < 0 || pointer.Length is int extent && offset > extent - length)
            {
                throw new NotSupportedException("Native actor glow resource read leaves its current section owner.");
            }
            if (pointer.Cpu is { Bank: < 3 } reference)
            {
                byte[] data = new byte[length];
                bool[] known = new bool[length];
                int first = checked(reference.Offset + offset);
                foreach (NativeBattleEffectMemoryRegion region in cpu.Cpu.Banks[reference.Bank].KnownRanges)
                {
                    int start = Math.Max(first, region.Offset);
                    int end = Math.Min(first + length, region.Offset + region.Data.Length);
                    if (end > start)
                    {
                        region.Data.AsSpan().Slice(start - region.Offset, end - start).CopyTo(data.AsSpan(start - first));
                        Array.Fill(known, true, start - first, end - start);
                    }
                }
                return (data, known);
            }
            uint address = checked(Address(pointer) + (uint)offset);
            return IsNativeEffectArenaAddress(address) ? frame.Arena.ReadSnapshot(ArenaOffset(address, length), length) : staging.ReadSnapshot(address, length);
        }

        byte[] ReadKnown(NativeBattleResourcePointer pointer, int offset, int length)
        {
            var result = ReadSnapshot(pointer, offset, length);
            if (result.Known.Any(value => !value))
            {
                throw new NotSupportedException("Native actor glow requires its actual consumed resource bytes.");
            }
            return result.Data;
        }

        void WriteResource(NativeBattleResourcePointer pointer, int offset, byte[] bytes)
        {
            if (pointer.Cpu is { Bank: < 3 } reference)
            {
                NativeBattleModelCpuBank current = cpu.Cpu.Banks[reference.Bank].Write(checked(reference.Offset + offset), bytes);
                cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(reference.Bank, current) } };
            }
            else
            {
                uint address = checked(Address(pointer) + (uint)offset);
                if (IsNativeEffectArenaAddress(address))
                {
                    frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, bytes.Length), bytes) };
                }
                else
                {
                    staging = staging.Write(address, bytes);
                }
            }
        }

        NativeBattleGeometryRegisterStep Registers()
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            return new(memory.GeometryRegisters ?? throw new NotSupportedException("Native actor glow requires retained geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor glow requires retained data registers."));
        }

        void PublishRegisters(NativeBattleGeometryRegisterStep step)
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeEffectMemory = memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };
        }

        static uint Address(NativeBattleResourcePointer pointer) => pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Native actor glow cannot invent a resource pointer.");
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
        void StoreDword(uint address, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        void SlotStore(int offset, uint value) => frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 4), BitConverter.GetBytes(value)) };
    }

    private static NativeBattleWaterExecutionFrame WriteTemporaryMatrix(NativeBattleWaterExecutionFrame frame, int offset, NativeBattleActionMatrix matrix)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            frame = frame.WriteTemporaryWord(offset + lane * 2, unchecked((ushort)matrix.Rotation[lane]));
        }
        return frame.WriteTemporaryDword(offset + 20, unchecked((uint)matrix.X)).WriteTemporaryDword(offset + 24, unchecked((uint)matrix.Y)).WriteTemporaryDword(offset + 28, unchecked((uint)matrix.Z));
    }
}
