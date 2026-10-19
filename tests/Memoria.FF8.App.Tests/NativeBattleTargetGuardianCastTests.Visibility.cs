using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
    private static ImmutableHashSet<NativeBattlePrimitiveReference> DoomtrainCommonPacketReferences(BattleSnapshot prior, BattleSnapshot current)
    {
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        if (prior.NativeGraphics!.Auxiliary.Slots.Any(task => (task.Flags & 1) != 0) || prior.NativeGraphics.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0) || graphics.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0) || current.NativeStage is not { } stage || stage.Groups.Any(group => (group.Flags & 3) == 3))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleGraphicsModel[] actors = graphics.Models.Where(model => (model.Flags0 & 6) == 2 && model.Flags2 is { } flags && (flags & 0x85) == 0).ToArray();
        if (actors.Length != 1 || graphics.Models.Count(model => (model.Flags0 & 6) == 2 && model.Flags2 is { } flags && (flags & 0x84) == 0) != 1)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleGraphicsModel actor = actors[0];
        NativeBattleGraphicsDefinition definition = current.Definition.NativeGraphics!;
        NativeBattleGraphicsResource resource = definition.Bindings.Single(binding => binding.ActorId == graphics.ActorIds[actor.Index]).Resource;
        if (resource.LinkedGeometry is not { IsEmpty: true } || resource.Geometry is not { } geometry || resource.PoseSource is not { } pose || actor.CurrentBodyBoneMatrices is null || actor.GeometryBounds52 is not { } bounds || actor.Color40 is not { } color || actor.Color44 is not { } shadowColor || actor.SequenceState?.MeshMask124 is not { } mask || current.NativeCamera?.Projection is not { } projection || prior.NativeCamera?.Projection?.View is not { } view)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        List<(int Offset, NativeBattleGeometryPrimitive Primitive)> expected = [];
        int cursor = 0;
        if ((actor.Flags0 & 0x20) == 0 && (actor.Flags2!.Value & 0x20) == 0 && shadowColor != 0x32000000)
        {
            bool gouraud = definition.PrimitiveCapabilities!.GouraudAlpha;
            NativeBattleShadowGeometry shadow = NativeBattlePoseMatrices.ShadowGeometry5088A0(actor, bounds, view, definition.Rules.ShadowSource!, gouraud);
            NativeBattleCameraProjection shadowProjection = projection with { View = view };
            NativeBattleModuleProjectedVertex[] vertices = shadow.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shadow.CameraMatrix, vertex, shadowProjection)).ToArray();
            NativeBattleGfGeometryBatch batch = NativeBattleShadowPackets.EmitBatch5088A0(vertices, shadowColor, gouraud, 4121, 16);
            foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
            {
                if (attempt.Primitive is { } primitive && (byte)(primitive.Words[1] >> 24) is >= 0x20 and < 0x80)
                {
                    expected.Add((attempt.ByteOffsetRelative, primitive));
                }
            }
            cursor = batch.AllocationBytes;
        }
        ImmutableArray<NativeBattleGeometryPrimitive> body = NativeBattleRawGeometryPackets.Emit5099D0(geometry, NativeBattleBodyBoneMatrices.Require(actor, pose), projection, color, mask, null, 17, 4, resource.GeometryRelocated507550 ? null : actor.BodyTextureSlots);
        foreach (NativeBattleGeometryPrimitive primitive in body)
        {
            expected.Add((cursor, primitive));
            cursor = checked(cursor + primitive.Words.Length * 4);
        }
        if (expected.Count == 0)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleLogicalCommand[] commands = graphics.PrimitiveFrame!.LastCommands.Select(resolved => resolved.Command).Where(command => command.Packet.Bank is "off_B6D070" or "off_B6D094").ToArray();
        foreach (NativeBattleLogicalCommand candidate in commands.Where(command => Matches(command, expected[0].Primitive)))
        {
            int start = candidate.Packet.ByteOffset - expected[0].Offset;
            if (start <= 0 || !expected.All(item => commands.Any(command => command.Packet.Bank == candidate.Packet.Bank && command.Packet.Buffer == candidate.Packet.Buffer && command.Packet.ByteOffset == start + item.Offset && Matches(command, item.Primitive))))
            {
                continue;
            }
            // The original effect runs before ordinary model submission. Match that complete renderer allocation before admitting its preceding effect packets.
            return commands.Where(command => command.Packet.Bank == candidate.Packet.Bank && command.Packet.Buffer == candidate.Packet.Buffer && command.Packet.ByteOffset < start && command.Opcode is >= 0x20 and < 0x80).Select(command => command.Packet).ToImmutableHashSet();
        }
        return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;

        static bool Matches(NativeBattleLogicalCommand command, NativeBattleGeometryPrimitive primitive)
        {
            return command.WordOffset == 1 && command.OrderingBucket == primitive.Bucket && command.Words.AsSpan().SequenceEqual(primitive.Words.AsSpan().Slice(1)) && command.VertexPayload4.AsSpan().SequenceEqual(primitive.VertexPayload4.AsSpan());
        }
    }

    private static ImmutableHashSet<NativeBattlePrimitiveReference> CactuarBodyPacketReferences(BattleSnapshot snapshot, NativeBattleCactuar199Resources resources, NativeBattleSummonColors colors)
    {
        NativeBattleCactuarRawModelState? model = snapshot.NativeEffectMemory?.Cactuar199?.RawModel;
        if (model?.ProjectedScratch is not { } scratch || resources.Model.Geometry.Length != 1 || snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: 199 } effect || !effect.Pools.SelectMany(pool => pool.Slots).Any(task => task.Callback == 0x5A8C20 && (task.Flags & 1) != 0))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleRawMesh mesh = resources.Model.Geometry[0];
        if (scratch.Length != mesh.Vertices.Length * 8 || model.Model.Length < 0x2C)
        {
            throw new InvalidDataException("The retained body projection must cover its actual mesh.");
        }
        NativeBattleProjectedVertex[] vertices = new NativeBattleProjectedVertex[mesh.Vertices.Length];
        for (int index = 0; index < vertices.Length; index++)
        {
            vertices[index] = new(BinaryPrimitives.ReadUInt32LittleEndian(scratch.AsSpan().Slice(index * 8)), BinaryPrimitives.ReadUInt32LittleEndian(scratch.AsSpan().Slice(index * 8 + 4)));
        }
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(model.Model.AsSpan().Slice(0x28));
        ImmutableArray<NativeBattleGeometryPrimitive> expected = NativeBattleRawGeometryPackets.EmitProjected5099D0(mesh.Polygons, vertices, color, 17, 4, 0, null, colors);
        NativeBattlePrimitiveFrame frame = snapshot.NativeGraphics?.PrimitiveFrame ?? throw new InvalidDataException("The visibility sample requires its rendered primitive frame.");
        ImmutableHashSet<NativeBattlePrimitiveReference>.Builder matched = ImmutableHashSet.CreateBuilder<NativeBattlePrimitiveReference>();
        int offset = 0;
        foreach (NativeBattleGeometryPrimitive primitive in expected)
        {
            foreach (NativeBattleLogicalResolvedCommand resolved in frame.LastCommands)
            {
                NativeBattleLogicalCommand command = resolved.Command;
                if (command.Packet.Bank is "off_B6D070" or "off_B6D094" && command.Packet.ByteOffset == offset && command.WordOffset == 1 && command.OrderingBucket == primitive.Bucket && command.Words.AsSpan().SequenceEqual(primitive.Words.AsSpan().Slice(1)) && command.VertexPayload4.AsSpan().SequenceEqual(primitive.VertexPayload4.AsSpan()))
                {
                    matched.Add(command.Packet);
                }
            }
            offset = checked(offset + primitive.Words.Length * 4);
        }
        return matched.ToImmutable();
    }
    private static ImmutableHashSet<NativeBattlePrimitiveReference> GuardianPacketReferences(BattleSnapshot prior, BattleSnapshot current, ushort effect, GameAssets game)
    {
        NativeBattlePrimitiveFrame primitive = current.NativeGraphics!.PrimitiveFrame!;
        ImmutableHashSet<NativeBattlePrimitiveReference>.Builder references = primitive.LastCommands.Where(command => command.Command.Packet.Bank == "20DFAB8").Select(command => command.Command.Packet).ToImmutableHashSet().ToBuilder();
        bool priorMesh = prior.NativeGraphics!.Auxiliary.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind is NativeBattleAuxiliaryKind.ModelGroupFade50F0E0 or NativeBattleAuxiliaryKind.ModelDeformation50F2E0);
        if (priorMesh)
        {
            return references.ToImmutable();
        }
        if (effect == 199)
        {
            references.UnionWith(CactuarBodyPacketReferences(current, game.ReadNativeCactuar199Resources(), current.Definition.NativeGraphics!.Rules.SummonColors!));
        }
        else if (effect == 191)
        {
            references.UnionWith(DoomtrainCommonPacketReferences(prior, current));
        }
        else if (current.NativeEffectMemory?.Cinematic?.Effect == effect && current.NativeEffectMemory.ScriptedEffects is { } core)
        {
            uint manager = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x27973EC, 4));
            uint end = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(manager + 0x7C, 4));
            foreach (NativeBattleLogicalResolvedCommand resolved in primitive.LastCommands)
            {
                NativeBattleLogicalCommand command = resolved.Command;
                if (command.Packet.Bank is "off_B6D070" or "off_B6D094" && command.Packet.ByteOffset >= 0 && (uint)command.Packet.ByteOffset < end && command.Opcode is >= 0x20 and < 0x80)
                {
                    references.Add(command.Packet);
                }
            }
        }
        return references.ToImmutable();
    }

}
