using System;
using System.Reflection;
using System.Threading.Tasks;
using BagoumLib.Expressions;
using UnityEngine;
using InternalTaskWhenAll = System.Func<System.Threading.Tasks.Task[], System.Threading.Tasks.Task>;

namespace Danmokou.Core {
public static class TaskHelpers {
    //Avoids repeat garbage allocation in Task.WhenAll
    public static readonly InternalTaskWhenAll TaskWhenAll = (InternalTaskWhenAll)
        Delegate.CreateDelegate(typeof(InternalTaskWhenAll),
            ExFunction.Wrap<Task>("InternalWhenAll", typeof(Task[])).Mi);
}
}