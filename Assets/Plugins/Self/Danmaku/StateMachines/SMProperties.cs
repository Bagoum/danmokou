using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Core;
using Danmaku;
using DMath;
using FParser;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;
using static SM.PatternProperty;
using static SM.PhaseProperty;
using static Danmaku.Enums;


namespace SM {

public class PatternProperty {
    /// <summary>
    /// Get metadata from a boss configuration. Includes things like UI colors, spell circles, tracker, etc.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static PatternProperty Boss(string key) => new BossProp(key);

    public static PatternProperty BGM((int, string)[] phasesAndTracks) => new BGMProp(phasesAndTracks);

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

    public class BGMProp : ValueProp<(int, string)[]> {
        public BGMProp((int, string)[] tracks) : base(tracks) { }
    }
    
    #endregion
}

public class PatternProperties {
    [CanBeNull] public readonly BossConfig boss;
    [CanBeNull] public readonly (int, string)[] bgms;
    public PatternProperties(PatternProperty[] props) {
        foreach (var prop in props) {
            if (prop is BossProp bp) boss = bp.value;
            else if (prop is BGMProp bgm) bgms = bgm.value;
            else if (prop is PatternProperty.EmptyProp) { }
            else throw new Exception($"Pattern is not allowed to have properties of type {prop.GetType()}");
        }
    }
}

/// <summary>
/// Markers that modify the behavior of PhaseSM.
/// </summary>
public class PhaseProperty {
    /// <summary>
    /// Hide the timeout display on the UI.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty HideTimeout() => new HideTimeoutFlag();
    /// <summary>
    /// Declares that this card is a stage section.
    /// </summary>
    public static PhaseProperty Stage() => Type(PhaseType.STAGE, null);
    /// <summary>
    /// Skip this phase.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Skip() => new SkipFlag();

    /// <summary>
    /// Declare that this card is a dialogue card. (Same as CARD DIALOGUE ``).
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Dialogue() => Type(PhaseType.DIALOGUE, null);
    /// <summary>
    /// Declare that this card is a stage midboss card.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Midboss() => Type(PhaseType.STAGEMIDBOSS, null);
    /// <summary>
    /// Declare that this card is a stage endboss card.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Endboss() => Type(PhaseType.STAGEENDBOSS, null);
    /// <summary>
    /// Declare the type and name of this card.
    /// </summary>
    public static PhaseProperty Type(PhaseType type, string name) => new PhaseTypeProp(type, name);
    /// <summary>
    /// Declare the amount of HP the boss has during this section.
    /// The boss will be invincible when the phase starts. Use `setstate vulnerable true`.
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
    /// Declare an event type that will be used in the script.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty Event0(Events.EventDeclaration<Events.Event0> ev) => new EmptyProp();
    /// <summary>
    /// Declare the background transition used when shifting into this spellcard.
    /// </summary>
    /// <returns></returns>
    public static PhaseProperty BGTIn(string style) => new BGTransitionProp(true, style);
    /// <summary>
    /// Declare the background style of this spellcard.
    /// </summary>
    /// <param name="style"></param>
    /// <returns></returns>
    public static PhaseProperty BG(string style) => new BackgroundProp(style);
    /// <summary>
    /// Declare the background transition used when ending this spellcard.
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
    /// spell-type cards. If the index is invalid, then no cutin will be summoned.
    /// </summary>
    public static PhaseProperty SpellCutin(int index) => new SpellCutinProp(index);
    
    #region Impls

    public class EmptyProp : PhaseProperty { }

    public class HideTimeoutFlag : PhaseProperty {}
    public class SkipFlag : PhaseProperty { }
    public class LenientFlag : PhaseProperty { }
    
