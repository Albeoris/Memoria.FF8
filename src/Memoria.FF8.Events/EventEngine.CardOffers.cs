using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record RareCardOffer(int ObjectId, int CardId);

public sealed partial class EventEngine
{
    /// <summary>Read-only native challenge analysis. Unknown predicates and unsupported paths produce no offer.</summary>
    public IReadOnlyList<RareCardOffer> GetRareCardOffers()
    {
        if (program is null || !defaultsStarted || transitionRequested || LastFault is not null || services.IsMoviePlaying || HasPendingBattle || PendingTripleTriad is not null || Scene.NativeTutorial is not null || !IsUserControlEnabled || IsDialogueOpen || services.CardInventory is not NativeCardInventory cards)
        {
            return [];
        }
        if (Enumerable.Range(0, NativeCardInventory.CardCount).Sum(card => Math.Max(0, cards.AvailableCount(card))) < 5)
        {
            return [];
        }
        List<RareCardOffer> offers = [];
        foreach (ActorState actor in actors)
        {
            EventObject obj = program.Objects[actor.ObjectId];
            if (!actor.Active || !actor.TalkEnabled || actor.RenderModelId is < 0 or ushort.MaxValue || obj.Scripts.Count < 3 || RequestPriorityOccupied(actor.ObjectId, 7) || Scene.NativeActorLayoutVersion == 1 && !Scene.NativeActorObjectIds.Contains(actor.ObjectId))
            {
                continue;
            }
            EventScript script = obj.Scripts[2];
            if (!CardOfferScriptCanReachGame(script, []))
            {
                continue;
            }
            foreach ((int owner, int limit, int objectId) in ReadCardChallengeOwners(script, actor.ObjectId))
            {
                if (!actors[objectId].Active || !actors[objectId].Visible || actors[objectId].RenderModelId is < 0 or ushort.MaxValue)
                {
                    continue;
                }
                int matched = 0;
                for (int card = NativeCardInventory.CommonCardCount; card < NativeCardInventory.CardCount; card++)
                {
                    if (cards.Location(card) == owner && matched++ < limit)
                    {
                        offers.Add(new RareCardOffer(objectId, card));
                    }
                }
            }
        }
        return offers.Distinct().ToArray();
    }

