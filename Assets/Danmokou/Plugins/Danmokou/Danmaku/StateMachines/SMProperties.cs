using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath.Functions;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;
using static Danmokou.SM.PatternProperty;
using static Danmokou.SM.PhaseProperty;


namespace Danmokou.SM {

/// <summary>
/// A modifier that affects an entire pattern script.
/// </summary>
[Reflect]
public class PatternProperty {
    /// <summary>
    /// Get metadata from a boss configuration. Includes things like UI colors, spell circles, tracker, etc.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static PatternProperty Boss(string key) => new BossProp(key);

    public static PatternProperty Bosses(string[] keys, (int phase, int index)[] uiUsage) => new BossesProp(keys, uiUsage);

    public static PatternProperty BGM((int, string?)[] phasesAndTracks) => new BGMProp(phasesAndTracks);
    
    public static PatternProperty SetUIFrom(int firstPhase) => new SetUIFromProp(firstPhase);

    #region Impls
    
    public class EmptyProp : PatternProperty { }

    public class ValueProp<T> : PatternProperty {
        public readonly T value;
        public ValueProp(T obj) {
            value = obj;
        }
    }

    public class BossProp : ValueProp<BossConfig> {
        public BossProp(string key): base(ResourceManager.GetBoss(key)) { }
    }
    public class BossesProp : ValueProp<(BossConfig[], (int, int)[])> {
        public BossesProp(string[] keys, (int, int)[] uiUsage): 
            base((keys.Select(ResourceManager.GetBoss).ToArray(), uiUsage)) { }
    }

    public class BGMProp : ValueProp<(int, string?)[]> {
        public BGMProp((int, string?)[] tracks) : base(tracks) { }
    }

    public class SetUIFromProp : ValueProp<int> {
        public SetUIFromProp(int from) : base(from) { }
    }
    
    #endregion
}

/// <summary>
/// A set of <see cref="PatternProperty"/>.
/// </summary>
public class PatternProperties {
    public readonly BossConfig? boss;
    public readonly BossConfig[]? bosses;
    public readonly (int phase, int index)[]? bossUI;
    public readonly int setUIFrom = 0;
    public readonly (int phase, string?)[]? bgms;
    public PatternProperties(PatternProperty[] props) {
        foreach (var prop in props) {
            if        (prop is BossProp bp) {
                boss = bp.value;
            } else if (prop is BossesProp bsp) {
                (bosses, bossUI) = bsp.value;
                boss = bosses[0];
            } else if (prop is BGMProp bgm) {
                bgms = bgm.value;
            } else if (prop is SetUIFromProp sui) 
                setUIFrom = sui.value;
            else if (prop is PatternProperty.EmptyProp) { }
            else throw new Exception($"Pattern is not allowed to have properties of type {prop.GetType()}");
        }
    }
}

/// <summary>
/// Markers that modify the behavior of PhaseSM.
/// </summary>
[Reflect]
public class PhaseProperty {
    /// <summary>
    /// Hide the timeout display on the UI.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty HideTimeout() => new HideTimeoutFlag();
    /// <summary>
    /// Declares that this phase is a stage announce section.
    /// </summary>
    public static PhaseProperty Announce() => new PhaseTypeProp(PhaseType.ANNOUNCE);
    /// <summary>
    /// Declares that this phase is a stage section.
    /// </summary>
    public static PhaseProperty Stage() => new PhaseTypeProp(PhaseType.STAGE);

    /// <summary>
    /// Don't play a sound effect (spellcard clear or stage section clear) when this phase is cleared.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Silent() => new SilentFlag();
    
    /// <summary>
    /// Skip this phase.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Skip() => new SkipFlag();

