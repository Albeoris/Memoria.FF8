using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    // Submit the representable eighth-pixel canvas so resizing can reveal existing scene geometry.
    private static readonly NativeBattleGeometryViewport SceneGeometryViewport = new(short.MinValue / 8, short.MinValue / 8, ushort.MaxValue / 8, ushort.MaxValue / 8);

    private void InitializeNativePrimitiveDepth47CE10()
    {
        if (definition.NativeGraphics?.PrimitiveTables is null)
        {
            return;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native47CE10 requires its retained primitive process owner.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native47CE10 requires its retained primitive process owner.");
        NativeBattlePrimitiveDepthState depth = process.Depth ?? throw new NotSupportedException("Native47CE10 requires its retained depth owner.");
        // The mode initialization callback selects the depth blend before47CCB0 enters500870/500400.
        NativeEffectMemory = memory with { PrimitiveProcess = process with { Depth = depth.SetBucketMix460B60(0.8f) } };
    }

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
            ordering.Prepend(primitive.Bucket, new(bank, process.PacketBuffer, cursor), primitive.Words, primitive.VertexPayload4.IsEmpty ? (ushort)1 : (ushort)0, primitive.VertexPayload4, moduleDraw, primitive.ProjectedCoordinates);
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
        ImmutableArray<NativeBattleRawMesh> geometry = RequireNativeStageGeometry();
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native500FD0 packet submission requires its prior camera/register owner.");
        int[] bases = [4126, 4122, 17, 4382];
        byte[] shifts = [8, 14, 4, 16];
        for (int index = 0; index < 4; index++)
        {
            NativeBattleStageGroup group = stage.Groups[index];
            if ((group.Flags & 3) != 3)
            {
                continue;
            }
            if (!ResetAndEnterNativeModelRenderer5099D0(group.ColorFlags))
            {
                continue;
            }
            ImmutableArray<NativeBattleRawMesh> meshes = geometry.Where(mesh => mesh.StageGroup == index).ToImmutableArray();
            ImmutableArray<NativeBattleGeometryPrimitive> primitives = NativeBattleRawGeometryPackets.Emit5099D0(meshes, group.Matrices, projection, group.ColorFlags, uint.MaxValue, group.ColorDepth, bases[index], shifts[index], null, definition.NativeGraphics!.Rules.SummonColors, viewport: SceneGeometryViewport);
            graphics = AppendNativeModelRendererPackets(graphics, primitives, !meshes.IsEmpty);
        }
        return graphics;
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
        NativeBattleVector? actualCameraEye = NativeCamera?.CurrentPose?.Eye is { } eye ? new(eye.X, eye.Y, eye.Z) : null;
        model = NativeBattlePoseMatrices.ComposeGeometryMatrices502D40(model, source, view, actualCameraEye);
        graphics = graphics with { Models = graphics.Models.SetItem(model.Index, model) };
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native502D40 requires its observed linked geometry presence.");
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder primitives = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native linked geometry requires its original507BF0/507E20 resource shape."));
        bool bodyDraw = ResetAndEnterNativeModelRenderer5099D0(color);
        if (bodyDraw && shared)
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
                primitives.AddRange(NativeBattleRawGeometryPackets.Emit5099D0([mesh], matrices, projection, color, 1, null, 17, 4, selectedBinding, publishProjectedScratch: PublishNativeBodyProjectionScratch, viewport: SceneGeometryViewport));
            }
        }
        else if (bodyDraw)
        {
            primitives.AddRange(NativeBattleRawGeometryPackets.Emit5099D0(geometry, matrices, projection, color, mask, null, 17, 4, resource.GeometryRelocated507550 ? null : binding, publishProjectedScratch: PublishNativeBodyProjectionScratch, viewport: SceneGeometryViewport));
        }
        if (bodyDraw)
        {
            bool selected = Enumerable.Range(0, geometry.Length).Any(index => (mask & (1u << (index & 31))) != 0);
            graphics = AppendNativeModelRendererPackets(graphics, primitives.ToImmutable(), selected);
        }
        if (!shared && !linked.IsEmpty && (model.Flags0 & 0x200) == 0)
        {
            ImmutableArray<NativeBattleActionMatrix> linkedMatrices = NativeBattlePoseMatrices.LinkedDrawGeometryMatrices502D40(model, resource.LinkedPoseSource ?? throw new NotSupportedException("Native linked geometry requires its original independent skeleton."), view, actualCameraEye);
            if (ResetAndEnterNativeModelRenderer5099D0(color))
            {
                ImmutableArray<NativeBattleGeometryPrimitive> linkedPrimitives = NativeBattleRawGeometryPackets.Emit5099D0(linked, linkedMatrices, projection, color, uint.MaxValue, null, 17, 4, model.LinkedTextureSlots ?? throw new NotSupportedException("Native linked geometry requires its executed507400 material relocation."), publishProjectedScratch: PublishNativeBodyProjectionScratch, viewport: SceneGeometryViewport);
                graphics = AppendNativeModelRendererPackets(graphics, linkedPrimitives, true);
            }
        }
        return graphics;
    }

    private void PublishNativeBodyProjectionScratch(IReadOnlyList<NativeBattleProjectedVertex> vertices)
    {
        ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native body projection requires its loaded scratch allocation maximum.");
        NativeFileStaging = NativeBattleBodyProjectionScratch.Publish(NativeFileStaging ?? throw new NotSupportedException("Native body projection requires its canonical shared byte owner."), maximum, vertices);
    }

    private void PublishNativeModelRendererColor(uint color)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native model color publication requires its process primitive owner.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native model color publication requires its retained renderer controls.");
        NativeEffectMemory = memory with { PrimitiveProcess = process.PublishModelColor(color) };
    }

    private bool ResetAndEnterNativeModelRenderer5099D0(uint color)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native model rendering requires its process primitive owner.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native model rendering requires its retained renderer gate.");
        bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native model renderer query requires its selected hardware capability.");
        bool draw;
        (process, draw) = process.Reset5099C0().Enter5099D0(hardware);
        NativeEffectMemory = memory with { PrimitiveProcess = draw ? process.PublishModelColor(color) : process };
        return draw;
    }

    private NativeBattleGraphicsState AppendNativeModelRendererPackets(NativeBattleGraphicsState graphics, ImmutableArray<NativeBattleGeometryPrimitive> primitives, bool renderedObject)
    {
        graphics = AppendNativePrimitivePackets(graphics, primitives);
        if (renderedObject)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native model packet publication requires its process owner.");
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native model packet publication requires its actual cursor owner.");
            int cursor = process.ByteCursor ?? throw new NotSupportedException("Native model packet publication requires its current bank-relative cursor.");
            NativeEffectMemory = memory with { PrimitiveProcess = process.Publish50FDF0(unchecked((uint)cursor), null) };
        }
        return graphics;
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
        if (reference.Bank == "1D9A020")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native final stage overlay uses its contiguous physical banks.");
            }
            NativeBattleFinalStage142State stage = NativeStage?.FinalStage142 ?? throw new NotSupportedException("Native final stage overlay requires its retained packet owner.");
            return stage.ReadOverlay(checked(NativeBattleFinalStage142.OverlayBankAddress + (uint)reference.ByteOffset), count);
        }
        if (reference.Bank == "20DFAB8" && reference.Buffer == 0)
        {
            return (memory.Arena ?? throw new NotSupportedException("Native retained effect packet alias requires its original arena.")).Read(reference.ByteOffset, count);
        }
        if (reference.Bank is "2491BC0" or "DD6780")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Tornado packet banks require their contiguous executable owners.");
            }
            NativeBattleTornado146State state = memory.Tornado146 ?? throw new NotSupportedException("Native Tornado requires its retained physical packet banks.");
            return reference.Bank == "2491BC0" ? state.Storage.Read(reference.ByteOffset, count) : (state.CapturePacket ?? throw new NotSupportedException("Native Tornado requires its executed capture template.")).AsSpan().Slice(reference.ByteOffset, count).ToArray();
        }
        if (reference.Bank is "2296A30" or "D1B490" or "2293F50" or "2294DA0" or "2295BC0")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Wall packet banks require their actual contiguous executable owners.");
            }
            NativeBattleWall189State state = memory.Wall189 ?? throw new NotSupportedException("Native Wall requires its retained executable packet banks.");
            if (reference.Bank == "2296A30")
            {
                return state.Packets.Read(reference.ByteOffset, count);
            }
            System.Collections.Immutable.ImmutableArray<byte> packet = reference.Bank switch
            {
                "D1B490" => state.ClearPacket,
                "2293F50" => state.Area,
                "2294DA0" => state.Offset,
                _ => state.Environment
            };
            return packet.AsSpan().Slice(reference.ByteOffset, count).ToArray();
        }
        if (reference.Bank is "22DB3C4" or "22BE8E8" or "D34868")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Ultra Waves packet banks require their physical contiguous owner.");
            }
            NativeBattleUltraWaves184State state = memory.UltraWaves184 ?? throw new NotSupportedException("Native Ultra Waves requires its retained executable packet banks.");
            int length = reference.Bank switch { "22DB3C4" => NativeBattleUltraWaves184Profile.PacketDrawingLength, "22BE8E8" => NativeBattleUltraWaves184Profile.QuadsLength, _ => 48 };
            if (reference.ByteOffset < 0 || count < 0 || reference.ByteOffset > length - count)
            {
                throw new InvalidDataException("Native Ultra Waves packet read leaves its physical drawing bank.");
            }
            NativeBattleEffectArena packets = reference.Bank switch { "22DB3C4" => state.Packets, "22BE8E8" => state.Quads, _ => state.CapturePackets };
            return packets.Read(reference.ByteOffset, count);
        }
        if (reference.Bank is "24224D4" or "24059F8" or "DBAEE0")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Ultra Waves packet banks share their physical contiguous owners.");
            }
            NativeBattleUltraWaves154State state = memory.UltraWaves154 ?? throw new NotSupportedException("Native Ultra Waves requires its retained executable packet banks.");
            NativeBattleEffectArena packets = reference.Bank switch { "24224D4" => state.Packets, "24059F8" => state.Quads, _ => state.CapturePackets };
            return packets.Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleSandStorm155Profile.PacketBankId)
        {
            if (reference.Buffer != 0 || reference.ByteOffset < 0 || count < 0 || reference.ByteOffset > NativeBattleSandStorm155Profile.PacketLength - count)
            {
                throw new InvalidDataException("Sand Storm packet read leaves its actual contiguous executable banks.");
            }
            return (memory.SandStorm155 ?? throw new NotSupportedException("Sand Storm requires its retained executable packet banks.")).Packets.Read(reference.ByteOffset, count);
        }
        if (reference.Bank == "2368580")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Sand Storm uses its original executable packet owner.");
            }
            return (memory.SandStorm176 ?? throw new NotSupportedException("Native Sand Storm requires its retained executable packet bank.")).Module.Read(reference.ByteOffset, count);
        }
        if (reference.Bank == "2476660")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Regen uses one retained physical packet bank.");
            }
            return (memory.Regen147 ?? throw new NotSupportedException("Native Regen requires its retained process storage.")).Storage.Read(reference.ByteOffset, count);
        }
        if (reference.Bank == "230AD0C")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Bomb glow packet banks share one physical contiguous owner.");
            }
            return (memory.BombGlow179 ?? throw new NotSupportedException("Native Bomb glow requires its retained executable packet banks.")).Packets.Read(reference.ByteOffset, count);
        }
        if (reference.Bank == "243BE6C")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Sticky Web packet banks share one contiguous physical owner.");
            }
            return (memory.StickyWeb153 ?? throw new NotSupportedException("Native Sticky Web packet alias requires its retained executable banks.")).Packets.Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleNeedlesFamilyPacketArena.Bank(177))
        {
            return NativeBattleNeedlesFamilyPacketArena.Read(memory.ThousandNeedles177 ?? throw new NotSupportedException("Needles packets require their retained native banks."), reference, count);
        }
        if (reference.Bank == NativeBattleNeedlesFamilyPacketArena.Bank(178))
        {
            return NativeBattleNeedlesFamilyPacketArena.Read(memory.TenThousandNeedles178 ?? throw new NotSupportedException("Needles packets require their retained native banks."), reference, count);
        }
        if (reference.Bank == NativeBattleGatlingGun305PacketArena.PacketBank)
        {
            return NativeBattleGatlingGun305PacketArena.Read(memory.GatlingGun305 ?? throw new NotSupportedException("Gatling Gun packets require their retained native banks."), reference, count);
        }
        if (reference.Bank == "22F421C")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Effect183 packet banks require their single contiguous physical owner.");
            }
            return (memory.Effect183 ?? throw new NotSupportedException("Effect183 requires its retained executable packet storage.")).ReadPacket(checked(NativeBattleEffect183Profile.PacketBase + (uint)reference.ByteOffset), count);
        }
        if (reference.Bank == NativeBattleMeltEye158PacketArena.PacketBank)
        {
            return NativeBattleMeltEye158PacketArena.Read(memory.MeltEye158 ?? throw new NotSupportedException("Melt-Eye packets require their retained native banks."), reference, count);
        }
        if (reference.Bank == "2466638")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Meltdown packet banks share one contiguous physical owner.");
            }
            return (memory.Meltdown148 ?? throw new NotSupportedException("Native Meltdown packet alias requires its retained executable banks.")).Packets.Read(reference.ByteOffset, count);
        }
        if (reference.Bank == "2523C98")
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Reflect packet banks share one contiguous physical owner.");
            }
            return (memory.Reflect106 ?? throw new NotSupportedException("Native Reflect packet alias requires its retained executable banks.")).Packets.Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleGuardianWorkPacketArena.PacketBank)
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native work packets have one physical buffer.");
            }
            return (memory.GuardianWorkBuffer ?? throw new NotSupportedException("Native work packet alias requires its retained physical buffer.")).Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleFullscreenOverlay.PacketBank)
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native fullscreen static packets have a single physical buffer.");
            }
            return (memory.FullscreenOverlay ?? throw new NotSupportedException("Native fullscreen packet alias requires its retained static bytes.")).Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleGroundCaptureMemory.PacketBank)
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native ground capture statics have one physical packet bank.");
            }
            return (memory.GroundCapture ?? throw new NotSupportedException("Native ground capture packet alias requires its retained static bytes.")).Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleThundaga105CaptureMemory.PacketBank)
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native capture statics have one physical packet bank.");
            }
            return (memory.Thundaga105Capture ?? throw new NotSupportedException("Native capture packet alias requires its retained static bytes.")).Read(reference.ByteOffset, count);
        }
        if (reference.Bank == NativeBattleFatedCircle162Capture.PacketBank)
        {
            if (reference.Buffer != 0)
            {
                throw new InvalidDataException("Native Fated Circle capture packet alias requires its physical static buffer.");
            }
            return (NativeFileStaging ?? throw new NotSupportedException("Native Fated Circle capture requires its retained physical packet bytes.")).Read(checked((uint)reference.ByteOffset), count);
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