    private IReadOnlySet<(int Owner, int Limit, int Object)> ReadCardChallengeOwners(EventScript script, int objectId, CardOfferBranch? seed = null, int depth = 0)
    {
        HashSet<(int Owner, int Limit, int Object)> owners = [];
        if (depth > 8)
        {
            return owners;
        }
        Queue<CardOfferBranch> pending = new();
        pending.Enqueue(seed ?? new CardOfferBranch(script.Start));
        int budget = 4096;
        while (pending.TryDequeue(out CardOfferBranch? branch) && budget > 0)
        {
            try
            {
                while (script.ContainsOperation(branch.Position) && budget-- > 0)
                {
                    JsmOperation operation = program!.Operations[branch.Position++];
                    int parameter = operation.Parameter;
                    switch (operation.Opcode)
                    {
                        case Jsm.Opcode.LBL:
                        case Jsm.Opcode.NOP:
                        case Jsm.Opcode.UCOFF:
                        case Jsm.Opcode.UCON:
                            break;
                        case Jsm.Opcode.PSHN_L:
                        case Jsm.Opcode.PSHAC:
                            branch.Stack.Add(parameter);
                            break;
                        case Jsm.Opcode.PSHI_L:
                            branch.Stack.Add(branch.Results.TryGetValue(parameter, out int? result) ? result : 0);
                            break;
                        case Jsm.Opcode.POPI_L:
                            branch.Results[parameter] = branch.Pop();
                            break;
                        case Jsm.Opcode.PSHM_B:
                        case Jsm.Opcode.PSHSM_B:
                        case Jsm.Opcode.PSHM_W:
                        case Jsm.Opcode.PSHSM_W:
                        case Jsm.Opcode.PSHM_L:
                        case Jsm.Opcode.PSHSM_L:
                            int width = NativeMemoryWidth(operation.Opcode);
                            if (Enumerable.Range(parameter, width).Any(branch.Writes.ContainsKey))
                            {
                                int? original = null;
                                if (Enumerable.Range(parameter, width).Any(address => !branch.Writes.ContainsKey(address)))
                                {
                                    NativeScalarValue read = ReadEventMemory(operation);
                                    original = read.IsExact ? read.RequireExact() : null;
                                }
                                int? stored = 0;
                                for (int offset = 0; offset < width; offset++)
                                {
                                    int? part = branch.Writes.TryGetValue(parameter + offset, out int? written) ? written : original is int known ? (known >> (offset * 8)) & 255 : null;
                                    stored = stored is int previous && part is int current ? previous | current << (offset * 8) : null;
                                }
                                branch.Stack.Add(ProjectCardOfferValue(stored, width, operation.Opcode is Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHSM_W, operation.Opcode == Jsm.Opcode.PSHSM_L));
                            }
                            else
                            {
                                NativeScalarValue value = ReadEventMemory(operation);
                                branch.Stack.Add(value.IsExact ? value.RequireExact() : null);
                            }
                            break;
                        case Jsm.Opcode.POPM_B:
                        case Jsm.Opcode.POPM_W:
                        case Jsm.Opcode.POPM_L:
                            int storeWidth = NativeMemoryWidth(operation.Opcode);
                            if (ResolveMemoryOwner(parameter, storeWidth) != NativeEventMemoryOwner.Generic)
                            {
                                throw new NotSupportedException("Challenge prerequisite writes a canonical state owner.");
                            }
                            branch.Store(parameter, storeWidth, branch.Pop());
                            break;
                        case Jsm.Opcode.CAL:
                            int? right = branch.Pop();
                            int? left = NativeScalarCalculation.OperandCount(parameter) == 1 ? 0 : branch.Pop();
                            branch.Stack.Add(right is int r && left is int l ? NativeScalarCalculation.Resolve(parameter, l, r).RequireExact() : null);
                            break;
                        case Jsm.Opcode.JMP:
                            branch.Position = JsmProgram.JumpTarget(operation.Index, parameter);
                            break;
                        case Jsm.Opcode.JPF:
                            int condition = branch.Pop() ?? throw new NotSupportedException("Unknown challenge predicate.");
                            if (condition == 0)
                            {
                                branch.Position = JsmProgram.JumpTarget(operation.Index, parameter);
                            }
                            break;
                        case Jsm.Opcode.WHERECARD:
                        case Jsm.Opcode.HOWMANYCARD:
                            int card = branch.Pop() ?? throw new NotSupportedException("Unknown card operand.");
                            branch.Results[0] = operation.Opcode == Jsm.Opcode.WHERECARD ? services.CardInventory!.Location(card) : services.CardInventory!.AvailableCount(card);
                            break;
                        case Jsm.Opcode.RND:
                            branch.Results[0] = null;
                            break;
                        case Jsm.Opcode.REQ:
                        case Jsm.Opcode.REQSW:
                        case Jsm.Opcode.REQEW:
                            int requested = branch.Pop() ?? throw new NotSupportedException("Unknown requested script.");
                            int priority = branch.Pop() ?? throw new NotSupportedException("Unknown request priority.");
                            if ((uint)parameter >= program.NativeObjects.Count)
                            {
                                throw new NotSupportedException("Unknown requested challenge object.");
                            }
                            int target = program.NativeObjects[parameter];
                            EventScript targetScript = program.Objects[target].Scripts.Single(candidate => candidate.Id == unchecked((ushort)requested));
                            if (target == objectId && operation.Opcode != Jsm.Opcode.REQ)
                            {
                                throw new NotSupportedException("Synchronous self-request cannot preempt the hypothetical talk slot7.");
                            }
                            if (CardOfferScriptCanReachGame(targetScript, []))
                            {
                                if (target == objectId || !actors[target].Active || RequestPriorityOccupied(target, unchecked((byte)priority)))
                                {
                                    throw new NotSupportedException("Requested challenge is unavailable.");
                                }
                                //A different requested actor has no input action2 flag:
                                //its native LBL starts R0..R7 at0. Parent results stay local.
                                CardOfferBranch called = new(targetScript.Start) { Writes = new(branch.Writes), Results = [] };
                                owners.UnionWith(ReadCardChallengeOwners(targetScript, target, called, depth + 1));
                                branch.Position = -1;
                                break;
                            }
                            InvalidateCardOfferRequest(branch, parameter, requested, priority, []);
                            break;
                        case Jsm.Opcode.PREQ:
                        case Jsm.Opcode.PREQSW:
                        case Jsm.Opcode.PREQEW:
                            int partyScript = branch.Pop() ?? throw new NotSupportedException("Unknown party request script.");
                            int partyPriority = branch.Pop() ?? throw new NotSupportedException("Unknown party request priority.");
                            ActorState? partyActor = ResolveJoinPartyActor(parameter);
                            if (partyActor is not null && !RequestPriorityOccupied(partyActor.ObjectId, unchecked((byte)partyPriority)))
                            {
                                IReadOnlyList<EventScript> partyScripts = program.Objects[partyActor.ObjectId].Scripts;
                                int partyScriptIndex = unchecked((ushort)partyScript);
                                if ((uint)partyScriptIndex >= partyScripts.Count)
                                {
                                    throw new NotSupportedException("Unknown party prerequisite script.");
                                }
                                EventScript partyTarget = partyScripts[partyScriptIndex];
                                int nativeObject = program.NativeObjects.ToList().IndexOf(partyActor.ObjectId);
                                InvalidateCardOfferRequest(branch, nativeObject, partyTarget.Id, partyPriority, []);
                            }
                            break;
                        case Jsm.Opcode.AASK:
                            int?[] question = branch.Pop(8);
                            int first = question[2] ?? throw new NotSupportedException("Unknown choices.");
                            int last = question[3] ?? throw new NotSupportedException("Unknown choices.");
                            if (last < first || last - first > 16)
                            {
                                throw new NotSupportedException("Unbounded choices.");
                            }
                            for (int choice = 0; choice <= last - first; choice++)
                            {
                                CardOfferBranch next = branch.Copy();
                                next.Results[0] = choice;
                                pending.Enqueue(next);
                            }
                            branch.Position = -1;
                            break;
                        case Jsm.Opcode.CARDGAME:
                            int?[] arguments = branch.Pop(7);
                            if (arguments[0] is int rawOwner && unchecked((byte)rawOwner) is byte owner && owner is not (0 or 0xF0) && arguments[3] is int chance && unchecked((byte)chance) != 0)
                            {
                                //537640 scans cards in ID order. At chance>=200 the
                                //subsequent half-chance remains>=100, forcing the first five.
                                owners.Add((owner, unchecked((byte)chance) >= 200 ? 5 : int.MaxValue, objectId));
                            }
                            branch.Position = -1;
                            break;
                        case Jsm.Opcode.AMESW:
                        case Jsm.Opcode.AMES:
                        case Jsm.Opcode.RAMESW:
                            branch.Pop(4);
                            break;
                        case Jsm.Opcode.MES:
                            branch.Pop(2);
                            break;
                        case Jsm.Opcode.WAIT:
                        case Jsm.Opcode.DIR:
                        case Jsm.Opcode.WINCLOSE:
                        case Jsm.Opcode.MESSYNC:
                            branch.Pop();
                            break;
                        case Jsm.Opcode.RET:
                        case Jsm.Opcode.HALT:
                            branch.Position = -1;
                            break;
                        default:
                            int presentationOperands = CardOfferPresentationOperandCount(operation.Opcode);
                            if (presentationOperands < 0)
                            {
                                throw new NotSupportedException("Unproved challenge path.");
                            }
                            branch.Pop(presentationOperands);
                            break;
                    }
                }
            }
            catch (Exception error) when (error is NotSupportedException or InvalidOperationException or InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException or DivideByZeroException or OverflowException)
            {
                // A presentation hint cannot choose an uncertain path or execute an unsupported contract.
            }
        }
        return owners;
    }

