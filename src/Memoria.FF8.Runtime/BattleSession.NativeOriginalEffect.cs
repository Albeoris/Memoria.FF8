namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState BeginNativeOriginalEffect(NativeBattleGraphicsState graphics, ushort effect, bool destination21DFEC0 = false)
    {
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effect, definition.NativeEffectCatalog);
        selected = NativeBattleEffectResourceResolver.MaterializeSelected(selected, nativeEffectResourceResolver);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50AF20 requires its retained571870 allocator owner; migrate the standalone snapshot explicitly.");
        memory = memory.RetainReturnedTaskOnModuleLoad(graphics.OriginalEffect);
        (memory, selected) = NativeBattleOriginalEffects.Initialize(memory, selected);
        NativeEffectMemory = memory.PublishEntry50AF20(selected.Binding, destination21DFEC0);
        return graphics with { OriginalEffect = selected };
    }

    private NativeBattleGraphicsState PrepareNativeOriginalEffectParameters(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte parameterFlags = 0)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native module invocation requires the retained50AF20 entry owner.");
        graphics = PrepareNativeHeaderGeometry(graphics, context);
        NativeBattleEffectParameters parameters = CreateNativeOriginalEffectParameters(context, parameterFlags);
        return graphics with { OriginalEffect = effect with { Parameters = parameters } };
    }

    private NativeBattleEffectParameters CreateNativeOriginalEffectParameters(NativeBattleActionGraphicsState context, byte parameterFlags = 0)
    {
        return new(context.ActorIndex, (byte)(context.HitFlags | parameterFlags), context.TargetMask, context.HeaderIndex, 0, 0) { Parameter80 = NativeEffectMemory?.Parameter80, Parameter82 = NativeEffectMemory?.Parameter82 };
    }

    private NativeBattleGraphicsState InvokeNativeOriginalEffect(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte parameterFlags = 0)
    {
        graphics = PrepareNativeOriginalEffectParameters(graphics, context, parameterFlags);
        graphics = PrepareNativeOriginalEffectActor(graphics, context.ActorIndex);
        return InvokeNativeOriginalEffect(graphics);
    }

    private static NativeBattleGraphicsState PrepareNativeOriginalEffectActor(NativeBattleGraphicsState graphics, byte actorIndex)
    {
        NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
        return graphics with { Models = graphics.Models.SetItem(actorIndex, actor with { Rotation12 = 0, Rotation14 = actorIndex >= 3 ? (short)2048 : (short)0, Rotation16 = 0 }) };
    }

    private NativeBattleGraphicsState InvokeNativeOriginalEffect(NativeBattleGraphicsState graphics, bool assignReturnedTask = true)
    {
        NativeBattleGraphicsState entered = InvokeNativeOriginalEffectEntry(graphics);
        if (assignReturnedTask)
        {
            NativeEffectMemory = (NativeEffectMemory ?? throw new NotSupportedException("Native assigned entry requires its process task pointer owner.")).AssignReturnedTask(entered.OriginalEffect ?? throw new InvalidDataException("Native entry lost its returned pool owner."));
        }
        return entered;
    }

    private NativeBattleGraphicsState InvokeNativeOriginalEffectEntry(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native effect invocation requires its actual loaded module entry.");
        _ = effect.Parameters ?? throw new InvalidDataException("Native effect entry lacks its50AFC0 parameter block.");
        ObserveNativeUnknownStackCallback(effect.Binding.Entry, "original effect entry");
        if (effect.Binding.Effect is >= 292 and <= 296)
        {
            return EnterNativeStatusCureItem(graphics);
        }
        if (effect.Binding.Entry == 0x694520)
        {
            return EnterNativeWhiteWind226(graphics);
        }
        if (effect.Binding.Entry == 0x8885D0)
        {
            return EnterNativeClash26(graphics);
        }
        if (effect.Binding.Entry == 0x6185D0)
        {
            return EnterNativeSleep145(graphics);
        }
        if (effect.Binding.Entry == NativeBattleShockwavePulsar260Profile.Entry)
        {
            return EnterNativeShockwavePulsar260(graphics);
        }
        if (effect.Binding.Entry == NativeBattleRayBomb307Profile.Entry)
        {
            return EnterNativeRayBomb307(graphics);
        }
        if (effect.Binding.Entry == NativeBattleUltraWaves184Profile.Entry)
        {
            return EnterNativeUltraWaves184(graphics);
        }
        if (effect.Binding.Entry == NativeBattleElectrocute275Profile.Entry)
        {
            return EnterNativeElectrocute275(graphics);
        }
        if (effect.Binding.Entry == NativeBattleLaserEye71Profile.Entry)
        {
            return RunNativeLaserEye71(graphics, true);
        }
        if (effect.Binding.Entry == NativeBattleEerieSoundWave130Profile.Entry)
        {
            return EnterNativeEerieSoundWave130(graphics);
        }
        if (effect.Binding.Entry == NativeBattlePetrifyStare127Profile.Entry)
        {
            return EnterNativePetrifyStare127(graphics);
        }
        if (effect.Binding.Entry == NativeBattleAcid317Profile.Entry)
        {
            return EnterNativeAcid317(graphics);
        }
        if (effect.Binding.Entry == NativeBattleGatlingGun305Profile.Entry)
        {
            return EnterNativeGatlingGun305(graphics);
        }
        if (effect.Binding.Entry == NativeBattleDegenerator306Profile.Entry)
        {
            return EnterNativeDegenerator306(graphics);
        }
        if (effect.Binding.Entry == NativeBattleBadBreath300Profile.Entry)
        {
            return EnterNativeBadBreath300(graphics);
        }
        if (effect.Binding.Entry == NativeBattleMicroMissiles299Profile.Entry)
        {
            return EnterNativeMicroMissiles299(graphics);
        }
        if (effect.Binding.Entry == NativeBattleWhiteWind297Profile.Entry)
        {
            return EnterNativeWhiteWind297(graphics);
        }
        if (effect.Binding.Entry == NativeBattleCurse47Controller.Entry)
        {
            return EnterNativeCurse47(graphics);
        }
        if (effect.Binding.Entry == NativeBattleDoom8Controller.Entry)
        {
            return EnterNativeDoom8(graphics);
        }
        if (effect.Binding.Entry is 0x8B68C0 or 0x7C4460)
        {
            return EnterNativeDeath(graphics);
        }
        if (effect.Binding.Entry == 0x60AFB0)
        {
            return EnterNativeUltrasonic152(graphics);
        }
        if (effect.Binding.Entry == 0x5CC840)
        {
            return EnterNativeSuicide180(graphics);
        }
        if (effect.Binding.Entry == 0x85F4E0)
        {
            return EnterNativeConfuse36(graphics);
        }
        if (effect.Binding.Entry == 0x6BD1C0)
        {
            return EnterNativeSlow123(graphics);
        }
        if (effect.Binding.Entry == 0x6BB6B0)
        {
            return EnterNativeHaste125(graphics);
        }
        if (effect.Binding.Entry == 0x6C9380)
        {
            return EnterNativeFloat115(graphics);
        }
        if (effect.Binding.Entry == 0x6BDE80)
        {
            return EnterNativeBlind(graphics);
        }
        if (effect.Binding.Entry == 0x614290)
        {
            return EnterNativeRegen147(graphics);
        }
        if (effect.Binding.Entry == 0x67E730)
        {
            return EnterNativeFullCure280(graphics);
        }
        if (effect.Binding.Entry == 0x6BBDA0)
        {
            return EnterNativeFlare124(graphics);
        }
        if (effect.Binding.Entry == 0x6C99C0)
        {
            return EnterNativeZombie(graphics);
        }
        if (effect.Binding.Entry == 0x6BEFD0)
        {
            return EnterNativeStop119(graphics);
        }
        if (effect.Binding.Entry == 0x6791F0)
        {
            return EnterNativeRapture288(graphics);
        }
        if (effect.Binding.Entry == 0x6C16A0)
        {
            return EnterNativeBreak117(graphics);
        }
        if (effect.Binding.Entry == 0x60F690)
        {
            return EnterNativeUltima149(graphics);
        }
        if (effect.Binding.Entry == 0x5BC960)
        {
            return EnterNativeWall189(graphics);
        }
        if (effect.Binding.Entry == 0x8D8510)
        {
            return EnterNativeQuake38(graphics);
        }
        if (effect.Binding.Entry == 0x7844A0)
        {
            return RunNativeTheEnd85(graphics, true);
        }
        if (effect.Binding.Entry == 0x865600)
        {
            return EnterNativePain34(graphics);
        }
        if (effect.Binding.Entry == 0x6CF4A0)
        {
            return EnterNativeDispel109(graphics);
        }
        if (effect.Binding.Entry == 0x614D70)
        {
            return EnterNativeTornado146(graphics);
        }
        if (effect.Binding.Entry is 0x8513D0 or 0x803200)
        {
            return EnterNativeDrain39(graphics);
        }
        if (effect.Binding.Entry == 0x6CFCD0)
        {
            return EnterNativeBerserk108(graphics);
        }
        if (effect.Binding.Entry == 0x8465C0)
        {
            return EnterNativeDribble41(graphics);
        }
        if (effect.Binding.Entry == 0x83ECC0)
        {
            return EnterNativeShoot42(graphics);
        }
        if (effect.Binding.Entry == 0x7003C0)
        {
            return EnterNativeLanding17(graphics);
        }
        if (effect.Binding.Entry == 0x6CE210)
        {
            return EnterNativeControl110(graphics);
        }
        if (effect.Binding.Entry == 0x5CDF40)
        {
            return EnterNativeBombGlow179(graphics);
        }
        if (effect.Binding.Entry == 0x684EE0)
        {
            return RunNativeXatmDeath276(graphics, true);
        }
        if (effect.Binding.Entry == 0x627E60)
        {
            return RunNativeRayBomb10(graphics, true);
        }
        if (effect.Binding.Entry == 0x683F10)
        {
            return EnterNativeFunguarLaser277(graphics);
        }
        if (effect.Binding.Entry == 0x5D00F0)
        {
            return EnterNativeSandStorm176(graphics);
        }
        if (effect.Binding.Entry == 0x607C10)
        {
            return EnterNativeUltraWaves154(graphics);
        }
        if (effect.Binding.Entry == 0x609640)
        {
            return EnterNativeStickyWeb153(graphics);
        }
        if (effect.Binding.Entry == 0x8BE1F0)
        {
            return EnterNativeDarkMist13(graphics);
        }
        if (effect.Binding.Entry == 0x6CAC20)
        {
            return EnterNativeBadBreath113(graphics);
        }
        if (effect.Binding.Entry == 0x6120D0)
        {
            return EnterNativeMeltdown(graphics);
        }
        if (effect.Binding.Entry == 0x6CD920)
        {
            return EnterNativeAura(graphics);
        }
        if (effect.Binding.Entry == 0x5DEE60)
        {
            return EnterNativeGastricJuice169(graphics);
        }
        if (effect.Binding.Entry == 0x866010)
        {
            return EnterNativeShell(graphics);
        }
        if (effect.Binding.Entry == 0x86B9A0)
        {
            return EnterNativeProtect(graphics);
        }
        if (effect.Binding.Entry == 0x5E1750)
        {
            return EnterNativeSleepingGas168(graphics);
        }
        if (effect.Binding.Entry == 0x6D2AE0)
        {
            return EnterNativeReflect(graphics);
        }
        if (effect.Binding.Entry == 0x6EA500)
        {
            return EnterNativeStorm11(graphics);
        }
        if (effect.Binding.Entry is 0x6BD8E0 or 0x6BE960)
        {
            return EnterNativeStatusMagic(graphics);
        }
        if (effect.Binding.Entry == 0x5A6C20)
        {
            return EnterNativeRenzokukenVariant(graphics, effect.Binding.Renzokuken259Resources ?? throw new NotSupportedException("Native259 requires its original immutable resources."), NativeBattleRenzokukenVariantProfile.Renzokuken259);
        }
        if (effect.Binding.Entry == 0x58B930)
        {
            return EnterNativeRenzokukenVariant(graphics, effect.Binding.Renzokuken332Resources ?? throw new NotSupportedException("Native332 requires its original immutable resources."), NativeBattleRenzokukenVariantProfile.Renzokuken332);
        }
        if (effect.Binding.Entry == 0x589BD0)
        {
            return EnterNativeRenzokukenVariant(graphics, effect.Binding.Renzokuken333Resources ?? throw new NotSupportedException("Native333 requires its original immutable resources."), NativeBattleRenzokukenVariantProfile.Renzokuken333);
        }
        if (effect.Binding.Entry == 0x61DE70)
        {
            return EnterNativeRenzokuken141(graphics, effect.Binding.Renzokuken141Resources ?? throw new NotSupportedException("Native141 requires its original immutable resource bundle."));
        }
        if (effect.Binding.Entry == 0x5FF080)
        {
            return EnterNativeRenzokuken160(graphics);
        }
        if (effect.Binding.Entry == 0x5F90F0)
        {
            return EnterNativeFatedCircle162(graphics, effect.Binding.FatedCircle162Resources ?? throw new NotSupportedException("Native Fated Circle requires its original resources."));
        }
        if (effect.Binding.Entry == 0x5B7190)
        {
            return EnterNativeShot193(graphics);
        }
        if (effect.Binding.Entry == 0x6CBBE0)
        {
            return EnterNativeDuel112(graphics, effect.Binding.Duel112Resources ?? throw new NotSupportedException("Native Duel112 requires its original resources."));
        }
        if (effect.Binding.Entry == 0x6897E0)
        {
            return EnterNativeDuel274(graphics, effect.Binding.Duel274Resources ?? throw new NotSupportedException("Native Duel opening requires its original resources."));
        }
        if (effect.Binding.Entry == 0x6A0D20)
        {
            return EnterNativeDuel212(graphics, effect.Binding.Duel212Resources ?? throw new NotSupportedException("Native Burning Rave requires its original resources."));
        }
        if (effect.Binding.Entry == 0x6959E0)
        {
            return EnterNativeDuel216(graphics, effect.Binding.Duel216Resources ?? throw new NotSupportedException("Native Different Beat requires its original resources."));
        }
        if (effect.Binding.Entry == 0x69AB60)
        {
            return EnterNativeDuel215(graphics, effect.Binding.Duel215Resources ?? throw new NotSupportedException("Native Duel215 requires its original resources."));
        }
        if (effect.Binding.Entry == 0x6391D0)
        {
            return EnterNativeNoMercy200(graphics, effect.Binding.NoMercy200Resources ?? throw new NotSupportedException("Native No Mercy requires its original resources."));
        }
        if (effect.Binding.Entry == 0x5E9470)
        {
            return EnterNativeLionHeart165(graphics, effect.Binding.LionHeart165Resources ?? throw new NotSupportedException("Native Lion Heart requires its original resources."));
        }
        if (effect.Binding.Entry == 0x5F0260)
        {
            return EnterNativeBlastingZone164(graphics, effect.Binding.BlastingZone164Resources ?? throw new NotSupportedException("Native Blasting Zone requires its original resources."));
        }
        if (effect.Binding.Entry == 0x5F5B80)
        {
            return EnterNativeRoughDivide163(graphics, effect.Binding.RoughDivide163Resources ?? throw new NotSupportedException("Native Rough Divide requires its original resources."));
        }
        if (effect.Binding.Entry == 0x78D7B0)
        {
            return EnterNativeAngelWing84(graphics);
        }
        if (effect.Binding.Entry == 0x77F250)
        {
            return EnterNativeRinoa86(graphics);
        }
        if (effect.Binding.Entry == 0x773870)
        {
            return EnterNativeRinoa87(graphics);
        }
        if (effect.Binding.Entry == 0x7684F0)
        {
            return EnterNativeRinoa88(graphics);
        }
        if (effect.Binding.Entry == 0x6F9CF0)
        {
            return EnterNativeRinoa89(graphics, effect.Binding.Rinoa89Resources ?? throw new NotSupportedException("Native Wishing Star requires its original resources."));
        }
        if (effect.Binding.Entry == 0x5AA3E0)
        {
            return EnterNativeShot198(graphics);
        }
        if (effect.Binding.Entry == 0x5AED30)
        {
            return EnterNativeShot197(graphics);
        }
        if (effect.Binding.Entry == 0x5B2A40)
        {
            return EnterNativeShot195(graphics);
        }
        if (effect.Binding.Entry == 0x5B4C60)
        {
            return EnterNativeShot194(graphics);
        }
        if (effect.Binding.Entry == 0x5B96F0)
        {
            return EnterNativeShot192(graphics);
        }
        if (effect.Binding.Entry == 0x5B1900)
        {
            return EnterNativeShot196(graphics);
        }
        if (effect.Binding.Entry == 0x5BE340)
        {
            return EnterNativeShot188(graphics);
        }
        if (effect.Binding.Entry == 0x84D030)
        {
            return RunNativeScan40(graphics, true);
        }
        if (effect.Binding.Entry == 0xA9B200)
        {
            return EnterNativeWaterA9B200(graphics);
        }
        if (NativeBattleCinematicProfile.IsSupported(effect.Binding.Effect))
        {
            return EnterNativeCinematic(graphics);
        }
        if (effect.Binding.Effect == 291)
        {
            return RunNativePandemona291(graphics, true);
        }
        if (effect.Binding.Effect == 191)
        {
            return RunNativeDoomtrain191(graphics, true);
        }
        if (effect.Binding.Effect == 95)
        {
            return RunNativeSiren95(graphics, true);
        }
        if (effect.Binding.Effect == 90)
        {
            return RunNativeTonberry90(graphics, true);
        }
        if (effect.Binding.Effect == 278)
        {
            return RunNativeCarbuncle278(graphics, true);
        }
        if (effect.Binding.Effect == 325)
        {
            return RunNativeDiablos325(graphics, true);
        }
        if (effect.Binding.Entry == 0x5D1AD0)
        {
            return RunNativeHoly175(graphics, true);
        }
        if (effect.Binding.Entry == 0x6D3DB0)
        {
            return RunNativeThundaga105(graphics, true);
        }
        if (effect.Binding.Entry is 0x58D760 or 0x58D930 or 0x58DB10 or 0x58DCF0)
        {
            return RunNativeGilgamesh(graphics, true);
        }
        if (effect.Binding.Entry == 0x5A8750)
        {
            return RunNativeCactuar199(graphics, true);
        }
        if (effect.Binding.Entry == 0x5C0D50)
        {
            return RunNativeShiva185(graphics, true);
        }
        if (effect.Binding.Entry == 0x70D390)
        {
            return EnterNativeChoco70D390(graphics);
        }
        if (effect.Binding.Entry == 0x717D30)
        {
            return EnterNativeChoco717D30(graphics);
        }
        if (effect.Binding.Entry == 0x721860)
        {
            return EnterNativeChoco721860(graphics);
        }
        if (effect.Binding.Entry == 0x729A60)
        {
            return EnterNativeChoco729A60(graphics);
        }
        if (effect.Binding.Entry == 0x731DE0)
        {
            return EnterNativeMoogle731DE0(graphics);
        }
        if (effect.Binding.Entry == 0x759370)
        {
            return EnterNativeRush759370(graphics);
        }
        if (effect.Binding.Entry is 0x74E390 or 0x7475F0)
        {
            return EnterNativeAngeloRestoration(graphics);
        }
        if (effect.Binding.Entry == 0x755130)
        {
            return EnterNativeSearch755130(graphics);
        }
        if (effect.Binding.Entry == 0x6652C0)
        {
            return EnterNativeEffect311(graphics);
        }
        if (effect.Binding.Entry == 0x6D1310)
        {
            return EnterNativeEffect107(graphics);
        }
        if (effect.Binding.Entry == 0x6C0C80)
        {
            return EnterNativeAero6C0CB0(graphics);
        }
        if (effect.Binding.Entry == 0x6D6310)
        {
            return EnterNativeBlizzaga6D6320(graphics);
        }
        if (effect.Binding.Entry == 0x6DA300)
        {
            return EnterNativeBlizzara6DA310(graphics);
        }
        if (effect.Binding.Entry == 0x6DC2D0)
        {
            return EnterNativeThundara6DC2E0(graphics);
        }
        if (effect.Binding.Entry == 0x61BFE0)
        {
            return EnterNativeFiraga61BFE0(graphics);
        }
        if (effect.Binding.Entry == 0x61CE10)
        {
            return EnterNativeFira61CE10(graphics);
        }
        if (effect.Binding.Entry == 0x61ABA0)
        {
            return EnterNativeBlizzard61ABA0(graphics);
        }
        if (effect.Binding.Entry is 0x8D6A00 or 0x81FA10)
        {
            return EnterNativeCure8D6A00(graphics);
        }
        if (effect.Binding.Entry is 0x8649C0 or 0x8D4450)
        {
            return EnterNativeLife(graphics);
        }
        if (effect.Binding.Entry == 0x8D5080)
        {
            return EnterNativeDouble8D5080(graphics);
        }
        if (effect.Binding.Entry == 0x89DEF0)
        {
            return EnterNativeTriple89DEF0(graphics);
        }
        if (effect.Binding.Entry == 0x89A6B0)
        {
            return EnterNativeBio22_89A6B0(graphics);
        }
        if (effect.Binding.Entry is 0x889D60 or 0x81C800)
        {
            return EnterNativeCura889D60(graphics);
        }
        if (effect.Binding.Entry is 0x8AB140 or 0x7E58C0 or 0x7A51B0 or 0x79EE30)
        {
            return RunNativeActorDirector(graphics, true);
        }
        if (effect.Binding.Entry == NativeBattleEffect20Profile.Entry)
        {
            return RunNativeEffect20(graphics, true);
        }
        if (effect.Binding.Entry == NativeBattleDevour62Profile.Entry)
        {
            return RunNativeDevour62(graphics, true);
        }
        if (effect.Binding.Entry == NativeBattleMeltingBubble43Profile.Entry)
        {
            return RunNativeMeltingBubble43(graphics, true);
        }
        if (effect.Binding.Entry == NativeBattleWindBlast29Profile.Entry)
        {
            return RunNativeWindBlast29(graphics, true);
        }
        if (effect.Binding.Entry == NativeBattleEffect60Profile.Entry)
        {
            return RunNativeEffect60(graphics, true);
        }
        if (effect.Binding.Entry == NativeBattlePsychoBlast23Profile.Entry)
        {
            return RunNativePsychoBlast23(graphics, true);
        }
        if (effect.Binding.Entry == NativeBattleRaldo19Profile.Entry)
        {
            return RunNativeRaldo19(graphics, true);
        }
        if (effect.Binding.Entry == NativeBattleDrinkMagic37Profile.Entry)
        {
            return RunNativeDrinkMagic37(graphics, true);
        }
        if (effect.Binding.Entry is 0x7BE2D0 or 0x7CBDB0)
        {
            return RunNativeMightyGuard(graphics, true);
        }
        if (effect.Binding.Entry is 0x666DD0 or 0x6664A0)
        {
            return RunNativeHero(graphics, true);
        }
        if (effect.Binding.Entry == NativeBattleDrawStock15Profile.Entry)
        {
            return RunNativeDrawStock15(graphics, true);
        }
        if (effect.Binding.Entry is 0x808820 or 0x7D1E80)
        {
            return RunNativeAqua(graphics, true);
        }
        if (effect.Binding.Entry is 0x87DB10 or 0x817410)
        {
            return EnterNativeCuraga87DB10(graphics);
        }
        if (effect.Binding.Entry is 0x88CF70 or 0x883110 or 0x7FD150 or 0x7DEA20)
        {
            return EnterNativeRestoration(graphics);
        }
        if (effect.Binding.Entry is 0x8CE3D0 or 0x811F70 or 0x7F7CB0 or 0x7F2810)
        {
            return EnterNativeRecovery(graphics);
        }
        if (effect.Binding.Entry == 0x6C3550)
        {
            NativeBattleQuezacotlEntry entered = NativeBattleQuezacotl.Enter6C3640(NativeEffectMemory ?? throw new NotSupportedException("Native Quezacotl requires its retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Quezacotl requires its retained55CBD2 random owner."), nativeActionPackets, index => (graphics.Models[index].Transform ?? throw new NotSupportedException("Native6C3640 requires the actual graphical model WORD32 position.")).Position28.Z);
            NativeEffectMemory = entered.Memory;
            nativeCrtRandom = entered.Random;
            return graphics with { OriginalEffect = entered.Effect };
        }
        if (effect.Binding.Entry == 0x6298A0)
        {
            if ((effect.Parameters.Flags79 & 1) == 0)
            {
                NativeBattleEffectProgramResources resources = effect.Binding.ProgramResources ?? throw new NotSupportedException("Native Fire6298A0 requires its original DED5FC camera descriptor and sprite resources.");
                NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Native Fire lost its original571870 arena owner.");
                NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Fire requires the retained505E30 upload slots and VRAM; migrate the named standalone snapshot explicitly.");
                textures = textures.Register5099A0(resources.Animation);
                NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new InvalidDataException("Native Fire lost its loaded mag001.tim resource.");
                (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
                NativeEffectMemory = memory with { Textures = textures };
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Fire camera registration requires its actual camera owner.")).RegisterControlResource(textures.Animation);
            }
            return graphics with { OriginalEffect = NativeBattleFireEffect.InitializePools(effect, nativeActionPackets) };
        }
        if (effect.Binding.Entry == 0x700C10)
        {
            if ((effect.Parameters.Flags79 & 1) == 0)
            {
                NativeBattleEffect3Resources resources = effect.Binding.Effect3Resources ?? throw new NotSupportedException("Native effect3 requires its original embedded TIM and camera resource.");
                NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Native effect3 lost its571870 arena owner.");
                NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native effect3 requires the retained505E30 upload slots and VRAM.");
                textures = textures.Register5099A0(resources.Animation);
                (textures, _) = textures.EnqueueTim505E30(resources.Texture);
                NativeEffectMemory = memory with { Textures = textures };
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native effect3 requires its camera owner.")).RegisterControlResource(textures.Animation);
            }
            return graphics with { OriginalEffect = NativeBattleEffect3.InitializePools(effect) };
        }
        throw new NotSupportedException($"Native effect{effect.Binding.Effect} entry{effect.Binding.Entry:X} requires its original module callback and graphics-resource contract.");
    }

    private bool NativeOriginalSoundBusy()
    {
        return (NativeEffectMemory ?? throw new NotSupportedException("Native501190 requires its published501230 sound flags.")).SoundPublishedFlags != 0;
    }

    private bool NativeOriginalEffectBusy(NativeBattleGraphicsState graphics)
    {
        //A replacement DWORD100 can reference an effect resource or a row in the current shared file staging.
        if (graphics.Models.Any(model => (model.Flags0 & 2) != 0 && (model.ResourceMagicId is not null || model.ActiveStagedResourceRow100 is not null)))
        {
            return true;
        }
        if ((NativeEffectMemory ?? throw new NotSupportedException("Native50AE80 requires its retained task pointer owner.")).ReadReturnedTask5044B0() != 0)
        {
            return true;
        }
        return NativeCameraTaskMask != 0 || NativeOriginalSoundBusy();
    }

    private bool NativeOriginalEffectDescriptorReleased(NativeBattleGraphicsState graphics)
    {
        return (NativeEffectMemory ?? throw new NotSupportedException("Native effect completion requires its canonical task pointer owner.")).ReadReturnedTask5044B0() == 0;
    }

    private NativeBattleGraphicsState PublishNativeOriginalEffectTitle(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        if (context.PendingData is null)
        {
            return graphics;
        }
        string text = context.PendingData.FormattedText ?? throw new NotSupportedException("Native50A750 requires the actual header title string.");
        byte duration = unchecked((byte)(8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native50A750 requires the saved message-speed BYTE.")) + 8));
        if (duration != 0)
        {
            events.Add(new(Frame, "NativeText", null, null, duration, text) { NativeMessageMode = 1 });
        }
        return graphics with { ActionGraphics = context with { PendingData = null } };
    }

    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeOriginalGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        if (phase == 0)
        {
            if (ModelsBusy(graphics.Models))
            {
                return (graphics, phase, false);
            }
            phase = 1;
        }
        if (phase == 1)
        {
            graphics = BeginNativeOriginalEffect(graphics, NativeBattleActionGraphics.Word(nativeActionPackets.Headers[context.HeaderIndex], 6));
            return (graphics, 2, false);
        }
        if (phase == 2)
        {
            int io = NativeEffectIoResult ?? throw new NotSupportedException("Native50B190 requires the original signed effect IO result.");
            return (graphics, io < 0 || graphics.ModuleEffects.PublishedCount != 0 ? phase : (byte)3, false);
        }
        if (phase == 3)
        {
            graphics = PublishNativeOriginalEffectTitle(graphics, context);
            graphics = PrepareNativeOriginalEffectParameters(graphics, context);
            graphics = PrepareNativeOriginalEffectActor(graphics, context.ActorIndex);
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50B190 requires the actual camera selector owner.")).SetActionSelector(65535);
            graphics = InvokeNativeOriginalEffect(graphics);
            return (graphics with { Flags = graphics.Flags | 0x10 }, 4, false);
        }
        if (phase == 4)
        {
            if (NativeOriginalEffectBusy(graphics))
            {
                return (graphics, phase, false);
            }
            graphics = CompleteNativeHeaderGraphics(graphics);
            NativeEffectMemory = (NativeEffectMemory ?? throw new InvalidDataException("Native50B190 lost its effect memory owner.")) with { Active = 0 };
            return (graphics with { Flags = graphics.Flags & ~0x10u }, phase, true);
        }
        return (graphics, phase, false);
    }
}
