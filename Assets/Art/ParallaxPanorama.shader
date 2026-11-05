Shader "WingSinger/URP Parallax Panorama" {
    Properties { _MainTex ("Panorama", 2D) = "white" {} _NextTex ("Next frame", 2D) = "white" {} _FrameBlend ("Frame blend", Range(0,1)) = 0 }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NextTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _FrameBlend;
            CBUFFER_END
            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }
            half4 frag(Varyings input) : SV_Target {
                half4 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 b = SAMPLE_TEXTURE2D(_NextTex, sampler_MainTex, input.uv);
                a.rgb *= a.a; b.rgb *= b.a;
                return lerp(a, b, _FrameBlend);
            }
            ENDHLSL
        }
    }
}
