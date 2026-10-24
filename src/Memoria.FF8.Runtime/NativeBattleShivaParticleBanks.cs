using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleShivaParticleBanks
{
    public static NativeBattleShivaEarlyTaskStep Advance5C42A0(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix entryWorld, NativeBattleModuleResource sprite, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleModuleDraw, NativeBattleShivaEarlyTaskFrame> draw571C80)
    {
        return Advance(frame, slot, graphicsFlags, entryWorld, sprite, draw571C80, false);
    }

    public static NativeBattleShivaEarlyTaskStep Advance5C4550(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix entryWorld, NativeBattleModuleResource sprite, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleModuleDraw, NativeBattleShivaEarlyTaskFrame> draw571C80)
    {
        return Advance(frame, slot, graphicsFlags, entryWorld, sprite, draw571C80, true);
    }

    private static NativeBattleShivaEarlyTaskStep Advance(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix entryWorld, NativeBattleModuleResource sprite, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleModuleDraw, NativeBattleShivaEarlyTaskFrame> draw571C80, bool growing)
    {
        frame.Pool.Validate();
        uint callback = growing ? 0x5C4550u : 0x5C42A0u;
        if (frame.Pool.DescriptorAddress != 0x22BC198 || frame.Pool.BankAddress != 0x22BC1A8 || frame.Pool.Stride != 0x24 || frame.Pool.Slots.Length != 100 || (uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot].Callback != callback || (frame.Pool.Slots[slot].Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Shiva particle callback requires its original allocated child task.");
        }
        using NativeBattleEffectArenaBatch arena = new(frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva particles require their retained physical arena."));
        bool paused = (graphicsFlags & 0x201) != 0;
        int bank = growing ? 0x2C000 : 0x2D000;
        uint flag = growing ? 0x200u : 0x100u;
        int alive = 0;
        for (int index = 0; index < (growing ? 150 : 100); index++)
        {
            int address = bank + index * 24;
            if ((Dword(address) & flag) == 0)
            {
                continue;
            }
            ArgumentNullException.ThrowIfNull(draw571C80);
            if (sprite.Address != 0xD20AD0)
            {
                throw new InvalidDataException("Native Shiva particles require the originalD20AD0 sprite.");
            }
            ushort phase = Uword(address + 4);
            NativeBattleModuleSpriteFrame selected = sprite.ProjectedFrame571C80(unchecked((short)phase));
            short size = Word(address + 6);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = sprite, SpriteFrame = selected, Phase = phase, Anchor = new(Word(address + 8), Word(address + 10), Word(address + 12), 0), Scale = size, DepthOffset = -(size >> 4), SpriteFlags = 8 };
            frame = draw571C80(frame with { Memory = frame.Memory with { Arena = arena.Publish() } }, draw);
            arena.Replace(frame.Memory.Arena ?? throw new InvalidDataException("Native Shiva sprite drawing lost its current arena."));
            if (paused)
            {
                continue;
            }
            PutWord(address + 4, phase + 1);
            if (selected.NextOffset < 0)
            {
                PutDword(address, 0);
                continue;
            }
            if (growing)
            {
                int scale = Word(address + 6);
                PutWord(address + 6, scale + (scale >> 6));
            }
            int vx = Word(address + 16);
            int vz = Word(address + 20);
            PutWord(address + 8, Word(address + 8) + vx);
            PutWord(address + 12, Word(address + 12) + vz);
            PutWord(address + 16, vx - (vx >> (growing ? 3 : 2)));
            PutWord(address + 20, vz - (vz >> (growing ? 3 : 2)));
            alive++;
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena.Publish() } };
        if (paused)
        {
            return new(frame, 0);
        }
        short counter = unchecked((short)frame.Pool.Slots[slot].ReadWord(0x0C));
        if (counter >= 0 && counter <= (growing ? 30 : 8))
        {
            for (int emitted = 0; emitted < (growing ? 6 : 12); emitted++)
            {
                int free = 0;
                while (free < 150 && Dword(bank + free * 24) != 0)
                {
                    free++;
                }
                if (free == 150)
                {
                    break;
                }
                int address = bank + free * 24;
                PutDword(address, flag);
                PutWord(address + 4, 0);
                int angle;
                int speed;
                if (growing)
                {
                    PutWord(address + 6, Draw() % 1152 + 896);
                    angle = Draw() % 4096;
                    int radius = Draw() % 450 + 400;
                    PutWord(address + 8, unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12);
                    PutWord(address + 10, -(Draw() % 200));
                    PutWord(address + 12, unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12);
                    RotatePosition(address);
                    TranslatePosition(address);
                    speed = Draw() % 70 + 30;
                }
                else
                {
                    PutWord(address + 6, Draw() % 1536 + 2048);
                    PutWord(address + 8, Draw() % 400 - 200);
                    PutWord(address + 10, -(Draw() % 100));
                    PutWord(address + 12, Draw() % 400 - 200);
                    //The original producer rotates the corresponding first-bank position before translating the second-bank particle.
                    RotatePosition(0x2C000 + free * 24);
                    TranslatePosition(address);
                    angle = Draw() % 4096;
                    speed = Draw() % 350 + 270;
                }
                PutWord(address + 16, unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12);
                PutWord(address + 20, unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12);
            }
        }
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot];
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        frame = frame with { Memory = frame.Memory with { Arena = arena.Publish() }, Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(slot, task) } };
        return new(frame, unchecked((short)task.ReadWord(0x0C)) >= 4 && alive == 0 ? 2 : 0);

        ushort Uword(int address) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(address, 2));
        short Word(int address) => unchecked((short)Uword(address));
        uint Dword(int address) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(address, 4));

        void PutWord(int address, int value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
            arena.Write(address, bytes);
        }

        void PutDword(int address, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            arena.Write(address, bytes);
        }

        ushort Draw()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }

        void RotatePosition(int address)
        {
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(entryWorld, new(Word(address + 8), Word(address + 10), Word(address + 12)));
            PutWord(address + 8, rotated.X);
            PutWord(address + 10, rotated.Y);
            PutWord(address + 12, rotated.Z);
        }

        void TranslatePosition(int address)
        {
            PutWord(address + 8, Word(address + 8) + entryWorld.X);
            PutWord(address + 10, Word(address + 10) + entryWorld.Y);
            PutWord(address + 12, Word(address + 12) + entryWorld.Z);
        }
    }
}
