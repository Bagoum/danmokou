
using System;
using System.Collections.Generic;
using Danmokou.Core;
using Scriptor;
using static Danmokou.Reflection.Reflector;
using static Danmokou.SM.ParsingProperty;

namespace Danmokou.SM {
/// <summary>
/// Properties starting with &lt;#&gt; for BDSL1 parsing.
/// </summary>
[Reflect]
public class ParsingProperty {
    public static ParsingProperty Strict(ReflCtx.Strictness strict) => new StrictProp(strict);
    public static ParsingProperty WarnPrefix() => new WarnPrefixFlag();

    /// <summary>
    /// Empty property that indicates that BDSL1 parsing should be used (instead of BDSL2).
    /// </summary>
    public static ParsingProperty BDSL1() => new NoOp();

    #region impl

    public class ValueProp<T> : ParsingProperty {
        public readonly T value;
        public ValueProp(T obj) => value = obj;
    }

    public class StrictProp : ValueProp<ReflCtx.Strictness> {
        public StrictProp(ReflCtx.Strictness strict) : base(strict) { }
    }

    public class WarnPrefixFlag : ParsingProperty { }
    
    public class NoOp : ParsingProperty { }

    #endregion
}

public class ParsingProperties {
    public readonly ReflCtx.Strictness strict = ReflCtx.Strictness.COMMAS;
    public readonly bool warnPrefix = false;

    public ParsingProperties(IEnumerable<ParsingProperty> props) {
        foreach (var p in props) {
            if      (p is StrictProp s) 
                strict = s.value;
            else if (p is WarnPrefixFlag) 
                warnPrefix = true;
            else if (p is NoOp) { }
            else throw new Exception($"No handling for parsing property {p.GetType()}");
        }
    }
}
}
