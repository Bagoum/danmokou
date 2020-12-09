using DMK.DMath;
using DMK.Graphics;
using UnityEngine;

namespace DMK.Behavior.Display {
/// <summary>
/// Class for basic powerup effects constructed from a circular aura going in or out.
/// </summary>
public class PowerUp : SpriteDisplayController {
    private TP4 color;
    private float maxTime;

    /// <summary>
    /// Initialize a powerup effect. By default, this appears to move inwards.
    /// For an outwards-moving "power release", set itrs negative.
    /// </summary>
    /// <param name="colorizer">Color function</param>
    /// <param name="time">Time to run the effect for</param>
    /// <param name="itrs">Number of iterations</param>
    public void Initialize(TP4 colorizer, float time, float itrs) {
        color = colorizer;
        sprite.color = new Color(0, 0, 0, 0);
        maxTime = time;
        pb.SetFloat(PropConsts.speed, itrs / time);
    }

    public override void UpdateRender() {
        sprite.color = ColorHelpers.V4C(color(beh.rBPI));
        base.UpdateRender();
        if (beh.rBPI.t > maxTime) {
            beh.InvokeCull();
        }
    }

    public void InvokeCull() => beh.InvokeCull();
}
}