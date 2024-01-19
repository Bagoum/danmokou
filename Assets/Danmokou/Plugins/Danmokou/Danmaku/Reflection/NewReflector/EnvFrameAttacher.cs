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
            efa.EnvFrame ??= ef;
            return sm;
        } else
            return new EFStateMachine(sm, ef);
    }
    
    public static AsyncPattern AttachAP(AsyncPattern ap, EnvFrame ef) {
        IEnumerator Inner(AsyncHandoff abh) {
            abh.ch = abh.ch.OverrideFrame(ef);
            yield return ap(abh);
            abh.ch.Dispose();
        }
        return Inner;
    }

    public static SyncPattern AttachSP(SyncPattern sp, EnvFrame ef) => sbh => {
        sbh.ch = sbh.ch.OverrideFrame(ef);
        sp(sbh);
        sbh.ch.Dispose();
    };

    public static StateMachine AttachScopeSM(StateMachine sm, LexicalScope scope) {
        sm.Scope = scope;
        return sm;
    }

    public static GenCtxProperties<T> AttachScopeProps<T>(GenCtxProperties<T> props, LexicalScope scope, AutoVars.GenCtx autovars) {
        props.Assign(scope, autovars);
        return props;
    }

    public static GenCtxProperty[] ExtendScopeProps(GenCtxProperty[] props, LexicalScope scope, AutoVars.GenCtx autovars) {
        return props.Append(GenCtxProperty._AssignLexicalScope(scope, autovars)).ToArray();
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
    private readonly StateMachine inner;
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