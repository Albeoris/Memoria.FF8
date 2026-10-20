using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85EnvironmentInput(NativeBattleModulePoolTask Task, ImmutableArray<byte> Context, NativeBattleStageState Stage, ImmutableArray<NativeBattleGraphicsModel> Models);
public sealed record NativeBattleTheEnd85EnvironmentStep(NativeBattleTheEnd85EnvironmentInput Input, bool Complete, short? MusicGain);

public static class NativeBattleTheEnd85Environment
{
    public static NativeBattleTheEnd85EnvironmentStep Advance(NativeBattleTheEnd85EnvironmentInput input, uint defaultColor)
    {
        if (input.Context.IsDefault || input.Context.Length != 84 || input.Models.IsDefault || input.Models.Length != 7 || input.Task.Payload.IsDefault || input.Task.Payload.Length != 96 - 12)
        {
            throw new InvalidDataException("Native The End environment requires its complete task, context, stage and graphics model owners.");
        }
        input.Stage.Validate();
        int phase = unchecked((sbyte)input.Task.ReadByte(41));
        short? musicGain = null;
        bool stage = input.Task.Callback == 0x787E20;
        bool actors = input.Task.Callback == 0x787FD0;
        if (!stage && !actors && input.Task.Callback != 0x7882E0)
        {
            throw new InvalidDataException("Native The End environment selected an unrelated callback.");
        }
        switch (phase)
        {
            case 0:
                if (unchecked((short)input.Task.ReadWord(36)) >= 0)
                {
                    Store(input.Task.WriteWord(28, stage || actors ? (ushort)0 : (ushort)127));
                    if (stage)
                    {
                        input = input with { Stage = input.Stage with { Groups = input.Stage.Groups.Select(group => group with { ColorDepth = 0, ColorFlags = group.ColorFlags & 0xFF000000 }).ToImmutableArray() } };
                    }
                    Next();
                }
                break;
            case 1:
                short increasing = unchecked((short)(input.Task.ReadWord(28) + (stage ? 256 : actors ? 512 : -8)));
                bool reached = stage || actors ? increasing >= 4096 : increasing <= 48;
                if (reached)
                {
                    increasing = stage || actors ? (short)4096 : (short)48;
                }
                Store(input.Task.WriteWord(28, unchecked((ushort)increasing)));
                if (reached)
                {
                    if (stage)
                    {
                        input = input with { Stage = input.Stage with { Groups = input.Stage.Groups.Select(group => group with { Flags = (byte)(group.Flags & 0xFD) }).ToImmutableArray() } };
                    }
                    else if (actors)
                    {
                        byte[] context = input.Context.ToArray();
                        ImmutableArray<NativeBattleGraphicsModel>.Builder models = input.Models.ToBuilder();
                        for (int index = 0; index < 3; index++)
                        {
                            if ((models[index].Flags0 & 2) != 0)
                            {
                                BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(32 + index * 4), models[index].Flags0);
                                models[index] = models[index] with { Flags0 = (ushort)(models[index].Flags0 | 4) };
                            }
                        }
                        input = input with { Context = context.ToImmutableArray(), Models = models.ToImmutable() };
                    }
                    Next();
                }
                Draw(increasing);
                break;
            case 2:
                if (BinaryPrimitives.ReadUInt16LittleEndian(input.Context.AsSpan().Slice(64)) >= 6)
                {
                    if (stage)
                    {
                        input = input with { Stage = input.Stage with { Groups = input.Stage.Groups.Select(group => group with { Flags = (byte)((group.Flags & 0xFD) | ((group.Flags >> 1) & 2)) }).ToImmutableArray() } };
                    }
                    else if (actors)
                    {
                        ImmutableArray<NativeBattleGraphicsModel>.Builder models = input.Models.ToBuilder();
                        for (int index = 0; index < 3; index++)
                        {
                            ushort flags = models[index].Flags0;
                            if ((flags & 2) != 0)
                            {
                                byte saved = input.Context[32 + index * 4];
                                models[index] = models[index] with { Flags0 = (ushort)(flags ^ ((flags ^ saved) & 4)) };
                            }
                        }
                        input = input with { Models = models.ToImmutable() };
                        Store(input.Task.WriteWord(28, 4096));
                    }
                    Next();
                }
                break;
            case 3:
                short decreasing = unchecked((short)(input.Task.ReadWord(28) + (stage || actors ? -256 : 8)));
                bool ended = stage || actors ? decreasing <= 0 : decreasing >= 127;
                if (ended)
                {
                    decreasing = stage || actors ? (short)0 : (short)127;
                    Store(input.Task.WriteByte(38, (byte)(input.Task.ReadByte(38) | 1)));
                    Next();
                }
                Store(input.Task.WriteWord(28, unchecked((ushort)decreasing)));
                Draw(decreasing);
                break;
            case 4:
                break;
            default:
                throw new NotSupportedException($"Native The End signed environment phase{phase} is unsupported.");
        }
        Store(input.Task.WriteWord(36, unchecked((ushort)(input.Task.ReadWord(36) + 1))));
        return new(input, (input.Task.ReadByte(38) & 1) != 0 && input.Task.ReadByte(40) == 0, musicGain);

        void Store(NativeBattleModulePoolTask task) => input = input with { Task = task };
        void Next() => Store(input.Task.WriteByte(41, unchecked((byte)(input.Task.ReadByte(41) + 1))));
        void Draw(short amount)
        {
            if (stage)
            {
                input = input with { Stage = input.Stage with { Groups = input.Stage.Groups.Select(group => group with { ColorDepth = amount }).ToImmutableArray() } };
            }
            else if (actors)
            {
                input = input with { Models = input.Models.Select((model, index) => index < 3 && (model.Flags0 & 2) != 0 ? Fade788120(model with { Flags0 = (ushort)(model.Flags0 | 0x800) }, amount, defaultColor) : model).ToImmutableArray() };
            }
            else
            {
                musicGain = amount;
            }
        }
    }

    private static NativeBattleGraphicsModel Fade788120(NativeBattleGraphicsModel model, short amount, uint defaultColor)
    {
        if (amount == 0)
        {
            return model with { Color40 = defaultColor & 0xFFFFFF, Alpha7 = 0 };
        }
        uint color = 0x02000000;
        for (int channel = 0; channel < 3; channel++)
        {
            int source = (byte)(defaultColor >> (channel * 8)) >> 1;
            color |= (uint)unchecked((byte)(source - amount * source / 4096)) << (channel * 8);
        }
        return model with { Color40 = color };
    }
}
