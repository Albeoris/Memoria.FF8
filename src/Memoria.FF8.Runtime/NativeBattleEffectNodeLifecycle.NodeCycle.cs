using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleEffectNodeLifecycle
{
    internal void AdvanceList(ref NativeCrtRandomState random)
    {
        NativeCrtRandomState current = random;
        NativeBattleEffectNodeAllocation.WalkList(() => memory, (node, kind) =>
        {
            switch (kind)
            {
                case 0:
                    byte[] emitterSource = ReadSource(unchecked((sbyte)memory.Byte(node + 0x6A)));
                    AdvanceEmitter(node, emitterSource, ref current);
                    break;
                case 1:
                    byte[] geometrySource = ReadSource(unchecked((sbyte)memory.Byte(node + 0x1D6)));
                    AdvanceGeometry(node, geometrySource);
                    break;
            }
        });
        random = current;
    }

    internal void AdvanceEmitter(int node, byte[] source, ref NativeCrtRandomState random)
    {
        if (memory.Word(node + 0x62) == 0)
        {
            InitializeEmitter(node, source, ref random);
            memory.Word(node + 0x62, unchecked((ushort)(memory.Word(node + 0x62) + 1)));
        }
        switch (memory.Byte(node + 0x68))
        {
            case 3:
                FollowParent(node, source);
                break;
            case 4:
                NativeBattleEffectAnimatedAnchor.Advance(memory, node, source.ToArray(), bank.Read);
                break;
        }
        EmitGeometry(node, source, ref random);
        memory.Word(node + 0x60, unchecked((ushort)(memory.Word(node + 0x60) + 1)));
        AdvanceEmitterLifetime(memory, node);
        inputs.EmitterAdvanced?.Invoke();
    }

    internal void EmitGeometry(int node, byte[] source, ref NativeCrtRandomState random)
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
            Spawn(node, divisor, source, ref random);
        }
        int remainder = count % divisor;
        if (remainder > 0)
        {
            Spawn(node, remainder, source, ref random);
        }
    }

    internal NativeBattleEffectNodeBank AdvanceGeometry(int node, byte[] source)
    {
        switch (unchecked((short)memory.Word(node + 0x1CC)))
        {
            case 0:
                AdvanceSpriteAnimation(memory, node, unchecked((sbyte)source[0x16]));
                bool complete = CompleteGeometry(node, source);
                if (complete)
                {
                    memory.Word(node + 0x1CC, unchecked((ushort)(memory.Word(node + 0x1CC) + 1)));
                    memory.Dword(node + 0x170, 0);
                }
                else
                {
                    AdvanceMotion(node, source);
                    AdvanceDrawable(node, source);
                    memory.Word(node + 0x1CA, unchecked((ushort)(memory.Word(node + 0x1CA) + 1)));
                }
                inputs.GeometryAdvanced?.Invoke(node, complete);
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
