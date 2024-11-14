using System.Collections;
using System.Collections.Generic;
using Danmokou.Scriptables;

namespace Danmokou.SRPG {
public interface ISRPGExecutor {
    IEnumerable<UnitDisplay> AllUnits { get; }
    UnitDisplay? FindUnit(Unit u);
    SRPGDataConfig Config { get; }
}
}