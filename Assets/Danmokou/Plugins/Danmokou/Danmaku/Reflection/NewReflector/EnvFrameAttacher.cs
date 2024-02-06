using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.Reflection;
using Danmokou.SM;

namespace Danmokou.Reflection2 {
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