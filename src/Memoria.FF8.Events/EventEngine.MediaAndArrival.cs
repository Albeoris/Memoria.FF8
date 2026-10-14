using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record FaceDirectionBaseline(int FirstAngle, int SecondAngle, int XAngle = 0);
public sealed record EventFault(int FieldId, int ObjectId, int ScriptId, int Operation, string Opcode, string Message);

public sealed partial class EventEngine
{
    private FieldTransition? pendingArrival;
    private int axisStart;
    private int axisTarget;
    private int axisDuration;
    private int axisElapsed;
    private bool movieProgressExpected;
    public Func<ActorState, FaceDirectionBaseline>? ReadFaceBaseline { get; set; }
    public Func<ActorState, float>? ReadFaceAnchorHeight { get; set; }
    public Func<ActorState, bool>? CanDisableAdditionalControl { get; set; }
    public Func<int, SoundResourceStatus>? LoadSoundResource { get; set; }
    public byte InitInputAxisDirection { get; set; }
    public int InputAxisDirection { get; private set; }
    public MoviePlaybackState? CurrentMoviePlayback { get; private set; }
    public EventFault? LastFault { get; private set; }
    public Func<MoviePlaybackState?>? MoviePlayback { get; set; }
    public Func<FieldTransition, WalkmeshPosition?>? ResolveFieldArrival { get; set; }

