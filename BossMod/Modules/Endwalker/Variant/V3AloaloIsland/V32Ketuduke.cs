namespace BossMod.Endwalker.VariantCriterion.V3AloaloIsloand.V22Ketuduke;

public enum OID : uint
{
    Helper = 0x233C, // R0.500, x?, Helper type
    Actor1ea1a1 = 0x1EA1A1, // R2.000, x?, EventObj type : Not rect20
    Actor1e8536 = 0x1E8536, // R2.000, x?, EventObj type : not rect20
    _Gen_ = 0x40B2, // R0.500, x? : Not the 20rect
    Matsya = 0x3FE7, // R1.900, x?
    Actor1e8f2f = 0x1E8F2F, // R0.500, x?, EventObj type : not rect20
    Ketuduke = 0x4091, // R8.000, x?
    ZealBlindZozone = 0x4096, // R0.500, x?
    SpringCrystal = 0x4092, // R4.200, x?
    SpringCrystal1 = 0x4093, // R4.200, x?
    SphereShatter = 0x1EB936, // R0.500, x?, EventObj type : Mob facing for the rectangle
    SummonedApa = 0x4113, // R2.880, x?
    Actor1eb937 = 0x1EB937, // R0.500, x?, EventObj type
    AiryBubble = 0x4095, // R1.300, x?
}


public enum AID : uint
{
    _AutoAttack_ = 35448, // Ketuduke->player, no cast, single-target
    TidalRoarVisual = 35493, // Ketuduke->self, 5.0s cast, single-target
    TidalRoar = 35494, // Helper->self, no cast, range 100 circle
    SpringCrystals = 35449, // Ketuduke->self, 2.2+0.8s cast, single-target : Summons the crystals to the board
    _Weaponskill_ = 35450, // SpringCrystal->self, no cast, single-target
    SaturateCircle = 35452, // SpringCrystal->self, 3.0s cast, range 8 circle
    _Weaponskill_1 = 35451, // SpringCrystal1->self, no cast, single-target
    SaturateRectangle = 35454, // SpringCrystal1->self, 3.0s cast, range 76 width 10 rect
    BubbleNetVisual = 35456, // Ketuduke->self, 4.1+0.9s cast, single-target
    BubbleNet = 35457, // Helper->self, 5.0s cast, range 65 circle
    FlukeTyphoonVisual = 35460, // Ketuduke->self, 3.0s cast, single-target
    FlukeTyphoon1 = 35461, // Helper->self, 5.0s cast, range 40 width 40 rect
    Saturate2 = 35455, // SpringCrystal1->self, 1.0s cast, range 76 width 10 rect
    StrewnBubbles = 35462, // Ketuduke->self, 2.2+0.8s cast, single-target : Bubbles that move across the arena in pairs
    SphereShatter = 35463, // Helper->self, no cast, range 20 width 10 rect :TODO Find the tell here
    EncroachingTwintides = 35487, // Ketuduke->self, 5.0s cast, range ?-60 donut
    NearTide = 35486, // Ketuduke->self, 1.5s cast, range 14 circle : Look for a tell here. It is hard to run out from if melee
    _Ability_ = 35447, // Ketuduke->location, no cast, single-target
    Summon = 35470, // ZealBlindZozone->self, 3.0s cast, single-target
    WaterIII = 36116, // SummonedApa->self, 10.0s cast, single-target
    WaterIII1 = 36125, // SummonedApa->player, no cast, single-target
    Hydrobomb = 35489, // Ketuduke->self, 2.2+0.8s cast, single-target
    Saturate3 = 35453, // SpringCrystal->self, 1.0s cast, range 8 circle
    Hydrobomb1 = 35490, // Helper->location, 3.0s cast, range 5 circle
    BlowingBubbles = 35464, // Ketuduke->self, 2.2+0.8s cast, single-target
    RecedingTwintides = 35485, // Ketuduke->self, 5.0s cast, range 14 circle
    FarTide = 35488, // Ketuduke->self, 1.5s cast, range ?-60 donut
}

public enum SID : uint
{
    _Gen_ = 3745, // none->SpringCrystal1/SpringCrystal, extra=0xC8
    _Gen_VulnerabilityUp = 1789, // Ketuduke->player, extra=0x1

}

public enum TetherID : uint
{
    StretchTether = 1, // SummonedApa->player
}


    // _AutoAttack_ = 35448, // Ketuduke->player, no cast, single-target
    // TidalRoarVisual = 35493, // Ketuduke->self, 5.0s cast, single-target
    // TidalRoar = 35494, // Helper->self, no cast, range 100 circle
sealed class TidalRoar(BossModule module) : Components.RaidwideCast(module, (uint)AID.TidalRoar);
    // SpringCrystals = 35449, // Ketuduke->self, 2.2+0.8s cast, single-target : Summons the crystals to the board
    // _Weaponskill_ = 35450, // SpringCrystal->self, no cast, single-target
    // SaturateCircle = 35452, // SpringCrystal->self, 3.0s cast, range 8 circle
sealed class SaturateCircle(BossModule module) : Components.SimpleAOEs(module,  (uint)AID.SaturateCircle, 8f);
    // _Weaponskill_1 = 35451, // SpringCrystal1->self, no cast, single-target
    // SaturateRectangle = 35454, // SpringCrystal1->self, 3.0s cast, range 76 width 10 rect
sealed class SaturateRectangle(BossModule module) : Components.SimpleAOEs(module,  (uint)AID.SaturateRectangle, new AOEShapeRect(76f, 5f));
    // BubbleNetVisual = 35456, // Ketuduke->self, 4.1+0.9s cast, single-target
    // BubbleNet = 35457, // Helper->self, 5.0s cast, range 65 circle
