using System;
using System.Collections.Generic;
using System.Linq;
using DMK.GameInstance;
using DMK.Scriptables;
using DMK.SM;

namespace DMK.Core {
public static class Statistics {
    public static Dictionary<((string campaign, string boss), int phase), (int success, int total)> AccSpellHistory(IEnumerable<InstanceRecord> over, Func<((string campaign, string boss), int phase), bool>? pred = null) {
        var res = new Dictionary<((string, string), int), (int, int)>();
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
        private readonly SMAnalysis.AnalyzedCampaign[] campaigns;
        private readonly Dictionary<((string, string), int), (int, int)> spellHist;
        public bool HasSpellHist;

        private static float SuccessRate((int success, int total) entry) =>
            entry.success == 0 ? 0 : // solves 0/0
            entry.success / (float) entry.total;

        private static float SpellHistSuccess((((string, string), int), (int success, int total)) entry) =>
            SuccessRate(entry.Item2);
        
        public StatsGenerator(IEnumerable<InstanceRecord> over, SMAnalysis.AnalyzedCampaign[] campaigns,
            Func<((string campaign, string boss), int phase), bool>? spellHistPred = null) {
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
        public (PlayerConfig, InstanceRecord[]) FavoritePlayer => 
            arr.GroupBy(ir => ir.SharedInstanceMetadata.team.players[0].player).MaxByGroupSize();
        public ((PlayerConfig, ShotConfig), InstanceRecord[]) FavoriteShot => 
            arr.GroupBy(ir => ir.SharedInstanceMetadata.team.players[0]).MaxByGroupSize();
        public long MaxScore => arr.Max(ir => ir.Score);

        public float CaptureRate =>
            SuccessRate(spellHist.Values.Fold((0, 0), (a, b) => (a.Item1 + b.Item1, a.Item2 + b.Item2)));

        public int TotalFrames => arr.Sum(ir => ir.TotalFrames);
        public int AvgFrames => TotalRuns == 0 ? 0 : TotalFrames / TotalRuns;
        
        public (BossPracticeRequest, float) BestCapture {
            get {
                var cpt = spellHist.Items().MaxBy(SpellHistSuccess);
                return (BossPracticeRequest.Reconstruct(cpt.key), SpellHistSuccess(cpt));
            }
        }
        public (BossPracticeRequest, float) WorstCapture {
            get {
                var cpt = spellHist.Items().MaxBy(x => -SpellHistSuccess(x));
                return (BossPracticeRequest.Reconstruct(cpt.key), SpellHistSuccess(cpt));
            }
        }

    }
}
}