    private static bool Supports(Jsm.Opcode opcode) => opcode switch
    {
            Jsm.Opcode.LBL or Jsm.Opcode.NOP or Jsm.Opcode.RET or Jsm.Opcode.HALT or Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or
            Jsm.Opcode.POPM_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.CAL or Jsm.Opcode.JMP or Jsm.Opcode.JPF or
            Jsm.Opcode.SETTIMER or Jsm.Opcode.DISPTIMER or Jsm.Opcode.GETTIMER or Jsm.Opcode.KILLTIMER or Jsm.Opcode.BATTLE or Jsm.Opcode.BATTLEMODE or Jsm.Opcode.BATTLERESULT or
            Jsm.Opcode.RND or Jsm.Opcode.Unknown10 or Jsm.Opcode.GETINFO or Jsm.Opcode.INITTRACE or Jsm.Opcode.SETCAMERA or Jsm.Opcode.SCROLLMODE2 or Jsm.Opcode.SCROLLRATIO2 or Jsm.Opcode.DSCROLL2 or Jsm.Opcode.DSCROLL or Jsm.Opcode.DSCROLLA or Jsm.Opcode.CSCROLLA or Jsm.Opcode.LSCROLLA or Jsm.Opcode.LSCROLLP or Jsm.Opcode.DSCROLLP or Jsm.Opcode.CSCROLLP or Jsm.Opcode.CSCROLL2 or Jsm.Opcode.CSCROLL or Jsm.Opcode.SCROLLSYNC or Jsm.Opcode.DIRA or Jsm.Opcode.PDIRA or Jsm.Opcode.FACEDIR or Jsm.Opcode.RFACEDIR or Jsm.Opcode.FACEDIRLIMIT or Jsm.Opcode.FACEDIRINIT or Jsm.Opcode.FACEDIRSYNC or Jsm.Opcode.FACEDIRA or Jsm.Opcode.FACEDIROFF or Jsm.Opcode.RFACEDIROFF or Jsm.Opcode.ISPARTY or Jsm.Opcode.GETPARTY or Jsm.Opcode.PGETINFO or Jsm.Opcode.ISTOUCH or Jsm.Opcode.REQ or Jsm.Opcode.REQSW or
            Jsm.Opcode.REQEW or Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW or Jsm.Opcode.REFRESHPARTY or Jsm.Opcode.JOIN or Jsm.Opcode.SPLIT or Jsm.Opcode.JUMP or Jsm.Opcode.JUMP3 or Jsm.Opcode.CLEAR or Jsm.Opcode.SETMODEL or Jsm.Opcode.SETDRESS or Jsm.Opcode.SETPC or Jsm.Opcode.ACTORMODE or Jsm.Opcode.UNUSE or Jsm.Opcode.USE or
            Jsm.Opcode.HIDE or Jsm.Opcode.SHOW or Jsm.Opcode.SET or Jsm.Opcode.SET3 or Jsm.Opcode.DIR or Jsm.Opcode.DIRP or Jsm.Opcode.Unknown8 or Jsm.Opcode.Unknown9 or Jsm.Opcode.CTURNL or Jsm.Opcode.CTURNR or
            Jsm.Opcode.LTURNL or Jsm.Opcode.LTURNR or Jsm.Opcode.CTURN or Jsm.Opcode.LTURN or Jsm.Opcode.PLTURN or Jsm.Opcode.PCTURN or Jsm.Opcode.MSPEED or Jsm.Opcode.MACCEL or Jsm.Opcode.MLIMIT or Jsm.Opcode.MOVE or
            Jsm.Opcode.RMOVE or Jsm.Opcode.FMOVE or Jsm.Opcode.RFMOVE or Jsm.Opcode.RCMOVE or Jsm.Opcode.CMOVE or Jsm.Opcode.MOVESYNC or Jsm.Opcode.MOVECANCEL or Jsm.Opcode.MOVEA or Jsm.Opcode.RMOVEA or Jsm.Opcode.SETGETA or Jsm.Opcode.LADDERANIME or Jsm.Opcode.SETROOTTRANS or
            Jsm.Opcode.SHADELEVEL or Jsm.Opcode.SHADEFORM or Jsm.Opcode.SHADESET or Jsm.Opcode.DOFFSET or Jsm.Opcode.BASEANIME or Jsm.Opcode.ANIME or Jsm.Opcode.ANIMEKEEP or Jsm.Opcode.RANIME or
            Jsm.Opcode.RANIMEKEEP or Jsm.Opcode.RANIMELOOP or Jsm.Opcode.CANIME or Jsm.Opcode.CANIMEKEEP or Jsm.Opcode.RCANIME or Jsm.Opcode.RCANIMEKEEP or
            Jsm.Opcode.RCANIMELOOP or Jsm.Opcode.ANIMESPEED or Jsm.Opcode.ANIMESYNC or Jsm.Opcode.ANIMESTOP or Jsm.Opcode.WAIT or Jsm.Opcode.LOADSYNC or
            Jsm.Opcode.UCOFF or Jsm.Opcode.UCON or Jsm.Opcode.MENUENABLE or Jsm.Opcode.MENUDISABLE or Jsm.Opcode.MENUTIPS or Jsm.Opcode.TUTO or Jsm.Opcode.MENUTUTO or Jsm.Opcode.MENUPHS or Jsm.Opcode.MENUNORMAL or Jsm.Opcode.RUNENABLE or Jsm.Opcode.RUNDISABLE or
            Jsm.Opcode.MOVIEREADY or Jsm.Opcode.MOVIE or Jsm.Opcode.MOVIESYNC or Jsm.Opcode.PREMAPJUMP2 or Jsm.Opcode.MAPJUMPO or
            Jsm.Opcode.IDLOCK or Jsm.Opcode.IDUNLOCK or Jsm.Opcode.POLYCOLOR or Jsm.Opcode.POLYCOLORALL or Jsm.Opcode.MAPJUMP or Jsm.Opcode.MAPJUMP3 or Jsm.Opcode.AXIS or Jsm.Opcode.AMESW or Jsm.Opcode.AMES or Jsm.Opcode.RAMESW or Jsm.Opcode.MES or
            Jsm.Opcode.MESVAR or Jsm.Opcode.AASK or Jsm.Opcode.WINCLOSE or Jsm.Opcode.WINSIZE or Jsm.Opcode.MESSYNC or Jsm.Opcode.MENUNAME or Jsm.Opcode.MESMODE or
            Jsm.Opcode.SETMESSPEED or Jsm.Opcode.BGDRAW or Jsm.Opcode.BGOFF or Jsm.Opcode.BGCLEAR or Jsm.Opcode.SETDRAWPOINT or Jsm.Opcode.Unknown16 or Jsm.Opcode.BGANIMESPEED or Jsm.Opcode.BGANIME or Jsm.Opcode.RBGANIME or
            Jsm.Opcode.BGANIMESYNC or Jsm.Opcode.RBGANIMELOOP or Jsm.Opcode.RBGSHADELOOP or Jsm.Opcode.BGSHADE or Jsm.Opcode.BGSHADEOFF or Jsm.Opcode.DCOLADD or Jsm.Opcode.DCOLSUB or Jsm.Opcode.TCOLADD or Jsm.Opcode.TCOLSUB or Jsm.Opcode.FCOLADD or Jsm.Opcode.FCOLSUB or
            Jsm.Opcode.COLSYNC or Jsm.Opcode.FADEIN or Jsm.Opcode.FADEOUT or Jsm.Opcode.FADEBLACK or Jsm.Opcode.FADENONE or Jsm.Opcode.SETVIBRATE or Jsm.Opcode.FADESYNC or Jsm.Opcode.MAPFADEOFF or
            Jsm.Opcode.MAPFADEON or Jsm.Opcode.MAPJUMPON or Jsm.Opcode.MAPJUMPOFF or Jsm.Opcode.WORLDMAPJUMP or Jsm.Opcode.TALKON or Jsm.Opcode.TALKOFF or Jsm.Opcode.PUSHON or Jsm.Opcode.PUSHOFF or Jsm.Opcode.TALKRADIUS or
            Jsm.Opcode.PUSHRADIUS or Jsm.Opcode.THROUGHON or Jsm.Opcode.THROUGHOFF or Jsm.Opcode.FOOTSTEPON or Jsm.Opcode.FOOTSTEPOFF or Jsm.Opcode.FOOTSTEP or Jsm.Opcode.FOOTSTEPCOPY or Jsm.Opcode.FOOTSTEPCUT or Jsm.Opcode.FOOTSTEPOFFALL or Jsm.Opcode.SETLINE or
            Jsm.Opcode.LINEON or Jsm.Opcode.LINEOFF or Jsm.Opcode.DOORLINEOFF or Jsm.Opcode.DOORLINEON or Jsm.Opcode.PREMAPJUMP or Jsm.Opcode.BATTLEOFF or Jsm.Opcode.BATTLEON or Jsm.Opcode.SARALYOFF or
            Jsm.Opcode.SARALYON or Jsm.Opcode.SARALYDISPON or Jsm.Opcode.SARALYDISPOFF or Jsm.Opcode.ADDSEEDLEVEL or Jsm.Opcode.ADDITEM or Jsm.Opcode.ADDGIL or Jsm.Opcode.ADDPASTGIL or Jsm.Opcode.SETPLACE or Jsm.Opcode.REST or Jsm.Opcode.ADDMEMBER or Jsm.Opcode.SUBMEMBER or Jsm.Opcode.ISMEMBER or Jsm.Opcode.ADDPARTY or Jsm.Opcode.SUBPARTY or Jsm.Opcode.SETPARTY or
            Jsm.Opcode.JUNCTION or Jsm.Opcode.HOLD or Jsm.Opcode.FOLLOWOFF or Jsm.Opcode.FOLLOWON or Jsm.Opcode.SHAKE or Jsm.Opcode.SHAKEOFF or Jsm.Opcode.SEVOLTRANS or Jsm.Opcode.SEVOL or Jsm.Opcode.SEPOS or Jsm.Opcode.SEPOSTRANS or Jsm.Opcode.KEYON or Jsm.Opcode.KEYSCAN or Jsm.Opcode.KEYSCAN2 or Jsm.Opcode.RESETGF or Jsm.Opcode.DYING or Jsm.Opcode.SETHP or Jsm.Opcode.PHSPOWER or Jsm.Opcode.SAVEENABLE or Jsm.Opcode.GETCARD or Jsm.Opcode.SETBATTLEMUSIC or Jsm.Opcode.EFFECTLOAD or Jsm.Opcode.EFFECTPLAY2 or Jsm.Opcode.EFFECTPLAY or Jsm.Opcode.SESTOP or Jsm.Opcode.INITSOUND or Jsm.Opcode.MUSICSTOP or Jsm.Opcode.MUSICSTATUS or Jsm.Opcode.MUSICLOAD or Jsm.Opcode.MUSICCHANGE or
            Jsm.Opcode.MUSICSKIP or Jsm.Opcode.CROSSMUSIC or Jsm.Opcode.DUALMUSIC or Jsm.Opcode.MUSICVOL or Jsm.Opcode.MUSICVOLTRANS or Jsm.Opcode.MUSICVOLFADE or Jsm.Opcode.MUSICVOLSYNC or Jsm.Opcode.ALLSEVOLTRANS or Jsm.Opcode.ALLSEVOL or Jsm.Opcode.LSCROLL3 or Jsm.Opcode.LSCROLL2 or Jsm.Opcode.LSCROLL or Jsm.Opcode.SCROLLSYNC2 or Jsm.Opcode.PARTICLEON or Jsm.Opcode.PARTICLEOFF or Jsm.Opcode.MODELDOOROPEN or Jsm.Opcode.MODELDOORCLOSE or Jsm.Opcode.COFFSET or Jsm.Opcode.COFFSETS or Jsm.Opcode.LOFFSET or Jsm.Opcode.LOFFSETS or Jsm.Opcode.OFFSETSYNC => true,
        _ => false
    };

