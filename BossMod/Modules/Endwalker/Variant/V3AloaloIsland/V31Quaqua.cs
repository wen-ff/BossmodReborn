namespace BossMod.Endwalker.VariantCriterion.V3AloaloIsloand.V21Quaqua;

public enum OID : uint
{
    Actor1e8f2f = 0x1E8F2F, // R0.500, x?, EventObj type
    Actor1e8fb8 = 0x1E8FB8, // R2.000, x?, EventObj type
    HammerTower = 0x40E0, // R1.000, x?
    AethericCharge = 0x40BB, // R1.500-2.200, x?, Helper type
    Quaqua = 0x40BA, // R5.250, x?
    Helper = 0x233C, // R0.500, x?, Helper type
    AnalaFamiliar1 = 0x40BE, // R1.500, x?
    _Gen_Actor1e8536 = 0x1E8536, // R2.000, x?, EventObj type
    DrakeFamiliar = 0x4135, // R1.000, x?
    AnalaFamiliar = 0x4134, // R1.000, x?
}

public enum AID : uint
{
    FlameDance = 36081, // 410E->location, 3.0s cast, range 6 circle
    Summon = 35718, // 40B7->self, 3.0s cast, single-target
    _Ability_ = 35719, // 40B7->self, no cast, single-target
    _Ability_1 = 35727, // Quaqua->location, no cast, single-target
    MadeMagic = 35732, // Quaqua->self, 5.0s cast, range 50 circle
    ArcaneArmaments = 35720, // Quaqua->self, 8.0s cast, single-target
    _Weaponskill_ = 35721, // 233C->self, no cast, single-target
    RavagingAxe = 35722, // AethericCharge->self, 2.0s cast, range 14 circle
    RingingQuoits = 35723, // AethericCharge->self, 2.0s cast, range 5-18 donut
    ArcaneArmaments1 = 35724, // Quaqua->self, 5.0s cast, single-target
    HammerLanding = 35725, // Quaqua->location, 8.0s cast, range 40 circle
    HammerLanding1 = 35726, // Quaqua->location, no cast, range 40 circle
    VioletStorm = 35733, // Quaqua->self, 5.5s cast, range 32 120.000-degree cone
    Howl = 35734, // Quaqua->self, 4.0s cast, single-target
    ScaldingWaves = 35735, // AnalaFamiliar1->self, 5.0s cast, range 50 width 8 rect
    ScaldingWaves1 = 35736, // AnalaFamiliar->self, no cast, range 50 width 4 rect
}

public enum SID : uint
{
    Transfiguration = 2548, // none->AethericCharge, extra=0x203/0x204
    ActivateStatus = 2056, // Quaqua->HammerTower/Quaqua/AethericCharge, extra=0x272/0x288/0x273/0x274 : Seems like an activation trigger for various OIDs where extra is order of pounce 272/273/274 for Hammerfall?
    _Gen_Bleeding = 3077, // none->player, extra=0x0
    _Gen_Bleeding1 = 3078, // none->player, extra=0x0
    VulnerabilityUp = 1789, // AnalaFamiliar->player, extra=0x1
}

public enum TetherID : uint
{
    ArcaneAxeTether = 256, // AethericCharge->Quaqua : leads to circle aoe
    ArcaneQuoitsTether = 257, // AethericCharge->Quaqua : leads to donut aoe
    FirstKnockbackTether = 249, // _Gen_->Quaqua
}


// _Weaponskill_MadeMagic = 35732, // Quaqua->self, 5.0s cast, range 50 circle
sealed class MadeMagic(BossModule module) : Components.RaidwideCast(module, (uint)AID.MadeMagic);

//Arcane armaments has a different tether for which aoe is going to land. the axe is circle, the weird loop icon is quoits : donut
sealed class ArcaneArmaments(BossModule module) : Components.GenericAOEs(module, (uint)AID.ArcaneArmaments)
{
    private readonly List<AOEInstance> _aoes = [];
    private readonly AOEShapeCircle _circle = new(14f);
    private readonly AOEShapeDonut _donut = new(5f, 18f);

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.RavagingAxe or (uint)AID.RingingQuoits)
        {
            if (_aoes.Count > 0)
            {
                _aoes.RemoveAt(0);
            }
        }
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        var target = WorldState.Actors.Find(tether.Target);
        if (tether.ID == (uint)TetherID.ArcaneAxeTether && target?.EventState != 7)
        {
            _aoes.Add(new(_circle, source.Position));
        }
        else if (tether.ID == (uint)TetherID.ArcaneQuoitsTether && target?.EventState != 7)
        {
            _aoes.Add(new(_donut, source.Position));
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);
}

// _Weaponskill_ = 35721, // 233C->self, no cast, single-target