    /// <summary>
    /// Declare that this phase is a dialogue phase. (Same as TYPE DIALOGUE ``).
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Dialogue() => new PhaseTypeProp(PhaseType.DIALOGUE);
    /// <summary>
    /// Declare that this phase is a stage midboss phase.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Midboss() => new PhaseTypeProp(PhaseType.STAGEMIDBOSS);
    /// <summary>
    /// Declare that this phase is a stage endboss phase.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Endboss() => new PhaseTypeProp(PhaseType.STAGEENDBOSS);
    /// <summary>
    /// Declare the type and name of this phase.
    /// </summary>
    public static PhaseProperty Type(PhaseType type, LString name) => new PhaseTypeProp(type, name);
    /// <summary>
    /// Declare the amount of photos necessary to defeat the boss.
    /// <br/>Note: this works similarly enough to HP that you could
    /// make a 6-stage game with a photo shot.
    /// </summary>
    public static PhaseProperty Photo(int photos) => new PhotoHPProp(photos, 0);
    /// <summary>
    /// Declare the amount of HP the boss has during this section.
    /// The boss will be invincible when the phase starts. Use `vulnerable true`.
    /// </summary>
    public static PhaseProperty HPn(int hp) => new HPProp(hp, null);
    /// <summary>
    /// Declare the amount of HP the boss has during this section.
    /// The boss will be immediately vulnerable once the phase starts.
    /// </summary>
    public static PhaseProperty HP(int hp) => new HPProp(hp, 0);
    /// <summary>
    /// Declare the amount of HP the boss has during this section.
    /// Also declare the amount of time for which the boss is invulnerable at the beginning of the phase.
    /// </summary>
    public static PhaseProperty HPi(int hp, float inv_time) => new HPProp(hp, inv_time);
    /// <summary>
    /// Declare the percentage of the remaining healthbar that this spellcard should consume.
    /// Note: By default, this is 0.5 for nonspells and 1 for spells.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty HPBar(float portion) => new HPBarProp(portion);
    /// <summary>
    /// Declare an event type that will be used in the phase.
    /// </summary>
    /// <returns></returns>
    [GAlias(typeof(float), "eventf")]
    [GAlias(typeof(Unit), "event0")]
    public static PhaseProperty Event<T>(string evName, Events.RuntimeEventType typ) => new EventProp<T>(evName, typ);
    /// <summary>
    /// Declare the background transition used when shifting into this spellcard.
    /// <br/>Note: This is automatically handled by the `boss` pattern property.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty BGTIn(string style) => new BGTransitionProp(true, style);
    /// <summary>
    /// Declare the background style of this spellcard.
    /// <br/>Note: This is automatically handled by the `boss` pattern property.
    /// </summary>
    /// <param name="style"></param>
    /// <returns></returns>
    public static PhaseProperty BG(string style) => new BackgroundProp(style);
    /// <summary>
    /// Declare the background transition used when ending this spellcard.
    /// <br/>Note: This is automatically handled by the `boss` pattern property.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty BGTOut(string style) => new BGTransitionProp(false, style);

    /// <summary>
    /// Automatically clear bullets and hoisted data at the end of this phase.
    /// Note that this is on by default for card-type spells (nons, spells, timeouts, finals).
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Clear() => new ClearProp(true, null, null);
    /// <summary>
    /// Don't automatically clear bullets and hoisted data at the end of this phase.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty NoClear() => new ClearProp(false, null, null);

    /// <summary>
    /// Same as RootT with a default time of 2 seconds.
    /// </summary>
    public static PhaseProperty Root(float x, float y) => RootT(2, x, y);

    /// <summary>
    /// Set the starting boss position for a persistent BEH entity, with a default time of 2 seconds.
    /// </summary>
    public static PhaseProperty RootOther(string who, float x, float y) => new RootProp(2, who, x, y);

    /// <summary>
    /// Set the starting boss position for this phase. The boss will move here before the phase timer starts.
    /// </summary>
    /// <param name="t">Time (seconds) to move to position</param>
    /// <param name="x">X position</param>
    /// <param name="y">Y position</param>
    /// <returns></returns>
    public static PhaseProperty RootT(float t, float x, float y) => new RootProp(t, null, x, y);
    
    /// <summary>
    /// Override the UI lives display to show a specific number.
    /// Note: this is for gimmick purposes.
    /// </summary>
    public static PhaseProperty ShowLives(int lives) => new LivesOverrideProp(lives);

    /// <summary>
    /// Don't drain the player's score multiplier during this phase. (Automatically set for dialogue)
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Lenient() => new LenientFlag();

    /// <summary>
    /// Perform the boss cutin before starting this phase. This should be done once per script.
    /// </summary>
    public static PhaseProperty BossCutin() => new BossCutinFlag();

    /// <summary>
    /// Perform a spell cutin before starting this phase. This is done automatically with index 0 for all
    /// spell-type cards if the `boss` pattern property is set. If the index is invalid, then no cutin will be summoned.
    /// </summary>
    public static PhaseProperty SpellCutin(int index) => new SpellCutinProp(index);

    public static PhaseProperty Challenge(Challenge c) => new ChallengeProp(c);
    
    #region Impls

    public class EmptyProp : PhaseProperty { }

    public class HideTimeoutFlag : PhaseProperty {}
    public class SilentFlag : PhaseProperty { }
    public class SkipFlag : PhaseProperty { }
    public class LenientFlag : PhaseProperty { }
    
