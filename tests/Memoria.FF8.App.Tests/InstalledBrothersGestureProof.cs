using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class InstalledBrothersGestureProof
{
    private static readonly int[] bodyJoints = [0, 23, 25];
    private static readonly int[] gestureJoints = [30, 31, 36, 37];

    internal sealed record Rig(byte Counter, ImmutableArray<int> Body, ImmutableArray<int> Hands);
    internal sealed record Sample(Rig Sacred, Rig Minotaur);

    internal static Sample Capture(BattlePresentationProcess process)
    {
        NativeBattleEffectMemory memory = process.Memory;
        NativeBattleScriptedEffectMemory core = Assert.IsType<NativeBattleScriptedEffectMemory>(memory.ScriptedEffects);
        NativeBattleEffectArena arena = Assert.IsType<NativeBattleEffectArena>(memory.Arena);
        uint manager = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x27973EC, 4));
        uint slots = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(manager + 0x90, 4));
        Rig? sacred = null;
        Rig? minotaur = null;
        for (uint address = 0x2797554; address < 0x27975D4; address++)
        {
            byte index = core.Read(address, 1)[0];
            if (index == 0xFF)
            {
                break;
            }
            int node = Offset(slots + 0x100u * index);
            if (arena.Read(node + 0x1C, 1)[0] != 3)
            {
                continue;
            }
            int model = Offset(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(node + 0xBC, 4)));
            ushort role = BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(model + 8, 2));
            if (role is not (0x0110 or 0x000F))
            {
                continue;
            }
            int skeleton = Offset(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(model + 0x30, 4)));
            Rig selected = new(arena.Read(model + 0x26, 1)[0], ReadMatrices(skeleton, bodyJoints), ReadMatrices(skeleton, gestureJoints));
            if (role == 0x0110)
            {
                Assert.Null(sacred);
                sacred = selected;
            }
            else
            {
                Assert.Null(minotaur);
                minotaur = selected;
            }
        }
        return new(Assert.IsType<Rig>(sacred), Assert.IsType<Rig>(minotaur));

        ImmutableArray<int> ReadMatrices(int skeleton, int[] joints)
        {
            var values = ImmutableArray.CreateBuilder<int>(12 * joints.Length);
            foreach (int joint in joints)
            {
                NativeBattleActionMatrix matrix = NativeBattleArenaPose.ReadMatrix(arena, skeleton + 0x20 + 48 * joint);
                foreach (short term in matrix.Rotation)
                {
                    values.Add(term);
                }
                values.Add(matrix.X);
                values.Add(matrix.Y);
                values.Add(matrix.Z);
            }
            return values.MoveToImmutable();
        }
    }

    internal static void AssertSwitched(Sample minotaurWins, Sample sacredWins)
    {
        AssertGestureChanged(minotaurWins.Sacred, sacredWins.Sacred);
        AssertGestureChanged(minotaurWins.Minotaur, sacredWins.Minotaur);

        static void AssertGestureChanged(Rig stock, Rig mirrored)
        {
            Assert.Equal(stock.Counter, mirrored.Counter);
            Assert.Equal(stock.Body.ToArray(), mirrored.Body.ToArray());
            Assert.False(stock.Hands.AsSpan().SequenceEqual(mirrored.Hands.AsSpan()));
        }
    }

    private static int Offset(uint address)
    {
        return checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
    }
}
