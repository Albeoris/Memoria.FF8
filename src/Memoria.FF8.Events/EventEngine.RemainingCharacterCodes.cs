using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private int PrepareNativeGetHitPoints(Fiber fiber)
    {
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("GETHP requires authentic saved character HP WORDs.");
        vitals.Validate();
        uint offset = unchecked((uint)fiber.Stack.PeekValue().RequireExact() * 152u);
        if (offset > 7u * 152u || offset % 152u != 0)
        {
            throw new NotSupportedException("GETHP full DWORD row address lies outside the eight owned saved HP WORDs.");
        }
        NativeHpWord hp = vitals.Records[offset / 152u].HitPoints;
        return hp.Minimum == hp.Maximum ? hp.Minimum : throw new NotSupportedException("GETHP cannot replace an uncertain saved HP WORD with an exact representative.");
    }

    private int PrepareNativeGetDress(Fiber fiber)
    {
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("GETDRESS requires authentic character identity rows.");
        NativeCharacterAppearance appearance = services.CharacterAppearance ?? throw new NotSupportedException("GETDRESS requires authentic saved costume BYTEs.");
        vitals.Validate();
        appearance.Validate();
        byte row = vitals.FindPhysicalRow(unchecked((byte)fiber.Stack.PeekValue().RequireExact()));
        if (row == 255 || appearance.InitOutSha256 != vitals.InitOutSha256)
        {
            throw new NotSupportedException("GETDRESS requires an owned saved identity and matching costume initialization.");
        }
        return appearance.Records[row].Dress;
    }

    private string? UnsupportedRemainingCharacterCode(Fiber fiber, Jsm.Opcode opcode)
    {
        if (opcode is not (Jsm.Opcode.GETHP or Jsm.Opcode.GETDRESS or Jsm.Opcode.ADDMAGIC or Jsm.Opcode.SETODIN or Jsm.Opcode.LASTOUT or Jsm.Opcode.SETPARTY2))
        {
            return null;
        }
        try
        {
            if (NativeRuntimeProfile is not { IsVerified: true })
            {
                throw new NotSupportedException($"{opcode} requires the authenticated native runtime profile.");
            }
            int count = opcode == Jsm.Opcode.ADDMAGIC ? 3 : opcode is Jsm.Opcode.GETHP or Jsm.Opcode.GETDRESS ? 1 : 0;
            if (fiber.Stack.Count < count)
            {
                throw new NotSupportedException($"{opcode} requires its original {count} stack operands.");
            }
            switch (opcode)
            {
                case Jsm.Opcode.GETHP:
                    _ = PrepareNativeGetHitPoints(fiber);
                    break;
                case Jsm.Opcode.GETDRESS:
                    _ = PrepareNativeGetDress(fiber);
                    break;
                case Jsm.Opcode.ADDMAGIC:
                    _ = PrepareNativeAddMagic(fiber);
                    break;
                case Jsm.Opcode.SETODIN:
                    if (services is not GameSession session || session.State.SavedBattleFlags is not { EffectiveKnownMask: byte.MaxValue } flags)
                    {
                        throw new NotSupportedException("SETODIN requires its complete canonical saved battle flag BYTE.");
                    }
                    flags.Validate();
                    break;
                case Jsm.Opcode.LASTOUT:
                case Jsm.Opcode.SETPARTY2:
                    _ = PrepareNativeRemainingParty(opcode);
                    break;
                default:
                    throw new NotSupportedException("Unsupported character opcode.");
            }
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private NativeCharacterVitals PrepareNativeAddMagic(Fiber fiber)
    {
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("ADDMAGIC requires authentic saved character inventory and combat-party owners.");
        return vitals.AddEventMagic(fiber.Stack.ElementAt(2), fiber.Stack.ElementAt(1), fiber.Stack.PeekValue().RequireExact());
    }

    private NativeCharacterVitals PrepareNativeRemainingParty(Jsm.Opcode opcode)
    {
        if (services.CharacterVitals is not { } current || services.WorldControls is not { } controls || !services.CanStoreNativeEventOwners)
        {
            throw new NotSupportedException($"{opcode} requires atomic saved party and field-control owners.");
        }
        controls.Validate();
        current.Validate();
        if (opcode == Jsm.Opcode.SETPARTY2)
        {
            if (!HasCanonicalPartySlots(Scene) || UnsupportedJunctionBackupAliases() is not null)
            {
                throw new NotSupportedException("SETPARTY2 requires the canonical three field identities and owned six reserve BYTEs.");
            }
            return PrepareNativeReserveParty(current).RebuildParty();
        }
        (services.InputConfiguration ?? throw new NotSupportedException("LASTOUT requires its saved options owner.")).Validate();
        return current.RebuildParty();
    }

    private int[] ExecuteRemainingCharacterCode(Fiber fiber, Jsm.Opcode opcode)
    {
        try
        {
            return ExecuteRemainingCharacterCodeCore(fiber, opcode);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native {opcode} owner publication rejected; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, opcode.ToString(), message);
            throw new EventScriptException(message, exception);
        }
    }

    private int[] ExecuteRemainingCharacterCodeCore(Fiber fiber, Jsm.Opcode opcode)
    {
        switch (opcode)
        {
            case Jsm.Opcode.GETHP:
                fiber.Results[0] = PrepareNativeGetHitPoints(fiber);
                return Pop(fiber, 1);
            case Jsm.Opcode.GETDRESS:
                fiber.Results[0] = PrepareNativeGetDress(fiber);
                return Pop(fiber, 1);
            case Jsm.Opcode.ADDMAGIC:
                services.SetCharacterVitals(PrepareNativeAddMagic(fiber));
                return Pop(fiber, 3);
            case Jsm.Opcode.SETODIN:
                ((GameSession)services).SetNativeOdin();
                return [];
            case Jsm.Opcode.LASTOUT:
                NativeCharacterVitals rebuilt = PrepareNativeRemainingParty(opcode);
                services.SetNativeEventOwners(services.WorldControls! with { EventFlags104 = services.WorldControls!.EventFlags104 & ~0x800u }, characterVitals: rebuilt, inputConfiguration: services.InputConfiguration! with { Options = services.InputConfiguration!.Options & 0x00FFFFFFu });
                return [];
            case Jsm.Opcode.SETPARTY2:
                services.SetNativeEventOwners(services.WorldControls!, characterVitals: PrepareNativeRemainingParty(opcode));
                return [];
            default:
                throw new NotSupportedException("Unsupported character opcode.");
        }
    }
}