    public class BossCutinFlag : PhaseProperty { }
    public class PhaseTypeProp : PhaseProperty {
        public readonly PhaseType type;
        public readonly LString? name;
        public PhaseTypeProp(PhaseType type, LString? name) {
            this.type = type;
            this.name = name;
        }
        public PhaseTypeProp(PhaseType type) : this(type, null) { }
    }

    public class PhotoHPProp : PhaseProperty {
        public readonly int hp;
        public readonly float? invulnT;
        public PhotoHPProp(int hp, float? invT) {
            this.hp = hp;
            this.invulnT = invT;
        }
    }
    public class HPProp : PhaseProperty {
        public readonly int hp;
        public readonly float? invulnT;
        public HPProp(int hp, float? invT) {
            this.hp = hp;
            invulnT = invT;
        }
    }

    public class HPBarProp : PhaseProperty {
        public readonly float portion;
        public HPBarProp(float portion) => this.portion = portion;
    }

    public abstract class EventProp : PhaseProperty {
        public abstract Func<IDisposable> CreateCreator();
    }
    public class EventProp<T> : EventProp { 
        public readonly string evName;
        public readonly Events.RuntimeEventType typ;
        public EventProp(string evName, Events.RuntimeEventType typ) {
            this.evName = evName;
            this.typ = typ;
        }

        public override Func<IDisposable> CreateCreator() => Events.CreateRuntimeEventCreator<T>(evName, typ);
    }

    public class BGTransitionProp : PhaseProperty {
        public readonly bool isInwardsTransition;
        public readonly string style;

        public BGTransitionProp(bool inwards, string style) {
            this.isInwardsTransition = inwards;
            this.style = style;
        }
    }

    public class BackgroundProp : PhaseProperty {
        public readonly string style;
        public BackgroundProp(string style) => this.style = style;
    }

    public class ClearProp : PhaseProperty {
        public readonly bool clear;
        public readonly string? target;
        public readonly string? defaulter;
        public ClearProp(bool doClear, string? targetPool, string? defaulter) {
            clear = doClear;
            target = targetPool;
            this.defaulter = defaulter;
        }
    }

    public class LivesOverrideProp : PhaseProperty {
        public readonly int lives;

        public LivesOverrideProp(int l) {
            lives = l;
        }
    }
    public class SpellCutinProp : PhaseProperty {
        public readonly int index;

        public SpellCutinProp(int x) {
            index = x;
        }
    }

    public class RootProp : PhaseProperty {
        public readonly float t;
        public readonly float x;
        public readonly float y;
        public readonly string? who;

        public RootProp(float t, string? who, float x, float y) {
            this.t = t;
            this.x = x;
            this.y = y;
            this.who = who;
        }
    }

    public class ChallengeProp : PhaseProperty {
        public readonly Challenge c;
        public ChallengeProp(Challenge c) => this.c = c;
    }

    #endregion
}

public readonly struct SoftcullProperties {
    public readonly Vector2 center;
    public readonly float advance;
    public readonly float minDist;
    public readonly float maxDist;
    public readonly string? autocullTarget;
    private readonly string autocullDefault;
    public string? DefaultPool => autocullTarget == null ? null : $"{autocullTarget}-{autocullDefault}";
    public readonly bool sendToC;

    public SoftcullProperties(Vector2 center, float advance, float minDist, float maxDist, string? target, string? dflt=null) {
        this.center = center;
        this.advance = advance;
        this.minDist = minDist;
        this.maxDist = maxDist;
        this.autocullTarget = target;
        this.autocullDefault = dflt ?? "black/b";
        this.sendToC = true;
    }

    public static SoftcullProperties OverTimeDefault(Vector2 center, float advance, float minDist, float maxDist,
        string? target = null, string? dflt = null) =>
        new SoftcullProperties(center, advance, minDist, maxDist, target, dflt);
    
    public static SoftcullProperties SynchronousDefault(Vector2 center, float advance, float minDist, float maxDist,
        string? target = null, string? dflt = null) =>
        new SoftcullProperties(center, advance, minDist, maxDist, target, dflt);

    //TODO review other usages
    public SoftcullProperties(string? target, string? dflt) : this(Vector2.zero, 0, 0, 0, target, dflt) {
        sendToC = false;
    }

    public readonly float AdvanceTime(Vector2 location) {
        var dist = (location - center).magnitude;
        return advance * (1 - Mathf.Clamp01((dist - minDist) / (maxDist - minDist)));
    }

    public SoftcullProperties WithNoAdvance() =>
        new SoftcullProperties(center, 0f, minDist, maxDist, autocullTarget, autocullDefault);
}
public class PhaseProperties {
    private readonly bool hideTimeout;
    public bool HideTimeout => hideTimeout || (!SaveData.Settings.TeleportAtPhaseStart && phaseType?.HideTimeout() == true);
    public readonly LString? cardTitle;
    public readonly int? hp = null;
    public readonly int? photoHP = null;
    public readonly float? invulnTime = null;
    public readonly float? hpbar = null;
    public readonly PhaseType? phaseType = null;
    public readonly List<Func<IDisposable>> phaseObjectGenerators = new List<Func<IDisposable>>();
    
