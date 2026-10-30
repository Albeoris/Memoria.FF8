using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceDebrisSprites636CA0(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleEffect218Resources resources, NativeBattlePackedMatrix32 currentView, NativeBattleCameraProjection projection, short cameraRoll, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        uint bank = frame.State.ReadDword(0xE257E0);
        (frame, uint scratchAddress) = Allocate(frame, 72);
        bool paused = (graphicsFlags & 0x201) != 0;
        if (!paused && Signed(Read(frame, address), 12) <= 18)
        {
            frame = SpawnDebrisSprites636CE1(frame, bank);
        }
        (frame, uint spriteAddress) = Allocate(frame, 180);
        NativeBattleEffectArena scratch = NativeBattleEffectNodeContextMemory.Copy(PackedArena(currentView), 0, Context(frame, scratchAddress, 72), 8, 32);
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(0xE19E68u)).Write(0x1C, BitConverter.GetBytes(0x00404040u)).Write(0x24, BitConverter.GetBytes((ushort)12));
        frame = PublishContext(PublishContext(frame, scratchAddress, scratch, 72), spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, currentView), currentView) };
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
            sprite = Context(frame, spriteAddress, 180).Write(4, BitConverter.GetBytes(unchecked((ushort)((ParticleWord218(row, 4) << 1) + 1))));
            frame = frame with { Memory = NativeBattleEffect315Gte.MacToTranslation64DE40(frame.Memory) };
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

    public static NativeBattleEffect218Frame SpawnDebrisSprites636CE1(NativeBattleEffect218Frame frame, uint bank)
    {
        for (int selected = 0; selected < 24; selected++)
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
            row = ParticleWord218(row, 6, scale % 2560 + 1536);
            row = NativeBattleEffectNodeContextMemory.Copy(ReadBytes(frame, checked(0x24F3378u + (uint)(selected * 16)), 8), 0, row, 8, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Roll(frame, out ushort jitter);
                row = ParticleWord218(row, 8 + axis * 2, ParticleWord218(row, 8 + axis * 2) + jitter % 1000 - 500);
            }
            frame = WriteBytes(frame, location, row, 24);
        }
        return frame;
    }
}
