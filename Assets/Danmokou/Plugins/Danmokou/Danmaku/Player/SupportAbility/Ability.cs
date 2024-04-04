using System;
using System.Collections;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.SM;
using Danmokou.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Danmokou.Player {
public abstract partial record Ability {
    public LString Title { get; init; } = LString.Empty;
    public LString ShortTitle { get; init; } = LString.Empty;

    public record Null : Ability {
        public static readonly Null Default = new() {
            Title = "No ability",
            ShortTitle = "No ability"
        };
    }
    public abstract record Bomb : Ability {
        public GameObject? Cutin { get; init; }
        public GameObject? SpellTitle { get; init; }
        public Color SpellColor1 { get; init; } = Color.clear;
        public Color SpellColor2 { get; init; } = Color.clear;
        public TextAsset? SMFile { get; init; }
        public virtual int DeathbombFrames => 20;
        public virtual double? PowerRequired => null;
        public virtual int? BombsRequired => 1;
        
        public StateMachine? SM => StateMachineManager.FromText(SMFile);


        protected abstract IEnumerator Execute(PlayerController bomber, IDisposable bombDisabler);
        
        public virtual double ContextCostMultiplier(PlayerController.BombContext ctx) => ctx switch {
            PlayerController.BombContext.DEATHBOMB => 2,
            _ => 1
        };

        public bool TryBomb(PlayerController bomber, PlayerController.BombContext ctx) {
            var mult = ContextCostMultiplier(ctx);
            if (PowerRequired.Try(out var rp) && !GameManagement.Instance.PowerF.TryConsumePower(-rp * mult))
                return false;
            if (BombsRequired.Try(out var rb) &&
                !GameManagement.Instance.BasicF.TryConsumeBombs((int) Math.Round(-rb * mult)))
                return false;
            ++GameManagement.Instance.BombsUsed;
            PlayerController.BombFired.OnNext((this, ctx));
            var ienum = Execute(bomber, bomber.BombsEnabled.AddConst(false));
            bomber.RunDroppableRIEnumerator(ienum);
            return true;
        }
        
        public void SpawnCutin() {
            if (Cutin != null)
                Object.Instantiate(Cutin);
            if (SpellTitle != null) {
                Object.Instantiate(SpellTitle).GetComponent<PlayerSpellTitle>().Initialize(Title, SpellColor1, SpellColor2);
            }
        }
        
        protected IDisposable DisableFire(PlayerController bomber) => bomber.FiringEnabled.AddConst(false);
    }

    public abstract record Metered : Ability { }
}
}