sealed class BubbleNet(BossModule module) : Components.RaidwideCast(module, (uint)AID.BubbleNet);
    // FlukeTyphoonVisual = 35460, // Ketuduke->self, 3.0s cast, single-target
    // FlukeTyphoon1 = 35461, // Helper->self, 5.0s cast, range 40 width 40 rect
// sealed class FlukeTyphoon(BossModule module) : Components.SimpleKnockbacks(module,  (uint)AID.FlukeTyphoon1, 20f, shape: new AOEShapeRect(40f, 20f));
// Not really a knockback for players, it will trigger the auto solver to run.
sealed class FlukeTyphoon(BossModule module) : Components.RaidwideCast(module,  (uint)AID.FlukeTyphoon1);

// Saturate2 = 35455, // SpringCrystal1->self, 1.0s cast, range 76 width 10 rect
sealed class Saturate2(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Saturate2, new AOEShapeRect(76f, 5f));
// StrewnBubbles = 35462, // Ketuduke->self, 2.2+0.8s cast, single-target
sealed class StrewnBubbles(BossModule module)
        : Components.Voidzone(module, 3f, m => m.Enemies((uint)OID.AiryBubble).Where(z => z.EventState != 7));
// SphereShatter = 35463, // Helper->self, no cast, range 20 width 10 rect
sealed class SphereShatterVZ(BossModule module)
    : Components.Voidzone(module, 3f, m => m.Enemies((uint)OID.SphereShatter).Where(z => z.EventState != 7))
{
    private AOEShapeRect rect = new AOEShapeRect(20f, 5f);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var aoes = new List<AOEInstance>();
        foreach (var source in Sources(Module))
        {
            if (ArenaProjectionLayerParticipantApplies(source, ArenaProjectionLayer, RestrictToArenaProjectionLayer))
                aoes.Add(new(rect, source.Position, source.Rotation, arenaProjectionLayer: ArenaProjectionLayer, restrictToArenaProjectionLayer: RestrictToArenaProjectionLayer));
        }
        return CollectionsMarshal.AsSpan(aoes);
    }
}


sealed class SphereShatter(BossModule module) : Components.SimpleAOEs(module,  (uint)AID.SphereShatter, new AOEShapeRect(20f, 5f));
// EncroachingTwintides = 35487, // Ketuduke->self, 5.0s cast, range ?-60 donut
sealed class EncroachingTwintides(BossModule module) : Components.SimpleAOEs(module, (uint)AID.EncroachingTwintides, new AOEShapeDonut(8f, 60f));
    // NearTide = 35486, // Ketuduke->self, 1.5s cast, range 14 circle : Look for a tell here. It is hard to run out from if melee
sealed class NearTide(BossModule module) : Components.SimpleAOEs(module, (uint)AID.NearTide, 14f);
    // _Ability_ = 35447, // Ketuduke->location, no cast, single-target
    // Summon = 35470, // ZealBlindZozone->self, 3.0s cast, single-target
    // WaterIII = 36116, // SummonedApa->self, 10.0s cast, single-target : Tether? Stretch Tether cast probably
sealed class WaterIII(BossModule module) : Components.StretchTetherSingle(module, (uint)TetherID.StretchTether, 35f, default, (uint)AID.WaterIII1, (uint)OID.SummonedApa);
    // WaterIII1 = 36125, // SummonedApa->player, no cast, single-target
    // Hydrobomb = 35489, // Ketuduke->self, 2.2+0.8s cast, single-target
    // Saturate3 = 35453, // SpringCrystal->self, 1.0s cast, range 8 circle
sealed class Saturate3(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Saturate3, new AOEShapeCircle(8f));
    // Hydrobomb1 = 35490, // Helper->location, 3.0s cast, range 5 circle
sealed class HydroBomb(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Hydrobomb1, 5f);
    // BlowingBubbles = 35464, // Ketuduke->self, 2.2+0.8s cast, single-target
    // RecedingTwintides = 35485, // Ketuduke->self, 5.0s cast, range 14 circle
sealed class RecedingTwintides(BossModule module): Components.SimpleAOEs(module, (uint)AID.RecedingTwintides, 14f);
    // FarTide = 35488, // Ketuduke->self, 1.5s cast, range ?-60 donut
sealed class FarTide(BossModule module) : Components.SimpleAOEs(module, (uint)AID.FarTide, new AOEShapeDonut(6f, 60f));





sealed class KetudukeStates : StateMachineBuilder
{
    public KetudukeStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<TidalRoar>()
            .ActivateOnEnter<SaturateCircle>()
            .ActivateOnEnter<SaturateRectangle>()
            .ActivateOnEnter<BubbleNet>()
            .ActivateOnEnter<FlukeTyphoon>()
            .ActivateOnEnter<Saturate2>()
            .ActivateOnEnter<StrewnBubbles>()
            .ActivateOnEnter<SphereShatter>()
            .ActivateOnEnter<SphereShatterVZ>()
            .ActivateOnEnter<EncroachingTwintides>()
            .ActivateOnEnter<NearTide>()
            .ActivateOnEnter<WaterIII>()
            .ActivateOnEnter<Saturate3>()
            .ActivateOnEnter<HydroBomb>()
            .ActivateOnEnter<RecedingTwintides>()
            .ActivateOnEnter<FarTide>()
            ;
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    PrimaryActorOID = (uint)OID.Ketuduke,
    Contributors = "wen",
    Category = BossModuleInfo.Category.VariantCriterion,
    GroupID = 961u,
    NameID = 12605u,
    SortOrder = 1)]


// Hyperborea coords are (-790f, 14, -405f)
public sealed class Ketuduke(WorldState ws, Actor primary) : BossModule(ws, primary, new(-790f, -395f), new ArenaBoundsSquare(20f));
