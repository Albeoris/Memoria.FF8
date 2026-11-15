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
            int selected = NativeBattleWaterSlots.LookupB65370(current, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }, (current, address) =>
        {
            BattleModelOwner index = address;
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, graphics.ActorModels.Get(index) with { Flags0 = (ushort)(graphics.ActorModels.Get(index).Flags0 | 4) }) };
            return current;
        }, address => graphics.ActorModels.Get(address).Color40 ?? throw new NotSupportedException("Native actor clone requires its actual physical color DWORD40."), address => presentationForeignActors?.ContainsKey(address) == true ? null : graphics.ActorModels.Get(address).SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native actor clone requires its actual physical object mask DWORD124."), (current, clone, address) =>
        {
            if (ReadNativeForeignModel(graphics, address) is null)
            {
                _ = NativeGraphicsPoseResource(graphics, address);
            }
            return current with { Arena = current.Arena.MarkUnknown(ArenaOffset(clone + 0x10, 12), 12).MarkUnknown(ArenaOffset(clone + 0x30, 16), 16) };
        }, (current, address) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native actor clone requires its canonical V0 input owner.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, address, registers) };
            return current;
        }, ComposeAndDraw, (current, address) =>
        {
            BattleModelOwner index = address;
            if (ReadNativeForeignModel(graphics, index) is not null)
            {
                return current;
            }
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native actor clone restoration requires its actual physical pose source.");
            NativeBattleGraphicsModel restored = NativeBattleBodyBoneMatrices.Publish508C90(graphics.ActorModels.Get(index), source);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, restored) };
            return current;
        });
        return (NativeBattleCinematicActorClone48.Draw(frame, services, graphics.NativeBindings.CaptureAliases(graphics.ActorModels)), graphics);

        NativeBattleCinematicActorCloneDraw ComposeAndDraw(NativeBattleWaterExecutionFrame current, uint clone, BattleModelOwner actorAddress, NativeBattleActionMatrix basis)
        {
            BattleModelOwner index = actorAddress;
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(index);
            if (ReadNativeForeignModel(graphics, index) is { } foreign)
            {
                uint hostOutput = CoreDword(current, unchecked(CoreDword(current, 0x27973EC) + 0x7C));
                NativeBattleEffectMemory hostMemory = RequireWaterMemory();
                NativeBattlePrimitiveProcess hostProcess = hostMemory.PrimitiveProcess ?? throw new NotSupportedException("Native actor clone requires its current renderer gate.");
                var hostGate = hostProcess.Enter5099D0(definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native actor clone requires its selected renderer capability."));
                NativeEffectMemory = hostMemory with { PrimitiveProcess = hostGate.Process };
                if (!hostGate.Draw)
                {
                    return new(current, hostOutput);
                }
                NativeBattleForeignActor host = presentationForeignActors![index];
                NativeBattlePosedActor hostPose = host.Pose ?? throw new NotSupportedException("The host actor clone requires its original posed basis.");
                NativeBattleForeignModelGeometry hostGeometry = host.Geometry ?? throw new NotSupportedException("The host actor clone requires its original posed geometry.");
                NativeBattleActionMatrix bodyBasis = NativeBattleGfMatrices.Compose56C2F0(basis, NativeBattleGfMatrices.Compose56C2F0(NativeBattleForeignPlacement.Inverse(hostPose.World), hostGeometry.World));
                NativeBattleCameraProjection hostProjection = NativeCamera?.Projection ?? throw new NotSupportedException("Native actor clone requires its current projection.");
                NativeBattleSummonColors hostColors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native actor clone requires its original color arithmetic.");
                uint hostColor = CloneDword(0x5C);
                NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = hostGate.Process.PublishModelColor(hostColor) };
                NativeBattleWaterSlotBank hostVisualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
                NativeBattlePilotVisualIdentity identity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, current, hostVisualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, hostVisualBank)) with { ModelOwner = index };
                NativeBattleRawModelBodyDraw hostDraw = NativeBattleForeignModelDrawing.DrawBody(hostGeometry, bodyBasis, hostProjection, hostColor, hostColors, identity, 4, new(CloneWord(0x54), CloneWord(0x56), CloneWord(0x58), CloneWord(0x5A)), OtBucket(current.ReadTemporaryDword(0x14C)), CloneWord(0x64));
                uint hostWork = CloneDword(0x44);
                if (hostDraw.ProjectedBytesWritten != 0)
                {
                    current = current with { Arena = current.Arena.Write(ArenaOffset(hostWork, hostDraw.ProjectedBytesWritten), hostDraw.ProjectedScratch.AsSpan().Slice(0, hostDraw.ProjectedBytesWritten)) };
                }
                var hostWritten = AppendNativeCinematicPackets(current, hostOutput, null, hostDraw.Packets, graphics);
                graphics = hostWritten.Graphics;
                return new(hostWritten.Frame, unchecked(hostOutput + (uint)hostDraw.Packets.AllocationBytes));
            }
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
            NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native actor clone requires its current physical rig.");
            _ = NativeBattleBodyBoneMatrices.Require(actor, source);
            actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, basis);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, actor) };
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
            NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            NativeBattlePilotVisualIdentity visualIdentity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, current, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, visualBank)) with { ModelOwner = index };
            NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, NativeBattleBodyBoneMatrices.Require(actor, source), projection, color, mask, new byte[length].ToImmutableArray(), colors, 4, viewport, OtBucket(current.ReadTemporaryDword(0x14C)), CloneWord(0x64), visualIdentity);
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
