// Rowdy Dandy: LEVIATHAN's tentacles (LeviathanArms.cs). The C# side bends a strip mesh along a wobbling spine every
// frame; this draws it like a wet, squishy, living thing - but in the game's pixel style:
//   - vertices snap to the 64-per-unit pixel grid (stair-stepped edges, no smooth vector look)
//   - a dark outline on both edges, a lighter belly with rows of sucker rings, banded (posterized) shading
//   - a glossy highlight that slides down the tentacle like slime, veins that pulse, a little colour breathing
// uv.x = along the tentacle (0 base .. 1 tip), uv.y = across (0 belly edge .. 1 back edge)
Shader "Rowdy Dandy/Tentacle"
{
    Properties
    {
        _Base ("Base", Color) = (0.42, 0.16, 0.62, 1)
        _Tip ("Tip", Color) = (0.35, 0.9, 1, 1)
        _Belly ("Belly", Color) = (1, 0.62, 0.8, 1)
        _Outline ("Outline", Color) = (0.1, 0.03, 0.16, 1)
        _Seed ("Seed", Float) = 0
        _Flash ("Flash", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Base, _Tip, _Belly, _Outline;
            float _Seed, _Flash;

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                w.xy = round(w.xy * 64.0) / 64.0;            // the game's pixel grid
                o.positionCS = TransformWorldToHClip(w);
                o.color = v.color;
                o.uv = v.uv;
                o.world = w.xy;
                return o;
            }

            float band(float x, float steps) { return floor(saturate(x) * steps + 0.5) / steps; }

            half4 frag(Varyings i) : SV_Target
            {
                float u = i.uv.x, v = i.uv.y;
                float t = _Time.y + _Seed * 7.13;
                float edge = min(v, 1 - v);
                // outline: thicker near the base, 1 px-ish at the tip
                float rim = lerp(0.09, 0.17, u);
                if (edge < rim) return half4(_Outline.rgb, i.color.a);

                float3 col = lerp(_Base.rgb, _Tip.rgb, band(u * u * 0.6 + u * 0.4, 5));
                // breathing colour
                col *= 0.92 + 0.08 * sin(t * 3 + u * 6);

                // the belly: lighter, with rows of sucker rings
                if (v < 0.46)
                {
                    float3 belly = lerp(_Belly.rgb, _Tip.rgb, u * 0.35);
                    col = lerp(col, belly, 0.75);
                    float count = 9;
                    float2 q = float2((frac(u * count) - 0.5) * 2.2, (v - 0.28) / 0.12);
                    float d = length(q);
                    float size = lerp(1.0, 0.45, u);           // suckers shrink toward the tip
                    if (d < size && d > size * 0.55) col = lerp(col, _Outline.rgb, 0.45);
                    else if (d <= size * 0.55) col = lerp(col, _Belly.rgb * 1.2, 0.6);
                }
                else
                {
                    // the back: banded shading, darker toward the far edge, pulsing veins
                    float shade = band(1.15 - (v - 0.46) * 0.9, 4);
                    col *= shade;
                    float vein = abs(sin(u * 23 + sin(u * 7 + t * 2) * 1.5 + v * 3));
                    if (vein < 0.08 && v > 0.55) col *= 0.7 + 0.15 * sin(t * 6);
                }

                // slime highlight sliding down toward the tip
                float sheen = exp(-pow((v - 0.68) * 9, 2)) * step(0.55, frac(u * 3.0 - t * 0.9));
                if (sheen > 0.45) col = lerp(col, float3(1, 1, 1), 0.65);
                // tiny glints
                float2 g = floor(i.world * 64);
                float h = frac(sin(dot(g, float2(12.9898, 78.233)) + floor(t * 6)) * 43758.5453);
                if (h > 0.985 && v > 0.5) col = lerp(col, float3(1, 1, 1), 0.5);

                col = lerp(col, float3(1, 0.85, 0.95), saturate(_Flash));
                return half4(col, i.color.a);
            }
            ENDHLSL
        }
    }
}