    private bool CardOfferScriptCanReachGame(EventScript script, HashSet<int> visited)
    {
        if (program is null || !visited.Add(script.Id))
        {
            return false;
        }
        foreach (JsmOperation operation in program.Operations.Where(operation => script.ContainsOperation(operation.Index)))
        {
            if (operation.Opcode == Jsm.Opcode.CARDGAME)
            {
                return true;
            }
            if (operation.Opcode is Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW && script.ContainsOperation(operation.Index - 2) && program.Operations[operation.Index - 1].Opcode == Jsm.Opcode.PSHN_L && (uint)operation.Parameter < program.NativeObjects.Count)
            {
                int target = program.NativeObjects[operation.Parameter];
                int requested = unchecked((ushort)program.Operations[operation.Index - 1].Parameter);
                EventScript? child = program.Objects[target].Scripts.FirstOrDefault(candidate => candidate.Id == requested);
                if (child is not null && CardOfferScriptCanReachGame(child, visited))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void InvalidateCardOfferRequest(CardOfferBranch branch, int nativeObject, int scriptId, int priority, HashSet<int> visited)
    {
        if (program is null || (uint)nativeObject >= program.NativeObjects.Count)
        {
            throw new NotSupportedException("Unknown challenge request owner.");
        }
        int objectId = program.NativeObjects[nativeObject];
        if (!actors[objectId].Active || RequestPriorityOccupied(objectId, unchecked((byte)priority)))
        {
            throw new NotSupportedException("Challenge prerequisite request is unavailable.");
        }
        EventScript requested = program.Objects[objectId].Scripts.Single(script => script.Id == unchecked((ushort)scriptId));
        if (!visited.Add(requested.Id))
        {
            return;
        }
        foreach (JsmOperation operation in program.Operations.Where(operation => requested.ContainsOperation(operation.Index)))
        {
            if (operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L)
            {
                if (ResolveMemoryOwner(operation.Parameter, NativeMemoryWidth(operation.Opcode)) != NativeEventMemoryOwner.Generic)
                {
                    throw new NotSupportedException("Prerequisite writes a canonical state owner.");
                }
                branch.Store(operation.Parameter, NativeMemoryWidth(operation.Opcode), null);
            }
            else if (operation.Opcode is Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW)
            {
                // Transitive calls require literal source operands before their effects can be bounded.
                int index = operation.Index;
                if (!requested.ContainsOperation(index - 2) || program.Operations[index - 1].Opcode != Jsm.Opcode.PSHN_L || program.Operations[index - 2].Opcode != Jsm.Opcode.PSHN_L)
                {
                    throw new NotSupportedException("Dynamic prerequisite request.");
                }
                InvalidateCardOfferRequest(branch, operation.Parameter, program.Operations[index - 1].Parameter, program.Operations[index - 2].Parameter, visited);
            }
            else if (CardOfferPresentationOperandCount(operation.Opcode) < 0 && operation.Opcode is not (Jsm.Opcode.LBL or Jsm.Opcode.RET or Jsm.Opcode.NOP or Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L or Jsm.Opcode.CAL or Jsm.Opcode.JMP or Jsm.Opcode.JPF or Jsm.Opcode.RND or Jsm.Opcode.AMESW or Jsm.Opcode.AMES or Jsm.Opcode.RAMESW or Jsm.Opcode.MES or Jsm.Opcode.AASK or Jsm.Opcode.WINCLOSE or Jsm.Opcode.MESSYNC or Jsm.Opcode.WAIT))
            {
                throw new NotSupportedException("Prerequisite changes challenge ownership or availability.");
            }
        }
    }

    //Existing Commands/PartyLinearTurn/PartyFace/NativeAnimation contracts:
    //these commands alter presentation, never card locations or scalar predicates.
    private static int CardOfferPresentationOperandCount(Jsm.Opcode opcode) => opcode switch
    {
        Jsm.Opcode.ANIME or Jsm.Opcode.ANIMEKEEP or Jsm.Opcode.RANIME or Jsm.Opcode.RANIMEKEEP or Jsm.Opcode.RANIMELOOP or Jsm.Opcode.ANIMESYNC or Jsm.Opcode.FACEDIRINIT or Jsm.Opcode.FACEDIRSYNC => 0,
        Jsm.Opcode.SETMESSPEED => 0,
        Jsm.Opcode.ANIMESPEED => 1,
        Jsm.Opcode.PCTURN or Jsm.Opcode.PLTURN or Jsm.Opcode.CTURN or Jsm.Opcode.LTURN or Jsm.Opcode.CTURNL or Jsm.Opcode.CTURNR or Jsm.Opcode.LTURNL or Jsm.Opcode.LTURNR or Jsm.Opcode.FACEDIRP or Jsm.Opcode.RFACEDIRP or Jsm.Opcode.CANIME or Jsm.Opcode.CANIMEKEEP or Jsm.Opcode.RCANIME or Jsm.Opcode.RCANIMEKEEP or Jsm.Opcode.RCANIMELOOP => 2,
        Jsm.Opcode.MESMODE => 3,
        _ => -1
    };

    private static int? ProjectCardOfferValue(int? value, int width, bool signed, bool saturateSignedLong = false)
    {
        return value is int known ? saturateSignedLong ? Math.Clamp(known, -0x800000, 0x7FFFFF) : NativeScalarValue.Exact(known).ToWidth(width, signed).RequireExact() : null;
    }

    private sealed class CardOfferBranch(int position)
    {
        public int Position = position;
        public List<int?> Stack = [];
        // Native4796E0 ->529FF0 marks the card action with actor flag20;
        // LBL51C570 clears all registers, then sets R0=1 for that flag.
        public Dictionary<int, int?> Results = new() { [0] = 1 };
        public Dictionary<int, int?> Writes = [];

        public void Store(int address, int width, int? value)
        {
            for (int offset = 0; offset < width; offset++)
            {
                Writes[address + offset] = value is int known ? (known >> (offset * 8)) & 255 : null;
            }
        }

        public int? Pop()
        {
            if (Stack.Count == 0)
            {
                throw new NotSupportedException("Challenge stack underflow.");
            }
            int? value = Stack[^1];
            Stack.RemoveAt(Stack.Count - 1);
            return value;
        }

        public int?[] Pop(int count)
        {
            int?[] values = new int?[count];
            for (int index = count - 1; index >= 0; index--)
            {
                values[index] = Pop();
            }
            return values;
        }

        public CardOfferBranch Copy()
        {
            return new CardOfferBranch(Position) { Stack = [.. Stack], Results = new(Results), Writes = new(Writes) };
        }
    }
}
