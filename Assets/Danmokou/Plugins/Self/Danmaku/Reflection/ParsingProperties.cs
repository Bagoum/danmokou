
using System;
using System.Collections.Generic;
using DMK.Core;
using static DMK.Reflection.Reflector;
using static DMK.SM.ParsingProperty;

namespace DMK.SM {
[Reflect]
public class ParsingProperty {
    public static ParsingProperty Strict(ReflCtx.Strictness strict) => new StrictProp(strict);
    public static ParsingProperty WarnPrefix() => new WarnPrefixFlag();
    public static ParsingProperty TrueArgumentOrder() => new TrueArgumentOrderFlag();

    #region impl

    public class ValueProp<T> : ParsingProperty {
        public readonly T value;
        public ValueProp(T obj) => value = obj;
    }

    public class StrictProp : ValueProp<ReflCtx.Strictness> {
        public StrictProp(ReflCtx.Strictness strict) : base(strict) { }
    }

    public class WarnPrefixFlag : ParsingProperty { }
    public class TrueArgumentOrderFlag : ParsingProperty { }

    #endregion
}

public class ParsingProperties {
    public readonly ReflCtx.Strictness strict = ReflCtx.Strictness.COMMAS;
    public readonly bool warnPrefix = false;
    public readonly bool trueArgumentOrder = false;

    public ParsingProperties(IEnumerable<ParsingProperty> props) {
        foreach (var p in props) {
            if      (p is StrictProp s) 
                strict = s.value;
            else if (p is WarnPrefixFlag) 
                warnPrefix = true;
            else if (p is TrueArgumentOrderFlag)
                trueArgumentOrder = true;
            else throw new Exception($"No handling for parsing property {p.GetType()}");
        }
    }
}
}
