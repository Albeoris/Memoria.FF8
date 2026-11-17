using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed record NativeBattleModelPrefixDiagnostic(int Start, int BodyPrimitives, int ExpectedPrimitives, int? MismatchOffset, NativeBattleGeometryPrimitive? Expected, ImmutableArray<NativeBattleLogicalCommand> Actual);

public sealed class NativeBattleAquaPresentationTests
{
    internal static ImmutableHashSet<NativeBattlePrimitiveReference> AquaPacketReferences(NativeBattleFixtureState prior, NativeBattleFixtureState current, NativeBattleGraphicsDefinition definition, Action<NativeBattleModelPrefixDiagnostic>? diagnostic = null, Func<NativeBattleGraphicsModel, NativeBattleGraphicsResource, NativeBattleGraphicsResource>? resolveResource = null)
    {
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        if (prior.NativeGraphics!.Auxiliary.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleAuxiliaryKind.TextureAnimation5057D0) || prior.NativeGraphics!.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0) || graphics.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        var (actor, actorSlot) = ReadOriginalModelCells(graphics).Select((model, slot) => (model, slot)).FirstOrDefault(entry => (entry.model.Flags0 & 6) == 2 && entry.model.Flags2 is { } flags && (flags & 0x85) == 0);
        if (actor is null || ReadOriginalModelCells(graphics).Take(actorSlot).Any(model => (model.Flags0 & 6) == 2 && model.Flags2 is { } flags && (flags & 0x84) == 0))
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleGraphicsResource resource = definition.Bindings.Single(binding => binding.ActorId == ReadOriginalModelActorId(graphics, actorSlot)).Resource;
        if (actor.ActiveStagedResourceRow100 is not null)
        {
            resource = resolveResource is null ? NativeBattleVictoryResources.Resolve(current.NativeFileStaging!, current.NativeModelCpuResources!, actor, resource) : resolveResource(actor, resource);
        }
        if (resource.Geometry is not { } geometry || resource.PoseSource is not { } pose || actor.CurrentBodyBoneMatrices is null || actor.GeometryBounds52 is not { } bounds || actor.Color40 is not { } color || actor.Color44 is not { } shadowColor || actor.SequenceState?.MeshMask124 is not { } mask || prior.NativeCamera?.Projection is not { } projection || projection.View is not { } view)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleGraphicsModel beforeActor = ReadOriginalModelCell(prior.NativeGraphics!, actorSlot);
        if (beforeActor.CurrentBodyBoneMatrices is null || beforeActor.GeometryWorldMatrix is null)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleGraphicsModel drawActor = actor with { CurrentBodyBoneMatrices = beforeActor.CurrentBodyBoneMatrices, GeometryWorldMatrix = beforeActor.GeometryWorldMatrix, Transform = beforeActor.Transform };
        drawActor = NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(drawActor, pose, view);
        ImmutableArray<NativeBattleActionMatrix> drawMatrices = NativeBattleBodyBoneMatrices.Require(drawActor, pose);
        mask = beforeActor.SequenceState?.MeshMask124 ?? throw new InvalidDataException("The model prefix requires the actual pre-submission object mask.");
        List<(int Offset, NativeBattleGeometryPrimitive Primitive)> expected = [];
        int cursor = 0;
        if ((actor.Flags0 & 0x20) == 0 && (actor.Flags2!.Value & 0x20) == 0 && shadowColor != 0x32000000)
        {
            bool gouraud = definition.PrimitiveCapabilities!.GouraudAlpha;
            NativeBattleShadowGeometry shadow = NativeBattlePoseMatrices.ShadowGeometry5088A0(drawActor, bounds, view, definition.Rules.ShadowSource!, gouraud);
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
        ImmutableArray<NativeBattleGeometryPrimitive> body;
        if (resource.LinkedGeometry is { IsEmpty: false } linked && resource.LinkedGeometryUsesBodySkeleton == true)
        {
            if (geometry.Length < 2 || linked.Length != 1)
            {
                return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
            }
            ImmutableArray<NativeBattleGeometryPrimitive>.Builder parts = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
            for (int index = 0; index < geometry.Length; index++)
            {
                if ((mask & (1u << (index & 31))) != 0)
                {
                    parts.AddRange(NativeBattleRawGeometryPackets.Emit5099D0([index == 1 ? linked[0] : geometry[index]], drawMatrices, projection, color, 1, null, 17, 4, index == 1 ? actor.LinkedTextureSlots : actor.BodyTextureSlots));
                }
            }
            body = parts.ToImmutable();
        }
        else
        {
            body = NativeBattleRawGeometryPackets.Emit5099D0(geometry, drawMatrices, projection, color, mask, null, 17, 4, resource.GeometryRelocated507550 ? null : actor.BodyTextureSlots);
        }
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
            (int Offset, NativeBattleGeometryPrimitive Primitive)[] missing = expected.Where(item => !commands.Any(command => command.Packet.Bank == candidate.Packet.Bank && command.Packet.Buffer == candidate.Packet.Buffer && command.Packet.ByteOffset == start + item.Offset && Matches(command, item.Primitive))).Take(1).ToArray();
            int? mismatch = missing.Length == 0 ? null : missing[0].Offset;
            diagnostic?.Invoke(new(start, body.Length, expected.Count, mismatch, missing.Length == 0 ? null : missing[0].Primitive, mismatch is int relative ? commands.Where(command => command.Packet.Bank == candidate.Packet.Bank && command.Packet.Buffer == candidate.Packet.Buffer && command.Packet.ByteOffset == start + relative).ToImmutableArray() : []));
            if (start < 0 || missing.Length != 0)
            {
                continue;
            }
            // Submission uses the retained prior pose/world matrices; the later sequence step republishes the snapshot's new local pose.
            // Match the complete first model allocation before admitting its preceding effect packets.
            return commands.Where(command => command.Packet.Bank == candidate.Packet.Bank && command.Packet.Buffer == candidate.Packet.Buffer && command.Packet.ByteOffset < start && command.Opcode is >= 0x20 and < 0x80).Select(command => command.Packet).ToImmutableHashSet();
        }
        return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;

        static bool Matches(NativeBattleLogicalCommand command, NativeBattleGeometryPrimitive primitive)
        {
            return command.WordOffset == 1 && command.OrderingBucket == primitive.Bucket && command.Words.AsSpan().SequenceEqual(primitive.Words.AsSpan().Slice(1)) && command.VertexPayload4.AsSpan().SequenceEqual(primitive.VertexPayload4.AsSpan());
        }
    }


    private static bool Released(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.ReadFixturePresentation().NativeGraphics!;
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.ReadFixturePresentation().NativeEffectMemory!.Active == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104) && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
    }

    private static int CountRgbChanges(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int changed = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (!first.AsSpan(offset, 3).SequenceEqual(second.AsSpan(offset, 3)))
            {
                changed++;
            }
        }
        return changed;
    }
}
