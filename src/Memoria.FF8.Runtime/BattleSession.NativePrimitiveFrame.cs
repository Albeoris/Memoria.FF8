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
        NativeBattleLogicalOrdering ordering = frame.Pending;
        ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        foreach (NativeBattleGeometryPrimitive primitive in primitives)
        {
            int next = checked(cursor + primitive.Words.Length * 4);
            int capacity = bank == "off_B6D070" ? 114688 : bank == "off_B6D094" ? 147456 : throw new NotSupportedException("Native callback requires an unknown original packet arena.");
            if (next > capacity)
            {
                throw new NotSupportedException("Native primitive packet cursor reaches its original arena capacity.");
            }
            bool linked = ordering.Proxies.Length < NativeBattlePrimitiveOrdering.ProxyCapacity;
            stores.AddRange(NativeBattleCommonPacketArena.PrimitiveStores(cursor, primitive, linked));
            ordering = ordering.Prepend(primitive.Bucket, new(bank, process.PacketBuffer, cursor), primitive.Words, primitive.VertexPayload4.IsEmpty ? (ushort)1 : (ushort)0, primitive.VertexPayload4, moduleDraw);
            cursor = next;
        }
        NativeEffectMemory = memory with { PrimitiveProcess = process.ApplyCommonWrites(stores.ToImmutable()) with { ByteCursor = cursor } };
        return graphics with { PrimitiveFrame = frame with { Pending = ordering } };
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
        for (int index = 0; index < 4; index++)
        {
            NativeBattleStageGroup group = stage.Groups[index];
            if ((group.Flags & 3) != 3)
            {
                continue;
            }
            ImmutableArray<NativeBattleRawMesh> meshes = geometry.Where(mesh => mesh.StageGroup == index).ToImmutableArray();
            graphics = AppendNativePrimitivePackets(graphics, NativeBattleRawGeometryPackets.Emit5099D0(meshes, group.Matrices, projection, group.ColorFlags, uint.MaxValue, group.ColorDepth, bases[index], shifts[index], null, definition.NativeGraphics!.Rules.SummonColors));
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
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.GeometryMatrices(model, source, view);
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native502D40 requires its observed linked geometry presence.");
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
                graphics = AppendNativePrimitivePackets(graphics, NativeBattleRawGeometryPackets.Emit5099D0([mesh], matrices, projection, color, 1, null, 17, 4, selectedBinding));
            }
        }
        else
        {
            graphics = AppendNativePrimitivePackets(graphics, NativeBattleRawGeometryPackets.Emit5099D0(geometry, matrices, projection, color, mask, null, 17, 4, binding));
        }
        if (!shared && !linked.IsEmpty && (model.Flags0 & 0x200) == 0)
        {
            ImmutableArray<NativeBattleActionMatrix> linkedMatrices = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(model, resource.LinkedPoseSource ?? throw new NotSupportedException("Native linked geometry requires its original independent skeleton."), view);
            graphics = AppendNativePrimitivePackets(graphics, NativeBattleRawGeometryPackets.Emit5099D0(linked, linkedMatrices, projection, color, uint.MaxValue, null, 17, 4, model.LinkedTextureSlots ?? throw new NotSupportedException("Native linked geometry requires its executed507400 material relocation.")));
        }
        return graphics;
    }

    private NativeBattleGraphicsState FinishNativePrimitiveFrame500900(NativeBattleGraphicsState graphics)
    {
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
        ImmutableArray<NativeBattleLogicalResolvedCommand> commands = [];
        if ((graphics.Flags & 4) != 0)
        {
            //500400 initializes both original battle drawenvs to(0,0,320,216);45B660→45B6F0 publishes flags1.
            NativeBattlePcTextureWindowCache cache = process.TextureWindowCache ?? throw new NotSupportedException("Native500900 requires its retained PC window-cache owner; explicitly reset it in a separately named snapshot migration.");
            var pass = NativeBattleLogicalPasses.Execute(frame.Pending, tables, new(process.TexturePage) { TextureWindowBits = process.TextureWindowBits }, 1, cache);
            commands = pass.Commands;
            process = process with { TexturePage = pass.Final.TexturePage, TextureWindowBits = pass.Final.TextureWindowBits, TextureWindowCache = pass.Cache };
        }
        ImmutableArray<int?>.Builder blends = ImmutableArray.CreateBuilder<int?>(graphics.ModuleDraws.Length);
        for (int index = 0; index < graphics.ModuleDraws.Length; index++)
        {
            int?[] modes = commands.Where(command => command.Pass > 0 && command.Command.ModuleDraw == index).Select(command => command.DrawMode.BlendMode).Distinct().ToArray();
            blends.Add(modes.Length == 1 ? modes[0] : null);
        }
        NativeEffectMemory = memory with { PrimitiveProcess = process.Reset500900(graphics.Flags) };
        return graphics with { PrimitiveFrame = frame with { Pending = NativeBattleLogicalOrdering.Clear(), LastCommands = commands, LastModuleBlendModes = blends.ToImmutable(), LastDescriptorPerspective = descriptor, LastDepthState = depth, LastColorState = color } };
    }
}
