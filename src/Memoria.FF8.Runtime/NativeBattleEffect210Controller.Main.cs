using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect210Controller
{
    public static NativeBattleEffect210Step AdvanceMain6F7310(NativeBattleEffect210Frame frame, uint address, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        int brightness = age < 12 ? age * 2300 / 12 : age >= 231 ? (243 - age) * 2300 / 12 : 2300;
        frame = frame with { State = frame.State.WriteDword(BrightnessGlobal, unchecked((uint)brightness)) };
        int caster = Actor(frame.State.ReadDword(CasterGlobal));
        if (age >= 55 && age < 62)
        {
            if (age == 55)
            {
                frame = NativeBattleEffect210Lower.InitializeEcho6A1230(frame, address + 0x60);
                frame = InitializeCommonPool(frame, EchoDescriptor);
                if (!Frozen(frame))
                {
                    frame = NativeBattleEffect210Lower.InitializeRibbon82A0(frame, resources, inputs);
                }
            }
            frame = inputs.Execute(frame, new NativeBattleEffect210DrawEcho7EE0(caster + 1, address + 0x60));
            if (age < 60 && !Frozen(frame))
            {
                frame = inputs.Execute(frame, new NativeBattleEffect210CaptureEcho8200(caster + 1, address + 0x60));
            }
        }
        if (age >= 64 && age < 79)
        {
            if (age == 64)
            {
                frame = NativeBattleEffect210Programme.Initialize(frame, address + 0x168, 0x146D8A4, 0xE0, resources);
            }
            NativeBattleEffect210PointStep anchor = inputs.Anchor571400(frame, caster, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = anchor.Frame;
            NativeBattleVector point = Vector(anchor.Output8);
            frame = RunProgramme(frame, address, 0xE0, new(point.X, 0, point.Z), SelectorsAddress, 0, null, null, resources, inputs);
        }
        if (age >= 65 && age < 73)
        {
            frame = DrawEarlyMesh(frame, caster, age - 65, resources, inputs);
        }
        if (age >= 72 && age < 100)
        {
            if (age == 72)
            {
                frame = NativeBattleEffect210Lower.InitializeEcho6A1230(frame, address + 0x60);
            }
            frame = inputs.Execute(frame, new NativeBattleEffect210DrawEcho7EE0(caster + 1, address + 0x60));
            if (age < 98 && !Frozen(frame))
            {
                frame = inputs.Execute(frame, new NativeBattleEffect210CaptureEcho8200(caster + 1, address + 0x60));
            }
        }
        if (age >= 85 && age < 115)
        {
            int phase = age - 85;
            if (phase == 0)
            {
                NativeBattleGfAnimationSource source = resources.Act(0x1462010);
                uint geometry = source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(24));
                NativeBattleDuel216Extents bounds = NativeBattleDuel216Camera.Extents69A700(resources.ReadGeometry(geometry).Bytes);
                NativeBattleEffectArena values = PutPoint(PutPoint(NativeBattleEffectArena.Clear().MarkUnknown(0, 12), 0, bounds.Minimum), 6, bounds.Maximum);
                frame = inputs.Execute(frame, new NativeBattleEffect210Capture9440(address + 0x58, values));
            }
            int scale = phase >= 22 ? (phase - 22) * 4096 / 8 : 0;
            NativeBattleEffectArena center = ReadBytes(frame, address + 0x58, 8);
            frame = NativeBattleEffect210Lower.Draw8BF0(frame, center, 2048, 0, phase * 512, phase * 32, scale, resources, inputs);
            if (phase >= 11)
            {
                frame = NativeBattleEffect210Lower.Draw8BF0(frame, center, -1024, 512, (phase - 11) * 512, phase * 32, scale, resources, inputs);
            }
        }
        if (age >= 115 && age < 195)
        {
            if (age == 115)
            {
                frame = InitializeTargetMotion(frame, address, inputs);
                frame = NativeBattleEffect210Programme.Initialize(frame, address + 0x168, 0x1462010, 0x244, resources);
                frame = InitializeCommonPool(frame, SpriteDescriptor);
                frame = InitializeCommonPool(frame, MeshDescriptor);
                frame = InitializeCommonPool(frame, ActDescriptor);
            }
            if (age < 119)
            {
                frame = BirthRingParticles(frame, address, resources, inputs);
            }
            frame = MoveTargets(frame, address, age, inputs);
            int phase = age - 115;
            frame = RunProgramme(frame, address, 0x244, Center(frame, address), MainSelectorsAddress, -128, unchecked((short)(phase << 4)), (uint)(phase - 26) < 100 ? unchecked((short)((phase - 26) * (phase - 26) >> 4)) : (short)0, resources, inputs);
        }
        if (age >= 195 && age < 235)
        {
            if (age == 195)
            {
                frame = InitializeCommonPool(frame, MeshDescriptor);
                frame = inputs.Execute(frame, new NativeBattleEffect210StageFlags(0xFD, 0));
                frame = NativeBattleEffect210Programme.Initialize(frame, address + 0x168, 0x146A668, 0x258, resources);
                frame = ModelFlags(frame, caster, 0xFFFF, 4, inputs, true);
                frame = ModelFlags(frame, caster + 1, 0xFFFF, 4, inputs, true);
            }
            frame = MoveTargets(frame, address, age, inputs);
            if (!Frozen(frame))
            {
                frame = BirthMeshParticles(frame, address, 2, 10, true);
            }
            frame = RunProgramme(frame, address, 0x258, Center(frame, address), SelectorsAddress, 256, null, null, resources, inputs);
        }
        if (age == 235)
        {
            for (int index = 0; index < Read(frame, address).ReadWord(14); index++)
            {
                uint record = address + 16u + (uint)(index * 24);
                int actor = Actor(Dword(ReadBytes(frame, record, 4), 0));
                frame = ModelFlags(frame, actor, 0xFFF3, 0, inputs);
                frame = WriteModel(frame, actor, 28, ReadBytes(frame, record + 4, 8), 8, inputs);
                short y = unchecked((short)(Word(inputs.ReadModelArena9C(frame, actor), 30) - 4096));
                frame = WriteModel(frame, actor, 30, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(y)), 2, inputs);
            }
            frame = ModelFlags(frame, caster, 0xFFFB, 0, inputs);
            frame = ModelFlags(frame, caster + 1, 0xFFFB, 0, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect210StageFlags(0xFF, 2));
            frame = DetachCommonPool(frame, MeshDescriptor, true);
        }
        else if (age > 235 && age <= 243 && !Frozen(frame))
        {
            frame = FinishTargets(frame, address, age == 243, resources, inputs);
            if (age == 241)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect210Sound501330(0x145D4E8, 0, 128));
            }
            if (age == 243)
            {
                NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect210 requires its current contact header.");
                NativeBattleActionHeaderSlot header = inputs.ReadPackets(frame).Headers[parameters.HeaderIndex7C];
                int count = header.ReadByte(16);
                frame = inputs.Execute(frame, new NativeBattleEffect210AllContacts506BA0(header.FirstContactIndex ?? (count == 0 ? 0 : throw new NotSupportedException("Effect210 requires its current contact pointer.")), count));
            }
        }
        if (age >= 227 && age < 243)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect210DrawBatch(NativeBattleModuleRenderCommands.Emit700150(age - 227, 240, 240, 240, 6, 4, 6), CursorGlobal));
        }
        if (age == 33)
        {
            frame = DetachCommonPool(frame, EarlyDescriptor, true);
        }
        else if (age == 35)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect210IoQueue5341D0(704, frame.State.ReadDword(ArenaGlobal) + 0xB1B8, 0));
        }
        else if (age == 54)
        {
            NativeBattleEffect210IoStep poll = inputs.Poll534270(frame);
            frame = poll.Frame;
            if (poll.Busy)
            {
                return new(frame, 0);
            }
        }
        if (age == 235)
        {
            foreach ((short file, byte type) in new (short, byte)[] { (360, 1), (361, 1), (362, 0) })
            {
                frame = inputs.Execute(frame, new NativeBattleEffect210IoQueue5341D0(file, frame.State.ReadDword(ArenaGlobal) + 0x1F08, type));
            }
        }
        frame = inputs.Execute(frame, new NativeBattleEffect210IoAdvance534210());
        uint sound = age switch { 0 => 0x145D4DCu, 55 => 0x145D4E0u, 115 => 0x145D4E4u, _ => 0u };
        if (sound != 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect210Sound501330(sound, 0, 128));
        }
        if (age >= 243)
        {
            NativeBattleEffect210IoCompletionStep completion = inputs.Select534300(frame, 2);
            frame = completion.Frame;
            if (completion.Complete)
            {
                return new(frame, 2);
            }
            return new(Write(frame, address, Read(frame, address).WriteWord(12, 307)), 0);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static bool Frozen(NativeBattleEffect210Frame frame) => frame.State.ReadDword(FrozenGlobal) != 0;
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleVector Center(NativeBattleEffect210Frame frame, uint main)
    {
        NativeBattleVector value = Vector(ReadBytes(frame, main + 0x58, 6));
        return new(value.X, 0, value.Z);
    }
    private static NativeBattleEffect210Frame WriteModel(NativeBattleEffect210Frame frame, int actor, int offset, NativeBattleEffectArena bytes, int count, NativeBattleEffect210Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect210WriteModelBytes(actor, offset, bytes, count));
    private static NativeBattleEffect210Frame ModelFlags(NativeBattleEffect210Frame frame, int actor, ushort andMask, ushort orMask, NativeBattleEffect210Inputs inputs, bool byteOnly = false)
    {
        ushort flags = byteOnly ? inputs.ReadModelArena9C(frame, actor).Read(0, 1)[0] : unchecked((ushort)Word(inputs.ReadModelArena9C(frame, actor), 0));
        ushort value = unchecked((ushort)((flags & andMask) | orMask));
        return WriteModel(frame, actor, 0, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), byteOnly ? 1 : 2, inputs);
    }
}
