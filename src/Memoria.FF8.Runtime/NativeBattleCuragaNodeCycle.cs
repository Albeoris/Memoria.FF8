using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeCycleStep(NativeBattleCuragaFrame Frame, NativeCrtRandomState Random);

public static partial class NativeBattleCuragaNodeKinematics
{
    /// <summary>8804D0's private updates share one transient view, including all nodes appended during this walk.</summary>
    public static NativeBattleCuragaNodeCycleStep AdvanceList8804D0(NativeBattleCuragaFrame frame, uint contextTask, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel, bool birth882E20 = false)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleCuragaExecutableState bank = frame.Memory.CuragaExecutable ?? throw new NotSupportedException("Native Curaga node cycle requires its actual mutable executable bank.");
        if (birth882E20)
        {
            NativeBattleCuragaNodeEngine.Birth(memory, bank);
        }
        (bank, random) = AdvanceList(memory, bank, random, readModel);
        NativeBattleCuragaFrame result = memory.Flush();
        return new(result with { Memory = result.Memory with { CuragaExecutable = bank } }, random);
    }

    internal static (NativeBattleCuragaExecutableState Bank, NativeCrtRandomState Random) AdvanceList(NativeBattleCuragaNodeMemory memory, NativeBattleCuragaExecutableState bank, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel)
    {
        NativeBattleCuragaNodeEngine.WalkList(() => memory, (node, kind) =>
        {
            switch (kind)
            {
                case 0:
                    byte[] emitterSource = ReadSource(memory, bank, unchecked((sbyte)memory.Byte(node + 0x6A)));
                    AdvanceEmitter(memory, node, emitterSource, bank, ref random, readModel);
                    break;
                case 1:
                    byte[] geometrySource = ReadSource(memory, bank, unchecked((sbyte)memory.Byte(node + 0x1D6)));
                    bank = AdvanceGeometry(memory, node, geometrySource, bank);
                    break;
            }
        });
        return (bank, random);
    }

    internal static void AdvanceEmitter(NativeBattleCuragaNodeMemory memory, int node, byte[] source, NativeBattleCuragaExecutableState bank, ref NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel)
    {
        if (memory.Word(node + 0x62) == 0)
        {
            InitializeEmitter(memory, node, source, readModel, bank);
            memory.Word(node + 0x62, unchecked((ushort)(memory.Word(node + 0x62) + 1)));
        }
        switch (memory.Byte(node + 0x68))
        {
            case 3:
                FollowParent(memory, node, source);
                break;
            case 4:
                FollowAnimatedAnchor(memory, node, source, bank);
                break;
        }
        EmitGeometry(memory, node, source, bank, ref random);
        memory.Word(node + 0x60, unchecked((ushort)(memory.Word(node + 0x60) + 1)));
        NativeBattleCuragaNodes.AdvanceEmitterLifetime(memory, node);
    }

    internal static void EmitGeometry(NativeBattleCuragaNodeMemory memory, int node, byte[] source, NativeBattleCuragaExecutableState bank, ref NativeCrtRandomState random)
    {
        int frame = unchecked((short)memory.Word(node + 0x60));
        if (frame > 39)
        {
            return;
        }
        uint channel = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(0xC8));
        int count = bank.Read(unchecked(channel + (uint)frame), 1)[0];
        if (count == 0)
        {
            return;
        }
        int divisor = unchecked((sbyte)source[0x36]);
        if (divisor == 0)
        {
            throw new InvalidDataException("Native881820 divides by the source's signedBYTE36 zero divisor.");
        }
        for (int group = count / divisor; group > 0; group--)
        {
            Spawn(memory, node, divisor, source, bank, ref random);
        }
        int remainder = count % divisor;
        if (remainder > 0)
        {
            Spawn(memory, node, remainder, source, bank, ref random);
        }
    }

    internal static NativeBattleCuragaExecutableState AdvanceGeometry(NativeBattleCuragaNodeMemory memory, int node, byte[] source, NativeBattleCuragaExecutableState bank)
    {
        switch (unchecked((short)memory.Word(node + 0x1CC)))
        {
            case 0:
                NativeBattleCuragaSpriteAnimation.Advance(memory, node, unchecked((sbyte)source[0x16]));
                if (CompleteGeometry(memory, node, source))
                {
                    memory.Word(node + 0x1CC, unchecked((ushort)(memory.Word(node + 0x1CC) + 1)));
                    memory.Dword(node + 0x170, 0);
                }
                else
                {
                    AdvanceMotion(memory, node, source, bank);
                    bank = AdvanceDrawable(memory, node, source, bank);
                    memory.Word(node + 0x1CA, unchecked((ushort)(memory.Word(node + 0x1CA) + 1)));
                }
                break;
            case 1:
                memory.Unlink882960(node);
                memory.Byte(node + 0x1D7, 0);
                memory.ContextWord(0x16, unchecked((ushort)(memory.ContextWord(0x16) - 1)));
                break;
        }
        return bank;
    }
}
