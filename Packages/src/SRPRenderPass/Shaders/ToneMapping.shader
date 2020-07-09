Shader "Hidden/ToneMapping"
{
    SubShader
    {
    
        Tags { "RenderType" = "Opaque" "RenderPipeline" = ""}

        Pass
        {
            Name "DeferredLighting"

            Blend One Zero
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma exclude_renderers gles d3d11_9x
            #pragma enable_d3d11_debug_symbols
            #pragma vertex Vertex
            #pragma fragment Fragment

            //#include "LWRP/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.render-pipelines.custom/ShaderLibrary/FrameBufferFetchUtl.hlsl"
            UNITY_DECLARE_FRAMEBUFFER_INPUT_FLOAT(0); // Albedo
            UNITY_DECLARE_FRAMEBUFFER_INPUT_FLOAT(1); // SpecRoughness
            UNITY_DECLARE_FRAMEBUFFER_INPUT_FLOAT(2); // Normal
            UNITY_DECLARE_FRAMEBUFFER_INPUT_FLOAT(3); // Depth

            float4 Vertex(float4 vertexPosition : POSITION) : SV_POSITION
            {
                return vertexPosition;
            }

            float4 Fragment(float4 pos : SV_POSITION) : SV_Target
            {
                float3 albedo = UNITY_READ_FRAMEBUFFER_INPUT(0, pos).rgb;
                /*half4 specRoughness = UNITY_READ_FRAMEBUFFER_INPUT(1, pos);
                half3 normalWS = normalize((UNITY_READ_FRAMEBUFFER_INPUT(2, pos).rgb * 2.0h - 1.0h));
                float depth = UNITY_READ_FRAMEBUFFER_INPUT(3, pos).r;

                float2 positionNDC = pos.xy * _ScreenSize.zw;
                float3 positionWS = ComputeWorldSpacePosition(positionNDC, depth, UNITY_MATRIX_I_VP);
                

                half3 viewDirection = half3(normalize(GetCameraPositionWS() - positionWS));

                Light mainLight = GetMainLight();
                half3 specular = specRoughness.rgb;
                half roughness = specRoughness.a;

                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 radiance = mainLight.color * (mainLight.attenuation * NdotL);
                half reflectance = BDRF(roughness, normalWS, mainLight.direction, viewDirection);
                half3 color = (albedo + specular * reflectance) * radiance;*/
                albedo.g = 0;
                return float4(albedo, 1.0);
            }
            ENDHLSL
        }
    }
}
