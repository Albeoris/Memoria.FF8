using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleReachedCaptureAssertions
{
    internal static void Moving(ushort effect, BattleSnapshot current, IEnumerable<NativeBattleLogicalCommand> owned)
    {
        foreach (NativeBattleLogicalCommand command in owned.Where(command => command.Opcode is >= 0x20 and < 0x80))
        {
            Assert.True(command.PilotGeometry is not null, $"Effect{effect} uncaptured owned geometry at frame {current.Frame}: {command.Packet}, opcode {command.Opcode:X2}, word {command.WordOffset}.");
            Assert.Equal(effect, command.PilotGeometry!.Identity.Effect);
        }
    }

    internal static void SearchCommon(BattleSnapshot prior, BattleSnapshot current, NativeBattleSearchResources resources)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        NativeBattleOriginalEffectState? previous = prior.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != 92 || effect.Pools.IsDefaultOrEmpty)
        {
            Assert.False(previous?.Binding.Effect == 92 && previous.Pools.Any(pool => pool.Slots.Any(task => task.Callback == 0x755EE0 && (task.Flags & 1) != 0)), "Search lost its physical mesh rows before reached-producer coverage could inspect their final callback.");
            return;
        }
        NativeBattleLogicalCommand[] common = current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank is "off_B6D070" or "off_B6D094" && command.WordOffset == 1 && command.Opcode is >= 0x20 and < 0x80).ToArray();
        HashSet<(NativeBattlePrimitiveReference Packet, int Word)> used = [];
        NativeBattleCameraProjection projection = prior.NativeCamera?.Projection ?? throw new InvalidDataException("Search coverage requires its pre-callback projection.");
        for (int bank = 0; bank < effect.Pools.Length; bank++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[bank];
            for (int slot = 0; slot < pool.Slots.Length; slot++)
            {
                NativeBattleModulePoolTask task = pool.Slots[slot];
                if (task.Callback != 0x755EE0)
                {
                    continue;
                }
                NativeBattleModulePoolTask? old = previous?.Binding.Effect == 92 && bank < previous.Pools.Length && slot < previous.Pools[bank].Slots.Length ? previous.Pools[bank].Slots[slot] : null;
                bool wasActive = old is not null && old.Callback == task.Callback && (old.Flags & 1) != 0;
                if (!wasActive && (task.Flags & 1) == 0 || wasActive && old!.ReadWord(0x24) == task.ReadWord(0x24))
                {
                    continue;
                }
                if ((task.ReadByte(0x26) & 4) != 0)
                {
                    continue;
                }
                ImmutableArray<byte> copiedView = current.NativeEffectMemory?.Search?.ViewCopy ?? throw new InvalidDataException("Search coverage requires its actual copied renderer view.");
                NativeBattleActionMatrix view = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(copiedView.AsSpan(index * 2, 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(copiedView.AsSpan(20, 4)), BinaryPrimitives.ReadInt32LittleEndian(copiedView.AsSpan(24, 4)), BinaryPrimitives.ReadInt32LittleEndian(copiedView.AsSpan(28, 4)), 4096);
                NativeBattleActionMatrix projectedView = projection.View ?? throw new InvalidDataException("Search coverage requires its renderer view.");
                Assert.Equal(view.Rotation.ToArray(), projectedView.Rotation.ToArray());
                Assert.Equal((view.X, view.Y, view.Z), (projectedView.X, projectedView.Y, projectedView.Z));
                NativeBattleModulePoolTask detached = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(task));
                NativeBattleGfGeometryBatch expected = NativeBattleSearchMeshDrawing.Draw755FC0(detached, resources.ParticleMesh, view, projection, current.Definition.NativeGraphics!.Rules.SummonColors!);
                foreach (NativeBattleGfGeometryAttempt attempt in expected.Attempts)
                {
                    if (attempt.Primitive is not { } primitive || primitive.Words.Length < 2 || (byte)(primitive.Words[1] >> 24) is not (>= 0x20 and < 0x80))
                    {
                        continue;
                    }
                    NativeBattleLogicalCommand? command = common.FirstOrDefault(candidate => !used.Contains((candidate.Packet, candidate.WordOffset)) && candidate.OrderingBucket == primitive.Bucket && candidate.VertexPayload4.SequenceEqual(primitive.VertexPayload4) && candidate.WordOffset + candidate.Words.Length <= primitive.Words.Length && candidate.Words.SequenceEqual(primitive.Words.AsSpan().Slice(candidate.WordOffset, candidate.Words.Length).ToArray()));
                    Assert.True(command is not null, $"Search common mesh row {bank}/{slot} at frame {current.Frame} has no exact native packet match at bucket {primitive.Bucket}.");
                    used.Add((command!.Packet, command.WordOffset));
                    Moving(92, current, [command]);
                }
            }
        }
    }
}
