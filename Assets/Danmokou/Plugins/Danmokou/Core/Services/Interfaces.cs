using System.Linq.Expressions;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.Services {
public enum SFXType {
    Default,
    TypingSound
}
public interface ISFXService {
    public enum SFXEventType {
        BossSpellCutin,
        BossCutin,
        BossExplode,
        FlakeItemCollected
    }
    void Request(string? key);
    void Request(string? key, SFXType type);
    void Request(SFXConfig? sfx, SFXType type = SFXType.Default);

    void RequestSFXEvent(SFXEventType ev);


    AudioSource? RequestSource(string? key, ICancellee cT);
    AudioSource? RequestSource(SFXConfig? aci, ICancellee cT);
    
    public static ISFXService SFXService => ServiceLocator.Find<ISFXService>();
    public static Expression SFXRequest(Expression style) => 
        sfxrequest.InstanceOf(Expression.Property(null, typeof(ISFXService), nameof(SFXService)), style);

    private static readonly ExFunction sfxrequest = ExFunction.Wrap<ISFXService>(nameof(ISFXService.Request), new[] {typeof(string)});
}
}