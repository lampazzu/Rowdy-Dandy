Shader "Custom/Water2D_Advanced"
{
    Properties
    {
        [Header(Color and Transparency)]
        _DeepColor ("Deep Water Color", Color) = (0.1, 0.05, 0.3, 0.9)
        _ShallowColor ("Shallow Surface Color", Color) = (0.5, 0.1, 0.8, 0.75)
        
        [Header(Surface Waves and Noise)]
        _WaveSpeed ("Wave Speed (X, Y)", Vector) = (0.1, 0.05, 0, 0)
        _WaveScale ("Wave Noise Scale", Float) = 25.0
        _WaveDistortion ("Wave Distortion Amount", Range(0.0, 0.1)) = 0.02
        
        [Header(Surface Foam Line)]
        _FoamColor ("Foam Highlight Color", Color) = (0.9, 0.7, 1.0, 1.0)
        _FoamThickness ("Foam Line Thickness", Range(0.01, 0.2)) = 0.06
        
        [Header(Underwater Caustics Pattern)]
        _CausticColor ("Caustics Color", Color) = (0.8, 0.4, 1.0, 0.3)
        _CausticScale ("Caustics Scale", Float) = 15.0
        _CausticSpeed ("Caustics Movement Speed", Float) = 0.3
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            float4 _DeepColor;
            float4 _ShallowColor;
            float4 _FoamColor;
            float4 _CausticColor;
            float4 _WaveSpeed;
            float _WaveScale;
            float _WaveDistortion;
            float _FoamThickness;
            float _CausticScale;
            float _CausticSpeed;

            // Self-contained procedural noise generator (no external libraries needed)
            float2 Hash2D(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float GradientNoise(float2 UV, float Scale)
            {
                float2 p = UV * Scale;
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(dot(Hash2D(i + float2(0.0, 0.0)), f - float2(0.0, 0.0)),
                         dot(Hash2D(i + float2(1.0, 0.0)), f - float2(1.0, 0.0)), u.x),
                    lerp(dot(Hash2D(i + float2(0.0, 1.0)), f - float2(0.0, 1.0)),
                         dot(Hash2D(i + float2(1.0, 1.0)), f - float2(1.0, 1.0)), u.x), u.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1. UV Wave Distortion
                float2 timeOffset = _Time.y * _WaveSpeed.xy;
                float noise = GradientNoise(i.uv + timeOffset, _WaveScale);
                float2 distortedUV = i.uv + float2(noise * _WaveDistortion, noise * (_WaveDistortion * 0.5));

                // 2. Vertical Color Depth Gradient (Deep -> Shallow)
                fixed4 waterColor = lerp(_DeepColor, _ShallowColor, saturate(distortedUV.y));

                // 3. Animated Caustics Overlay
                float2 causticUV = i.worldPos.xy * _CausticScale * 0.1 + float2(_Time.y * _CausticSpeed, _Time.y * (_CausticSpeed * 0.7));
                float causticPattern = saturate(GradientNoise(causticUV, 5.0) * 2.0);
                waterColor.rgb += _CausticColor.rgb * causticPattern * _CausticColor.a * saturate(distortedUV.y);

                // 4. Surface Wave Foam Line
                float foamStep = smoothstep(1.0 - _FoamThickness, 1.0, distortedUV.y + (noise * 0.03));
                fixed4 finalColor = lerp(waterColor, _FoamColor, foamStep * _FoamColor.a);

                return finalColor;
            }
            ENDCG
        }
    }
}