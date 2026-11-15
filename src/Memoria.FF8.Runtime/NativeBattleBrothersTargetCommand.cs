using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBrothersTargetServices(ushort? Encounter, Func<byte> ReadFlag, Action<byte> WriteFlag, Func<BattleModelOwner> ResolveModel3, Func<BattleModelOwner, NativeBattleGraphicsModel> ReadModel, NativeBattleWaterContextServices Context);

public static class NativeBattleBrothersTargetCommand
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleBrothersTargetServices services)
    {
        if (services.Encounter != 63)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, 4);
        }
        int mode = frame.ReadTemporaryWord(0x14A) >> 12;
        if (mode > 1)
        {
            return frame;
        }
        if (mode == 1)
        {
            if (services.ReadFlag() == 0 || NativeBattleCinematicActorAccess.ReadSlotActor(frame) != services.ResolveModel3())
            {
                return NativeBattleScriptedCommandReads.Advance(frame, 4);
            }
            short displacement = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
            return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + (uint)displacement));
        }
        NativeBattleCinematicActorContext actors = NativeBattleCinematicActorAccess.ReadContext(frame);
        BattleModelOwner target = services.ResolveModel3();
        bool original = services.Context.StorageContract == BattleContactTaskStorageContract.OriginalArena;
        bool alreadySelected = original || actors.CurrentActors.IsEmpty
            ? actors.ReadFirstActor() == target
            : actors.CurrentActors.Contains(target);
        if (alreadySelected)
        {
            services.WriteFlag(0);
            return NativeBattleScriptedCommandReads.Advance(frame, 4);
        }
        services.WriteFlag(1);
        uint header = CoreDword(frame, 0x279744C);
        NativeBattleModelAliases aliases = services.Context.CaptureAliases();
        ImmutableArray<BattleModelOwner> selected;
        if (original)
        {
            // The authored correction writes only the fourth byte. The first three retain their previous owners.
            var prefix = ImmutableArray.CreateBuilder<BattleModelOwner>(4);
            for (int index = 0; index < 3; index++)
            {
                prefix.Add(aliases.DecodeLiteral(frame.Core.Read(header + 0x48 + (uint)index, 1)[0]));
            }
            prefix.Add(target);
            selected = prefix.MoveToImmutable();
            frame = HeaderByte(frame, 0x41, 4);
            frame = HeaderByte(frame, 0x4B, 3);
        }
        else
        {
            selected = actors.CurrentActors.Add(target);
            frame = selected.Length <= byte.MaxValue
                ? HeaderByte(frame, 0x41, (byte)selected.Length)
                : frame with { Core = frame.Core.MarkUnknown(header + 0x41, 1) };
            for (int index = 0; index < Math.Min(selected.Length, 6); index++)
            {
                frame = frame with { Core = NativeBattleWaterContextCommands.WriteActor(frame.Core, header + 0x48 + (uint)index, selected[index], aliases) };
            }
        }
        NativeBattleGraphicsModel model = services.ReadModel(target);
        NativeBattleVector position = (model.Transform ?? throw new NotSupportedException("Native Brothers target correction requires original model3 position28.")).Position28;
        NativeBattleCinematicActorCache cached = actors.ReadCachedActor(target);
        actors = actors.WithCurrentActors(selected) with { CachedActors = actors.CachedActors.SetItem(target, cached with { Position28 = position, Flags0 = model.Flags0 }) };
        frame = frame with { Core = frame.Core with { PresentationActors = actors } };
        frame = RefreshModel3Header(frame, model.Flags0, position);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    private static NativeBattleWaterExecutionFrame RefreshModel3Header(NativeBattleWaterExecutionFrame frame, ushort flags, NativeBattleVector position)
    {
        frame = HeaderWord(frame, 0x9C, unchecked((ushort)position.X));
        frame = HeaderWord(frame, 0x9E, unchecked((ushort)position.Y));
        frame = HeaderWord(frame, 0xA0, unchecked((ushort)position.Z));
        frame = HeaderWord(frame, 0x54, flags);
        return WriteCoreDword(frame, CoreDword(frame, 0x279744C) + 0x6C, NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(3)));
    }

    private static NativeBattleWaterExecutionFrame HeaderByte(NativeBattleWaterExecutionFrame frame, uint offset, byte value) => frame with { Core = frame.Core.Write(CoreDword(frame, 0x279744C) + offset, new byte[] { value }) };
    private static NativeBattleWaterExecutionFrame HeaderWord(NativeBattleWaterExecutionFrame frame, uint offset, ushort value) => WriteCoreWord(frame, CoreDword(frame, 0x279744C) + offset, value);
}
