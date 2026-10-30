using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceRadial634300(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleEffect218Resources resources, NativeBattleCameraProjection projection, short cameraRoll, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        uint bank = frame.State.ReadDword(0xE257E0);
        (frame, uint scratchAddress) = Allocate(frame, 72);
        bool paused = (graphicsFlags & 0x201) != 0;
        if (!paused && Signed(Read(frame, address), 12) <= 4)
        {
            frame = SpawnRadial634300(frame, bank);
        }
        (frame, uint spriteAddress) = Allocate(frame, 180);
        NativeBattleEffectArena view = frame.State.ReadView(CurrentView, 32);
        (byte[] viewBytes, bool[] viewKnown) = view.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 packed = new(viewBytes.ToImmutableArray(), viewKnown.ToImmutableArray());
        NativeBattleEffectArena scratch = NativeBattleEffectNodeContextMemory.Copy(view, 0, Context(frame, scratchAddress, 72), 8, 32);
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(0xE19CBCu)).Write(0x24, BitConverter.GetBytes((ushort)8));
        frame = PublishContext(PublishContext(frame, scratchAddress, scratch, 72), spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, packed), packed) };
        int survivors = 0;
        for (int index = 0; index < 200; index++)
        {
            uint location = checked(bank + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, location, 24);
            if ((ParticleDword218(row, 0) & 1) == 0)
            {
                continue;
            }
            (byte[] wBytes, bool[] wKnown) = row.ReadSnapshot(14, 2);
            short? w = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(ParticleWord218(row, 8), ParticleWord218(row, 10), ParticleWord218(row, 12)), w);
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
            frame = frame with { Memory = NativeBattleEffect315Gte.SetDiagonal64DE00(NativeBattleEffect315Gte.TransformAlternate4607E0(frame.Memory), unchecked((ushort)ParticleWord218(row, 6))) };
            sprite = Context(frame, spriteAddress, 180).Write(4, row.Read(4, 2));
            NativeBattleScriptedColorRegisters transformed = frame.Memory.ScriptedColorRegisters!;
            int x = transformed.Mac[0] ?? throw new InvalidDataException("Effect218 radial particle lacks its current X MAC.");
            int y = transformed.Mac[1] ?? throw new InvalidDataException("Effect218 radial particle lacks its current Y MAC.");
            int z = transformed.Mac[2] ?? throw new InvalidDataException("Effect218 radial particle lacks its current Z MAC.");
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
            scratch = Context(frame, scratchAddress, 72).Write(0x28, BitConverter.GetBytes(direction.X)).Write(0x2C, BitConverter.GetBytes(direction.Y)).Write(0x30, BitConverter.GetBytes(direction.Z));
            int offset = -(ParticleWord218(row, 6) >> 3);
            scratch = scratch.Write(0x38, BitConverter.GetBytes(unchecked(x + (unchecked(direction.X * offset) >> 12)))).Write(0x3C, BitConverter.GetBytes(unchecked(y + (unchecked(direction.Y * offset) >> 12)))).Write(0x40, BitConverter.GetBytes(unchecked(z + (unchecked(direction.Z * offset) >> 12))));
            frame = PublishContext(frame, scratchAddress, scratch, 72);
            frame = frame with { Memory = NativeBattleEffect315Gte.LoadTranslation64DD70(frame.Memory, NativeBattleEffectNodeContextMemory.Copy(scratch, 0x38, NativeBattleEffectArena.Clear(), 0, 12)) };
            NativeBattleModuleSpriteDraw drawn = NativeBattleEffect218Presentation.DrawSprite(frame.Memory, sprite, resources.Sprite, projection, cameraRoll, pilotIdentity: NativeBattleEffect218Presentation.Row(frame, bank, index, 0));
            frame = PublishContext(frame with { Memory = drawn.Memory }, spriteAddress, drawn.Temporary180, 180);
            frame = deliver(frame, drawn.Batch);
            if (!paused)
            {
                if (ParticleWord218(Context(frame, spriteAddress, 180), 0x28) < 0)
                {
                    row = row.Write(0, new byte[4]);
                }
                else
                {
                    row = ParticleWord218(row, 4, ParticleWord218(row, 4) + 1);
                    short vx = ParticleWord218(row, 16);
                    short vz = ParticleWord218(row, 20);
                    row = ParticleWord218(row, 8, ParticleWord218(row, 8) + vx);
                    row = ParticleWord218(row, 12, ParticleWord218(row, 12) + vz);
                    row = ParticleWord218(row, 16, vx - (vx >> 3));
                    row = ParticleWord218(row, 20, vz - (vz >> 3));
                    survivors++;
                }
                frame = WriteBytes(frame, location, row, 24);
            }
        }
        frame = Release(Release(frame, 180), 72);
        if (paused)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return (frame, survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect218Frame SpawnRadial634300(NativeBattleEffect218Frame frame, uint bank)
    {
        for (int birth = 0; birth < 8; birth++)
        {
            int slot = 0;
            while (slot < 200 && ParticleDword218(ReadBytes(frame, checked(bank + (uint)(slot * 24)), 4), 0) != 0)
            {
                slot++;
            }
            if (slot == 200)
            {
                break;
            }
            uint location = checked(bank + (uint)(slot * 24));
            if (frame.CapturePresentation)
            {
                NativeBattlePilotVisualLifetimes.Allocated(frame.State.ParticleGlobals, bank, slot);
            }
            NativeBattleEffectArena row = ReadBytes(frame, location, 24).Write(0, BitConverter.GetBytes(1u)).Write(4, new byte[2]);
            frame = Roll(frame, out ushort scale);
            row = ParticleWord218(row, 6, scale % 3072 + 2560);
            frame = Roll(frame, out ushort angle);
            int phase = angle & 4095;
            frame = Roll(frame, out ushort radius);
            int distance = radius % 300 + 400;
            row = ParticleWord218(row, 8, unchecked(NativeBattleCameraMath.Sine(phase) * distance) >> 12);
            frame = Roll(frame, out ushort y);
            row = ParticleWord218(row, 10, -(y % 60));
            row = ParticleWord218(row, 12, unchecked(NativeBattleCameraMath.Cosine(phase) * distance) >> 12);
            frame = Roll(frame, out ushort velocity);
            int speed = velocity % 120 + 40;
            row = ParticleWord218(row, 16, unchecked(NativeBattleCameraMath.Sine(phase) * speed) >> 12);
            row = ParticleWord218(row, 20, unchecked(NativeBattleCameraMath.Cosine(phase) * speed) >> 12);
            frame = WriteBytes(frame, location, row, 24);
        }
        return frame;
    }

    private static short ParticleWord218(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadInt16LittleEndian(row.Read(offset, 2));
    private static uint ParticleDword218(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(row.Read(offset, 4));
    private static NativeBattleEffectArena ParticleWord218(NativeBattleEffectArena row, int offset, int value) => row.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
}
