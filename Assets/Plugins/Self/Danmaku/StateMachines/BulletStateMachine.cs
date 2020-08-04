using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using Danmaku;
using DMath;
using Core;
using JetBrains.Annotations;
using UnityEngine;

namespace SM {
/// <summary>
/// `clear`: Clear the state of the running danmaku scene.
/// <br/>Primary usage is `clear phase` at the end of a phase to destroy all temporary information except bullets themselves.
/// </summary>
public class ClearLASM : ReflectableLASM {
    public ClearLASM(TaskPattern rs) : base(rs) {}

    public static TaskPattern Phase() => smh => {
        GameManagement.ClearPhase();
        return Task.CompletedTask;
    };
    public static TaskPattern PhaseAutocull(string cullPool, string defaulter) => smh => {
        GameManagement.ClearPhaseAutocull(cullPool, defaulter);
        return Task.CompletedTask;
    };

    public static TaskPattern BulletControl() => smh => {
        BulletManager.ClearPoolControls();
        return Task.CompletedTask;
    };
    public static TaskPattern Bullet() => smh => {
        BulletManager.ClearAllBullets();
        return Task.CompletedTask;
    };
    public static TaskPattern BulletFancy() => smh => {
        BulletManager.ClearNonSimpleBullets();
        return Task.CompletedTask;
    };
}
}
