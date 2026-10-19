using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicActorClone48(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicActorClone48Services services = new((current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }, (current, address) =>
        {
            int index = ActorIndex(address);
            graphics = graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Flags0 = (ushort)(graphics.Models[index].Flags0 | 4) }) };
            return current;
        }, address => graphics.Models[ActorIndex(address)].Color40 ?? throw new NotSupportedException("Native actor clone requires its actual physical color DWORD40."), address => graphics.Models[ActorIndex(address)].SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native actor clone requires its actual physical object mask DWORD124."), (current, clone, address) =>
        {
            _ = NativeGraphicsPoseResource(graphics, ActorIndex(address));
            return current with { Arena = current.Arena.MarkUnknown(ArenaOffset(clone + 0x10, 12), 12).MarkUnknown(ArenaOffset(clone + 0x30, 16), 16) };
        }, (current, address) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor clone requires its canonical V0 input owner.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, address, registers) };
            return current;
        }, ComposeAndDraw, (current, address) =>
        {
            int index = ActorIndex(address);
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native actor clone restoration requires its actual physical pose source.");
            NativeBattleGraphicsModel restored = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[index], source);
            graphics = graphics with { Models = graphics.Models.SetItem(index, restored) };
            return current;
        });
        return (NativeBattleCinematicActorClone48.Draw(frame, services), graphics);

        int ActorIndex(uint address)
        {
            if (address < 0x1D972C0 || (address - 0x1D972C0) % 156 != 0 || (address - 0x1D972C0) / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Native actor clone requires its actual physical model pointer.");
            }
            return checked((int)((address - 0x1D972C0) / 156));
        }

        NativeBattleCinematicActorCloneDraw ComposeAndDraw(NativeBattleWaterExecutionFrame current, uint clone, uint actorAddress, NativeBattleActionMatrix basis)
        {
            int index = ActorIndex(actorAddress);
            NativeBattleGraphicsModel actor = graphics.Models[index];
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
            NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native actor clone requires its current physical rig.");
            _ = NativeBattleBodyBoneMatrices.Require(actor, source);
            actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, basis);
            graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
            uint output = CoreDword(current, unchecked(CoreDword(current, 0x27973EC) + 0x7C));
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native actor clone requires its actual model renderer gate.");
            bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native actor clone requires its selected renderer capability.");
            var gate = process.Enter5099D0(hardware);
            process = gate.Process;
            if (!gate.Draw)
            {
                NativeEffectMemory = memory with { PrimitiveProcess = process };
                return new(current, output);
            }
            uint color = CloneDword(0x5C);
            process = process.PublishModelColor(color);
            NativeEffectMemory = memory with { PrimitiveProcess = process };
            uint mask = CloneDword(0x60);
            ImmutableArray<NativeBattleRawMesh> meshes = SelectGeometry(resource, actor);
            int length = meshes.Select((mesh, meshIndex) => (mask & (1u << (meshIndex & 31))) != 0 ? checked(mesh.Vertices.Length * 8) : 0).DefaultIfEmpty().Max();
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native actor clone requires its actual projection controls.");
            NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native actor clone requires its original color arithmetic tables.");
            NativeBattleGeometryViewport viewport = new(CloneWord(0x54), CloneWord(0x56), CloneWord(0x58), CloneWord(0x5A));
            NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, NativeBattleBodyBoneMatrices.Require(actor, source), projection, color, mask, new byte[length].ToImmutableArray(), colors, 4, viewport, OtBucket(current.ReadTemporaryDword(0x14C)), CloneWord(0x64));
            uint work = CloneDword(0x44);
            if (draw.ProjectedBytesWritten != 0)
            {
                current = current with { Arena = current.Arena.Write(ArenaOffset(work, draw.ProjectedBytesWritten), draw.ProjectedScratch.AsSpan().Slice(0, draw.ProjectedBytesWritten)) };
            }
            NativeBattleRawMesh? last = meshes.Where((_, meshIndex) => (mask & (1u << (meshIndex & 31))) != 0).LastOrDefault();
            if (last is not null)
            {
                byte[] counts = new byte[8];
                for (int group = 0; group < 4; group++)
                {
                    int count = last.Polygons.Count(polygon => (polygon.Quad ? 1 : 0) + (polygon.Bytes.Length == (polygon.Quad ? 24 : 20) ? 2 : 0) == group);
                    BinaryPrimitives.WriteUInt16LittleEndian(counts.AsSpan(group * 2), checked((ushort)count));
                }
                current = current with { Arena = current.Arena.MarkUnknown(ArenaOffset(clone + 0x40, 4), 4).Write(ArenaOffset(clone + 0x48, 8), counts) };
            }
            var written = AppendNativeCinematicPackets(current, output, null, draw.Packets, graphics);
            current = written.Frame;
            graphics = written.Graphics;
            uint end = unchecked(output + (uint)draw.Packets.AllocationBytes);
            if (last is not null)
            {
                NativeBattleEffectMemory completed = RequireWaterMemory();
                NativeEffectMemory = completed with { PrimitiveProcess = (completed.PrimitiveProcess ?? throw new NotSupportedException("Native actor clone lost its renderer process.")).Publish50FDF0(end, null) };
            }
            return new(current, end);

            uint CloneDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(unchecked(clone + (uint)offset), 4), 4));
            short CloneWord(int offset) => BinaryPrimitives.ReadInt16LittleEndian(current.Arena.Read(ArenaOffset(unchecked(clone + (uint)offset), 2), 2));
        }

        static ImmutableArray<NativeBattleRawMesh> SelectGeometry(NativeBattleGraphicsResource resource, NativeBattleGraphicsModel actor)
        {
            ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native actor clone requires its actual body geometry.");
            ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native actor clone requires its actual linked geometry presence.");
            bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native actor clone requires its executed geometry linking layout."));
            if (shared && (geometry.Length < 2 || linked.Length != 1))
            {
                throw new NotSupportedException("Native actor clone requires its actual shared skeleton weapon directory slot.");
            }
            return geometry.Select((mesh, index) =>
            {
                NativeBattleRawMesh selected = shared && index == 1 ? linked[0] : mesh;
                NativeBattleModelTextureBinding? binding = shared ? index == 1 ? actor.LinkedTextureSlots ?? throw new NotSupportedException("Native actor clone requires its actual linked texture relocation.") : actor.BodyTextureSlots ?? throw new NotSupportedException("Native actor clone requires its actual body texture relocation.") : resource.GeometryRelocated507550 ? null : actor.BodyTextureSlots ?? throw new NotSupportedException("Native actor clone requires its actual body texture relocation.");
                return binding is null ? selected : selected with { Polygons = selected.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, binding) }).ToImmutableArray() };
            }).ToImmutableArray();
        }
    }
}
