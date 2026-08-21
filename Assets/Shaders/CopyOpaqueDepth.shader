Shader "Hidden/LowResWorld/CopyOpaqueDepth"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "CopyOpaqueDepth"
            ZWrite On
            ZTest Always
            Cull Off
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
            TEXTURE2D_X_FLOAT(_LowResSourceDepth);
            SAMPLER(sampler_LowResSourceDepth);
			
            struct Attributes 
			{ 
				uint vertexID : SV_VertexID; 
				UNITY_VERTEX_INPUT_INSTANCE_ID 
			};
			
            struct Varyings 
			{ 
				float4 positionCS : SV_POSITION; 
				float2 uv : TEXCOORD0; 
				UNITY_VERTEX_OUTPUT_STEREO 
			};
			
            Varyings Vert(Attributes input) 
			{ 
				Varyings output; 
				UNITY_SETUP_INSTANCE_ID(input); 
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output); 
				output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID); 
				output.uv = GetFullScreenTriangleTexCoord(input.vertexID); 
				return output; 
			}
			
            float Frag(Varyings input) : SV_Depth 
			{ 
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); 
				return SAMPLE_TEXTURE2D_X_LOD(_LowResSourceDepth, sampler_LowResSourceDepth, input.uv, 0).r; 
			}
			
            ENDHLSL
        }
    }
}