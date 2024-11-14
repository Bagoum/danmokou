using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib.Expressions;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.SM;
using Scriptor;
using Scriptor.Analysis;
using Scriptor.Reflection;

namespace Danmokou.Reflection {
public interface EnvFrameAttacher {
    public EnvFrame? EnvFrame { get; set; }

    public static StateMachine AttachSM(StateMachine sm, EnvFrame ef) {
        if (sm is EnvFrameAttacher efa) {
            (efa.EnvFrame ??= ef).Mirror();
            return sm;
        } else
            return new EFStateMachine(sm, ef.Mirror());
    }

    public static AsyncPattern AttachAP(AsyncPattern ap, EnvFrame ef) => ap with {
        EnvFrame = ef.Mirror()
    };

    public static SyncPattern AttachSP(SyncPattern sp, EnvFrame ef) => sp with {
        EnvFrame = ef.Mirror()
    };

    public static StateMachine AttachScopeSM(StateMachine sm, LexicalScope scope) {
        sm.Scope = scope;
        return sm;
    }

    //NB: need a separate generic method here because we need to preserve the static type
    // when invoking this from expression code
    public static GenCtxProperties<T> AttachScopeProps<T>(GenCtxProperties<T> props, LexicalScope scope) {
        props.Assign(scope);
        return props;
    }
    
    public static GenCtxProperties AttachScopePropsAny(GenCtxProperties props, LexicalScope scope) {
        props.Assign(scope);
        return props;
    }

    public static GenCtxProperty[] ExtendScopeProps(GenCtxProperty[] props, LexicalScope scope) {
        return props.Append(GenCtxProperty._AssignLexicalScope(scope)).ToArray();
    }

    public static readonly ExFunction attachSM = ExFunction.WrapAny<EnvFrameAttacher>(nameof(AttachSM));
    public static readonly ExFunction attachAP = ExFunction.WrapAny<EnvFrameAttacher>(nameof(AttachAP));
    public static readonly ExFunction attachSP = ExFunction.WrapAny<EnvFrameAttacher>(nameof(AttachSP));
    public static readonly ExFunction attachScopeSM = ExFunction.WrapAny<EnvFrameAttacher>(nameof(AttachScopeSM));
    public static readonly GenericMethodSignature attachScopeProps =
        (GenericMethodSignature)MethodSignature.Get(typeof(EnvFrameAttacher).GetMethod(nameof(AttachScopeProps))!);
    public static readonly ExFunction extendScopeProps = ExFunction.WrapAny<EnvFrameAttacher>(nameof(ExtendScopeProps));
}

public class AttachEFSMConv : FixedImplicitTypeConv<StateMachine, StateMachine> {
    public static AttachEFSMConv Singleton { get; } = new();
    public AttachEFSMConv() : base(sm => tac => EnvFrameAttacher.attachSM.Of(sm(tac), tac.EnvFrame)) { }
}

public class AttachEFAPConv : FixedImplicitTypeConv<AsyncPattern, AsyncPattern> {
    public static AttachEFAPConv Singleton { get; } = new();
    public AttachEFAPConv() : base(ap => tac => EnvFrameAttacher.attachAP.Of(ap(tac), tac.EnvFrame)) { }
}
public class AttachEFSPConv : FixedImplicitTypeConv<SyncPattern, SyncPattern> {
    public static AttachEFSPConv Singleton { get; } = new();
    public AttachEFSPConv() : base(sp => tac => EnvFrameAttacher.attachSP.Of(sp(tac), tac.EnvFrame)) { }
}

[DontReflect]
public class EFStateMachine : StateMachine, EnvFrameAttacher {
    public readonly StateMachine inner;
    public EnvFrame? EnvFrame { get; set; }
    public EFStateMachine(StateMachine inner, EnvFrame ef) {
        this.inner = inner;
        this.EnvFrame = ef;
    }
    
    public override async Task Start(SMHandoff smh) {
        using var efsmh = smh.OverrideEnvFrame(EnvFrame);
        await inner.Start(efsmh);
    }
}



}