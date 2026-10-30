using static Memoria.FF8.Runtime.NativeBattleEffect76Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect76Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect76Particles
{
    public static NativeBattleEffect76Step Advance(NativeBattleEffect76Frame frame, uint taskAddress, int kind, NativeBattleEffect76Resources resources, NativeBattleEffect76Inputs inputs)
    {
        if ((uint)kind > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        int temporaryLength = kind == 2 ? 152 : kind == 4 ? 104 : 120;
        int contextLength = kind == 4 ? 92 : 88;
        (frame, uint temporaryAddress) = Allocate(frame, temporaryLength);
        NativeBattleEffectArena temporary = Context(frame, temporaryAddress, temporaryLength);
        short age = unchecked((short)Read(frame, taskAddress).ReadWord(12));
        if (!Paused(frame, inputs))
        {
            (frame, temporary) = Birth(frame, temporary, age, kind, inputs.CapturePresentation);
        }
        (frame, uint contextAddress) = Allocate(frame, contextLength);
        uint[] sources = [0xDE2DE0, 0xDE2EB8, 0xDE25E8, 0xDE2EB8, 0xDE2470];
        NativeBattleEffectArena context = Context(frame, contextAddress, contextLength).Write(0, BitConverter.GetBytes(sources[kind])).Write(8, BitConverter.GetBytes(0u));
        NativeBattleActionMatrix localBase = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = Short(ReadBytes(frame, 0x24C5828, 8), 0), Y = kind is 0 or 1 ? 4000 : kind == 2 ? 3000 : 0, Z = Short(ReadBytes(frame, 0x24C5828, 8), 4) };
        temporary = PutVector(temporary, 0, 0, 0, 0);
        temporary = WriteMatrix(temporary, 8, localBase);
        NativeBattleActionMatrix basis = NativeBattlePoseMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), localBase);
        temporary = WriteMatrix(temporary, 8, basis);
        if (kind == 0)
        {
            temporary = Long(temporary, 0x70, 0);
        }
        else if (kind == 1)
        {
            temporary = Long(temporary, 0x6C, 24576);
        }
        else if (kind == 2)
        {
            temporary = Word(Word(Long(temporary, 0x90, 0), 0, 0), 4, 0);
        }
        uint bank = kind is 0 or 1 or 3 ? FirstRecords : SecondRecords;
        int capacity = kind is 0 or 1 or 3 ? 100 : 200;
        int marker = kind == 1 ? 2 : kind == 3 ? 4 : 1;
        int survivors = 0;
        for (int index = 0; index < capacity; index++)
        {
            uint address = bank + (uint)(index * 32);
            NativeBattleEffectArena row = ReadBytes(frame, address, 32);
            if ((Dword(row, 0) & marker) == 0)
            {
                continue;
            }
            bool draw = kind != 3 || Short(row, 6) == 0;
            if (draw)
            {
                (temporary, NativeBattleActionMatrix particle) = ParticleMatrix(temporary, row, kind, resources);
                NativeBattleActionMatrix composed = NativeBattlePoseMatrices.Compose56C2F0(basis, particle);
                temporary = WriteMatrix(temporary, 0x28, composed);
                frame = PublishContext(frame, temporaryAddress, temporary, temporaryLength);
                frame = Load(frame, NativeBattleEffectNodeContextMemory.Copy(temporary, 0x28, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), 0, 32));
                context = Cue(context, Short(row, 4), kind);
                frame = PublishContext(frame, contextAddress, context, contextLength);
                frame = NativeBattleEffect76Meshes.Draw(frame, contextAddress, sources[kind], kind is 0 or 1 or 4 ? CommonCursor : PrivateCursor, kind == 4, resources, inputs.CapturePresentation ? inputs with { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(76, frame.State.Storage, bank, index) with { ResourceGeneration = sources[kind] } } : inputs);
                context = Context(frame, contextAddress, contextLength);
            }
            if (!Paused(frame, inputs))
            {
                (row, bool alive) = Update(row, kind);
                frame = WriteBytes(frame, address, row, 32);
                if (alive)
                {
                    survivors++;
                }
            }
        }
        frame = PublishContext(frame, temporaryAddress, temporary, temporaryLength);
        frame = Release(Release(frame, contextLength), temporaryLength);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        int result = survivors == 0 && (kind != 3 || unchecked((short)(age + 1)) >= 32) ? 2 : 0;
        return Tick(frame, taskAddress, result);
    }

    public static (NativeBattleEffect76Frame Frame, NativeBattleEffectArena Temporary) Birth(NativeBattleEffect76Frame frame, NativeBattleEffectArena temporary, short age, int kind, bool capturePresentation = false)
    {
        int births = kind switch
        {
            0 when age <= 50 => age / 20 + 1,
            1 when age <= 50 && age % 4 == 0 => 1,
            2 when age <= 90 && age % 3 == 0 => 1,
            3 when age == 0 => 10,
            4 when age <= 42 => 10,
            _ => 0
        };
        uint bank = kind is 0 or 1 or 3 ? FirstRecords : SecondRecords;
        int capacity = kind is 0 or 1 or 3 ? 100 : 200;
        for (int birth = 0; birth < births; birth++)
        {
            uint address = 0;
            for (int index = 0; index < capacity; index++)
            {
                uint candidate = bank + (uint)(index * 32);
                if (Dword(frame, candidate) == 0)
                {
                    address = candidate;
                    break;
                }
            }
            if (address == 0)
            {
                break;
            }
            if (capturePresentation)
            {
                NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, bank, checked((int)((address - bank) / 32)));
            }
            NativeBattleEffectArena row = Marker(Word(ReadBytes(frame, address, 32), 4, 0), kind == 1 ? 2 : kind == 3 ? 4 : 1);
            if (kind == 0 || kind == 2)
            {
                if (kind == 0)
                {
                    row = Word(row, 6, Roll(ref frame) % 1664 + 512);
                }
                int x = Roll(ref frame) % (kind == 0 ? 4096 : 4608) - (kind == 0 ? 2048 : 2304);
                int y = -2560 - Roll(ref frame) % 1536;
                int z = Roll(ref frame) % (kind == 0 ? 4096 : 4608) - (kind == 0 ? 2048 : 2304);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                int vector = kind == 0 ? 0x48 : 0x68;
                temporary = Long(Long(Long(temporary, vector, direction.X), vector + 4, direction.Y), vector + 8, direction.Z);
                int radius = Roll(ref frame) % (kind == 0 ? 1000 : 500) + 2500;
                row = PutVector(row, 8, unchecked(direction.X * radius) >> 12, unchecked(direction.Y * radius) >> 12, unchecked(direction.Z * radius) >> 12);
                if (kind == 0)
                {
                    int speed = Roll(ref frame) % 200 + 20;
                    row = PutVector(row, 0x10, unchecked(direction.X * speed) >> 12, unchecked(direction.Y * speed) >> 12, unchecked(direction.Z * speed) >> 12);
                }
                else
                {
                    int size = Roll(ref frame) % 4096 + 1536;
                    row = Word(Word(row, 0x10, size), 0x14, (size & 1) << 11);
                    int growth = (Roll(ref frame) % 2048 + 4096) / 6;
                    row = Word(Word(row, 0x12, growth), 0x16, growth);
                }
                row = PutVector(row, 0x18, direction.X, direction.Y, direction.Z);
            }
            else if (kind == 1)
            {
                row = Word(Word(row, 6, Roll(ref frame) % 4096 + 1024), 0x14, 0);
                int speed = Roll(ref frame) % 4 + 2;
                row = Word(row, 0x16, (speed & 1) != 0 ? -speed : speed);
                int x = Roll(ref frame) % 4096 - 2048;
                int y = -2560 - Roll(ref frame) % 1536;
                int z = Roll(ref frame) % 4096 - 2048;
                temporary = PutVector(temporary, 0, x, y, z);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked((short)x), unchecked((short)y), unchecked((short)z));
                row = PutVector(row, 0x18, direction.X, direction.Y, direction.Z);
            }
            else if (kind == 3)
            {
                row = Word(row, 6, Roll(ref frame) % 4 + birth);
                int angle = Roll(ref frame) % 3400 + 348;
                int sine = NativeBattleCameraMath.Sine(angle);
                int cosine = NativeBattleCameraMath.Cosine(angle);
                temporary = Long(Long(Long(temporary, 0x48, sine), 0x4C, 0), 0x50, cosine);
                int radius = Roll(ref frame) % 100 + 1400;
                row = PutVector(row, 8, unchecked(sine * radius) >> 12, 0, unchecked(cosine * radius) >> 12);
                row = Word(Word(row, 0x10, Roll(ref frame) % 4096 + 2048), 0x14, 0);
                row = Word(row, 14, Roll(ref frame) % 16 + 8);
                row = Word(Word(row, 0x12, 2560), 0x16, 2560);
                int radiusDirection = Roll(ref frame) % 1024 + 256;
                int x = unchecked(sine * radiusDirection) >> 12;
                int z = unchecked(cosine * radiusDirection) >> 12;
                int y = -2560 - Roll(ref frame) % 1536;
                temporary = PutVector(temporary, 0, x, y, z);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked((short)x), unchecked((short)y), unchecked((short)z));
                row = PutVector(row, 0x18, direction.X, direction.Y, direction.Z);
            }
            else
            {
                row = Word(row, 6, Roll(ref frame) % 2048 + 256);
                int angle = Roll(ref frame) % 4096;
                int sine = NativeBattleCameraMath.Sine(angle);
                int cosine = NativeBattleCameraMath.Cosine(angle);
                temporary = Long(Long(temporary, 0x48, sine), 0x50, cosine);
                int radius = Roll(ref frame) % 1000 + 800;
                int y = -100 - Roll(ref frame) % 300;
                row = PutVector(row, 8, unchecked(sine * radius) >> 12, y, unchecked(cosine * radius) >> 12);
                int speed = Roll(ref frame) % 390 + 80;
                int vy = -4 - Roll(ref frame) % 96;
                row = PutVector(row, 0x10, unchecked(sine * speed) >> 12, vy, unchecked(cosine * speed) >> 12);
                row = Word(row, 0x18, Roll(ref frame) % 4096 - 2048);
                int spin = Roll(ref frame) % 24 + 16;
                row = Word(row, 0x1A, (spin & 1) != 0 ? -spin : spin);
                row = Word(row, 0x1C, Roll(ref frame) % 4096 - 2048);
                spin = Roll(ref frame) % 32 + 12;
                row = Word(row, 0x1E, (spin & 1) != 0 ? -spin : spin);
            }
            frame = WriteBytes(frame, address, row, 32);
        }
        return (frame, temporary);
    }

    public static (NativeBattleEffectArena Row, bool Alive) Update(NativeBattleEffectArena row, int kind)
    {
        short age = Short(row, 4);
        int end = kind is 0 or 1 ? 24 : kind == 2 ? 20 : kind == 3 ? 70 : 32;
        if (age >= end)
        {
            return (Marker(row, 0), false);
        }
        if (kind == 3 && Short(row, 6) > 0)
        {
            return (Word(row, 6, Short(row, 6) - 1), true);
        }
        age = unchecked((short)(age + 1));
        row = Word(row, 4, age);
        if (kind == 0 || kind == 4)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int velocity = Short(row, 0x10 + axis * 2);
                row = Word(row, 8 + axis * 2, Short(row, 8 + axis * 2) + velocity);
                int change = velocity >> (kind == 4 && axis == 1 ? 4 : 3);
                row = Word(row, 0x10 + axis * 2, velocity + (kind == 0 || axis == 1 ? change : -change));
            }
            if (kind == 4)
            {
                row = Word(row, 0x18, Short(row, 0x18) + Short(row, 0x1A));
                row = Word(row, 0x1A, Short(row, 0x1A) + (Short(row, 0x1A) >> 4));
                row = Word(row, 0x1C, Short(row, 0x1C) + Short(row, 0x1E));
                row = Word(row, 0x1E, Short(row, 0x1E) + (Short(row, 0x1E) >> 4));
            }
        }
        else if (kind == 1)
        {
            row = Word(row, 0x14, Short(row, 0x14) + Short(row, 0x16));
        }
        else if (kind == 2)
        {
            int velocity = Short(row, 0x16);
            row = Word(Word(row, 0x12, Short(row, 0x12) + velocity), 0x16, velocity - velocity / 8);
        }
        else
        {
            if (age < 8)
            {
                int velocity = Short(row, 0x16);
                row = Word(Word(row, 0x12, Short(row, 0x12) + velocity), 0x16, velocity - velocity / 6);
            }
            if (age >= 16)
            {
                row = Word(row, 0x14, Short(row, 0x14) + Short(row, 14));
            }
        }
        return (row, true);
    }

    public static NativeBattleEffectArena Cue(NativeBattleEffectArena context, short age, int kind, bool capturePresentation = false)
    {
        bool fading;
        int cue;
        if (kind == 1)
        {
            fading = age < 8 || age >= 16;
            cue = age < 8 ? (8 - age) << 9 : (age - 16) << 9;
        }
        else if (kind == 4)
        {
            fading = age < 4 || age >= 24;
            cue = age < 4 ? (4 - age) << 10 : (age - 24) << 9;
        }
        else
        {
            int start = kind == 2 ? 12 : kind == 3 ? 54 : 16;
            fading = age >= start;
            cue = (age - start) << (kind == 3 ? 8 : 9);
        }
        context = Long(context, 0x1C, fading ? 0xF3 : 0x33);
        return fading ? Long(context, 12, cue) : context;
    }

    private static (NativeBattleEffectArena Temporary, NativeBattleActionMatrix Matrix) ParticleMatrix(NativeBattleEffectArena temporary, NativeBattleEffectArena row, int kind, NativeBattleEffect76Resources resources)
    {
        NativeBattleActionMatrix matrix;
        int sx;
        int sy;
        int sz;
        if (kind == 4)
        {
            temporary = PutVector(temporary, 0, Short(row, 0x18), Short(row, 0x1C), Short(row, 0x1C));
            matrix = NativeBattleGfMatrices.Euler56CE30(new(Short(row, 0x18), Short(row, 0x1C), Short(row, 0x1C)));
            sx = sy = sz = Short(row, 6);
            temporary = Long(Long(Long(temporary, 0x48, sx), 0x4C, sy), 0x50, sz);
        }
        else
        {
            int direction = kind == 2 ? 0x68 : 0x48;
            int axis = kind == 2 ? 0x78 : 0x58;
            temporary = Long(Long(Long(temporary, direction, Short(row, 0x18)), direction + 4, Short(row, 0x1A)), direction + 8, Short(row, 0x1C));
            byte[] source = resources.ReadStatic(0xDE4150, 12);
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(BitConverter.ToInt32(source, 0), BitConverter.ToInt32(source, 4), BitConverter.ToInt32(source, 8), Short(row, 0x18), Short(row, 0x1A), Short(row, 0x1C));
            temporary = Long(Long(Long(temporary, axis, alignment.Axis.X), axis + 4, alignment.Axis.Y), axis + 8, alignment.Axis.Z);
            int angle = alignment.Angle + (kind is 1 or 3 ? Short(row, 0x14) : 0);
            NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(angle, alignment.Axis), 0, 0, 0, 4096);
            if (kind == 2)
            {
                temporary = Word(temporary, 2, Short(row, 0x14));
                NativeBattleActionMatrix spin = NativeBattleGfMatrices.Euler56CE30(new(0, Short(row, 0x14), 0));
                temporary = WriteRotation(temporary, 0x28, spin, true);
                temporary = WriteRotation(temporary, 0x48, rotation, false);
                matrix = NativeBattleGfMatrices.Multiply56C090(rotation, spin);
                sx = Short(row, 0x10);
                sy = Short(row, 0x12);
                sz = 0;
                temporary = Long(Long(temporary, 0x88, sx), 0x8C, sy);
            }
            else
            {
                matrix = rotation;
                sx = kind == 0 ? Short(row, 6) >> 1 : kind == 1 ? Short(row, 6) : Short(row, 0x10);
                sy = kind == 0 ? Short(row, 6) : kind == 1 ? 24576 : Short(row, 0x12);
                sz = kind == 0 ? 0 : sx;
                temporary = Long(Long(Long(temporary, 0x68, sx), 0x6C, sy), 0x70, sz);
            }
        }
        temporary = WriteRotation(temporary, 0x28, matrix, kind == 4 || kind == 2);
        matrix = matrix with { X = kind == 1 ? 0 : Short(row, 8), Y = kind == 1 ? 0 : Short(row, 10), Z = kind == 1 ? 0 : Short(row, 12) };
        temporary = Long(Long(Long(temporary, 0x3C, matrix.X), 0x40, matrix.Y), 0x44, matrix.Z);
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, sx, sy, sz);
        temporary = WriteRotation(temporary, 0x28, matrix, false);
        return (temporary, matrix);
    }
}
