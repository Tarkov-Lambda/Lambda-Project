Shader "CustomFX/HDRFireParticle"
{
    Properties
    {
        _TintColor ("Smoke Color", Color) = (0.5,0.5,0.5,0.5)
        _MainTex ("Particle Texture", 2D) = "white" {}
        _HDRAmount ("HDR Mult", Range(1, 20)) = 1
    }
    SubShader
    {
        Tags 
        { 
            "IGNOREPROJECTOR" = "true" 
            "QUEUE" = "Transparent" 
            "RenderType" = "Transparent" 
        }

        Pass
        {
            Name "MBOIT_Pass"
            Tags 
            { 
                "IGNOREPROJECTOR" = "true" 
                "QUEUE" = "Transparent" 
                "RenderType" = "Transparent" 
            }

            Blend One One, One One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0
            #pragma multi_compile_particles
            
            // Re-adding the original keywords for compatibility, 
            // though this shader implements the "OFF" logic (Standard)
            #pragma shader_feature MBOIT_PARTICLES_OFF MBOIT_PARTICLES_ON MBOIT_PARTICLES_MOMENTS MBOIT_PARTICLES_NORM MBOIT_PARTICLES_OUT

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            float _HDRAmount;
            
            // Depth texture for soft particles
            sampler2D_float _CameraDepthTexture;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                #ifdef SOFTPARTICLES_ON
                    float4 projPos : TEXCOORD1;
                #endif
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                #ifdef SOFTPARTICLES_ON
                    o.projPos = ComputeScreenPos(o.vertex);
                    COMPUTE_EYEDEPTH(o.projPos.z);
                #endif

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Handle Soft Particles (Depth Fading)
                #ifdef SOFTPARTICLES_ON
                    float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                    float partZ = i.projPos.z;
                    float fade = saturate(sceneZ - partZ);
                    i.color.a *= fade;
                #endif

                float4 tex = tex2D(_MainTex, i.texcoord);
                
                // Reconstruction of the specific math found in the baked source:
                // 1. It uses the Texture Alpha (tex.w) for the RGB intensity.
                // 2. It multiplies everything by the vertex Alpha (i.color.a).
                // 3. It applies the HDR multiplier.
                
                fixed4 col;
                
                // RGB Calculation: TextureAlpha * VertexColor * Tint * HDR * VertexAlpha
                col.rgb = tex.a * i.color.rgb * _TintColor.rgb * _HDRAmount * i.color.a;
                
                // Alpha Calculation: TextureAlpha * VertexAlpha * TintAlpha
                col.a = tex.a * i.color.a * _TintColor.a;

                return col;
            }
            ENDCG
        }
    }
}