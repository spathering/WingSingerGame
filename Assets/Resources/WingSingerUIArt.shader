Shader "WingSinger/UI Art Color" {
 Properties {
  [PerRendererData] _MainTex("Artwork",2D)="white" {}
  _Color("Tint",Color)=(1,1,1,1)
  _Saturation("Saturation",Range(0,2))=1.15
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
   struct appdata { float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 world:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
   sampler2D _MainTex;
   float4 _Color,_TextureSampleAdd,_ClipRect;
   float _Saturation;
   v2f vert(appdata input) {
    v2f o;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=input.vertex;o.vertex=UnityObjectToClipPos(input.vertex);o.uv=input.uv;o.color=input.color*_Color;return o;
   }
   half4 frag(v2f input):SV_Target {
    half4 color=(tex2D(_MainTex,input.uv)+_TextureSampleAdd)*input.color;
    half luminance=dot(color.rgb,half3(.2126,.7152,.0722));
    color.rgb=max(0,lerp(luminance.xxx,color.rgb,_Saturation));
    #ifdef UNITY_UI_CLIP_RECT
    color.a*=UnityGet2DClipping(input.world.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(color.a-.001);
    #endif
    color.rgb*=color.a;return color;
   }
   ENDCG
  }
 }
}