    public SOBgTransition? BgTransitionIn { get; }
    public SOBgTransition? BgTransitionOut { get; }
    public GameObject? Background { get; }

    public readonly float cardValueMult = 1f;
    private readonly bool? cleanup = null;
    public bool Cleanup => cleanup ?? phaseType?.IsPattern() ?? false;
    public readonly bool endSound = true;
    private readonly string? autocullTarget;
    private readonly string? autocullBehTarget = "cwheel";
    private readonly string? autocullDefault;

    public SoftcullProperties SoftcullProps(BehaviorEntity exec) =>
        SoftcullProperties.SynchronousDefault(exec.GlobalPosition(), 0.4f, 0.5f, 4f, autocullTarget, autocullDefault);
    public SoftcullProperties SoftcullPropsBeh(BehaviorEntity exec) =>
        SoftcullProperties.SynchronousDefault(exec.GlobalPosition(), 0.4f, 0.5f, 4f, autocullBehTarget, autocullDefault);
    public SoftcullProperties SoftcullPropsOverTime(BehaviorEntity exec, float advance) =>
        SoftcullProperties.OverTimeDefault(exec.GlobalPosition(), advance, 0.5f, 8f, autocullTarget, autocullDefault);
    
    public readonly int? livesOverride = null;
    public readonly StateMachine? rootMove = null;
    public readonly bool skip = false;
    private readonly bool? lenient = null;
    public bool Lenient => lenient ?? phaseType?.IsLenient() ?? false;
    public readonly bool bossCutin = false;
    public readonly int? spellCutinIndex = null;

    public readonly List<Challenge> challenges = new List<Challenge>();

    public PhaseProperties(IReadOnlyList<PhaseProperty> props) {
        List<StateMachine> rootMoves = new List<StateMachine>();
        foreach (var prop in props) {
            if      (prop is HideTimeoutFlag) 
                hideTimeout = true;
            else if (prop is SilentFlag)
                endSound = false;
            else if (prop is SkipFlag) 
                skip = true;
            else if (prop is LenientFlag) 
                lenient = true;
            else if (prop is BossCutinFlag) 
                bossCutin = true;
            else if (prop is PhaseTypeProp s) {
                phaseType = s.type;
                cardTitle = s.name;
            } else if (prop is PhotoHPProp php) {
                photoHP = php.hp;
                invulnTime = php.invulnT;
            } else if (prop is HPProp h) {
                hp = h.hp;
                invulnTime = h.invulnT;
            } else if (prop is HPBarProp hb) 
                hpbar = hb.portion;
            else if (prop is EventProp ep)
                phaseObjectGenerators.Add(ep.CreateCreator());
            else if (prop is BackgroundProp bp) 
                Background = ResourceManager.GetBackground(bp.style);
            else if (prop is BGTransitionProp btp) {
                if (btp.isInwardsTransition) BgTransitionIn = ResourceManager.GetBackgroundTransition(btp.style);
                else BgTransitionOut = ResourceManager.GetBackgroundTransition(btp.style);
            } else if (prop is ClearProp cp) {
                cleanup = cp.clear;
                autocullTarget = cp.target ?? autocullTarget;
                autocullDefault = cp.defaulter ?? autocullDefault;
            } else if (prop is LivesOverrideProp lop) 
                livesOverride = lop.lives;
            else if (prop is SpellCutinProp scp) 
                spellCutinIndex = scp.index;
            else if (prop is RootProp rp) {
                StateMachine rm = new ReflectableLASM(SaveData.Settings.TeleportAtPhaseStart ?
                    SMReflection.Position(_ => rp.x, _ => rp.y) :
                    SMReflection.MoveTarget(BPYRepo.Const(rp.t), ExMEasers.EIOSine, Parametrics.CXY(rp.x, rp.y)));
                rootMoves.Add(rp.who == null ? rm : RetargetUSM.Retarget(rm, rp.who));
            } else if (prop is ChallengeProp clp) 
                challenges.Add(clp.c);
            else if (prop is PhaseProperty.EmptyProp) { }
            else throw new Exception($"Phase is not allowed to have properties of type {prop.GetType()}");
            
            if (rootMoves.Count > 0) rootMove = new ParallelSM(rootMoves);
        }
    }
}

}