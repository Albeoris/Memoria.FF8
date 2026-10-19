using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
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
