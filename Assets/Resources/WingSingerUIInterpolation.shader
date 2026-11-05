Shader "WingSinger/UI Frame Blend" {
 Properties {
  [PerRendererData] _MainTex("Sprite", 2D)="white" {}
  _NextTex("Next frame",2D)="white" {}
  _Color("Tint",Color)=(1,1,1,1)
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
 }
 SubShader {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
  Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend One OneMinusSrcAlpha ColorMask [_ColorMask]
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
   sampler2D _MainTex, _NextTex;
   float4 _FrameRect, _NextRect, _ClipRect, _Color;
   float _FrameBlend;
   v2f vert(appdata input) {
    v2f o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=input.vertex; o.vertex=UnityObjectToClipPos(input.vertex); o.uv=input.uv; o.color=input.color*_Color; return o;
   }
   half4 frag(v2f input):SV_Target {
    float2 local=(input.uv-_FrameRect.zw)/max(_FrameRect.xy,float2(.00001,.00001));
    half4 a=tex2D(_MainTex,input.uv), b=tex2D(_NextTex,local*_NextRect.xy+_NextRect.zw);
    a.rgb*=a.a; b.rgb*=b.a;
    half4 color=lerp(a,b,_FrameBlend); color.rgb*=input.color.rgb*input.color.a; color.a*=input.color.a;
    #ifdef UNITY_UI_CLIP_RECT
    color*=UnityGet2DClipping(input.world.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(color.a-.001);
    #endif
    return color;
   }
   ENDCG
  }
 }
}
