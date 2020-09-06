using System.Collections.Generic;

namespace Danmaku {
public static class PlayerFireDataHoisting {
     private static readonly Dictionary<uint, (int bossDmg, int stageDmg, EffectStrategy eff)> fires = 
          new Dictionary<uint, (int, int, EffectStrategy)>();

     public static void DestroyAll() {
          fires.Clear();
     }
     public static void Record(uint id, (int, int, EffectStrategy) cfg) {
          fires[id] = cfg;
     }

     public static (int bossDmg, int stageDmg, EffectStrategy eff)? Retrieve(uint id) {
          if (fires.TryGetValue(id, out var eff)) {
               return eff;
          }
          return null;
     }
     public static void Delete(uint id) {
          fires.Remove(id);
     }

}
}