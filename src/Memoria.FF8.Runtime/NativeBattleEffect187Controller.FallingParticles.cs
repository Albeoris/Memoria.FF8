using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceFalling647F80(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        (frame, uint spriteAddress) = Allocate(frame, 180);
        (frame, uint scratchAddress) = Allocate(frame, 104);
        NativeBattleEffectArena scratch = Context(frame, scratchAddress, 104);
        scratch = CopyBytes(PackedArena(inputs.ReadView(frame)), 0, scratch, 8, 32);
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(0xE3CFD0u)).Write(4, BitConverter.GetBytes((ushort)0)).Write(12, BitConverter.GetBytes(4096)).Write(20, BitConverter.GetBytes(4096)).Write(0x24, BitConverter.GetBytes((ushort)2));
        frame = PublishContext(PublishContext(frame, scratchAddress, scratch, 104), spriteAddress, sprite, 180);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, view), view) };
        int survivors = 0;
        for (int index = 0; index < 150; index++)
        {
            uint rowAddress = checked(frame.State.ReadDword(0xE41E50) + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, rowAddress, 24);
            if ((ParticleDword(row, 0) & 1) == 0)
            {
                continue;
            }
            frame = TransformParticlePosition(frame, row);
            int width = ParticleSigned(row, 6);
            frame = frame with { Memory = NativeBattleEffect315Gte.SetDiagonal64DE00(frame.Memory, unchecked((ushort)width)) };
            sprite = Context(frame, spriteAddress, 180).Write(0x10, BitConverter.GetBytes(unchecked(width * 2 + 0x1200)));
            frame = PublishContext(frame, spriteAddress, sprite, 180);
            frame = frame with { Memory = IrToTranslation64DE90(frame.Memory) };
            frame = DrawSprite(frame, spriteAddress, resources, inputs);
            if ((inputs.ReadFlags(frame) & 0x201) != 0)
            {
                continue;
            }
            short velocity = ParticleSigned(row, 0x12);
            row = ParticleWord(row, 10, ParticleSigned(row, 10) + velocity);
            row = ParticleWord(row, 0x12, velocity + (velocity >> 5));
            if (ParticleSigned(row, 10) < 0)
            {
                survivors++;
            }
            else
            {
                row = row.Write(0, BitConverter.GetBytes(0u));
                uint? wave = FindFreeParticle(frame, 0xE41E54);
                if (wave is uint destination)
                {
                    NativeBattleEffectArena ripple = ReadBytes(frame, destination, 24).Write(0, BitConverter.GetBytes(1u));
                    ripple = ParticleWord(ParticleWord(ripple, 4, 0), 6, width + ParticleRoll(ref frame) % 1024 + 1024);
                    ripple = ParticleWord(ParticleWord(ParticleWord(ripple, 8, ParticleSigned(row, 8)), 10, 0), 12, ParticleSigned(row, 12));
                    frame = WriteBytes(frame, destination, ripple, 24);
                }
            }
            frame = WriteBytes(frame, rowAddress, row, 24);
        }
        sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(0xE3CFF8u)).Write(0x24, BitConverter.GetBytes((ushort)0x200));
        frame = PublishContext(frame, spriteAddress, sprite, 180);
        scratch = Context(frame, scratchAddress, 104).Write(0, BitConverter.GetBytes((ushort)0x400)).Write(2, new byte[4]);
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Euler56CE30(new(0x400, 0, 0));
        scratch = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(basis, null)), 0, scratch, 8, 20);
        frame = PublishContext(frame, scratchAddress, scratch, 104);
        for (int index = 0; index < 150; index++)
        {
            uint rowAddress = checked(frame.State.ReadDword(0xE41E54) + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, rowAddress, 24);
            if ((ParticleDword(row, 0) & 1) == 0)
            {
                continue;
            }
            int scale = ParticleSigned(row, 6);
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(basis, scale, scale, scale) with { X = ParticleSigned(row, 8), Y = ParticleSigned(row, 10), Z = ParticleSigned(row, 12) };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            scratch = Context(frame, scratchAddress, 104).Write(0x48, BitConverter.GetBytes(scale)).Write(0x4C, BitConverter.GetBytes(scale)).Write(0x50, BitConverter.GetBytes(scale));
            scratch = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 0, scratch, 0x28, 32);
            frame = PublishContext(frame, scratchAddress, scratch, 104);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
            frame = PublishContext(frame, spriteAddress, ParticleWord(Context(frame, spriteAddress, 180), 4, ParticleSigned(row, 4)), 180);
            frame = DrawSprite(frame, spriteAddress, resources, inputs);
            if ((inputs.ReadFlags(frame) & 0x201) == 0)
            {
                row = ParticleWord(row, 4, ParticleSigned(row, 4) + 1);
                if (ParticleSigned(Context(frame, spriteAddress, 180), 0x28) < 0)
                {
                    row = row.Write(0, BitConverter.GetBytes(0u));
                }
                else
                {
                    survivors++;
                }
                frame = WriteBytes(frame, rowAddress, row, 24);
            }
        }
        frame = Release(Release(frame, 104), 180);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        (frame, _) = Allocate(frame, 104);
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age is >= 0 and <= 150)
        {
            int births = Math.Min(ParticleRoll(ref frame) % 6 + age / 4 + 1, 30);
            for (int birth = 0; birth < births; birth++)
            {
                uint? free = FindFreeParticle(frame, 0xE41E50);
                if (free is not uint destination)
                {
                    break;
                }
                NativeBattleEffectArena row = ReadBytes(frame, destination, 24).Write(0, BitConverter.GetBytes(1u));
                row = ParticleWord(ParticleWord(row, 4, 0), 6, ParticleRoll(ref frame) % 1152 + 768);
                row = ParticleWord(row, 8, ParticleRoll(ref frame) % 6000 - 3000);
                row = ParticleWord(row, 10, -4000 - ParticleRoll(ref frame) % 1000);
                row = ParticleWord(row, 12, ParticleRoll(ref frame) % 10000 - 5000);
                row = ParticleWord(row, 0x12, ParticleRoll(ref frame) % 550 + 180);
                frame = WriteBytes(frame, destination, row, 24);
            }
        }
        frame = Release(frame, 104);
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        return new(frame, unchecked((short)next) >= 4 && survivors == 0 ? 2 : 0);
    }

    private static NativeBattleEffect187Frame TransformParticlePosition(NativeBattleEffect187Frame frame, NativeBattleEffectArena row)
    {
        NativeBattleVector point = new(ParticleSigned(row, 8), ParticleSigned(row, 10), ParticleSigned(row, 12));
        frame = Publish(frame, NativeBattleGeometry.LoadVector(Registers(frame), 0, point, Padding(row, 14)));
        return frame with { Memory = NativeBattleEffect315Gte.TransformAlternate4607E0(frame.Memory) };
    }

    private static NativeBattleEffect187Frame DrawSprite(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleModuleSpriteDraw drawn = NativeBattleEffect187SpriteDrawing.Draw571C80(frame.Memory, Context(frame, address, 180), resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame));
        frame = PublishContext(frame with { Memory = drawn.Memory }, address, drawn.Temporary180, 180);
        return inputs.Execute(frame, new NativeBattleEffect187DeliverBatch(drawn.Batch, 0x1D8E054));
    }

    private static uint? FindFreeParticle(NativeBattleEffect187Frame frame, uint global)
    {
        uint start = frame.State.ReadDword(global);
        for (int index = 0; index < 150; index++)
        {
            uint address = checked(start + (uint)(index * 24));
            if (ParticleDword(ReadBytes(frame, address, 4), 0) == 0)
            {
                return address;
            }
        }
        return null;
    }
}
