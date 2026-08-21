Shader "Hidden/LowResWorld/CompositeTransparent"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
	
    half4 CompositeTransparent(Varyings input) : SV_Target 
	{ 
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); 
		return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord); 
	}
	
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "CompositeTransparent"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeTransparent
            ENDHLSL
        }
    }
}