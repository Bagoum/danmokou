using System;
using BagoumLib;
using Newtonsoft.Json;

namespace MiniProjects.PJ24 {
public record Request(string Requestor, ItemInstance Required, int ReqCount, (ItemInstance item, int ct)[] Reward, string Descr) {
    public string ShortDescr => $"{Requestor}'s Request";
    public bool Complete { get; set; }
    public bool Visible { get; set; }
    
    /// <summary>
    /// True iff the provided item matches the requirement.
    /// </summary>
    public bool Matches(ItemInstance item) {
        if (item.Type != Required.Type) return false;
        for (int ii = 0; ii < Required.Effects.Count; ++ii) {
            for (int jj = 0; jj < item.Effects.Count; ++jj)
                if (Required.Effects[ii].Type == item.Effects[jj].Type)
                    goto nxt;
            return false;
            nxt: ;
        }
        for (int ii = 0; ii < Required.Traits.Count; ++ii) {
            for (int jj = 0; jj < item.Traits.Count; ++jj)
                if (Required.Traits[ii].Type == item.Traits[jj].Type)
                    goto nxt;
            return false;
            nxt: ;
        }
        return true;
    }
}
}