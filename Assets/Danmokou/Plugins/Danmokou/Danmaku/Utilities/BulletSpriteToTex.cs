using System.Collections;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Graphics;
using SuzunoyaUnity.Rendering;
using UnityEngine;

public class BulletSpriteToTex : CoroutineRegularUpdater {
#if UNITY_EDITOR
    public override void FirstFrame() {
        var sr = GetComponent<SpriteRenderer>();
        var sprite = BulletManager.TPool("scircle-red/b").BC.GetOrLoadRI();
        sr.sprite = sprite.Sprite;
        var rt = RenderHelpers.DefaultTempRT((sprite.Sprite!.texture.width, sprite.Sprite.texture.height));
        Graphics.Blit(sprite.Sprite.texture, rt, sr.material);
        var tex = rt.IntoTex();
        rt.Release();
        FileUtils.WriteTex("bs1.png", tex, FileUtils.ImageFormat.PNG);
        Object.Destroy(tex);
    }
#endif
}