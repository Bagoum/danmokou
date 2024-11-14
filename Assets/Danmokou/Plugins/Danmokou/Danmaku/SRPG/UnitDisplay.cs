using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Reflection;
using Danmokou;
using Danmokou.Behavior.Display;
using Danmokou.Danmaku.Descriptors;
using Danmokou.SM;
using Danmokou.SRPG;
using Danmokou.SRPG.Diffs;
using Danmokou.SRPG.Nodes;
using Scriptor.Compile;
using Scriptor.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Danmokou.SRPG {

public interface IUnitDisplay {
    void RunRIEnumerator(IEnumerator ienum);
    void SetLocation(Node? node);

    /// <summary>
    /// Called only when <see cref="NewUnit"/> is reversed.
    /// </summary>
    void Uninstantiate();
}

public class UnitDisplay : CoroutineRegularUpdater, IUnitDisplay,
    ICircularGrazableEnemySimpleBulletCollisionReceiver,
    ICircularGrazableEnemyPatherCollisionReceiver,
    ICircularGrazableEnemyLaserCollisionReceiver,
    ICircularGrazableEnemyBulletCollisionReceiver {
    public Transform Tr { get; private set; } = null!;
    public Hurtbox Hurtbox => new(Beh.Location, 0.2f, 0.4f);
    private ISRPGExecutor exec = null!;
    private Material mat = null!;
    private MaterialPropertyBlock pb = null!;
    private float t;
    public Unit Unit { get; private set; } = null!;
    private readonly Evented<UnitStatus> status = new(UnitStatus.NotMyTurn);
    private readonly PushLerper<Color> color = new(0.1f);
    private readonly Pushable pusher = new(0.3f, 1f, 3f);

    [field: SerializeField] public BehaviorEntity Beh { get; private set; } = null!;
    public SpriteRenderer sr = null!;
    [field: SerializeField] public Sprite Portrait { get; private set; } = null!;
    public Transform pushMe = null!;
    public TextAsset tmp_ExecOnAtk = null!;
    private ReflWrap<Func<BehaviorEntity[], Vector2, StateMachine>> tmp_AttackOp = null!;
    private readonly HitCooldowns hitCDs = new();

    private void Awake() {
        Tr = transform;
        mat = sr.material; //non-shared for keywords
        sr.GetPropertyBlock(pb = new());
        tmp_AttackOp = ReflWrap.FromFunc($"UnitDisplayAttack{GetHashCode()}", () =>
            CompileHelpers.ParseAndCompileDelegate<Func<BehaviorEntity[], Vector2, StateMachine>>(
                tmp_ExecOnAtk.text, new DelegateArg<BehaviorEntity[]>("targets"),
                new DelegateArg<Vector2>("targetLoc")));
    }

    protected override void BindListeners() {
        RegisterService<IEnemySimpleBulletCollisionReceiver>(this);
        RegisterService<IEnemyPatherCollisionReceiver>(this);
        RegisterService<IEnemyLaserCollisionReceiver>(this);
        RegisterService<IEnemyBulletCollisionReceiver>(this);
        base.BindListeners();
    }

    private void UpdateDisplayer() {
        if (Unit == null!) return;
        Beh.Dependent<DisplayController>().SetFlip(Unit.Team.FlipSprite, false);
    }

    public void Initialize(ISRPGExecutor executor, Unit u) {
        exec = executor;
        Unit = u;
        Beh.UpdateID(u.Key);
        UpdateDisplayer();
        Listen(color, c => sr.color = c);
        Listen(status, s => {
            mat.SetOrUnsetKeyword(s is UnitStatus.NotMyTurn, "FT_GRAYSCALE");
            color.Push(Unit.Status switch {
                UnitStatus.CanMove or UnitStatus.MustAct => Color.white,
                UnitStatus.Exhausted or UnitStatus.NotMyTurn => new Color(0.7f, 0.7f, 0.7f),
                _ => throw new ArgumentOutOfRangeException()
            });
        });
    }

    public async Task AnimateAttack(UseUnitSkill skill, ICancellee cT, SubList<IGameDiff> caused) {
        var targets = caused
            .CastFilter<IGameDiff, IUnitXUnitGameDiff>()
            .Select(x => x.Target)
            .SelectNotNull(exec.FindUnit)
            .ToHashSet();
        var behs = targets.Select(target => target.Beh).ToArray();
        await Beh.RunExternalSM(SMRunner.RunRoot(tmp_AttackOp.Value(behs, skill.Target.CellCenter), cT));
    }

    public Task AnimateMove(MoveUnit ev, ICancellee cT) {
        var tcs = new TaskCompletionSource<System.Reactive.Unit>();
        RunRIEnumerator(_AnimateMove());
        return tcs.Task;
        IEnumerator _AnimateMove() {
            var time = 0.3f * MathF.Log(ev.Path!.Count);
            for (var elapsed = 0f; elapsed < time && !cT.Cancelled; elapsed += ETime.FRAME_TIME) {
                var effT = Easers.EIOSine(elapsed / time);
                //rounding errors can make effT close enough to 1 for idx to be path.Count
                var idx = Math.Min(ev.Path.Count - 1, (int)Math.Floor(effT * ev.Path.Count));
                var prevLoc = (ev.Path.Try(idx - 1) ?? ev.From).CellCenter;
                SetLocation(
                    Vector3.Lerp(prevLoc, ev.Path[idx].CellCenter, effT * ev.Path.Count - idx));
                yield return null;
            }
            tcs.SetResult(default);
        }
    }

    public async Task SendToGraveyard(GraveyardUnit ev, ICancellee cT) {
        var eff = CompileHelpers.ParseAndCompileDelegate<Func<Vector4, Vector4, StateMachine>>(
            exec.Config.UnitDeathScript.text,
            new DelegateArg<Vector4>("theme1"), new DelegateArg<Vector4>("theme2"));
        try {
            await Beh.RunExternalSM(SMRunner.RunRoot(eff(Unit.Theme1, Unit.Theme2), cT));
        } finally {
            if (!cT.IsHardCancelled())
                gameObject.SetActive(false);
        }
    }

    //If the node is null, we don't need to change the current position, as we should immediately
    // receive either an Uninstantiate (destroy) or Graveyard (disable) command.
    public void SetLocation(Node? node) {
        if (node != null)
            SetLocation(node.CellCenter);
    }
    public void SetLocation(Vector2 worldLoc) => Beh.ExternalSetLocalPosition(Tr.position = worldLoc);

    public override void RegularUpdate() {
        base.RegularUpdate();
        status.PublishIfNotSame(Unit.Status);
        color.Update(ETime.FRAME_TIME);
        pb.SetFloat(PropConsts.time, t += ETime.FRAME_TIME);
        sr.SetPropertyBlock(pb);
        pusher.DoUpdate(ETime.FRAME_TIME);
        pushMe.localPosition = pusher.CurrDisplace;
        hitCDs.ProcessFrame();
    }

    public void Uninstantiate() => Object.Destroy(this);

    public bool TakeHit(CollisionResult coll, int damage, in ParametricInfo bulletBPI, ushort grazeEveryFrames,
        Vector2? collLoc = null) {
        var at = collLoc ?? bulletBPI.LocV2;
        if (coll.collide && hitCDs.TryAdd(bulletBPI.id, 6)) {
            //todo - fixed cd at 6 frames for now
            pusher.Push((Hurtbox.location - at).normalized * 0.14f);
            if (bulletBPI.ctx.onHit != null)
                bulletBPI.ctx.onHit.Proc(at, Hurtbox.location, Hurtbox.radius);
            return true;
        } else if (coll.graze) {
            return false;
        }
        return false;
    }

    bool ICircularGrazableEnemySimpleBulletCollisionReceiver.TakeHit(BulletManager.SimpleBulletCollection sbc,
        CollisionResult coll, int damage, in ParametricInfo bulletBPI) {
        if (sbc.MetaType == BulletManager.SimpleBulletCollection.CollectionType.Normal)
            return TakeHit(coll, damage, bulletBPI, sbc.BC.grazeEveryFrames);
        else
            return false;
    }

    void ICircularGrazableEnemyPatherCollisionReceiver.TakeHit(CurvedTileRenderPather pather, Vector2 collLoc,
        CollisionResult collision) =>
        TakeHit(collision, pather.Pather.Damage, in pather.BPI, pather.Pather.collisionInfo.grazeEveryFrames, collLoc);

    void ICircularGrazableEnemyLaserCollisionReceiver.TakeHit(CurvedTileRenderLaser laser, Vector2 collLoc,
        CollisionResult collision) =>
        TakeHit(collision, laser.Laser.Damage, in laser.BPI, laser.Laser.collisionInfo.grazeEveryFrames, collLoc);

    void ICircularGrazableEnemyBulletCollisionReceiver.TakeHit(Bullet bullet, Vector2 collLoc,
        CollisionResult collision) =>
        TakeHit(collision, bullet.Damage, in bullet.rBPI, bullet.collisionInfo.grazeEveryFrames, collLoc);


    public bool ReceivesBulletCollisions(string? style) =>
        style?.EndsWith(Beh.ID) is true || //eg. needle-orange.Yukari; targeted at one unit
        style?.Contains('.') is false; // eg. gdlaser-red/w; targeted at a location collides with any enemies
    
    protected override void OnEnable() {
        UpdateDisplayer();
        base.OnEnable();
    }

    protected override void OnDisable() {
        hitCDs.Clear();
        base.OnDisable();
    }

    [ContextMenu("Push")]
    public void Push() {
        pusher.Push(new(0.5f, 0.5f));
    }


#if UNITY_EDITOR
    private void OnDrawGizmos() {
        if (!Application.isPlaying) return;
        Handles.color = Color.cyan;
        var position = Hurtbox.location;
        Handles.DrawWireDisc(position, Vector3.forward, Hurtbox.radius);
        Handles.color = Color.blue;
        Handles.DrawWireDisc(position, Vector3.forward, Hurtbox.grazeRadius);
        Handles.color = Color.black;
        Handles.DrawLine(position + Vector2.up * .5f, position + Vector2.down * .5f);
        Handles.DrawLine(position + Vector2.right * .2f, position + Vector2.left * .2f);
        Handles.color = Color.yellow;
    }
#endif
}
}