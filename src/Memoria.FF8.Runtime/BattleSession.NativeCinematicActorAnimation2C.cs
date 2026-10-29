using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicActorAnimation2C(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native actor animation requires its current CPU resource owner.");
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native actor animation requires its current staging owner.");
        Dictionary<uint, NativeBattleResourcePointer> pointers = [];
        uint existing = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        uint actorAddress = existing == 0 ? CoreDword(frame, CoreDword(frame, 0x279744C) + 0x60u + 4u * ReadSlot(frame, 0x1B, 1)[0]) : Dword(frame, existing + 0xC);
        int actorIndex = NativeEffect301Actor(graphics, actorAddress);
        NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
        NativeBattleResourcePointer[] fields = Enumerable.Range(0, 4).Select(ReadResource).ToArray();
        NativeBattleCinematicModelServices modelServices = new((_, _) => throw new NotSupportedException("Native actor animation uses its copied physical resource row, not an embedded shape."), (_, address) =>
        {
            NativeBattleResourcePointer pointer = RequirePointer(address);
            return Read(pointer, pointer.Length ?? throw new NotSupportedException("Native actor animation section has no retained extent."));
        });
        NativeBattleCinematicActorAnimation2CServices services = new(CaptureActor, PrepareBasis, modelServices, DrawBody);
        return (NativeBattleCinematicActorAnimation2C.Draw(frame, services), graphics);

        NativeBattleResourcePointer ReadResource(int field)
        {
            NativeBattleResourcePointer pointer;
            if (actor.ActiveStagedResourceRow100 is uint stagedRow)
            {
                pointer = staging.ReadResourcePointer(stagedRow + 4u * (uint)field) ?? throw new NotSupportedException("Native actor animation requires its actual retained row pointer.");
            }
            else
            {
                int index = actor.ActiveResourceRow100 ?? throw new NotSupportedException("Native actor animation requires its actual current CPU resource row.");
                int offset = new[] { 12, 16, 20, 44 }[field];
                NativeBattleModelCpuResourceRow cpuRow = cpu.Rows[index];
                if (!cpuRow.Fields.TryGetValue(offset, out NativeBattleModelCpuReference? reference))
                {
                    throw new NotSupportedException("Native actor animation reached an unknown resource pointer field.");
                }
                pointer = reference is null ? new(null, 0) : new(reference, null, cpuRow.Lengths.TryGetValue(offset, out int length) ? length : null);
            }
            pointers[Address(pointer)] = pointer;
            return pointer;
        }

        NativeBattleCinematicActorAnimation2CSource CaptureActor(uint address)
        {
            if (address != actorAddress)
            {
                throw new InvalidDataException("Native actor animation changed physical actor during clone construction.");
            }
            NativeBattleEffectArena resources = NativeBattleEffectArena.Clear();
            for (int index = 0; index < fields.Length; index++)
            {
                resources = resources.Write(index * 4, BitConverter.GetBytes(Address(fields[index])));
            }
            int count = Read(fields[0], 1)[0];
            byte[] skeleton = Read(fields[0], checked(16 + count * 48));
            NativeBattleEffectArena input = NativeBattleEffectArena.Clear().MarkUnknown(0, 12);
            return new(input, resources, skeleton, actor.Color40 ?? throw new NotSupportedException("Native actor animation requires current actor color DWORD40."), actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native actor animation requires its current object mask."));
        }

        NativeBattleWaterExecutionFrame PrepareBasis(NativeBattleWaterExecutionFrame current)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            uint source = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, SlotWord(current, 0x9C));
            byte[] rotation = current.Core.Read(source, 18);
            NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(current, source + 20)), unchecked((int)CoreDword(current, source + 24)), unchecked((int)CoreDword(current, source + 28)), 4096);
            uint owner = CoreDword(current, 0x27973E8);
            NativeBattleVector position = NativeBattleScriptedMeshPreparation.Vector(current.Arena.Read(ArenaOffset(owner + 0x94, 6), 6));
            NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(basis, position);
            current = NativeBattleCinematicModelRefresh89.PrepareBasis(current, (selected, selector) => NativeBattleWaterMatrixCommands.ResolveB65520(selected, bank, selector)).Frame;
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters colors = NativeBattleCinematicRibbonInput.Capture(current, owner + 0x94, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor animation requires its actual packed input registers."));
            NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Native actor animation requires its actual geometry registers.");
            Span<byte> rawRotation = stackalloc byte[20];
            Span<bool> rotationKnown = stackalloc bool[20];
            current.TemporaryFrame.ReadWithKnownness(0xE0, rawRotation, rotationKnown);
            byte?[] packed = new byte?[20];
            for (int index = 0; index < packed.Length; index++)
            {
                packed[index] = rotationKnown[index] ? rawRotation[index] : null;
            }
            NativeEffectMemory = memory with { ScriptedColorRegisters = TransformRegisters(colors, transformed), GeometryRegisters = geometry with { Rotation20 = packed.ToImmutableArray(), Translation = [transformed.X, transformed.Y, transformed.Z] } };
            return current;
        }

        NativeBattleCinematicActorCloneDraw DrawBody(NativeBattleWaterExecutionFrame current, uint clone)
        {
            uint output = CoreDword(current, CoreDword(current, 0x27973EC) + 0x7C);
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native actor animation requires its physical renderer gate.");
            bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native actor animation requires current renderer capabilities.");
            var gate = process.Enter5099D0(hardware);
            process = gate.Process;
            if (!gate.Draw)
            {
                NativeEffectMemory = memory with { PrimitiveProcess = process };
                return new(current, output);
            }
            uint color = Dword(current, clone + 0x5C);
            process = process.PublishModelColor(color);
            NativeEffectMemory = memory with { PrimitiveProcess = process };
            uint mask = Dword(current, clone + 0x60);
            uint skeleton = Dword(current, clone + 0x30);
            int boneCount = current.Arena.Read(ArenaOffset(skeleton, 1), 1)[0];
            ImmutableArray<NativeBattleActionMatrix> bones = Enumerable.Range(0, boneCount).Select(index => NativeBattleArenaPose.ReadMatrix(current.Arena, ArenaOffset(skeleton + 0x20u + 0x30u * (uint)index, 32))).ToImmutableArray();
            uint geometry = Dword(current, clone + 0x34);
            NativeBattleResourcePointer geometryPointer = RequirePointer(geometry);
            if (geometryPointer.Length is null)
            {
                throw new NotSupportedException("Native actor animation geometry has no retained source extent.");
            }
            ImmutableArray<ushort> lastCounts = default;
            int lastPayload = 0;
            int lastEnd = 0;
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => ReadAt(geometryPointer, offset, length), boneCount, (_, counts, payload, end) =>
            {
                NativeBattleCinematicActorAnimation2C.RequireSelectedPrimitiveGroups(counts);
                lastCounts = counts;
                lastPayload = payload;
                lastEnd = end;
            }, mask);
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native actor animation requires current projection registers.");
            NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native actor animation requires original color arithmetic tables.");
            NativeBattleGeometryViewport viewport = new(Word(current, clone + 0x54), Word(current, clone + 0x56), Word(current, clone + 0x58), Word(current, clone + 0x5A));
            int length = meshes.Where((_, index) => (mask & (1u << (index & 31))) != 0).Select(mesh => mesh.Vertices.Length * 8).DefaultIfEmpty().Max();
            NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            NativeBattlePilotVisualIdentity visualIdentity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, current, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, visualBank)) with { ResourceGeneration = geometry };
            NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, bones, projection, color, mask, new byte[length].ToImmutableArray(), colors, 4, viewport, OtBucket(current.ReadTemporaryDword(0x14C)), Word(current, clone + 0x64), visualIdentity);
            NativeEffectMemory = NativeBattleCinematicActorAnimation2C.PublishProjectionRegisters(RequireWaterMemory(), current.Arena, skeleton, meshes, mask);
            if (draw.ProjectedBytesWritten != 0)
            {
                uint work = Dword(current, clone + 0x44);
                current = current with { Arena = current.Arena.Write(ArenaOffset(work, draw.ProjectedBytesWritten), draw.ProjectedScratch.AsSpan().Slice(0, draw.ProjectedBytesWritten)) };
            }
            if (!lastCounts.IsDefault)
            {
                byte[] counts = new byte[8];
                for (int index = 0; index < 4; index++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(counts.AsSpan(index * 2), lastCounts[index]);
                }
                current = current with { Arena = current.Arena.Write(ArenaOffset(clone + 0x40, 4), BitConverter.GetBytes(geometry + (uint)lastPayload)).Write(ArenaOffset(clone + 0x48, 8), counts) };
            }
            var written = AppendNativeCinematicPackets(current, output, null, draw.Packets, graphics);
            graphics = written.Graphics;
            uint cursor = unchecked(output + (uint)draw.Packets.AllocationBytes);
            if (!lastCounts.IsDefault)
            {
                NativeBattleEffectMemory completed = RequireWaterMemory();
                NativeEffectMemory = completed with { PrimitiveProcess = (completed.PrimitiveProcess ?? throw new NotSupportedException("Native actor animation lost its renderer process.")).Publish50FDF0(cursor, geometry + (uint)lastEnd) };
            }
            return new(written.Frame, cursor);
        }

        NativeBattleResourcePointer RequirePointer(uint address) => pointers.TryGetValue(address, out NativeBattleResourcePointer? pointer) ? pointer : throw new NotSupportedException("Native actor animation pointer has no current retained resource owner.");
        byte[] Read(NativeBattleResourcePointer pointer, int length) => ReadAt(pointer, 0, length);
        byte[] ReadAt(NativeBattleResourcePointer pointer, int offset, int length)
        {
            if (offset < 0 || length < 0 || pointer.Length is int extent && offset > extent - length)
            {
                throw new NotSupportedException("Native actor animation read leaves its proved current resource extent.");
            }
            return pointer.Cpu is { } reference ? NativeBattleModelEnemyHeap.Read(cpu, staging, reference with { Offset = checked(reference.Offset + offset) }, length) : staging.Read(checked((pointer.StagingAddress ?? throw new NotSupportedException("Native actor animation resource pointer has no physical backing.")) + (uint)offset), length);
        }
        static uint Address(NativeBattleResourcePointer pointer) => pointer.Cpu is { } reference ? reference.PhysicalAddress : pointer.StagingAddress ?? throw new NotSupportedException("Native actor animation cannot invent a numeric resource-table address.");
        static uint Dword(NativeBattleWaterExecutionFrame current, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(address, 4), 4));
        static short Word(NativeBattleWaterExecutionFrame current, uint address) => BinaryPrimitives.ReadInt16LittleEndian(current.Arena.Read(ArenaOffset(address, 2), 2));
    }
}
