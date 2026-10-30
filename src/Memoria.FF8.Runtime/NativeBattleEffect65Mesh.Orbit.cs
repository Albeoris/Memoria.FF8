using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Mesh
{
    public static NativeBattleEffect65Frame Draw6E48A0(NativeBattleEffect65Frame frame, NativeBattleEffectArena first8, NativeBattleEffectArena second8, uint descriptorAddress, int ratioA, int ratioB, int mode, uint source, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        inputs = inputs with { PilotIdentity = NativeBattleEffect65Presentation.Source(frame, inputs, source, unchecked((int)descriptorAddress)) };
        (frame, uint address) = Allocate(frame, 220);
        NativeBattleEffectArena temporary = Context(frame, address, 220);
        NativeBattleEffectArena descriptor = ReadSource(frame, resources, descriptorAddress, 20);
        int complementA = unchecked(4096 - ratioA);
        int complementB = unchecked(4096 - ratioB);
        int x = unchecked(Word(second8, 0) + unchecked(Word(descriptor, 0) * complementA) / 4096);
        int y = unchecked(Word(second8, 2) + unchecked(Word(descriptor, 2) * complementA) / 4096);
        int z = unchecked(Word(second8, 4) + unchecked(Word(descriptor, 4) * complementA) / 4096);
        int dx = unchecked(x - Word(first8, 0));
        int dy = unchecked(y - Word(first8, 2));
        int dz = unchecked(Word(first8, 4) - z);
        temporary = Put(temporary, 0, unchecked((uint)dx));
        temporary = Put(temporary, 4, unchecked((uint)dy));
        temporary = Put(temporary, 8, unchecked((uint)dz));
        (NativeBattleActionMatrix basis, int length) = NativeBattleFunguarLaser277.Basis50CBA0(dx, dy, dz);
        temporary = StoreMatrix(temporary, 0x10, basis, false);
        int difference = unchecked((int)Dword(descriptor, 12) - length);
        int frequency = -2048;
        NativeBattleActionMatrix first = basis;
        if (complementB < 2048)
        {
            frequency = unchecked(2 * complementB - 6144);
            int factor = unchecked(4096 - NativeBattleCameraMath.Cosine(complementB / 2));
            difference = unchecked(difference * factor) >> 12;
            length = unchecked(length * factor) / 4096;
            if (frame.State.ReadDword(ControlGlobal) == 0)
            {
                ushort angle = unchecked((ushort)(Word(descriptor, 18) + Word(descriptor, 16)));
                frame = frame with { State = frame.State.WriteWord(descriptorAddress + 18, angle) };
                descriptor = PutWord(descriptor, 18, angle);
            }
            NativeBattleActionMatrix rotation = NativeBattlePandemona291Restoration.Rotation701270(Word(descriptor, 18));
            temporary = temporary.Write(0x50, new byte[20]);
            temporary = StoreMatrix(temporary, 0x50, rotation, false);
            first = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
        }
        temporary = StoreMatrix(temporary, 0x30, first with { X = x, Y = y, Z = z });
        temporary = StoreMatrix(temporary, 0x30, NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), first with { X = x, Y = y, Z = z }));
        temporary = Put(temporary, 0x70, source);
        temporary = Put(temporary, 0x88, unchecked((uint)-2048));
        temporary = Put(temporary, 0x8C, 0);
        temporary = Put(temporary, 0x90, 0);
        frame = PublishContext(frame, address, temporary, 220);
        frame = LoadMatrix(frame, Packed(temporary, 0x30));
        frame = DrawRaw(frame, address, 220, 0x70, source, CursorGlobal, resources, inputs);
        frame = Transform4B70(frame, unchecked(difference * mode) / 4, frequency, resources);
        temporary = Context(frame, address, 220);
        temporary = Put(temporary, 0, 8192);
        temporary = Put(temporary, 4, 8192);
        temporary = Put(temporary, 8, unchecked((uint)length));
        basis = basis with { X = Word(first8, 0), Y = Word(first8, 2), Z = Word(first8, 4) };
        temporary = StoreMatrix(temporary, 0x10, basis);
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(basis, 8192, 8192, length);
        temporary = StoreMatrix(temporary, 0x10, scaled);
        temporary = StoreMatrix(temporary, 0x30, NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), scaled));
        temporary = Put(temporary, 0x70, 0x133A0FC);
        temporary = Put(temporary, 0x74, checked(frame.State.ReadDword(ArenaGlobal) + 0x6CB8));
        temporary = Put(temporary, 0x88, unchecked((uint)-1024));
        temporary = Put(temporary, 0x8C, 0x2030);
        frame = PublishContext(frame, address, temporary, 220);
        frame = LoadMatrix(frame, Packed(temporary, 0x30));
        frame = DrawRaw(frame, address, 220, 0x70, 0x133A0FC, CursorGlobal, resources, inputs);
        return Release(frame, 220);
    }

    public static NativeBattleEffect65Frame Draw6E4BD0(NativeBattleEffect65Frame frame, uint descriptorAddress, NativeBattleEffectArena center8, int radius, uint source, bool mode, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        inputs = inputs with { PilotIdentity = NativeBattleEffect65Presentation.Source(frame, inputs, source, unchecked((int)descriptorAddress)) };
        NativeBattleEffectArena descriptor = ReadSource(frame, resources, descriptorAddress, 36);
        if (frame.State.ReadDword(ControlGlobal) == 0)
        {
            if (mode)
            {
                short angle = Word(descriptor, 0);
                uint point = descriptorAddress + 28;
                frame = NativeBattleEffect65Strips.Birth6E4DA0(frame, center8, angle, 1024, radius, Word(descriptor, 4), Word(descriptor, 6), point, Dword(descriptor, 16), resources, inputs);
                frame = NativeBattleEffect65Strips.Birth6E4DA0(frame, center8, angle, 2048, unchecked(radius * 5793) / 4096, Word(descriptor, 4), Word(descriptor, 8), point, Dword(descriptor, 20), resources, inputs);
                frame = NativeBattleEffect65Strips.Birth6E4DA0(frame, center8, angle, 3072, radius, Word(descriptor, 4), Word(descriptor, 10), point, Dword(descriptor, 24), resources, inputs);
            }
            descriptor = PutWord(descriptor, 12, unchecked(Word(descriptor, 12) + Word(descriptor, 14)));
            frame = frame with { State = frame.State.WriteWord(descriptorAddress + 12, unchecked((ushort)Word(descriptor, 12))) };
        }
        (frame, uint address) = Allocate(frame, 148);
        NativeBattleEffectArena temporary = Context(frame, address, 148);
        temporary = PutWord(PutWord(PutWord(temporary, 0, 0), 2, 0), 4, 4096);
        NativeBattleActionMatrix matrix = NativeBattlePandemona291Restoration.Rotation701270(Word(descriptor, 0));
        temporary = temporary.Write(8, new byte[20]);
        temporary = StoreMatrix(temporary, 8, matrix, false);
        (int dx, int dy, int dz) = NativeBattleGfMatrices.Rotate56C3D0(matrix, new(0, 0, 4096));
        temporary = Put(temporary, 0x1C, unchecked((uint)dx));
        temporary = Put(temporary, 0x20, unchecked((uint)dy));
        temporary = Put(temporary, 0x24, unchecked((uint)dz));
        int distance = unchecked(Word(descriptor, 4) + radius);
        int x = unchecked(unchecked(dx * distance) / 4096 + Word(center8, 0));
        int y = unchecked(unchecked(dy * distance) / 4096 + Word(center8, 2));
        int z = unchecked(unchecked(dz * distance) / 4096 + Word(center8, 4));
        frame = frame with { State = frame.State.WriteWord(descriptorAddress + 28, unchecked((ushort)x)).WriteWord(descriptorAddress + 30, unchecked((ushort)y)).WriteWord(descriptorAddress + 32, unchecked((ushort)z)) };
        matrix = NativeBattlePandemona291Restoration.Rotation701270(Word(descriptor, 12)) with { X = x, Y = y, Z = z };
        temporary = temporary.Write(8, new byte[20]);
        temporary = StoreMatrix(temporary, 8, matrix);
        temporary = StoreMatrix(temporary, 8, NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), matrix));
        temporary = Put(temporary, 0x28, source);
        temporary = Put(temporary, 0x40, unchecked((uint)Word(descriptor, 2)));
        temporary = Put(temporary, 0x44, 0);
        temporary = Put(temporary, 0x48, 0);
        frame = PublishContext(frame, address, temporary, 148);
        frame = LoadMatrix(frame, Packed(temporary, 8));
        frame = DrawRaw(frame, address, 148, 0x28, source, CursorGlobal, resources, inputs);
        return Release(frame, 148);
    }

}