    public class BossCutinFlag : PhaseProperty { }
    public class PhaseTypeProp : PhaseProperty {
        public readonly PhaseType type;
        [CanBeNull] public readonly string name;
        public PhaseTypeProp(PhaseType type, string name) {
            this.type = type;
            this.name = name;
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
        public readonly string target;
        public readonly string defaulter;
        public ClearProp(bool doClear, [CanBeNull] string targetPool, [CanBeNull] string defaulter) {
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
        [CanBeNull] public readonly string who;

        public RootProp(float t, [CanBeNull] string who, float x, float y) {
            this.t = t;
            this.x = x;
            this.y = y;
            this.who = who;
        }
    }

    #endregion
}

public class PhaseProperties {
    public readonly bool hideTimeout;
    [CanBeNull] public readonly string cardTitle;
    public readonly int? hp = null;
    public readonly float? invulnTime = null;
    public readonly float? hpbar = null;
    public readonly PhaseType? phaseType = null;
    [CanBeNull] public SOBgTransition BgTransitionIn { get; private set; }
    [CanBeNull] public SOBgTransition BgTransitionOut { get; private set; }
    [CanBeNull] public GameObject Background { get; private set; }
    [CanBeNull] public BossConfig Boss { get; private set; }
    public readonly float cardValueMult = 1f;
    private readonly bool? cleanup = null;
    public bool Cleanup => cleanup ?? phaseType?.IsPattern() ?? false;
    public readonly string autocullTarget = "cwheel";
    public readonly string autocullDefault = "red/b";
    public readonly int? livesOverride = null;
    [CanBeNull] public readonly StateMachine rootMove = null;
    public readonly bool skip = false;
    private readonly bool? lenient = null;
    public bool Lenient => lenient ?? phaseType?.IsLenient() ?? false;
    public readonly bool bossCutin = false;
    private readonly int? spellCutinIndex = null;

    public bool GetSpellCutin(out GameObject go) {
        go = null;
        if (Boss != null) {
            var index = spellCutinIndex ?? ((phaseType?.IsSpell() ?? false) ? (int?)0 : null);
            if (index.HasValue) return Boss.spellCutins.Try(index.Value, out go);
        }
        return false;
    }

    public void LoadDefaults(PatternProperties pat) {
        if (pat.boss != null) {
            Boss = pat.boss;
            if (phaseType.HasValue) {
                Background = (Background == null) ? Boss.Background(phaseType.Value) : Background;
                BgTransitionIn = (BgTransitionIn == null) ? Boss.IntoTransition(phaseType.Value) : BgTransitionIn;
            }
        }
    }
    public PhaseProperties(IReadOnlyList<PhaseProperty> props) {
        List<StateMachine> rootMoves = new List<StateMachine>();
        foreach (var prop in props) {
            if (prop is HideTimeoutFlag) hideTimeout = true;
            else if (prop is SkipFlag) skip = true;
            else if (prop is LenientFlag) lenient = true;
            else if (prop is BossCutinFlag) bossCutin = true;
            else if (prop is PhaseTypeProp s) {
                phaseType = s.type;
                cardTitle = s.name;
            } else if (prop is HPProp h) {
                hp = h.hp;
                invulnTime = h.invulnT;
            } else if (prop is HPBarProp hb) hpbar = hb.portion;
            else if (prop is BackgroundProp bp) Background = ResourceManager.GetBackground(bp.style);
            else if (prop is BGTransitionProp btp) {
                if (btp.isInwardsTransition) BgTransitionIn = ResourceManager.GetBackgroundTransition(btp.style);
                else BgTransitionOut = ResourceManager.GetBackgroundTransition(btp.style);
            } else if (prop is ClearProp cp) {
                cleanup = cp.clear;
                autocullTarget = cp.target ?? autocullTarget;
                autocullDefault = cp.defaulter ?? autocullDefault;
            } else if (prop is LivesOverrideProp lop) livesOverride = lop.lives;
            else if (prop is SpellCutinProp scp) spellCutinIndex = scp.index;
            else if (prop is RootProp rp) {
                StateMachine rm = new ReflectableLASM(SaveData.Settings.TeleportAtPhaseStart ? 
                    SMReflection.Position(_ => rp.x, _ => rp.y) : 
                    SMReflection.MoveTarget(BPYRepo.Const(rp.t), "io-sine", Parametrics.CXY(rp.x, rp.y)));
                rootMoves.Add(rp.who == null ? rm : RetargetUSM.Retarget(rm, rp.who));
            } else if (prop is PhaseProperty.EmptyProp) { }
            else throw new Exception($"Phase is not allowed to have properties of type {prop.GetType()}");
            
            if (rootMoves.Count > 0) rootMove = new ParallelSM(rootMoves, Enums.Blocking.BLOCKING);
        }
    }
}



/// <summary>
/// A object that holds a partial parsing state for tasks such as StateMachine parsing.
/// </summary>
public class ParsingQueue: IDisposable {
    private const string LINE_DELIM = "\n";
    /// <summary>
    /// Backing array of all words that have been lexed.
    /// </summary>
    private readonly string[] words;
    private readonly FParsec.Position[] positions;
    /// <summary>
    /// Index of next word to yield to parsing.
    /// </summary>
    public int Index { get; private set; }

    public static readonly HashSet<string> ARR_EMPTY = new HashSet<string>() {
        ".", "{}", "_"
    }; 
    public const string ARR_OPEN = "{";
    public const string ARR_CLOSE = "}";
    public readonly List<PhaseProperty> queuedProps = new List<PhaseProperty>();

