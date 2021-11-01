using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
using Danmokou.SM;

namespace Danmokou.Core {
public static class Statistics {
    public static Dictionary<BossPracticeRequestKey, (int success, int total)> AccSpellHistory(IEnumerable<InstanceRecord> over, Func<BossPracticeRequestKey, bool>? pred = null) {
        var res = new Dictionary<BossPracticeRequestKey, (int, int)>();
        foreach (var g in over) {
            foreach (var cpt in g.CardHistory.record.Values.SelectMany(x => x)) {
                if (pred?.Invoke(cpt.Key) ?? true) {
                    var (success, total) = res.SetDefault(cpt.Key, (0, 0));
                    ++total;
                    if (cpt.Captured) ++success;
                    res[cpt.Key] = (success, total);
                }
            }
        }
        return res;
    }

    public class StatsGenerator {
        private readonly InstanceRecord[] arr;
        // ReSharper disable once NotAccessedField.Local
        private readonly SMAnalysis.AnalyzedCampaign[] campaigns;
        private readonly Dictionary<BossPracticeRequestKey, (int, int)> spellHist;
        public readonly bool HasSpellHist;

        private static float SuccessRate((int success, int total) entry) =>
            entry.success == 0 ? 0 : // solves 0/0
            entry.success / (float) entry.total;

        private static float SpellHistSuccess((BossPracticeRequestKey, (int success, int total)) entry) =>
            SuccessRate(entry.Item2);
        
        public StatsGenerator(IEnumerable<InstanceRecord> over, SMAnalysis.AnalyzedCampaign[] campaigns,
            Func<BossPracticeRequestKey, bool>? spellHistPred = null) {
            arr = over.ToArray();
            this.campaigns = campaigns;
            this.spellHist = AccSpellHistory(arr, spellHistPred);
            HasSpellHist = spellHist.Count > 0;
        }

        public int TotalRuns => arr.Length;
        public int CompletedRuns => arr.Count(ir => ir.Completed);
        public int OneCCRuns => arr.Count(ir => ir.OneCreditClear);
        public int TotalDeaths => arr.Sum(ir => ir.HitsTaken);
        public (DayOfWeek, InstanceRecord[]) FavoriteDay => 
            arr.GroupBy(ir => ir.Date.DayOfWeek).MaxByGroupSize();
        public (ShipConfig, InstanceRecord[]) FavoriteShip => 
            arr.GroupBy(ir => ir.SharedInstanceMetadata.team.ships[0].ship).MaxByGroupSize();
        public ((ShipConfig, ShotConfig), InstanceRecord[]) FavoriteShot => 
            arr.GroupBy(ir => ir.SharedInstanceMetadata.team.ships[0]).MaxByGroupSize();
        public long MaxScore => arr.Max(ir => ir.Score);

        public float CaptureRate =>
            SuccessRate(spellHist.Values.Aggregate((0, 0), (a, b) => (a.Item1 + b.Item1, a.Item2 + b.Item2)));

        public int TotalFrames => arr.Sum(ir => ir.TotalFrames);
        public int AvgFrames => TotalRuns == 0 ? 0 : TotalFrames / TotalRuns;
        
        public (BossPracticeRequest, float) BestCapture {
            get {
                var (cpt, succ) = spellHist.Items().MaxBy(SpellHistSuccess);
                return ((cpt.Reconstruct() as BossPracticeRequest)!, SuccessRate(succ));
            }
        }
        public (BossPracticeRequest, float) WorstCapture {
            get {
                var (cpt, succ) = spellHist.Items().MaxBy(x => -SpellHistSuccess(x));
                return ((cpt.Reconstruct() as BossPracticeRequest)!, SuccessRate(succ));
            }
        }

    }
}
}