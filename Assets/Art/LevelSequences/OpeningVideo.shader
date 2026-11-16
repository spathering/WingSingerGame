Shader "WingSinger/Opening Video" {
    Properties { _MainTex("Video",2D)="black"{} }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 frag(Varyings i):SV_Target{half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);c.a=smoothstep(.015,.055,max(c.r,max(c.g,c.b)));return c;}
            ENDHLSL
        }
    }
}