    public string this[int index] => words[index];

    private ParsingQueue(string[] words, FParsec.Position[] positions) {
        this.words = words;
        this.positions = positions;
    }

    private void ThrowIfOOB(int index) {
        if (index >= words.Length) throw new Exception("The parser ran out of text to read.");
    }

    /// <summary>
    /// Returns the next non-newline word in the stream.
    /// Does not advance the stream.
    /// </summary>
    /// <param name="index">Index of the returned word</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public string Scan(out int index) {
        int max = words.Length;
        for (index = Index; index < max && words[index] == LINE_DELIM; ++index) { }
        ThrowIfOOB(index);
        return words[index];
    }

    /// <summary>
    /// Returns the next non-newline word in the stream, or null if at the end.
    /// Does not advance the stream.
    /// </summary>
    [CanBeNull]
    public string SoftScan(out int index) {
        int max = words.Length;
        for (index = Index; index < max && words[index] == LINE_DELIM; ++index) { }
        return (index < max) ? words[index] : null;
    }

    public string Prev() => words[Math.Max(0, Index - 1)];

    /// <summary>
    /// Returns the next non-newline word in the stream, but skips the line if it is a property declaration.
    /// </summary>
    /// <returns></returns>
    public string ScanNonProperty() {
        int max = words.Length;
        int index = Index;
        for (; index < max && words[index] == LINE_DELIM; ++index) { }
        ThrowIfOOB(index);
        while (true) {
            if (words[index] != SMParser.PROP_KW) return words[index];
            for (; index < max && words[index] != LINE_DELIM; ++index) { }
            ThrowIfOOB(index);
            for (; index < max && words[index] == LINE_DELIM; ++index) { }
            ThrowIfOOB(index);
            
        }
    }
    /// <summary>
    /// Returns the next non-newline word in the stream.
    /// Does not advance the stream.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public string Scan() => Scan(out var _);

    public bool IsNewline() => Index < words.Length && words[Index] == LINE_DELIM;
    public bool IsNewlineOrEmpty() => Index >= words.Length || words[Index] == LINE_DELIM;

    /// <summary>
    /// Returns the next non-newline word in the stream.
    /// Advances the stream to the word after that.
    /// </summary>
    /// <param name="index">Index of the returned word</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public string Next(out int index) {
        string s = Scan(out index);
        Index = index + 1;
        return s;
    }
    /// <summary>
    /// Returns the next non-newline word in the stream.
    /// Advances the stream to the word after that.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public string Next() => Next(out var _);

    public int GetLastLine() => GetLastLine(Index);
    public int GetLastLine(int index) {
        for (int ii = Math.Min(words.Length - 1, index); ii >= 0; --ii) {
            if (positions[ii] != null && words[ii] != LINE_DELIM) {
                return (int)positions[ii].Line;
            }
        }
        return 0;
    }
    public string PrintLine(int index, bool circleThis) {
        StringBuilder sb = new StringBuilder();
        int si = index - 1;
        for (; si > 0; --si) {
            if (words[si] == LINE_DELIM) break;
        }
        int ei = index;
        for (; ei < words.Length; ++ei) {
            if (words[ei] == LINE_DELIM) break;
        }
        for (++si; si < ei;) {
            if (si == index && circleThis) {
                sb.Append('[');
                sb.Append(words[si]);
                sb.Append(']');
            } else sb.Append(words[si]);
            if (++si != ei) sb.Append(' ');
        }
        return sb.ToString();
    }

    private string ShowWords(int start, int end) {
        StringBuilder sb = new StringBuilder();
        while (start < end) {
            sb.Append(words[start++]);
            sb.Append(' ');
        }
        return sb.ToString();
    }

    public bool Empty() {
        int max = words.Length;
        int ii = Index;
        for (; ii < max && words[ii] == LINE_DELIM; ++ii) { }
        return ii == max; //Don't advance ind in this call, it should not change state
    }
    
    public void Dispose() {
        if (!Empty()) {
            Log.Unity($"Parsing completed on line {GetLastLine(Index)} but there is more text:\n\t" +
                                $"{ShowWords(Index, Math.Min(words.Length, Index + 10))}", true, Log.Level.WARNING);
        }
    }
    public static ParsingQueue Lex(string s) {
        Profiler.BeginSample("Lex");
        var (words, poss) = SMParser.lSMParser(s).Try;
        Profiler.EndSample();
        return new ParsingQueue(words.ToArray(), FSInterop.ToNullableArray(poss));
        //return new ParsingQueue(FParser.Parser.SMParser.Invoke(s).Try);
    }
}



}