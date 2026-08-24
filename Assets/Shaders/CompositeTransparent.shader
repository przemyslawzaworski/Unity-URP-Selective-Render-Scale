Shader "Hidden/LowResWorld/CompositeTransparent"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
	
    half4 CompositeTransparent(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        // TransparentColor is built in low resolution.  It contains hard holes where
        // PrepareTransparentDepth occluded low-res transparent geometry with the
        // high-resolution opaque world.  Sampling the whole buffer bilinearly makes
        // RGB/alpha from a neighbouring visible texel leak into such a hole, which
        // appears as a coloured halo on Default/non-RenderScale geometry.
        //
        // Keep bilinear filtering for the colour *inside* covered low-res pixels, but
        // use nearest-neighbour alpha as an explicit coverage mask.  This prevents a
        // covered texel from contributing to a neighbouring occluded texel.
        half4 filtered = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
        half  coverage = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord).a;

        // TransparentColor is already premultiplied by the object's normal transparent
        // blend when it is rendered over a clear RT, so zeroing the complete value is
        // correct for an uncovered destination pixel.
        return filtered * step(1.0h / 255.0h, coverage);
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