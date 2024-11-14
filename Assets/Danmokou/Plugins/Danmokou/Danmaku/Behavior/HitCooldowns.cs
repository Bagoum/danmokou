using BagoumLib.DataStructures;

namespace Danmokou.Behavior {
public class HitCooldowns {
    private readonly DictionaryWithKeys<uint, int> hitCooldowns = new();
    
    public bool TryAdd(uint id, int cooldownFrames) {
        if (hitCooldowns.Data.ContainsKey(id))
            return false;
        hitCooldowns[id] = cooldownFrames;
        return true;
    }

    public void ProcessFrame() {
        for (int ii = 0; ii < hitCooldowns.Keys.Count; ++ii)
            if (hitCooldowns.Keys.GetMarkerIfExistsAt(ii, out var dm))
                if (--hitCooldowns[dm.Value] <= 0)
                    dm.MarkForDeletion();
        hitCooldowns.Keys.Compact();
    }

    public void Clear() {
        hitCooldowns.Clear();
    }
}
}