    private string? UnsupportedRequestContext(Fiber fiber, JsmOperation operation)
    {
        if (UnsupportedNativeMusicStatus(operation) is string musicStatus)
        {
            return musicStatus;
        }
        string? scalarAccess = UnsupportedNativeScalarAccess(fiber, operation);
        if (scalarAccess is not null)
        {
            return scalarAccess;
        }
        if (UnsupportedNativeModelDoor(fiber, operation) is string modelDoor)
        {
            return modelDoor;
        }
        if (UnsupportedNativePolygonColor(fiber, operation) is string polygonColor)
        {
            return polygonColor;
        }
        if (operation.Opcode == Jsm.Opcode.LADDERANIME && (fiber.Stack.Count < 2 || NativeRuntimeProfile is not { IsVerified: true } || actors[fiber.ObjectId].NativeLadderAnimation is null))
        {
            return "Native LADDERANIME requires its allocated caller body and two original operands; stack and PC preserved";
        }
        if (UnsupportedNativeEconomy(fiber, operation) is string economy)
        {
            return economy;
        }
        string? messageVariable = UnsupportedMessageVariable(fiber, operation);
        if (messageVariable is not null)
        {
            return messageVariable;
        }
        string? seeD = UnsupportedNativeSeeD(fiber, operation);
        if (seeD is not null)
        {
            return seeD;
        }
        // TODO(TD-003): reconstruct occupied RAMESW physical-stack retries
        if (operation.Opcode == Jsm.Opcode.RAMESW && fiber.Stack.Count >= 4 && dialogues.ContainsKey(fiber.Stack.ElementAt(3)))
        {
            return "Occupied native RAMESW consumes its operands before return5; its physical stack-residue retry requires a wider native VM contract";
        }
        string? actorInfo = UnsupportedNativeActorInfo(fiber, operation);
        if (actorInfo is not null)
        {
            return actorInfo;
        }
        if (operation.Opcode == Jsm.Opcode.SETHP)
        {
            return UnsupportedSetHitPoints(fiber);
        }
        string? guardianForceReset = UnsupportedGuardianForceReset(fiber, operation);
        if (guardianForceReset is not null)
        {
            return guardianForceReset;
        }
        string? footstepOffAll = UnsupportedFootstepOffAll(fiber, operation);
        if (footstepOffAll is not null)
        {
            return footstepOffAll;
        }
        if (operation.Opcode == Jsm.Opcode.JUMP)
        {
            return UnsupportedNativeJump(fiber, operation.Parameter);
        }
        if (operation.Opcode == Jsm.Opcode.JUMP3)
        {
            return UnsupportedNativeJump(fiber, operation.Parameter, true);
        }
        if (operation.Opcode == Jsm.Opcode.PLTURN)
        {
            return UnsupportedPartyLinearTurn(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.CMOVE)
        {
            return UnsupportedNativeScriptMove(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.MSPEED && (fiber.Stack.Count == 0 || actors[fiber.ObjectId].NativeScriptMovement is not null && unchecked((short)fiber.Stack.Peek()) <= 0))
        {
            return "MSPEED requires its original WORD operand and a supported positive retained native movement speed";
        }
        string? movieActors = UnsupportedMovieActors(fiber, operation);
        if (movieActors is not null)
        {
            return movieActors;
        }
        string? vibration = UnsupportedVibrationRequest(fiber, operation);
        if (vibration is not null)
        {
            return vibration;
        }
        string? allSoundVolume = UnsupportedAllSoundVolumeTransition(fiber, operation) ?? UnsupportedAllSoundVolume(fiber, operation);
        if (allSoundVolume is not null)
        {
            return allSoundVolume;
        }
        string? soundVolume = UnsupportedSoundVolume(fiber, operation);
        if (soundVolume is not null)
        {
            return soundVolume;
        }
        if (UnsupportedSoundPosition(fiber, operation) is string soundPosition)
        {
            return soundPosition;
        }
        string? footstepPair = UnsupportedFootstepPair(fiber, operation);
        if (footstepPair is not null)
        {
            return footstepPair;
        }
        string? actorShadow = UnsupportedActorShadow(fiber, operation);
        if (actorShadow is not null)
        {
            return actorShadow;
        }
        if (operation.Opcode == Jsm.Opcode.FACEDIRSYNC)
        {
            return UnsupportedNativeFaceSync(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.DIRP)
        {
            return UnsupportedNativeDirectionPoint(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.PDIRA)
        {
            return UnsupportedPartyActorDirection(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.Unknown8)
        {
            return UnsupportedNegativeCosineTurn(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.Unknown9)
        {
            return UnsupportedPositiveCosineTurn(fiber);
        }
        string? musicLoad = UnsupportedMusicLoad(fiber, operation);
        if (musicLoad is not null)
        {
            return musicLoad;
        }
        if (operation.Opcode == Jsm.Opcode.SET)
        {
            return UnsupportedNativeSet(fiber);
        }
        string? shake = UnsupportedShake(fiber, operation);
        if (shake is not null)
        {
            return shake;
        }
        string? volumeTransition = UnsupportedSoundVolumeTransition(fiber, operation);
        if (volumeTransition is not null)
        {
            return volumeTransition;
        }
        string? followOff = UnsupportedFollowOff(fiber, operation);
        if (followOff is not null)
        {
            return followOff;
        }
        if (operation.Opcode == Jsm.Opcode.MAPJUMPO && fiber.Stack.Count < 2)
        {
            return "MAPJUMPO requires its original field and triangle WORD operands; PC and operands preserved";
        }
        string? keyOn = UnsupportedButtonQuery(fiber, operation);
        if (keyOn is not null)
        {
            return keyOn;
        }
        string? characterVitals = UnsupportedCharacterVitals(fiber, operation);
        if (characterVitals is not null)
        {
            return characterVitals;
        }
        string? savedCharacter = UnsupportedSavedCharacterCommand(fiber, operation);
        if (savedCharacter is not null)
        {
            return savedCharacter;
        }
        if (operation.Opcode == Jsm.Opcode.CLEAR && services.NativeVariableValues is { } memory && memory.Any(pair => pair.Key < 256 && pair.Key + pair.Value.Width > 256 || pair.Key < 1280 && pair.Key + pair.Value.Width > 1280))
        {
            return "CLEAR partially overlaps an uncertain native scalar; its original byte projection requires a separate contract";
        }
        if (operation.Opcode == Jsm.Opcode.JUNCTION)
        {
            return UnsupportedJunction(fiber);
        }
        if (UnsupportedJunctionBackupMemory(fiber, operation) is string junctionMemory)
        {
            return junctionMemory;
        }
        if (operation.Opcode == Jsm.Opcode.SETDRESS)
        {
            return UnsupportedNativeDress(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.GETCARD)
        {
            return UnsupportedGetCard(fiber);
        }
        if (UnsupportedCardMemoryAccess(operation) is string cardAccess)
        {
            return cardAccess;
        }
        if (operation.Opcode == Jsm.Opcode.SAVEENABLE)
        {
            return UnsupportedSaveEnable(fiber);
        }
        string? battle = UnsupportedBattle(fiber, operation);
        if (battle is not null)
        {
            return battle;
        }
        if (operation.Opcode == Jsm.Opcode.EFFECTPLAY2)
        {
            return UnsupportedFieldSound(fiber, operation.Parameter);
        }
        if (operation.Opcode is Jsm.Opcode.PARTICLEON or Jsm.Opcode.PARTICLEOFF)
        {
            return UnsupportedParticle(fiber);
        }
        if (operation.Opcode is Jsm.Opcode.DOFFSET or Jsm.Opcode.MODELDOOROPEN or Jsm.Opcode.MODELDOORCLOSE or Jsm.Opcode.COFFSET or Jsm.Opcode.COFFSETS or Jsm.Opcode.LOFFSET or Jsm.Opcode.LOFFSETS or Jsm.Opcode.OFFSETSYNC)
        {
            return UnsupportedDisplayOffset(fiber, operation.Opcode);
        }
        string? setLine = UnsupportedNativeSetLine(fiber, operation);
        if (setLine is not null)
        {
            return setLine;
        }
        string? timerAccess = UnsupportedTimerAccess(fiber, operation);
        if (timerAccess is not null)
        {
            return timerAccess;
        }
        string? soundStop = UnsupportedSoundStop(fiber, operation);
        if (soundStop is not null)
        {
            return soundStop;
        }
        string? previousFieldAccess = UnsupportedPreviousFieldAccess(operation);
        if (previousFieldAccess is not null)
        {
            return previousFieldAccess;
        }
        string? previousWorldAccess = UnsupportedPreviousWorldEntityAccess(operation);
        if (previousWorldAccess is not null)
        {
            return previousWorldAccess;
        }
        if (operation.Opcode is Jsm.Opcode.SETDRAWPOINT or Jsm.Opcode.Unknown16)
        {
            return UnsupportedDrawPointSetup(fiber, operation);
        }
        string? soundInitializationAccess = UnsupportedSoundInitializationAccess(fiber, operation);
        if (soundInitializationAccess is not null)
        {
            return soundInitializationAccess;
        }
        string? musicStopAccess = UnsupportedMusicStopAccess(fiber, operation);
        if (musicStopAccess is not null)
        {
            return musicStopAccess;
        }
        string? fadeNoneAccess = UnsupportedNativeFadeAccess(fiber, operation);
        if (fadeNoneAccess is not null)
        {
            return fadeNoneAccess;
        }
        string? drawPointAccess = UnsupportedDrawPointAccess(operation);
        if (drawPointAccess is not null)
        {
            return drawPointAccess;
        }
        if (operation.Opcode == Jsm.Opcode.BGCLEAR && (FieldId < 72 || fiber.Stack.Count == 0 || unchecked((byte)fiber.Stack.Peek()) != 1))
        {
            return "Unsupported BGCLEAR requires an ordinary field and low-BYTE1 color-buffer clear; retained framebuffer modes preserve PC and operand";
        }
        if (operation.Opcode == Jsm.Opcode.GETPARTY)
        {
            return UnsupportedPartyQuery(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.SETPARTY)
        {
            return UnsupportedCanonicalPartyWrite(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.SUBPARTY)
        {
            return UnsupportedPartyRemoval(fiber);
        }
        if (operation.Opcode is Jsm.Opcode.MENUPHS or Jsm.Opcode.MENUNORMAL)
        {
            return UnsupportedPartyMenuRequest(operation.Opcode);
        }
        if (UnsupportedMinimalMenu(fiber, operation) is string unsupportedMenu)
        {
            return unsupportedMenu;
        }
        if (operation.Opcode == Jsm.Opcode.RBGANIMELOOP && program?.Objects[fiber.ObjectId].BackgroundModuleIndex is null)
        {
            return "Unsupported RBGANIMELOOP requires an original background module entity";
        }
        if (operation.Opcode == Jsm.Opcode.BGSHADEOFF && program?.Objects[fiber.ObjectId].BackgroundModuleIndex is null)
        {
            return "Unsupported BGSHADEOFF requires an original background module entity; PC and operands preserved";
        }
        ActorState caller = actors[fiber.ObjectId];
        if (operation.Opcode == Jsm.Opcode.SETROOTTRANS && (fiber.Stack.Count == 0 || FieldId < 72 || caller.ModelId < 0 || program is null || !program.NativeObjects.Take(program.NativeActorCount).Contains(fiber.ObjectId) || Scene.NativeActorLayoutVersion == 1 && !Scene.NativeActorObjectIds.Contains(fiber.ObjectId)))
        {
            return "Unsupported SETROOTTRANS requires one operand and a modeled native actor in an ordinary field; PC and operand preserved";
        }
        if (operation.Opcode == Jsm.Opcode.WORLDMAPJUMP)
        {
            return UnsupportedWorldJump(fiber);
        }
        if (operation.Opcode is Jsm.Opcode.RCMOVE or Jsm.Opcode.RMOVEA or Jsm.Opcode.MOVE or Jsm.Opcode.MACCEL or Jsm.Opcode.FMOVE or Jsm.Opcode.RFMOVE or Jsm.Opcode.RMOVE or Jsm.Opcode.MOVEA)
        {
            return UnsupportedNativeScriptMove(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.MOVESYNC && caller.NativeMovementCompletion542 is null)
        {
            return "MOVESYNC requires the executed allocator or writer of physical WORD542; PC preserved";
        }
        if (operation.Opcode == Jsm.Opcode.MOVECANCEL)
        {
            return UnsupportedNativeMovementCancellation(fiber);
        }
        string? worldControlAccess = UnsupportedWorldControlAccess(operation);
        if (worldControlAccess is not null)
        {
            return worldControlAccess;
        }
        if (operation.Opcode == Jsm.Opcode.SCROLLMODE2)
        {
            return UnsupportedBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.SCROLLRATIO2)
        {
            return UnsupportedBackgroundScrollRatio(fiber);
        }
        if (operation.Opcode is Jsm.Opcode.DSCROLL2 or Jsm.Opcode.DSCROLL)
        {
            return UnsupportedDirectBackgroundScroll(fiber, layerZero: operation.Opcode == Jsm.Opcode.DSCROLL);
        }
        if (operation.Opcode == Jsm.Opcode.DSCROLLA)
        {
            return UnsupportedActorBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.CSCROLLA)
        {
            return UnsupportedNativeActorCosineBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.LSCROLLA)
        {
            return UnsupportedNativeActorLinearBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.LSCROLLP)
        {
            return UnsupportedNativePartyLinearBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.DSCROLLP)
        {
            return UnsupportedNativePartyBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.CSCROLLP)
        {
            return UnsupportedPartyBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.CSCROLL2)
        {
            return UnsupportedCosineBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.CSCROLL)
        {
            return UnsupportedLayerZeroCosineBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.SCROLLSYNC)
        {
            return UnsupportedBackgroundScrollSync();
        }
        if (operation.Opcode == Jsm.Opcode.LSCROLL3)
        {
            return UnsupportedLinearBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.LSCROLL2)
        {
            return UnsupportedLinearBackgroundScroll2(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.LSCROLL)
        {
            return UnsupportedNativeLayerZeroLinearBackgroundScroll(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.SCROLLSYNC2)
        {
            return UnsupportedBackgroundScrollSync2(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.SPLIT)
        {
            return UnsupportedSplitContext(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.EFFECTPLAY)
        {
            return UnsupportedGlobalSound(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.JOIN)
        {
            return UnsupportedJoinContext(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.INITTRACE && PlayerActor is not { Active: true })
        {
            return "INITTRACE requires the current native runtime leader binding";
        }
        if (operation.Opcode == Jsm.Opcode.REFRESHPARTY && Scene.NativeActorLayoutVersion != 1)
        {
            return "Unsupported REFRESHPARTY requires original post-init native actor compaction; replay player-performance-classroom-exit-preboundary-checkpoint.json before field139 loads";
        }
        if (operation.Opcode == Jsm.Opcode.REFRESHPARTY && (variables.TryGetValue(104, out int partyFlags) && (partyFlags & 0x800) != 0 || variables.TryGetValue(105, out int partyFlagByte) && (partyFlagByte & 8) != 0))
        {
            return "Unsupported REFRESHPARTY auxiliary party-list branch requires its own native continuation evidence";
        }
        if (operation.Opcode == Jsm.Opcode.PCTURN && Scene.FieldPartyNativeActorIds.Count == 3 && fiber.Stack.Count > 1 && ResolvePartyActor(fiber.Stack.Skip(1).First()) is null)
        {
            return "Unsupported PCTURN refreshed party binding has no native actor object";
        }
        if (operation.Opcode is Jsm.Opcode.DOORLINEOFF or Jsm.Opcode.DOORLINEON && program?.Objects[fiber.ObjectId].IsArea != true)
        {
            return "Native DOORLINEON/OFF requires its original area entity rather than a SETLINE or model actor";
        }
        if (operation.Opcode is Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW)
        {
            if (program is null || (uint)operation.Parameter >= program.NativeObjects.Count || fiber.Stack.Count < 2)
            {
                return $"Unsupported {operation.Opcode} native target or operand stack is outside the verified request bounds";
            }
            int targetId = program.NativeObjects[operation.Parameter];
            if (Scene.NativeActorLayoutVersion == 1 && operation.Parameter < program.NativeActorCount && !Scene.NativeActorObjectIds.Contains(targetId))
            {
                // Native null table slots consume their two operands and return3.
                return null;
            }
            int requestPriority = fiber.Stack.Skip(1).First();
            int scriptId = fiber.Stack.Peek();
            if ((uint)requestPriority >= 8 || !actors[targetId].Active)
            {
                return $"Unsupported {operation.Opcode} priority or inactive target requires further native evidence";
            }
            bool occupied = RequestPriorityOccupied(targetId, requestPriority);
            if (!(occupied && operation.Opcode == Jsm.Opcode.REQ) && !program.Objects[targetId].Scripts.Any(script => script.Id == scriptId))
            {
                return $"Unsupported {operation.Opcode} native target {operation.Parameter} (object {targetId}) has no script label {scriptId}";
            }
            if (!occupied)
            {
                string? movementRequest = UnsupportedNativeMovementRequest(actors[targetId], requestPriority, scriptId);
                if (movementRequest is not null)
                {
                    return movementRequest;
                }
            }
        }
        if (operation.Opcode == Jsm.Opcode.ACTORMODE && fiber.Stack.Count > 0)
        {
            int mode = fiber.Stack.Peek();
            if (mode is not (2 or 3))
            {
                return $"Unsupported ACTORMODE {mode} requires native primitive-transparency support or unproved mode semantics";
            }
            if (mode == 3 && CanDisableAdditionalControl?.Invoke(actors[fiber.ObjectId]) != true)
            {
                return "Unsupported ACTORMODE 3 requires proved additional-control metadata without descendant bone transforms";
            }
        }
        if (operation.Opcode is not (Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW))
        {
            return null;
        }
        if (fiber.Stack.Count < 2 || (uint)operation.Parameter >= 3 || (uint)fiber.Stack.Skip(1).First() >= 8)
        {
            return $"Unsupported {operation.Opcode} party slot, priority or operand stack is outside the native request bounds";
        }
        int priority = fiber.Stack.Skip(1).First();
        ActorState? target = ResolveJoinPartyActor(operation.Parameter);
        if (target is not null && !target.Active)
        {
            return $"Unsupported {operation.Opcode} refreshed inactive party actor requires further native execution evidence";
        }
        if (target is null || RequestPriorityOccupied(target.ObjectId, priority))
        {
            return null;
        }
        if ((uint)fiber.Stack.Peek() >= program!.Objects[target.ObjectId].Scripts.Count)
        {
            return $"Unsupported {operation.Opcode} targets an unknown original party actor local script index";
        }
        int partyScriptId = program.Objects[target.ObjectId].Scripts[fiber.Stack.Peek()].Id;
        return UnsupportedNativeMovementRequest(target, priority, partyScriptId);
    }

    private void SampleMoviePlayback()
    {
        if (MoviePlayback?.Invoke() is not MoviePlaybackState playback)
        {
            return;
        }
        if (playback.FrameIndex < 0 || playback.TerminalFrame < playback.FrameIndex || playback.TerminalFrame > ushort.MaxValue)
        {
            throw new InvalidDataException("Movie provider returned invalid native frame bounds.");
        }
        if (movieProgressExpected || playback.IsPlaying)
        {
            variables[80] = (ushort)playback.FrameIndex;
        }
        CurrentMoviePlayback = playback;
        if (playback.Completed)
        {
            movieProgressExpected = false;
        }
    }

    private void StepInputAxis()
    {
        if (axisElapsed >= axisDuration)
        {
            return;
        }
        axisElapsed++;
        int interpolated = axisStart + (axisTarget - axisStart) * axisElapsed / axisDuration;
        InputAxisDirection = (byte)interpolated;
        if (axisElapsed == axisDuration)
        {
            axisStart = InputAxisDirection;
        }
    }

    public IReadOnlyDictionary<int, System.Numerics.Vector3> GetBackgroundModuleShades()
    {
        Dictionary<int, System.Numerics.Vector3> colors = Scene.BackgroundShadeLoops.ToDictionary(pair => pair.Key, pair => pair.Value.CurrentColor);
        foreach ((int module, BackgroundShade shade) in Scene.BackgroundShades)
        {
            int duration = shade.FadeFrames;
            int elapsed = (int)Math.Clamp(Frame - shade.StartFrame, 0, Math.Max(0, duration));
            float Channel(float first, float last) => duration == 0 ? last : (int)first + ((int)last - (int)first) * elapsed / duration;
            colors[module] = new System.Numerics.Vector3(Channel(shade.FirstColor.X, shade.SecondColor.X), Channel(shade.FirstColor.Y, shade.SecondColor.Y), Channel(shade.FirstColor.Z, shade.SecondColor.Z));
        }
        return colors;
    }

    private void StepBackgroundShadeLoops()
    {
        foreach ((int module, BackgroundShadeLoop shade) in Scene.BackgroundShadeLoops.ToArray())
        {
            if (shade.Remaining > 0)
            {
                int elapsed = shade.Elapsed + 1;
                int duration = shade.FirstToSecond ? shade.FadeFirst : shade.FadeSecond;
                System.Numerics.Vector3 from = shade.FirstToSecond ? shade.FirstColor : shade.SecondColor;
                System.Numerics.Vector3 to = shade.FirstToSecond ? shade.SecondColor : shade.FirstColor;
                float Channel(float first, float last) => (int)first + ((int)last - (int)first) * elapsed / duration;
                var color = new System.Numerics.Vector3(Channel(from.X, to.X), Channel(from.Y, to.Y), Channel(from.Z, to.Z));
                Scene.BackgroundShadeLoops[module] = shade with { Elapsed = elapsed, Remaining = shade.Remaining - 1, CurrentColor = color };
            }
            else if (shade.HoldRemaining > 0)
            {
                Scene.BackgroundShadeLoops[module] = shade with { HoldRemaining = shade.HoldRemaining - 1 };
            }
            else
            {
                bool forward = !shade.FirstToSecond;
                int duration = forward ? shade.FadeFirst : shade.FadeSecond;
                Scene.BackgroundShadeLoops[module] = shade with { FirstToSecond = forward, Elapsed = 0, Remaining = duration, HoldRemaining = Math.Max(0, (forward ? shade.HoldFirst : shade.HoldSecond) - 1), CurrentColor = duration == 0 ? forward ? shade.SecondColor : shade.FirstColor : shade.CurrentColor };
            }
        }
    }

    private static int NativeDirection(float fromX, float fromY, float toX, float toY)
    {
        long dx = (long)MathF.Floor(toX) - (long)MathF.Floor(fromX);
        long dy = (long)MathF.Floor(toY) - (long)MathF.Floor(fromY);
        long distance = (long)Math.Sqrt(dx * dx + dy * dy);
        if (distance == 0)
        {
            return 0;
        }
        long x = dx * 4096 / distance / 32;
        long y = dy * 4096 / distance / 32;
        bool horizontal = x * x > y * y;
        double minor = Math.Min(1, Math.Abs(horizontal ? y : x) / 128d);
        int angle = Math.Min(32, (int)Math.Round(Math.Asin(minor) * 128 / Math.PI));
        int direction = horizontal ? x > 0 ? 64 + Math.Sign(y) * angle : 192 - Math.Sign(y) * angle : y > 0 ? 128 - Math.Sign(x) * angle : Math.Sign(x) * angle;
        return (byte)direction;
    }

    private bool SoundResourceReady(Fiber fiber)
    {
        SoundResourceStatus status = LoadSoundResource?.Invoke(fiber.SoundResourceId) ?? throw Fault(fiber, "Saved sound-resource wait requires its field-audio backend.");
        if (!Enum.IsDefined(status))
        {
            throw Fault(fiber, "Field-audio backend returned an invalid resource status.");
        }
        SoundResourceRequest prior = Scene.LoadedSoundEffects[fiber.SoundResourceId];
        Scene.LoadedSoundEffects[fiber.SoundResourceId] = prior with { Status = status };
        return status != SoundResourceStatus.Pending;
    }

    private void ApplyPendingArrival()
    {
        if (pendingArrival is not FieldTransition arrival)
        {
            return;
        }
        WalkmeshPosition spawn = ResolveFieldArrival?.Invoke(arrival) ?? throw new InvalidDataException($"Field {FieldId} coordinate arrival requires destination triangle {arrival.Triangle} geometry.");
        if (spawn.Triangle != arrival.Triangle || !float.IsFinite(spawn.Position.X) || !float.IsFinite(spawn.Position.Y) || !float.IsFinite(spawn.Position.Z) || Math.Abs(spawn.Position.X - arrival.X) > 0.01f || Math.Abs(spawn.Position.Y - arrival.Y) > 0.01f)
        {
            throw new InvalidDataException("Arrival resolver did not preserve the native spawn coordinates and triangle.");
        }
        ActorState player = PlayerActor is { Active: true } boundPlayer ? boundPlayer : throw new InvalidDataException("Coordinate arrival requires an initialized current native runtime leader binding.");
        player.X = spawn.Position.X;
        player.Y = spawn.Position.Y;
        player.Z = spawn.Position.Z;
        player.WalkmeshTriangle = spawn.Triangle;
        player.Direction = arrival.Direction;
        pendingArrival = null;
    }
}
