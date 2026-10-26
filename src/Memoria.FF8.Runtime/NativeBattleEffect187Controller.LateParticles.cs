using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceLateParticles(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool breath = task.Callback == 0x64ACB0;
        if (!breath && task.Callback != 0x64A9D0)
        {
            throw new InvalidDataException("Effect187 late particles require their actual callback.");
        }
        uint bankGlobal = breath ? 0xE41E50u : 0xE41E54u;
        uint flag = breath ? 4u : 2u;
        (frame, uint spriteAddress) = Allocate(frame, 180);
        (frame, uint scratchAddress) = Allocate(frame, 72);
        NativeBattleEffectArena scratch = Context(frame, scratchAddress, 72).Write(0, new byte[6]);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        if (breath)
        {
            scratch = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(local, null)), 0, scratch, 8, 20);
            frame = PublishContext(frame, scratchAddress, scratch, 72);
            NativeBattleEffect187PointStep anchor = ReadOwnedBone502170(frame, resources, 0xF0, 0, ParticleSlice(scratch, 0, 8));
            frame = anchor.Frame;
            scratch = CopyBytes(anchor.Point8, 0, Context(frame, scratchAddress, 72), 0, 6);
            local = local with { X = ParticleSigned(scratch, 0), Y = ParticleSigned(scratch, 2) + 20, Z = ParticleSigned(scratch, 4) - 50 };
        }
        else
        {
            local = local with { X = 0, Y = -40, Z = 40 };
        }
        NativeBattlePackedMatrix32 view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, ViewMatrix).ReadMatrix(), local), null);
        scratch = CopyBytes(PackedArena(view), 0, scratch, 8, 32);
        frame = PublishContext(frame, scratchAddress, scratch, 72);
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(0xE3CC38u)).Write(0x24, BitConverter.GetBytes((ushort)(breath ? 4 : 0)));
        if (breath)
        {
            sprite = sprite.Write(0x1C, BitConverter.GetBytes(0x404040u));
        }
        frame = PublishContext(frame, spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, view), view) };
        int survivors = 0;
        for (int index = 0; index < 150; index++)
        {
            uint rowAddress = checked(frame.State.ReadDword(bankGlobal) + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, rowAddress, 24);
            if ((ParticleDword(row, 0) & flag) == 0)
            {
                continue;
            }
            frame = TransformParticlePosition(frame, row);
            frame = frame with { Memory = NativeBattleEffect315Gte.SetDiagonal64DE00(frame.Memory, unchecked((ushort)ParticleSigned(row, 6))) };
            frame = PublishContext(frame, spriteAddress, ParticleWord(Context(frame, spriteAddress, 180), 4, ParticleSigned(row, 4)), 180);
            frame = frame with { Memory = IrToTranslation64DE90(frame.Memory) };
            frame = DrawSprite(frame, spriteAddress, resources, inputs);
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
                if (breath)
                {
                    int width = ParticleSigned(row, 6);
                    row = ParticleWord(row, 6, width + (width >> 5));
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    if (!breath && axis == 1)
                    {
                        continue;
                    }
                    short velocity = ParticleSigned(row, 16 + axis * 2);
                    row = ParticleWord(row, 8 + axis * 2, ParticleSigned(row, 8 + axis * 2) + velocity);
                    row = ParticleWord(row, 16 + axis * 2, velocity - (velocity >> 3));
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
        (frame, uint birthScratch) = Allocate(frame, 72);
        int age = Signed(Read(frame, address), 12);
        if (age >= 0 && age <= (breath ? 7 : 1))
        {
            for (int birth = 0; birth < (breath ? 2 : 12); birth++)
            {
                uint? free = FindFreeParticle(frame, bankGlobal);
                if (free is not uint destination)
                {
                    break;
                }
                NativeBattleEffectArena row = ReadBytes(frame, destination, 24).Write(0, BitConverter.GetBytes(flag));
                row = ParticleWord(row, 4, 0);
                if (breath)
                {
                    row = ParticleWord(row, 6, (ParticleRoll(ref frame) & 127) + 512);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        row = ParticleWord(row, 8 + axis * 2, ParticleRoll(ref frame) % 10 - 5);
                    }
                    int speed = (ParticleRoll(ref frame) & 7) + 15;
                    int x = (ParticleRoll(ref frame) & 511) - 256;
                    int y = (ParticleRoll(ref frame) & 1023) - 384;
                    int z = -3584 - (ParticleRoll(ref frame) & 511);
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    NativeBattleEffectArena context = Context(frame, birthScratch, 72).Write(0x28, BitConverter.GetBytes(direction.X)).Write(0x2C, BitConverter.GetBytes(direction.Y)).Write(0x30, BitConverter.GetBytes(direction.Z));
                    frame = PublishContext(frame, birthScratch, context, 72);
                    row = ParticleWord(ParticleWord(ParticleWord(row, 16, unchecked(direction.X * speed) >> 12), 18, unchecked(direction.Y * speed) >> 12), 20, unchecked(direction.Z * speed) >> 12);
                }
                else
                {
                    row = ParticleWord(row, 6, ParticleRoll(ref frame) % 640 + 1536);
                    int angle = ParticleRoll(ref frame) & 4095;
                    int radius = ParticleRoll(ref frame) % 60 + 170;
                    row = ParticleWord(row, 8, unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12);
                    row = ParticleWord(row, 10, 0);
                    row = ParticleWord(row, 12, unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12);
                    int speed = ParticleRoll(ref frame) % 20 + 15;
                    row = ParticleWord(row, 16, unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12);
                    row = ParticleWord(row, 20, unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12);
                }
                frame = WriteBytes(frame, destination, row, 24);
            }
        }
        frame = Release(frame, 72);
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, next));
        return new(frame, unchecked((short)next) >= 4 && survivors == 0 ? 2 : 0);
    }
}
