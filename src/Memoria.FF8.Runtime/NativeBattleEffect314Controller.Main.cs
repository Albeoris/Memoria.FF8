using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static NativeBattleEffect314Step AdvanceMain662F40(NativeBattleEffect314Frame frame, uint main, NativeBattleEffect314Resources resources, NativeBattleEffect314Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, main);
        short age = Signed(task, 0xC);
        uint model = frame.State.ReadDword(CasterGlobal);
        if (age >= 0xE2)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(model, 2, 0xFFF7, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(model + 156, 2, 0xFFF7, 0));
            frame = WriteModelDword(frame, inputs, model, 0x2C, task.ReadDword(0x14));
            frame = WriteModelDword(frame, inputs, model + 156, 0x2C, task.ReadDword(0x18));
            frame = inputs.Execute(frame, new NativeBattleEffect314ClearGeometryMarkers6E5D20(model));
            frame = inputs.Execute(frame, new NativeBattleEffect314ClearGeometryMarkers6E5D20(model + 156));
            for (int physical = 0; physical < 3; physical++)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(ModelAddress(physical), 2, 0xFFFB, 0));
            }
            return new(frame, 2);
        }
        int amplitude = age < 8 ? age * 125 : age > 218 ? (226 - age) * 125 : 1000;
        frame = frame with { State = frame.State.WriteDword(AmplitudeGlobal, unchecked((uint)amplitude)) };
        if ((uint)(age - 16) < 50)
        {
            frame = NativeBattleEffect314Body.Draw663590(frame, inputs, model, 512 - 84 * (age - 16), 13, 0x314);
            frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(model, 1, 0xFF, 4));
        }
        if ((uint)(age - 81) < 16)
        {
            int secondWeight = (age - 81) << 8;
            for (int actor = 0; actor < 2; actor++)
            {
                uint owner = model + (uint)(actor * 156);
                frame = inputs.Execute(frame, new NativeBattleEffect314Blend56CC00(main + 0x14u + (uint)(actor * 4), ColorGlobal, 4096 - secondWeight, secondWeight, owner + 0x2C));
                frame = inputs.Execute(frame, new NativeBattleEffect314WriteModel(owner, 0x2F, NativeBattleEffectArena.Clear().Write(0, [0x32]), 1));
            }
        }
        if (age == 66)
        {
            foreach (uint program in ProgramAddresses)
            {
                frame = SpawnParticle664CD0(frame, resources, model, main + 0x20, program).Frame;
            }
        }
        if (age >= 66)
        {
            frame = NativeBattleEffect314Morph.Draw663850(frame, resources, inputs, model, main + 0x20, 13, 0x314);
            frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(model, 1, 0xFF, 4));
        }
        foreach (int start in new[] { 0, 15, 19 })
        {
            int flashAge = age - start;
            if ((uint)flashAge < 5)
            {
                int factor = NativeBattleRinoa89FadeDrawing.Factor6FC450(flashAge, 1, 0, 4);
                frame = inputs.Execute(frame, new NativeBattleEffect314DrawBatch(NativeBattleRinoa89FadeDrawing.Draw7040B0(255, 128, 128, factor), CursorGlobal));
            }
        }
        frame = DrawSparks665160(frame, main, resources, inputs);
        frame = DrawFragments664020(frame, main, inputs);
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect314Sound501330(SoundAddress, 0, 128));
        }
        else if (age == 222)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect314StopVibration4A2940(unchecked((int)Read(frame, main).ReadDword(0x10))));
        }
        frame = AdvanceFragments6642F0(frame, main, inputs);
        if ((uint)age <= 8)
        {
            for (int actor = 0; actor < 2; actor++)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(model + (uint)(actor * 156), (age & 1) != 0 ? 2 : 1, (age & 1) != 0 ? (ushort)0xFFF7 : (ushort)0xFF, (age & 1) != 0 ? (ushort)0 : (ushort)8));
            }
        }
        if ((uint)age <= 16)
        {
            frame = EmitOpeningSparks(frame, main, model, inputs);
            int secondWeight = (int)((uint)(age << 12) >> 4) + 128;
            frame = inputs.Execute(frame, new NativeBattleEffect314Blend56CC00(main + 0x1C, ColorGlobal, 4096 - secondWeight, secondWeight, model + 0xC4));
            frame = inputs.Execute(frame, new NativeBattleEffect314WriteModel(model + 156, 0x2B, NativeBattleEffectArena.Clear().Write(0, [2]), 1));
            frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(model + 156, 2, 0xFFFF, 0x800));
        }
        if (age == 16)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(model + 156, 1, 0xFF, 4));
        }
        if ((uint)(age - 100) <= 110)
        {
            int input = unchecked(((age - 100) * 3 - 330) * 1000);
            int scaled = input / 110 + 1024;
            frame = EmitShards664420(frame, resources, inputs, model, main + 0x20, main + 0x4728, scaled, 127, 13, 0x314);
        }
        return new(Write(frame, main, Read(frame, main).WriteWord(0xC, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleEffect314Frame WriteModelDword(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint model, int offset, uint value) => inputs.Execute(frame, new NativeBattleEffect314WriteModel(model, offset, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4));

    private static NativeBattleEffect314Frame EmitOpeningSparks(NativeBattleEffect314Frame frame, uint main, uint model, NativeBattleEffect314Inputs inputs)
    {
        ushort Draw()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        int bones = inputs.ReadBoneCount(frame, model);
        for (int index = 0; index < 8; index++)
        {
            (frame, uint spark) = AllocateSpark6643D0(frame, main);
            if (spark == 0)
            {
                break;
            }
            int bone = Draw() % bones;
            frame = inputs.Execute(frame, new NativeBattleEffect314BonePoint502170(model, bone, 0, spark));
            NativeBattleEffectArena record = ReadBytes(frame, spark, 16).Write(6, BitConverter.GetBytes((short)15));
            record = record.Write(8, BitConverter.GetBytes((short)((Draw() & 0x7FF) + 0x200))).Write(12, new byte[4]);
            record = record.Write(15, [unchecked((byte)((Draw() & 1) + 2))]);
            frame = WriteBytes(frame, spark, record, 16);
        }
        return frame;
    }
}
