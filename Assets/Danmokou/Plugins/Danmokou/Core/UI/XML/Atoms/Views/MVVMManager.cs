using System;
using BagoumLib.DataStructures;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
public class MVVMManager {
    private record ViewState(IUIView View) {
        public BindingUpdateTrigger LastUpdateTrigger { get; set; } = BindingUpdateTrigger.EveryUpdate;
        public long LastHashCode { get; set; } = 0;

        public bool Update() {
            var vm = View.ViewModel;
            vm.UpdateEvents();
            var nxtUpdateTrigger = View.UpdateTrigger;
            var reqUpdateBase = nxtUpdateTrigger is BindingUpdateTrigger.EveryUpdate ||
                                nxtUpdateTrigger != LastUpdateTrigger;
            //dirty can be used even if binding trigger is not WhenDirty
            var isDirty = View.TryConsumeDirty();
            var reqUpdate = reqUpdateBase || isDirty;
            if (!reqUpdate && nxtUpdateTrigger is BindingUpdateTrigger.OnSourceChanged) {
                var nxtHash = vm.OverrideViewHash?.Invoke() ?? vm.GetViewHash();
                reqUpdate = nxtHash != LastHashCode;
                LastHashCode = nxtHash;
            }
            LastUpdateTrigger = nxtUpdateTrigger;
            if (!reqUpdate)
                return false;
            View.UpdateHTML();
            return true;
        }
    }
    private DMCompactingArray<ViewState> Views { get; } = new(32);
    
    public void UpdateViews() {
        var reqCompact = false;
        for (int ii = 0; ii < Views.Count; ++ii) {
            if (Views.GetIfExistsAt(ii, out var view)) {
                view.Update();
            } else
                reqCompact = true;
        }
        if (reqCompact)
            Views.Compact();
    }

    public IDisposable RegisterView(IUIView view) {
        return Views.Add(new(view));
    }
}
}