// _Weaponskill_HammerLanding = 35725, // Quaqua->location, 8.0s cast, range 40 circle
// Seems like there can be more than one knockback and we should goal zone into the next one if possible
// HammerLanding Objects do spawn one after another. First one has a tether to point out which will be next.
sealed class HammerLandingKB(BossModule module)
    : Components.SimpleKnockbacks(module, (uint)AID.HammerLanding, 20f, maxCasts: 3, shape: new AOEShapeCircle(40f))
{
    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (actor.OID == (uint)OID.HammerTower && status.ID == (uint)SID.ActivateStatus)
        {
            var activation = WorldState.FutureTime(8d);
            Casters.Add(new(actor.Position, 20f, activation, Shape));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        // Remove one knockback each time the knockback happens
        if (spell.Action.ID is (uint)AID.HammerLanding1 or (uint)AID.HammerLanding)
        {
            Casters.RemoveAll(kb => kb.Origin.AlmostEqual(spell.TargetXZ, 0.5f));
        }
    }
}

// Used voidzones to show where boss will jump. 3f as radius is just an arbitrary number. Can be adjusted if we need
// a little more room to predict our knockbacks.
class HammerVoidzone(BossModule module)
    : Components.Voidzone(module, 3f, m => m.Enemies((uint)OID.HammerTower).Where(z => z.EventState != 7))
{
    private bool _isCastingHammer;
    // Waiting room is the spot where this tower objects hang out until the status change happens to make them a knockback source.
    private WPos _waitingRoom = new WPos((float)-527.022, (float)93.980);
    // Keep track of where boss lands. If boss has landed near a voidzone, then the voidzone should no longer exist.
    private List<WPos> _landed = new List<WPos>();

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var aoes = new List<AOEInstance>();
        foreach (var source in Sources(Module))
        {
            bool alreadyHit = false;
            foreach (var landingSpot in _landed)
            {
                if (landingSpot.AlmostEqual(source.Position, 0.5f))
                    alreadyHit = true;
            }
            // make the void zone disappear once the boss jumps to the spot.
            if (ArenaProjectionLayerParticipantApplies(source, ArenaProjectionLayer, RestrictToArenaProjectionLayer) && _isCastingHammer && source.Position != _waitingRoom && !alreadyHit)
                aoes.Add(new(Shape, source.Position, arenaProjectionLayer: ArenaProjectionLayer, restrictToArenaProjectionLayer: RestrictToArenaProjectionLayer));
        }
        return CollectionsMarshal.AsSpan(aoes);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.HammerLanding)
        {
            _isCastingHammer = true;
        }
    }

    // First jump happens on the casted HammerLanding
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.HammerLanding)
            _landed.Add(spell.LocXZ);
    }

    // Next jumps happen on instant cast HammerLanding1
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.HammerLanding1)
            _landed.Add(spell.TargetXZ);

        // After 3 jumps the knockback sequence is finished.
        if (_landed.Count >= 3)
        {
            _landed.Clear();
            _isCastingHammer = false;
        }
    }
}


// _Weaponskill_VioletStorm = 35733, // Quaqua->self, 5.5s cast, range 32 120.000-degree cone
sealed class VioletStorm(BossModule module) : Components.SimpleAOEs(module, (uint)AID.VioletStorm, new AOEShapeCone(32f, 60f.Degrees()));
// _Weaponskill_Howl = 35734, // Quaqua->self, 4.0s cast, single-target
// _Weaponskill_ScaldingWaves = 35735, // AnalaFamiliar1->self, 5.0s cast, range 50 width 8 rect

// This is the fire mermen who cast a single rectangle then instant cast additional rectangles across the arena like lava waves moving outwards from first rectangle.
sealed class ScaldingWaves(BossModule module) :  Components.SimpleAOEs(module, (uint)AID.ScaldingWaves, new AOEShapeRect(50f, 4f));
// _Weaponskill_ScaldingWaves1 = 35736, // AnalaFamiliar->self, no cast, range 50 width 4 rect
/*
 * These instants are immediately right and left of the initial cast. They continue until they hit the edge of the arena.
 *
 * So on first cast we have to calculate how much room is left in the arena for these casts divided by aoe width and based on orientation of first casts.
 * Then spin up new rectangle aoes with activation times that are spaced by whichever number they are in the rotation.
 * Reference the crown of immaculate maybe for some ideas on the aoe generation.
 */


/*
 *
 * Crown of the immaculate reference code
/*
 * This is the wavy aoe that rotates across the arena.
 * Seems like the first round is 8 casts of instant and second round is 12 casts.
 * /
 */
