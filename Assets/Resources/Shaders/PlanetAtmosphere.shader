// Nexus Civilization - planet atmosphere shell.
//
// Renders the inner faces of a slightly larger sphere with an additive Fresnel term, which
// produces a soft cyan limb glow around the planet without any post processing. It is a
// single forward pass with no lighting: the cheapest convincing atmosphere for mobile GPUs.
Shader "Nexus/Planet Atmosphere"
{
    Properties
    {
        _Color ("Atmosphere Color", Color) = (0.133, 0.827, 0.933, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.1
        _Power ("Rim Power", Range(0.5, 12)) = 2.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Front

        Pass
        {
            Name "Atmosphere"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Power;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positions.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(input.viewWS);

                // Rim term: strongest where the surface turns away from the camera.
                float fresnel = pow(saturate(1.0 - saturate(dot(normal, view))), _Power);

                half3 color = _Color.rgb * _Intensity * fresnel;
                half alpha = saturate(fresnel * _Color.a);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
