using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

internal sealed class NativeBattleDuel112Fixture
{
    public NativeBattleDuel112Frame Frame { get; set; }
    public NativeBattleGraphicsModel Actor { get; set; } = new(0, 0x1003) { Rotation14 = 0, Color40 = 0x11223344, Color44 = 0x55667788, Alpha7 = 0x99, Transform = new(new(0, 0, 0), new(10, 20, 30), 40, 17, 4096) };
    public NativeBattleGraphicsModel Enemy { get; set; } = new(3, 0x808B) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 1000) };
    public NativeBattleDuel112DrawingInputs Drawing { get; }

    public NativeBattleDuel112Fixture(short phase)
    {
        byte[] image = new byte[NativeBattleDuel112Resources.EndAddress - NativeBattleDuel112Resources.Address];
        foreach (uint source in new uint[] { 0x12C7420, 0x12C8020, 0x12CA7B4, 0x12CB40C, 0x12D19F4, 0x12D4684, 0x12D8D28, 0x12D9DC0 })
        {
            int relative = checked((int)(source - NativeBattleDuel112Resources.Address));
            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(relative + 4), 16);
            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(relative + 20), 100);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(checked((int)(0x12DB654 - NativeBattleDuel112Resources.Address))), 0x12345678);
        NativeBattleDuel112Resources resources = new(image.ToImmutableArray(), [0, 0, 0, 0], [], []);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521960, 0x25218F8, 28, 1);
        NativeBattleEffectTaskPool target = NativeBattleEffectTaskPool.Initialize(0x2521950, 0x20DFAB8, 0xF48, 1);
        (target, _) = target.Append(0x6CBF90);
        NativeBattleModulePoolTask task = target.Slots[0].WriteWord(12, unchecked((ushort)phase)).WriteWord(14, 2).WriteDword(176, 0x1D972C0 + 156 * 3).WriteDword(172, 1000).WriteDword(48, 0xCC1E3C78).WriteDword(52, 0xAB142850);
        task = task.WriteWord(76, 1).WriteWord(78, 2).WriteWord(80, 3).WriteWord(92, 100).WriteWord(94, 200).WriteWord(96, 300).WriteWord(100, 400).WriteWord(102, 500).WriteWord(104, 600);
        for (int word = 0; word < 9; word++)
        {
            task = task.WriteWord(140 + word * 2, (ushort)(word % 4 == 0 ? 4096 : 0));
        }
        target = NativeBattleTaskFixture.EditPool(target, data => data with { Slots = target.Slots.SetItem(0, task) });
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, 0x2521960) { Pools = [root, target, NativeBattleEffectTaskPool.Initialize(0x2521940, 0x20E0F40, 28, 32), NativeBattleEffectTaskPool.Initialize(0x2521930, 0x20E0D00, 36, 16), NativeBattleEffectTaskPool.Initialize(0x2521920, 0x20E0A00, 48, 16)], DrawArenaOffset = 0x1E88 };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Duel112 = new(true, parameters, resources) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x25218E8, BitConverter.GetBytes(0x20DFAB8u)).Write(0x2521914, BitConverter.GetBytes(0x20E1940u)).Write(0x2521918, new byte[4]);
        Frame = new(memory, effect, staging, new(0x1234));
        foreach (int offset in new[] { 180, 372, 644, 1468, 2316, 2584, 3480, 3704 })
        {
            Frame = NativeBattleDuel112Workspaces.Initialize7016B0(Frame, 0, offset);
        }
        NativeBattleDuel112ActorServices actor = new(index => index == 0 ? Actor : Enemy, (index, model) =>
        {
            if (index == 0)
            {
                Actor = model;
            }
            else
            {
                Assert.Equal(3, index);
                Enemy = model;
            }
        }, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException());
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        Drawing = new(actor, (_, _, _) => throw new InvalidOperationException("This fixture does not provide an actual decoded actor pose."), view, null!, null!, (_, _, _, _) => throw new InvalidOperationException("This phase does not queue a common flash."));
    }

    public void Fill(int poolIndex, uint callback)
    {
        NativeBattleEffectTaskPool pool = Frame.Effect.Pools[poolIndex];
        while (pool.Slots.Any(task => (task.Flags & 1) == 0))
        {
            (pool, _) = pool.Append(callback);
        }
        Frame = Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(poolIndex, pool) } };
    }
}
