using BagoumLib;
using Danmokou.Core;
using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Behavior {
public class SpikeObstacle : MonoBehaviour {
    public bool m_ignoresInvuln;
    private void OnTriggerEnter2D(Collider2D other) {
        if (ServiceLocator.MaybeFind<PlayerController>().Try(out var player) && player.unityCollider == other) {
            player.LoseLives(1, m_ignoresInvuln, m_ignoresInvuln);
        }
    }
}
}