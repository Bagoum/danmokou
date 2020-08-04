using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Danmaku;
using DMath;
using static MaterialUtils;

namespace SM {
/// <summary>
/// `op`: A SM class that handles many types of cancellable coroutine invocations.
/// </summary>
public class CoroutineLASM : ReflectableLASM {

    public CoroutineLASM(TaskPattern rs) : base(rs) { }

}
}