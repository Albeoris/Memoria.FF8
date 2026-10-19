using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) ExecuteNativeCinematicOffscreen(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, int resourceLength, NativeBattleGraphicsState graphics)
    {
        uint privateRoot = 0;
        int privateEntries = 0;
        NativeBattleCinematicOffscreenServices services = new(
            _ => Process().Buffer80 ?? throw new NotSupportedException("Native cinematic capture requires its current draw buffer."),
            (_, bank) =>
            {
                NativeSoftwareDrawingEnvironmentBuffers environments = Process().Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native cinematic capture requires its current drawing environments.");
                return bank == 0 ? environments.Buffer0 : environments.Buffer1;
            },
            current =>
            {
                NativeBattlePrimitiveProcess process = Process();
                NativeBattleSoftwarePrimitiveState software = process.Software ?? throw new NotSupportedException("Native cinematic capture requires its retained software environment.");
                NativeEffectMemory = Memory(current) with { PrimitiveProcess = process with { Software = software with { Environment = software.Environment with { Debug = 1 } } } };
                return current;
            },
            (current, focal) =>
            {
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native cinematic capture requires its current camera registers.");
                NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native cinematic capture requires its current GTE projection.");
                NativeCamera = camera with { Projection = projection with { FocalLength = focal } };
                return current;
            },
            (current, selector) => NativeBattleWaterMatrixCommands.ResolveB65520(current, NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength), selector),
            ProjectAnchor,
            ClearOrdering,
            InsertPacket,
            DrawStage,
            ExecuteSoftware,
            _ => RequireWaterMemory().SelectedIndex);
        frame = NativeBattleCinematicOffscreen.Execute(frame, program, services);
        NativeEffectMemory = Memory(frame);
        return (frame, graphics);

        NativeBattlePrimitiveProcess Process() => RequireWaterMemory().PrimitiveProcess ?? throw new NotSupportedException("Native cinematic capture requires its canonical primitive process.");
        NativeBattleEffectMemory Memory(NativeBattleWaterExecutionFrame current) => RequireWaterMemory() with { Arena = current.Arena, ScriptedEffects = current.Core };
        uint Dword(NativeBattleWaterExecutionFrame current, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(address, 4), 4));
        short Word(NativeBattleWaterExecutionFrame current, uint address) => BinaryPrimitives.ReadInt16LittleEndian(current.Arena.Read(ArenaOffset(address, 2), 2));

        NativeBattleWaterExecutionFrame ProjectAnchor(NativeBattleWaterExecutionFrame current)
        {
            NativeBattleActionMatrix matrix = NativeBattleCinematicAnchor.Prepare(current, NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength));
            current = current.WriteTemporaryDword(0xF0, BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(current, 0x94, 4))).WriteTemporaryWord(0xF4, SlotWord(current, 0x98));
            NativeBattleScriptedGraphicsMemory registers = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native cinematic capture requires its current depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic capture requires its current projection controls.");
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, NativeBattleScriptedMeshPreparation.Vector(current.ReadTemporary(0xF0, 6)), projection, registers.Dqa, registers.Dqb);
            return current.WriteTemporaryWord(0xD0, unchecked((ushort)(unchecked((short)projected.Projected.Screen) >> 3))).WriteTemporaryWord(0xD2, unchecked((ushort)(unchecked((short)(projected.Projected.Screen >> 16)) >> 3))).WriteTemporaryDword(0xFC, projected.Projected.Flags);
        }

        NativeBattleWaterExecutionFrame ClearOrdering(NativeBattleWaterExecutionFrame current, uint address, int entries)
        {
            privateRoot = address;
            privateEntries = entries;
            byte[] roots = new byte[checked(entries * 4)];
            BinaryPrimitives.WriteUInt32LittleEndian(roots, uint.MaxValue);
            for (int index = 1; index < entries; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(roots.AsSpan(index * 4), unchecked(address + (uint)((index - 1) * 4)));
            }
            return current with { Arena = current.Arena.Write(ArenaOffset(address, roots.Length), roots) };
        }

        NativeBattleWaterExecutionFrame AppendPrivate(NativeBattleWaterExecutionFrame current, uint output, uint ordering, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic capture requires its shared proxy bank.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.AppendPrivate(Memory(current), ArenaOffset(output, 0), primitive.Pending, batch, ordering);
            NativeEffectMemory = written.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Arena = written.Memory.Arena!, Core = written.Memory.ScriptedEffects! };
        }

        NativeBattleWaterExecutionFrame InsertPacket(NativeBattleWaterExecutionFrame current, uint ordering, uint packet)
        {
            byte length = current.Arena.Read(ArenaOffset(unchecked(packet + 3), 1), 1)[0];
            byte[] bytes = new byte[checked((length + 1) * 4)];
            bytes[3] = length;
            current.Arena.Read(ArenaOffset(unchecked(packet + 4), length * 4), length * 4).CopyTo(bytes, 4);
            ImmutableArray<uint> words = Enumerable.Range(0, length + 1).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, bytes.Length).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(0, words, []);
            return AppendPrivate(current, packet, ordering, new([new(0, bytes.ToImmutableArray(), mask, primitive)], bytes.Length));
        }

        NativeBattleCinematicOffscreenStageStep DrawStage(NativeBattleWaterExecutionFrame current, NativeBattleCinematicOffscreenStageRequest request)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native cinematic capture requires its current stage groups.");
            NativeBattleStageGroup group = stage.Groups[request.Group];
            if ((group.Flags & 1) == 0)
            {
                return new(current, request.OutputAddress);
            }
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native cinematic capture requires its current stage pose.");
            NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            NativeBattleActionMatrix matrix = ReadMatrix(current, request.MatrixAddress);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(source, pose), matrix, 0) };
            NativeStage = stage with { Groups = stage.Groups.SetItem(request.Group, group) };
            NativeBattlePrimitiveProcess process = Process();
            bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native cinematic capture requires its selected renderer capability.");
            var gate = process.Enter5099D0(hardware);
            process = gate.Process;
            NativeEffectMemory = Memory(current) with { PrimitiveProcess = process };
            if (!gate.Draw)
            {
                return new(current, request.OutputAddress);
            }
            uint context = request.ContextAddress;
            uint color = Dword(current, unchecked(context + 0x1C));
            uint objectMask = Dword(current, unchecked(context + 0x20));
            short cue = Word(current, unchecked(context + 0x24));
            NativeBattleGeometryViewport viewport = new(Word(current, unchecked(context + 0x14)), Word(current, unchecked(context + 0x16)), Word(current, unchecked(context + 0x18)), Word(current, unchecked(context + 0x1A)));
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic capture requires its actual projection registers.");
            NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native cinematic capture requires its original color arithmetic.");
            ImmutableArray<NativeBattleRawMesh> meshes = (definition.NativeStageSource?.Geometry ?? throw new NotSupportedException("Native cinematic capture requires its original stage geometry.")).Where(mesh => mesh.StageGroup == request.Group).ToImmutableArray();
            process = process.PublishModelColor(color);
            NativeEffectMemory = Memory(current) with { PrimitiveProcess = process };
            uint output = request.OutputAddress;
            for (int index = 0; index < meshes.Length; index++)
            {
                if ((objectMask & (1u << (index & 31))) == 0)
                {
                    continue;
                }
                NativeBattleRawMesh mesh = meshes[index];
                NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, group.Matrices, projection, color, 1, new byte[checked(mesh.Vertices.Length * 8)].ToImmutableArray(), colors, request.DepthShift, viewport, 0, cue);
                uint work = Dword(current, unchecked(context + 4));
                current = current with { Arena = current.Arena.Write(ArenaOffset(work, draw.ProjectedBytesWritten), draw.ProjectedScratch.AsSpan().Slice(0, draw.ProjectedBytesWritten)) };
                ushort[] counts = new ushort[4];
                foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
                {
                    int kind = (polygon.Quad ? 1 : 0) + (polygon.Bytes.Length == (polygon.Quad ? 24 : 20) ? 2 : 0);
                    counts[kind] = checked((ushort)(counts[kind] + 1));
                }
                byte[] countBytes = new byte[8];
                for (int kind = 0; kind < counts.Length; kind++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(countBytes.AsSpan(kind * 2), counts[kind]);
                }
                current = current with { Arena = current.Arena.Write(ArenaOffset(unchecked(context + 8), 8), countBytes).MarkUnknown(ArenaOffset(context, 4), 4) };
                if (!draw.Packets.Attempts.IsEmpty)
                {
                    current = AppendPrivate(current, output, request.OrderingAddress, draw.Packets);
                }
                output = unchecked(output + (uint)draw.Packets.AllocationBytes);
                NativeEffectMemory = Memory(current) with { PrimitiveProcess = Process().Publish50FDF0(output, null) };
            }
            return new(current, output);
        }

        NativeBattleActionMatrix ReadMatrix(NativeBattleWaterExecutionFrame current, uint address)
        {
            byte[] Read(int offset, int length) => address >= current.TemporaryFrame.Address && address - current.TemporaryFrame.Address <= 0x180 - offset - length ? current.ReadTemporary(checked((int)(address - current.TemporaryFrame.Address)) + offset, length) : current.Core.Read(unchecked(address + (uint)offset), length);
            byte[] rotation = Read(0, 18);
            return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(Read(20, 4)), BinaryPrimitives.ReadInt32LittleEndian(Read(24, 4)), BinaryPrimitives.ReadInt32LittleEndian(Read(28, 4)), 4096);
        }

        NativeBattleWaterExecutionFrame ExecuteSoftware(NativeBattleWaterExecutionFrame current, uint lastRoot)
        {
            NativeBattleEffectMemory memory = Memory(current);
            NativeEffectMemory = memory;
            NativeBattlePrimitiveProcess process = Process();
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic capture requires its shared ordering owner.");
            NativeBattleSoftwarePrimitiveState software = process.Software ?? throw new NotSupportedException("Native cinematic capture requires its retained software renderer.");
            NativeBattlePcTextureWindowCache cache = process.TextureWindowCache ?? throw new NotSupportedException("Native cinematic capture requires its retained texture cache.");
            NativeSoftwarePrimitiveTable table = definition.NativeGraphics?.SoftwarePrimitiveTable ?? throw new NotSupportedException("Native cinematic capture requires its original software dispatch table.");
            bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native cinematic capture requires its selected backend capability.");
            NativeBattleSoftwarePrimitiveExecution execution = new(software, memory.Textures ?? throw new NotSupportedException("Native cinematic capture requires its shared VRAM image."), table, hardware, cache);
            NativeBattlePrimitiveTables tables = definition.NativeGraphics?.PrimitiveTables ?? throw new NotSupportedException("Native cinematic capture requires its original command lengths.");
            NativeBattleCinematicSoftwareOrdering.Execute45CEE0(primitive.Pending.ProxyCursor ?? throw new NotSupportedException("Native cinematic traversal requires its retained proxy cursor."), () => primitive.Pending.Traverse(tables.WordLengths, ReadNativePhysicalPrimitive, lastRoot, address =>
            {
                if (address < privateRoot || (address - privateRoot) / 4 >= privateEntries || ((address - privateRoot) & 3) != 0)
                {
                    throw new NotSupportedException("Native cinematic traversal leaves its current private ordering table.");
                }
                return new(Dword(current, address), checked((int)((address - privateRoot) / 4)));
            }), execution);
            process = process with { Software = execution.State, TextureWindowCache = execution.Cache, TexturePage = execution.State.Texture.LastTPage is uint page ? unchecked((ushort)page) : null };
            NativeEffectMemory = memory with { PrimitiveProcess = process, Textures = execution.Textures };
            return current;
        }
    }
}
