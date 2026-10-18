using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState InitializeNativePrimitiveFrame500400(NativeBattleGraphicsState graphics)
    {
        if (definition.NativeGraphics?.PrimitiveTables is null)
        {
            return graphics;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native500400 requires its retained process primitive owner.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native500400 requires observed retained texture-page/buffer state; migrate the named standalone snapshot explicitly.");
        NativeEffectMemory = memory with { PrimitiveProcess = process.Initialize500400() };
        return graphics with { PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
    }

    private NativeBattleGraphicsState AppendNativePrimitivePackets(NativeBattleGraphicsState graphics, ImmutableArray<NativeBattleGeometryPrimitive> primitives, int? moduleDraw = null)
    {
        if (definition.NativeGraphics?.PrimitiveTables is null)
        {
            return graphics;
        }
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native callback primitive submission requires its original initialized OT owner.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native callback primitive submission requires its process owner.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native callback primitive submission requires its retained native packet bank.");
        string bank = process.PacketBank ?? throw new NotSupportedException("Native callback primitive submission has no allocated logical packet bank.");
        int cursor = process.ByteCursor ?? throw new NotSupportedException("Native callback primitive submission has no observed packet cursor.");
        NativeBattleLogicalOrdering.Builder ordering = new(frame.Pending);
        ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        foreach (NativeBattleGeometryPrimitive primitive in primitives)
        {
            int next = checked(cursor + primitive.Words.Length * 4);
            int capacity = process.CommonPacketWriteCapacity;
            if (next > capacity)
            {
                throw new NotSupportedException("Native primitive packet cursor reaches its original arena capacity.");
            }
            bool linked = ordering.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            ordering.InvalidatePacketRange(new(bank, process.PacketBuffer, cursor), primitive.Words.Length * 4);
            stores.AddRange(NativeBattleCommonPacketArena.PrimitiveStores(cursor, primitive, linked));
            ordering.Prepend(primitive.Bucket, new(bank, process.PacketBuffer, cursor), primitive.Words, primitive.VertexPayload4.IsEmpty ? (ushort)1 : (ushort)0, primitive.VertexPayload4, moduleDraw);
            if (linked)
            {
                stores.Add(new(cursor, NativeBattleLogicalOrdering.LinkBytes(ordering.LastHeaderLink).ToImmutableArray(), NativeBattleCommonPacketArena.KnownLinkMask));
            }
            cursor = next;
        }
        NativeEffectMemory = memory with { PrimitiveProcess = process.ApplyCommonWrites(stores.ToImmutable()) with { ByteCursor = cursor } };
        return graphics with { PrimitiveFrame = frame with { Pending = ordering.ToImmutable() } };
    }

    private NativeBattleGraphicsState SubmitNativeStagePackets(NativeBattleGraphicsState graphics, NativeBattleStageState stage)
    {
        if (definition.NativeGraphics?.PrimitiveTables is null)
        {
            return graphics;
        }
        ImmutableArray<NativeBattleRawMesh> geometry = definition.NativeStageSource?.Geometry ?? throw new NotSupportedException("Native500FD0 requires its original four-group geometry DTOs.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native500FD0 packet submission requires its prior camera/register owner.");
        int[] bases = [4126, 4122, 17, 4382];
        byte[] shifts = [8, 14, 4, 16];
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder primitives = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        for (int index = 0; index < 4; index++)
        {
            NativeBattleStageGroup group = stage.Groups[index];
            if ((group.Flags & 3) != 3)
            {
                continue;
            }
            ImmutableArray<NativeBattleRawMesh> meshes = geometry.Where(mesh => mesh.StageGroup == index).ToImmutableArray();
            primitives.AddRange(NativeBattleRawGeometryPackets.Emit5099D0(meshes, group.Matrices, projection, group.ColorFlags, uint.MaxValue, group.ColorDepth, bases[index], shifts[index], null, definition.NativeGraphics!.Rules.SummonColors));
        }
        return AppendNativePrimitivePackets(graphics, primitives.ToImmutable());
    }

    private NativeBattleGraphicsState SubmitNativeModelPackets502D40(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource)
    {
        if (definition.NativeGraphics?.PrimitiveTables is null || (model.Flags0 & 6) != 2 || (model.Flags2!.Value & 0x84) != 0)
        {
            return graphics;
        }
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native502D40 requires its prior camera/register owner.");
        graphics = SubmitNativeShadowPackets5088A0(graphics, model, projection, 4121, true);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native502D40 requires its original body skeleton/animation.");
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native502D40 requires its original body geometry DTO.");
        NativeBattleModelTextureBinding binding = model.BodyTextureSlots ?? throw new NotSupportedException("Native502D40 requires its executed body507400 relocation.");
        uint color = model.Color40 ?? throw new NotSupportedException("Native502D40 requires its color DWORD40.");
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native502D40 requires its object mask DWORD124.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native502D40 requires its actual previously published camera matrix.");
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.GeometryMatrices(model, source, view);
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native502D40 requires its observed linked geometry presence.");
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder primitives = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native linked geometry requires its original507BF0/507E20 resource shape."));
        if (shared)
        {
            //507E20 patches body geometry directory DWORD2 to the single weapon object at copied resource+8.
            if (geometry.Length < 2 || linked.Length != 1)
            {
                throw new NotSupportedException("Native507E20 shared-skeleton geometry requires its original single weapon object and body directory slot1.");
            }
            for (int index = 0; index < geometry.Length; index++)
            {
                if ((mask & (1u << (index & 31))) == 0)
                {
                    continue;
                }
                NativeBattleRawMesh mesh = index == 1 ? linked[0] : geometry[index];
                NativeBattleModelTextureBinding selectedBinding = index == 1 ? model.LinkedTextureSlots ?? throw new NotSupportedException("Native507E20 requires its executed weapon texture relocation.") : binding;
                primitives.AddRange(NativeBattleRawGeometryPackets.Emit5099D0([mesh], matrices, projection, color, 1, null, 17, 4, selectedBinding));
            }
        }
        else
        {
            primitives.AddRange(NativeBattleRawGeometryPackets.Emit5099D0(geometry, matrices, projection, color, mask, null, 17, 4, resource.GeometryRelocated507550 ? null : binding));
        }
        if (!shared && !linked.IsEmpty && (model.Flags0 & 0x200) == 0)
        {
            ImmutableArray<NativeBattleActionMatrix> linkedMatrices = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(model, resource.LinkedPoseSource ?? throw new NotSupportedException("Native linked geometry requires its original independent skeleton."), view);
            primitives.AddRange(NativeBattleRawGeometryPackets.Emit5099D0(linked, linkedMatrices, projection, color, uint.MaxValue, null, 17, 4, model.LinkedTextureSlots ?? throw new NotSupportedException("Native linked geometry requires its executed507400 material relocation.")));
        }
        return AppendNativePrimitivePackets(graphics, primitives.ToImmutable());
    }

    private NativeBattleGraphicsState FinishNativePrimitiveFrame500900(NativeBattleGraphicsState graphics)
    {
        AdvanceNativeTimerDisplay4A9160(graphics);
        if (definition.NativeGraphics?.PrimitiveTables is not { } tables)
        {
            return graphics;
        }
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native500900 requires its original pending OT owner.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native500900 requires its process primitive memory.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native500900 requires its retained draw mode.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native500900 requires its actual retained OT bank selector.");
        if (process.TablePerspective.IsDefault || process.TablePerspective.Length != 2 || buffer > 1)
        {
            throw new InvalidDataException("Native500900 requires its two original OT descriptor owners.");
        }
        bool? descriptor = process.TablePerspective[buffer];
        NativeBattlePrimitiveDepthState? depth = process.Depth;
        NativeBattlePrimitiveColorState? color = process.Color;
        NativeBattleLogicalOrdering pending = frame.Pending.SelectTable(buffer);
        ImmutableArray<NativeBattleLogicalResolvedCommand> commands = [];
        ImmutableArray<NativeBattlePcTextureWindowPixels>? textureSnapshots = null;
        if ((graphics.Flags & 4) != 0)
        {
            NativeBattlePcTextureWindowCache cache = process.TextureWindowCache ?? throw new NotSupportedException("Native500900 requires its retained PC window-cache owner; explicitly reset it in a separately named snapshot migration.");
            NativeBattleSoftwarePrimitiveState software = process.Software ?? throw new NotSupportedException("Native500900 requires its retained software drawing owners; migrate the named snapshot explicitly.");
            if (software.Texture.LastTPage != process.TexturePage)
            {
                throw new NotSupportedException("Native500900 requires consistent retained aliases for its shared cached texture page.");
            }
            NativeSoftwarePrimitiveTable softwareTable = definition.NativeGraphics.SoftwarePrimitiveTable ?? throw new NotSupportedException("Native500900 requires its original software dispatch table.");
            bool hardware = definition.NativeGraphics.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native500900 requires its explicitly selected hardware execution capability.");
            NativeBattleSoftwarePrimitiveExecution execution = new(software, memory.Textures ?? throw new NotSupportedException("Native500900 requires its retained shared VRAM image."), softwareTable, hardware, cache);
            execution.ApplyEnvironment45B660(buffer ^ 1);
            var pass = NativeBattleLogicalPasses.Execute(pending, tables, new(process.TexturePage) { TextureWindowBits = process.TextureWindowBits }, execution, (reference, count) => ReadNativePhysicalPrimitive(reference, count));
            commands = pass.Commands;
            textureSnapshots = execution.TextureSnapshots;
            process = process with { PublicationState03A = process.PublicationState03A is byte state ? state <= 16 ? (byte)0 : (byte)(state - 16) : null, TexturePage = pass.Final.TexturePage, TextureWindowBits = pass.Final.TextureWindowBits, TextureWindowCache = pass.Cache, Software = execution.State };
            memory = memory with { Textures = execution.Textures };
            pending = pending.ResetAfterDraw45D080();
        }
        ImmutableArray<int?>.Builder blends = ImmutableArray.CreateBuilder<int?>(graphics.ModuleDraws.Length);
        for (int index = 0; index < graphics.ModuleDraws.Length; index++)
        {
            int?[] modes = commands.Where(command => command.Pass > 0 && command.Command.ModuleDraw == index).Select(command => command.DrawMode.BlendMode).Distinct().ToArray();
            blends.Add(modes.Length == 1 ? modes[0] : null);
        }
        NativeEffectMemory = memory with { PrimitiveProcess = process.Reset500900(graphics.Flags) };
        if (process.DisableTableReset != true)
        {
            pending = pending.ClearRoots45D550();
        }
        return graphics with { PrimitiveFrame = frame with { Pending = pending, LastCommands = commands, LastModuleBlendModes = blends.ToImmutable(), LastDescriptorPerspective = descriptor, LastDepthState = depth, LastColorState = color, LastTextureSnapshots = textureSnapshots } };
    }

    private byte[] ReadNativePhysicalPrimitive(NativeBattlePrimitiveReference reference, int count)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native retained packet alias requires its process arena owners.");
        if (reference.Bank == "20DFAB8" && reference.Buffer == 0)
        {
            return (memory.Arena ?? throw new NotSupportedException("Native retained effect packet alias requires its original arena.")).Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleFullscreenOverlay.PacketBank)
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native fullscreen static packets have a single physical buffer.");
            }
            return (memory.FullscreenOverlay ?? throw new NotSupportedException("Native fullscreen packet alias requires its retained static bytes.")).Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleThundaga105CaptureMemory.PacketBank)
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native capture statics have one physical packet bank.");
            }
            return (memory.Thundaga105Capture ?? throw new NotSupportedException("Native capture packet alias requires its retained static bytes.")).Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleHoly175WarpDrawing.PacketBank)
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Holy warp packets require their two interleaved banks in one physical owner.");
            }
            return (memory.Holy175 ?? throw new NotSupportedException("Native Holy warp alias requires its retained physical packet owner.")).Warp.PhysicalPackets.Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleHoly175CaptureMemory.PacketBank)
        {
            if (reference.Buffer > 1 || reference.ByteOffset / 24 != reference.Buffer)
            {
                throw new InvalidDataException("Native Holy capture packet alias requires its actual selected physical record.");
            }
            return (memory.Holy175 ?? throw new NotSupportedException("Native Holy capture packet alias requires its retained static bytes.")).BlackPackets.Read(reference.ByteOffset, count);
        }
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native retained common packet alias requires its physical banks.");
        return (process with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, count);
    }
}
