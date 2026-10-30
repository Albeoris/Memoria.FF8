using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceRadial64A370(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        (frame, uint spriteAddress) = Allocate(frame, 180);
        (frame, uint scratchAddress) = Allocate(frame, 72);
        NativeBattleEffectArena scratch = Context(frame, scratchAddress, 72).Write(0, new byte[6]);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { Z = 40 };
        NativeBattlePackedMatrix32 view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, ViewMatrix).ReadMatrix(), local), null);
        scratch = CopyBytes(PackedArena(view), 0, scratch, 8, 32);
        frame = PublishContext(frame, scratchAddress, scratch, 72);
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(0xE3D300u)).Write(0x24, BitConverter.GetBytes((ushort)0));
        frame = PublishContext(frame, spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, view), view) };
        int survivors = 0;
        for (int index = 0; index < 150; index++)
        {
            uint rowAddress = checked(frame.State.ReadDword(0xE41E50) + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, rowAddress, 24);
            if ((ParticleDword(row, 0) & 2) == 0)
            {
                continue;
            }
            frame = TransformParticlePosition(frame, row);
            frame = frame with { Memory = NativeBattleEffect315Gte.SetDiagonal64DE00(frame.Memory, unchecked((ushort)ParticleSigned(row, 6))) };
            frame = PublishContext(frame, spriteAddress, ParticleWord(Context(frame, spriteAddress, 180), 4, ParticleSigned(row, 4)), 180);
            frame = frame with { Memory = IrToTranslation64DE90(frame.Memory) };
            frame = DrawSprite(frame, spriteAddress, resources, inputs, Visual187(frame, address, inputs, unchecked((uint)ParticleDword(Context(frame, spriteAddress, 180), 0)), rowAddress));
            if ((inputs.ReadFlags(frame) & 0x201) != 0)
            {
                continue;
            }
            row = ParticleWord(row, 4, ParticleSigned(row, 4) + 1);
            if (ParticleSigned(Context(frame, spriteAddress, 180), 0x28) < 0)
            {
                row = row.Write(0, BitConverter.GetBytes(0u));
            }
            else
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    short velocity = ParticleSigned(row, 16 + axis * 2);
                    row = ParticleWord(row, 8 + axis * 2, ParticleSigned(row, 8 + axis * 2) + velocity);
                    row = ParticleWord(row, 16 + axis * 2, velocity - (velocity >> 2));
                }
                survivors++;
            }
            frame = WriteBytes(frame, rowAddress, row, 24);
        }
        frame = Release(Release(frame, 72), 180);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        (frame, _) = Allocate(frame, 72);
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age is >= 0 and <= 3)
        {
            for (int birth = 0; birth < 10; birth++)
            {
                uint? free = FindFreeParticle(frame, 0xE41E50, inputs.CapturePresentation ? Read(frame, address) : null);
                if (free is not uint destination)
                {
                    break;
                }
                NativeBattleEffectArena row = ReadBytes(frame, destination, 24).Write(0, BitConverter.GetBytes(2u));
                row = ParticleWord(ParticleWord(row, 4, 0), 6, ParticleRoll(ref frame) % 352 + 128);
                int angle = ParticleRoll(ref frame) & 4095;
                int radius = ParticleRoll(ref frame) % 150 + 200;
                row = ParticleWord(row, 8, unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12);
                row = ParticleWord(row, 10, -(ParticleRoll(ref frame) % 100));
                row = ParticleWord(row, 12, unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12);
                int speed = ParticleRoll(ref frame) % 55 + 35;
                row = ParticleWord(row, 16, unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12);
                row = ParticleWord(row, 18, -50 - ParticleRoll(ref frame) % 125);
                row = ParticleWord(row, 20, unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12);
                frame = WriteBytes(frame, destination, row, 24);
            }
        }
        frame = Release(frame, 72);
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        return new(frame, unchecked((short)next) >= 4 && survivors == 0 ? 2 : 0);
    }
}
