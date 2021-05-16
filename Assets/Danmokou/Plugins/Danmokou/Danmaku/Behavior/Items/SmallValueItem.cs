
using Danmokou.Core;
using Danmokou.Player;

namespace Danmokou.Behavior.Items {
public class SmallValueItem : ValueItem {
    protected override ItemType Type => ItemType.SMALL_VALUE;
    protected override short RenderOffsetIndex => 3;
    protected override float RotationTurns => -2;
    
    protected override void AddMe(PlayerController collector, double bonus) => 
        collector.AddSmallValueItems(1, bonus);
}
}