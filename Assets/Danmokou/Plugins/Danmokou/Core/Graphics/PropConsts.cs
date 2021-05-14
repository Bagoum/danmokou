using Unity.Collections;
using UnityEngine;

namespace Danmokou.Graphics {
public static class PropConsts {
    public const string cycleKW = "FT_CYCLE";
    public const string tintKW = "FT_TINT";

    public static readonly int mainTex = Shader.PropertyToID("_MainTex");
    public static readonly int renderTex = Shader.PropertyToID("_RenderTex");
    public static readonly int trueTex = Shader.PropertyToID("_TrueTex");
    public static readonly int faderTex = Shader.PropertyToID("_FaderTex");
    public static readonly int fromTex = Shader.PropertyToID("_FromTex");
    public static readonly int toTex = Shader.PropertyToID("_ToTex");
    public static readonly int time = Shader.PropertyToID("_T");
    public static readonly int speed = Shader.PropertyToID("_Speed");
    public static readonly int xBlocks = Shader.PropertyToID("_BX");
    public static readonly int yBlocks = Shader.PropertyToID("_BY");
    public static readonly int maxTime = Shader.PropertyToID("_MaxT");
    public static readonly int frameT = Shader.PropertyToID("_InvFrameT");
    public static readonly int frameCt = Shader.PropertyToID("_Frames");
    public static readonly int slideInT = Shader.PropertyToID("_SlideInT");
    public static readonly int fadeInT = Shader.PropertyToID("_FadeInT");
    public static readonly int scaleInT = Shader.PropertyToID("_ScaleInT");
    public static readonly int scaleInMin = Shader.PropertyToID("_ScaleInMin");
    public static readonly int cycleSpeed = Shader.PropertyToID("_CycleSpeed");
    public static readonly int blendSrcMethod = Shader.PropertyToID("_BlendFrom");
    public static readonly int blendDstMethod = Shader.PropertyToID("_BlendTo");
    public static readonly int blendOp = Shader.PropertyToID("_BlendOp");
    public static readonly int radius = Shader.PropertyToID("_R");
    public static readonly int fillRatio = Shader.PropertyToID("_F");
    public static readonly int innerFillRatio = Shader.PropertyToID("_FI");
    public static readonly int subradius = Shader.PropertyToID("_Subradius");
    public static readonly int threshold = Shader.PropertyToID("_Threshold");
    public static readonly int texWidth = Shader.PropertyToID("_TexWidth");
    public static readonly int texHeight = Shader.PropertyToID("_TexHeight");

    public static readonly int tint = Shader.PropertyToID("_Tint");

    public static readonly int fillColor2 = Shader.PropertyToID("_CF2");
    public static readonly int fillColor = Shader.PropertyToID("_CF");
    public static readonly int fillInnerColor = Shader.PropertyToID("_CFI");
    public static readonly int unfillColor = Shader.PropertyToID("_CE");
    public static readonly int redColor = Shader.PropertyToID("_CR");
    public static readonly int greenColor = Shader.PropertyToID("_CG");
    public static readonly int blueColor = Shader.PropertyToID("_CB");
    public static readonly int color1 = Shader.PropertyToID("_C1");
    public static readonly int color2 = Shader.PropertyToID("_C2");
    public static readonly int color3 = Shader.PropertyToID("_C3");

    public static readonly int blurRadius = Shader.PropertyToID("_BlurRad");

    public static readonly int R2CPhaseStart = Shader.PropertyToID("_P1");
    public static readonly int R2NPhaseStart = Shader.PropertyToID("_P2");
    public static readonly int R2NColor = Shader.PropertyToID("_CN");

    public static readonly int FragmentDiameter = Shader.PropertyToID("_FragDiameter");
    public static readonly int FragmentSides = Shader.PropertyToID("_FragSides");
    public static readonly int UVX = Shader.PropertyToID("_UVX");
    public static readonly int UVY = Shader.PropertyToID("_UVY");
    public static readonly int ScreenX = Shader.PropertyToID("_ScreenX");
    public static readonly int ScreenY = Shader.PropertyToID("_ScreenY");
    public static readonly int OffsetX = Shader.PropertyToID("_OffX");
    public static readonly int OffsetY = Shader.PropertyToID("_OffY");
    public static readonly int Zoom = Shader.PropertyToID("_Zoom");
    public static readonly int ScaleX = Shader.PropertyToID("_ScaleX");
    public static readonly int ScaleY = Shader.PropertyToID("_ScaleY");
    public static readonly int Angle = Shader.PropertyToID("_Angle");

    public static readonly int DisplaceTex = Shader.PropertyToID("_DisplaceTex");
    public static readonly int DisplaceMask = Shader.PropertyToID("_DisplaceMask");
    public static readonly int DisplaceMag = Shader.PropertyToID("_DisplaceMagnitude");
    public static readonly int DisplaceSpd = Shader.PropertyToID("_DisplaceSpeed");
    public static readonly int DisplaceXMul = Shader.PropertyToID("_DisplaceXMul");
    public static readonly int SharedOpacityMul = Shader.PropertyToID("_SharedOpacityMul");

    public static readonly int multiplier = Shader.PropertyToID("_Mult");

    public static readonly int angle0 = Shader.PropertyToID("_A0");
    public static readonly int pmDirection = Shader.PropertyToID("_PMDir");

    public static readonly int HueShift = Shader.PropertyToID("_HueShift");
    public static readonly int RecolorB = Shader.PropertyToID("_RecolorizeB");
    public static readonly int RecolorW = Shader.PropertyToID("_RecolorizeW");


    private static readonly int outlineColorProp = Shader.PropertyToID("_OutlineColor");
    private static readonly int underlayColorProp = Shader.PropertyToID("_UnderlayColor");

    public static void SetMaterialOutline(this Material m, Color c) {
        m.SetColor(outlineColorProp, c);
        m.SetColor(underlayColorProp, c);
    }

}

public static class PBHelpers {
    /*
    public static void SetArray<T>(this MaterialPropertyBlock pb, int propertyID, ComputeBuffer intermediary, NativeArray<T> data,
        int count) where T: struct {
        intermediary.SetData(data, 0, 0, count);
        pb.SetBuffer(propertyID, intermediary);
    }
    public static void SetArray<T>(this MaterialPropertyBlock pb, int propertyID, ComputeBuffer intermediary, T[] data,
        int count) where T: struct {
        intermediary.SetData(data, 0, 0, count);
        pb.SetBuffer(propertyID, intermediary);
    }*/
}
}
