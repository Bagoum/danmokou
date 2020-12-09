
using DMK.Core;

namespace DMK.Behavior.Items {
public class SmallValueItem : ValueItem {
    protected override short RenderOffsetIndex => 3;
    protected override float RotationTurns => -2;
    
    protected override void AddMe(double bonus) => GameManagement.instance.AddSmallValueItems(1, bonus);
}
}