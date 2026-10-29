using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlaster186Controller
{
    public static (NativeBattleBlaster186Frame Frame, int Result) AdvanceSlots(NativeBattleBlaster186Frame frame, uint address, NativeBattleBlaster186Resources resources, NativeBattleBlaster186Inputs inputs)
    {
        uint callback = Read(frame, address).Callback;
        if (callback is not (0x5C0410 or 0x5C0900))
        {
            throw new NotSupportedException("Blaster particle callback is unsupported.");
        }
        bool local = callback == 0x5C0900;
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        int survivors = 0;
        (frame, uint temporary) = Allocate(frame, 180);
        uint scratch = 0;
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(local ? 0xD1E128u : 0xD1DF7Cu)).Write(0x24, new byte[2]), 180);
        if (local)
        {
            (frame, scratch) = Allocate(frame, 72);
            NativeBattleActionMatrix basis = Matrix186(frame, 0x22BBA70).ReadMatrix() with { X = Word186(frame, 0x22BC0F0), Y = Word186(frame, 0x22BC0F2), Z = Word186(frame, 0x22BC0F4) };
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), basis), null);
            frame = PublishContext(frame, scratch, NativeBattleEffectNodeContextMemory.Copy(PackedArena(packed), 0, Context(frame, scratch, 72), 8, 32), 72);
            frame = frame with { Memory = NativeBattleLightBackTransform.LoadBone(frame.Memory, packed) };
        }
        for (int index = 0; index < 100; index++)
        {
            uint row = 0x22B7638u + (uint)(index * 24);
            if ((frame.State.Read(row, 1)[0] & (local ? 2 : 1)) == 0)
            {
                continue;
            }
            short size = Word186(frame, row + 6);
            frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(frame.State.ReadWord(row + 4))), 180);
            NativeBattleEffectArena point = frame.State.ReadView(row + 8, 8);
            if (local)
            {
                (byte[] wBytes, bool[] wKnown) = point.ReadSnapshot(6, 2);
                NativeBattleVector vector = new(Word186(frame, row + 8), Word186(frame, row + 10), Word186(frame, row + 12));
                NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Blaster particles require current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Blaster particles require current arithmetic registers.")), 0, vector, wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null);
                frame = frame with { Memory = NativeBattleLightBackTransform.Transform(frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, 0) };
                NativeBattleActionMatrix scale = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleCuragaMatrices.Identity8DD770(), size, size, size);
                frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters!, scale, true, false, 0) } };
                int[] mac = frame.Memory.ScriptedColorRegisters!.Mac.Select(value => value ?? throw new NotSupportedException("Blaster particles require their actual transformed full MAC values.")).ToArray();
                var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(mac[0], mac[1], mac[2]);
                int[] direction = [normalized.X, normalized.Y, normalized.Z];
                NativeBattleEffectArena context = Context(frame, scratch, 72);
                for (int axis = 0; axis < 3; axis++)
                {
                    context = context.Write(0x28 + axis * 4, BitConverter.GetBytes(direction[axis]));
                    mac[axis] = unchecked(mac[axis] + (unchecked(direction[axis] * -(size >> 3)) >> 12));
                    context = context.Write(0x38 + axis * 4, BitConverter.GetBytes(mac[axis]));
                }
                frame = PublishContext(frame, scratch, context, 72);
                frame = frame with { Memory = frame.Memory with { GeometryRegisters = frame.Memory.GeometryRegisters! with { Translation = mac.Select(value => (int?)value).ToImmutableArray() } } };
                frame = inputs.Execute(frame, new NativeBattleBlaster186DrawCommonSprite180(temporary, 0xD1E128) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(186, Read(frame, address), 100 + index) with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(186, frame.State.Globals, row, 0).Generation } });
            }
            else
            {
                frame = SetupSprite186(frame, point, size, unchecked((short)-(size >> 4)), inputs);
                frame = inputs.Execute(frame, new NativeBattleBlaster186DrawSprite180(temporary, 0xD1DF7C) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(186, Read(frame, address), index) with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(186, frame.State.Globals, row, 0).Generation } });
            }
            if (paused)
            {
                continue;
            }
            frame = frame with { State = frame.State.WriteWord(row + 4, unchecked((ushort)(frame.State.ReadWord(row + 4) + 1))) };
            if (BinaryPrimitives.ReadInt16LittleEndian(Context(frame, temporary, 180).Read(0x28, 2)) < 0)
            {
                frame = frame with { State = frame.State.WriteDword(row, 0) };
                continue;
            }
            if (local)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    uint velocity = row + 16 + (uint)(axis * 2);
                    short speed = Word186(frame, velocity);
                    frame = frame with { State = frame.State.WriteWord(row + 8 + (uint)(axis * 2), unchecked((ushort)(Word186(frame, row + 8 + (uint)(axis * 2)) + speed))).WriteWord(velocity, unchecked((ushort)(speed - (speed >> 2)))) };
                }
            }
            survivors++;
        }
        if (local)
        {
            frame = Release(frame, 72);
        }
        frame = Release(frame, 180);
        if (paused)
        {
            return (frame, 0);
        }
        if (local)
        {
            (frame, scratch) = Allocate(frame, 72);
        }
        short age = Signed(Read(frame, address), 12);
        if (age >= 0 && age <= (local ? 14 : 16))
        {
            for (int birth = 0; birth < (local ? 4 : 2); birth++)
            {
                uint row = FreeParticle186(frame);
                if (row == 0)
                {
                    break;
                }
                frame = frame with { State = frame.State.WriteDword(row, local ? 2u : 1u).WriteWord(row + 4, 0) };
                frame = DrawRandom(frame, out ushort sizeDraw);
                if (local)
                {
                    frame = frame with { State = frame.State.WriteWord(row + 6, (ushort)((sizeDraw & 2047) + 2304)) };
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = DrawRandom(frame, out ushort position);
                        frame = frame with { State = frame.State.WriteWord(row + 8 + (uint)(axis * 2), unchecked((ushort)(axis == 2 ? -(position % 100) : position % 400 - 200))) };
                    }
                    frame = DrawRandom(frame, out ushort x);
                    frame = DrawRandom(frame, out ushort y);
                    frame = DrawRandom(frame, out ushort z);
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50((x & 4095) - 2048, (y & 4095) - 2048, -(z % 1280));
                    NativeBattleEffectArena context = Context(frame, scratch, 72).Write(0x28, BitConverter.GetBytes(direction.X)).Write(0x2C, BitConverter.GetBytes(direction.Y)).Write(0x30, BitConverter.GetBytes(direction.Z));
                    frame = PublishContext(frame, scratch, context, 72);
                    frame = DrawRandom(frame, out ushort velocity);
                    int speed = velocity % 260 + 200;
                    frame = frame with { State = frame.State.WriteWord(row + 16, unchecked((ushort)(unchecked(direction.X * speed) >> 12))).WriteWord(row + 18, unchecked((ushort)(unchecked(direction.Y * speed) >> 12))).WriteWord(row + 20, unchecked((ushort)(unchecked(direction.Z * speed) >> 12))) };
                }
                else
                {
                    frame = frame with { State = frame.State.WriteWord(row + 6, (ushort)(birth == 0 ? (sizeDraw & 511) + 1280 : sizeDraw % 1280 + 1792)).WriteView(row + 8, frame.State.ReadView(birth == 0 ? 0x22BC108u : 0x22BC0F0u, 8), 8) };
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = DrawRandom(frame, out ushort jitter);
                        uint at = row + 8 + (uint)(axis * 2);
                        frame = frame with { State = frame.State.WriteWord(at, unchecked((ushort)(Word186(frame, at) + (birth == 0 ? jitter % 320 - 160 : jitter % 520 - 260)))) };
                    }
                }
                NativeBattlePilotVisualLifetimes.Allocated(frame.State.Globals, row, 0);
            }
        }
        if (local)
        {
            frame = Release(frame, 72);
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 4 && survivors == 0 ? 2 : 0);
    }

    private static uint FreeParticle186(NativeBattleBlaster186Frame frame)
    {
        for (int index = 0; index < 100; index++)
        {
            uint row = 0x22B7638u + (uint)(index * 24);
            if (frame.State.ReadDword(row) == 0)
            {
                return row;
            }
        }
        return 0;
    }
}
