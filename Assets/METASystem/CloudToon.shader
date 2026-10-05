Shader "CloudHop/Soft Cloud"
{
    Properties { _BaseColor("Macaron Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Cloud"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float light = smoothstep(-.75, .8, dot(normal, normalize(float3(-.4, .85, -.3))));
                half3 baseColor = _BaseColor.rgb * input.color.rgb;
                half3 shadow = baseColor * half3(.76, .79, .89);
                half3 color = lerp(shadow, lerp(baseColor, half3(1, .99, .97), .24), light);
                float rim = pow(1 - saturate(dot(normal, view)), 3) * .12;
                color = lerp(color, half3(1, 1, 1), rim);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