sealed class ScaldingWavesCascade(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ScaldingWaves, new AOEShapeRect(50f, 4f), maxCasts: 4)
{
    // Shape for the instant cast cascading waves
    AOEShapeRect thinRect = new (50f, 2f);
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.ScaldingWaves)
            BespokeAOE(caster, spell);
    }

    public void BespokeAOE(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.ScaldingWaves)
        {
            var rotation = spell.Rotation;
            /*
             * spin up aoe's for the rotating aoe.  _genAOE is a magic number because we do not have a good way to predict
             * how many will be cast. Instead we make extra and then clear the Casters queue when the Thorn actor
             * that is in the Arena.Center position dies.
             */
            //var _genAOE = 26;

            /*
             * for every piece we move 4 units left or right across the x axis.
             * We will have to find out if there is a possibility for them to be left/right rects instead of up down/rects also. We would have to account for rotation then
             */



            // x position in the x line and we only want to generate enough aoes to cross the remaining space and no more.
            // could determine based off which coordinate is on an axis line. That is the direction of travel across that axis.
            // The center of arena is (-538f, 94f), arena radius is 20. So the bounds are x: (-558 to -518) z: (74, 114)
            var initialOffset = 3f; // initial offset if halfwidth of wider rectangle + halfwidth of cascading rectangle (4 + 2)
            var cascadeOffset = 4f; // cascade offset is moving the center 2 * halfwidth of cascade rectangles.
            var westernBound = -558f;
            //var westernBound = Arena.Center.X + spell.LocXZ.X - initialOffset;
            //var easternBound = Arena.Center.X - spell.LocXZ.X + initialOffset;
            var easternBound = -518;

            // How many aoe do I need for each cascade? directional bound / cascadeOffset
            int eastToWestAOE = Math.Abs((int)((westernBound - spell.LocXZ.X))) + 1;
            //var eastToWestAOE = 10;
            int westToEastAOE = Math.Abs((int)((spell.LocXZ.X - easternBound))) + 1;
            //var westToEastAOE = 10;


            /*for (var i = 0; i < _genAOE; i++)
            {
                var rotAngFloat = 8f * i;
                WPos futureOrigin = WPos.RotateAroundOrigin(-rotAngFloat, Arena.Center, spell.LocXZ);
                Angle futureRot = rotation + rotAngFloat.Degrees();
                // The first long cast takes approx 5 seconds. The instants that follow every 0.6 seconds or so.
                DateTime _activation = WorldState.FutureTime(5f + (0.6f * i));

                Casters.Add(new(Shape, futureOrigin.Quantized(), futureRot, _activation,
                    actorID: caster.InstanceID,
                    shapeDistance: Shape.Distance(futureOrigin.Quantized(), futureRot)));
            }*/
            //Casters.Add(new AOEInstance(thinRect, spell.LocXZ));


            //TODO you are here on thin rectangles east to west
            // TODO the initial future position is being based off of the boss maybe?
            for (var i = 0; i < eastToWestAOE; i++)
            {
                WPos firstThinRect = new WPos(spell.LocXZ.X - 6, spell.LocXZ.Z);
                WPos futureOrigin = new WPos(firstThinRect.X - (i * (cascadeOffset/2)), spell.LocXZ.Z);
                DateTime _activation = WorldState.FutureTime(4.7f + (1.1f * i));

                Casters.Add(new (thinRect, futureOrigin.Quantized(), rotation, _activation, actorID: caster.InstanceID, shapeDistance: thinRect.Distance(futureOrigin.Quantized(), rotation)));
                //Casters.Add(new AOEInstance(thinRect, spell.LocXZ));
            }

            // West to east bound aoe
            for (var i = 0; i < westToEastAOE; i++)
            {
                WPos firstThinRect = new WPos(spell.LocXZ.X + 6, spell.LocXZ.Z);
                WPos futureOrigin = new WPos(firstThinRect.X + (i * (cascadeOffset/2)), spell.LocXZ.Z);
                DateTime _activation = WorldState.FutureTime(4.7f + (1.1f * i));

                Casters.Add(new (thinRect, futureOrigin.Quantized(), rotation, _activation, actorID: caster.InstanceID, shapeDistance: thinRect.Distance(futureOrigin.Quantized(), rotation)));
                //Casters.Add(new AOEInstance(thinRect, spell.LocXZ));
            }
            SortHelpers.SortAOEByActivation(Casters);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.ScaldingWaves && Casters.Count != 0)
            Casters.RemoveAll(cast => cast.Activation <= WorldState.CurrentTime && cast.Shape is AOEShapeRect);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.ScaldingWaves1 && Casters.Count != 0)
        {
            Casters.RemoveAll(cast => cast.Activation <= WorldState.CurrentTime && cast.Shape is AOEShapeRect);
        }
    }

    /*public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.NailOfCondemnation && actor.Position == Arena.Center)
        {
            Casters.Clear();
        }
    }*/
}


sealed class QuaquaStates : StateMachineBuilder
{
    public QuaquaStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<MadeMagic>()
            .ActivateOnEnter<ArcaneArmaments>()
            .ActivateOnEnter<VioletStorm>()
            .ActivateOnEnter<ScaldingWaves>()
            .ActivateOnEnter<ScaldingWavesCascade>()
            .ActivateOnEnter<HammerVoidzone>()
            .ActivateOnEnter<HammerLandingKB>()

            ;
    }
}


[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    PrimaryActorOID = (uint)OID.Quaqua,
    Contributors = "wen",
    Category = BossModuleInfo.Category.VariantCriterion,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 961u,
    NameID = 12527u,
    SortOrder = 1)]

//Arena center is -538, 18, 95 in vec3
public sealed class Quaqua(WorldState ws, Actor primary) : BossModule(ws, primary, new(-538f, 94f), new ArenaBoundsCircle(20f));
