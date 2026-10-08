Shader "Custom/Water2D_Advanced"
{
    // Pixel-art water for DynamicWater2D.
    // Everything is snapped to the world pixel grid (Pixels Per Unit): the wavy surface becomes a stair-stepped
    // pixel edge and the colors become flat bands. Depth comes from layering, top to bottom:
    //   far foam edge -> top surface (seen from above, lit by the wave slopes, glints, crest foam)
    //   -> bright front lip -> shadow under the lip -> body (light shafts, reflections, depth bands)
    Properties
    {
        [Header(Pixel Art)]
        _PixelsPerUnit ("Pixels Per Unit (match your sprites)", Float) = 64
        _ColorSteps ("Depth Color Bands", Range(1, 8)) = 4
        _AnimFPS ("Animation Frame Rate", Float) = 10

        [Header(Body Color)]
        _DeepColor ("Deep Water Color", Color) = (0.1, 0.05, 0.3, 0.9)
        _ShallowColor ("Shallow Surface Color", Color) = (0.5, 0.1, 0.8, 0.75)
        _DepthFade ("Depth To Reach Deep Color (units)", Float) = 1.2
        _BandWobble ("Band Wobble", Range(0, 0.5)) = 0.12
        _BandWobbleScale ("Band Wobble Scale", Float) = 1.5
        _BandWobbleSpeed ("Band Wobble Speed", Float) = 0.6

        [Header(Top Surface)]
        _TopFacePixels ("Top Surface Thickness (pixels)", Range(0, 10)) = 3
        _SurfaceTopColor ("Top Surface Color", Color) = (0.95, 0.45, 1.0, 1.0)
        _SlopeLight ("Wave Slope Lighting", Range(0, 10)) = 4
        _GlintColor ("Glint Color", Color) = (1, 1, 1, 0.85)
        _GlintAmount ("Glint Amount", Range(0, 1)) = 0.12
        _GlintLength ("Glint Length (pixels)", Float) = 5
        _GlintSpeed ("Glint Drift Speed", Float) = 0.4
        _CrestFoamHeight ("Crest Foam Height (units)", Float) = 0.05

        [Header(Edges)]
        _FoamColor ("Far Edge Foam Color", Color) = (0.9, 0.7, 1.0, 1.0)
        _FoamPixels ("Far Edge Foam (pixels)", Range(0, 6)) = 1
        _FoamSparkle ("Far Edge Sparkle", Range(0, 1)) = 0.35
        _EdgeColor ("Front Lip Color", Color) = (1, 0.8, 1, 1)
        _LipShadow ("Shadow Under Lip", Range(0, 1)) = 0.35

        [Header(Under The Surface)]
        _HighlightColor ("Reflection Streaks", Color) = (1, 0.6, 1, 0.35)
        _StreakDepthPixels ("Reflection Streak Depth (pixels)", Float) = 8
        _StreakAmount ("Reflection Streak Amount", Range(0, 1)) = 0.25
        _RayColor ("Light Shafts Color", Color) = (1, 0.6, 1, 0.12)
        _RayScale ("Light Shafts Scale", Float) = 2.5
        _RayAmount ("Light Shafts Amount", Range(0, 1)) = 0.35

        [Header(Bottom Edge)]
        _BottomFade ("Bottom Fade Height (units, 0 = hard edge)", Float) = 1.25
        _BottomFadeDither ("Bottom Fade Dithered (pixel look)", Range(0, 1)) = 1

        [Header(Underwater Caustics)]
        _CausticColor ("Caustics Color", Color) = (0.8, 0.4, 1.0, 0.3)
        _CausticScale ("Caustics Scale", Float) = 15.0
        _CausticSpeed ("Caustics Speed", Float) = 0.3
        _CausticThreshold ("Caustics Amount", Range(0, 1)) = 0.6
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
                float2 surface : TEXCOORD1; // x = local Y of the surface, y = wave height above rest (world units)
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float surfaceY : TEXCOORD1; // world Y of the surface
                float wave : TEXCOORD2;
                float2 uv : TEXCOORD3;      // y: 0 at the bottom edge, 1 at the surface
            };

            float _PixelsPerUnit, _ColorSteps, _AnimFPS;
            float4 _DeepColor, _ShallowColor;
            float _DepthFade, _BandWobble, _BandWobbleScale, _BandWobbleSpeed;
            float _TopFacePixels, _SlopeLight, _GlintAmount, _GlintLength, _GlintSpeed, _CrestFoamHeight;
            float4 _SurfaceTopColor, _GlintColor;
            float4 _FoamColor, _EdgeColor;
            float _FoamPixels, _FoamSparkle, _LipShadow;
            float4 _HighlightColor, _RayColor;
            float _StreakDepthPixels, _StreakAmount, _RayScale, _RayAmount;
            float4 _CausticColor;
            float _CausticScale, _CausticSpeed, _CausticThreshold;
            float _BottomFade, _BottomFadeDither;

            // 4x4 ordered dither threshold (0..1) for a pixel
            float Bayer4(float2 p)
            {
                int x = (int)fmod(p.x, 4.0), y = (int)fmod(p.y, 4.0);
                static const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (m[y * 4 + x] + 0.5) / 16.0;
            }

            float2 Hash2D(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float Hash1D(float p)
            {
                return frac(sin(p * 127.1) * 43758.5453123);
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(41.3, 289.1))) * 43758.5453123);
            }

            float GradientNoise(float2 p)
            {
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
                o.surfaceY = mul(unity_ObjectToWorld, float4(v.vertex.x, v.surface.x, v.vertex.z, 1.0)).y;
                o.wave = v.surface.y;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float ppu = max(1.0, _PixelsPerUnit);

                // Center of the world pixel this fragment belongs to
                float2 px = (floor(i.worldPos.xy * ppu) + 0.5) / ppu;

                // Surface height and slope at that pixel's center (both linear across the triangle)
                float dx = ddx(i.worldPos.x);
                float slope = abs(dx) > 1e-6 ? ddx(i.surfaceY) / dx : 0.0;
                float surface = i.surfaceY + slope * (px.x - i.worldPos.x);

                // Whole pixels below the surface; above it -> not water
                float depthPx = floor((surface - px.y) * ppu);
                clip(depthPx);

                // Stepped time, like a sprite animation
                float t = floor(_Time.y * _AnimFPS) / max(1.0, _AnimFPS);

                // Wave faces turned toward the light (upper left) get brighter, the others darker; in 3 steps
                float shade = clamp(round(slope * _SlopeLight * 3.0) / 3.0, -1.0, 1.0);

                // ---------- Body ----------
                float depth = depthPx / ppu;
                float wobble = GradientNoise(px * _BandWobbleScale + float2(t * _BandWobbleSpeed, 0)) * _BandWobble;
                float band = saturate(depth / max(0.01, _DepthFade) + wobble);
                band = floor(band * _ColorSteps + 0.5) / max(1.0, _ColorSteps);
                fixed4 col = lerp(_ShallowColor, _DeepColor, band);

                // Light shafts slanting down from the surface
                float ray = GradientNoise(float2((px.x + px.y * 0.45) * _RayScale, t * 0.15));
                if (ray > 0.5 - _RayAmount * 0.5)
                    col.rgb += _RayColor.rgb * _RayColor.a * (1.0 - band);

                // Caustics: chunky pixel lines, fading out with depth
                float2 causticUV = px * _CausticScale * 0.1 + float2(t * _CausticSpeed, t * _CausticSpeed * 0.7);
                float caustic = abs(GradientNoise(causticUV)) < (1.0 - _CausticThreshold) * 0.12 ? 1.0 : 0.0;
                col.rgb += _CausticColor.rgb * _CausticColor.a * caustic * (1.0 - band);

                // ---------- Surface layers ----------
                float columnId = floor(px.x * ppu);
                float sparkle = Hash1D(columnId + floor(t * 4.0) * 17.0) < _FoamSparkle * 0.5 ? 1.0 : 0.0;

                // Crests grow a pixel of top surface, troughs lose one, so the surface reads as rolling
                float crest = i.wave > _CrestFoamHeight ? 1.0 : 0.0;
                float trough = i.wave < -_CrestFoamHeight ? 1.0 : 0.0;

                float foamEnd = _FoamPixels + sparkle;
                float topEnd = foamEnd + max(0.0, _TopFacePixels + crest - trough);
                float lipEnd = topEnd + 1.0;
                float shadowEnd = lipEnd + 1.0;

                if (depthPx < foamEnd)
                {
                    // Far edge of the surface
                    col = lerp(col, _FoamColor, _FoamColor.a);
                }
                else if (depthPx < topEnd)
                {
                    // Top surface seen from above
                    fixed4 top = _SurfaceTopColor;
                    top.rgb *= 1.0 + shade * 0.35;

                    // Glints: short horizontal dashes drifting along
                    float row = depthPx;
                    float dash = floor((columnId + t * _GlintSpeed * ppu * (row + 1.0) * 0.5) / max(1.0, _GlintLength));
                    if (Hash21(float2(dash, row)) < _GlintAmount)
                        top.rgb = lerp(top.rgb, _GlintColor.rgb, _GlintColor.a);

                    // Foam riding on the crests
                    if (crest > 0.5)
                        top.rgb = lerp(top.rgb, _FoamColor.rgb, 0.6);

                    col = top;
                }
                else if (depthPx < lipEnd)
                {
                    // Bright front lip where the surface meets the body
                    col = _EdgeColor;
                    col.rgb *= 1.0 + shade * 0.25;
                }
                else if (depthPx < shadowEnd)
                {
                    col.rgb *= 1.0 - _LipShadow;
                }
                else
                {
                    // Reflection streaks just under the lip, thinning out with depth
                    float under = depthPx - shadowEnd;
                    if (under < _StreakDepthPixels)
                    {
                        float dash = floor((columnId + t * 6.0) / 4.0);
                        float chance = _StreakAmount * (1.0 - under / max(1.0, _StreakDepthPixels));
                        if (fmod(under, 2.0) < 1.0 && Hash21(float2(dash, under)) < chance)
                            col.rgb = lerp(col.rgb, _HighlightColor.rgb, _HighlightColor.a);
                    }

                    // Wave slope light carries a little into the water right below
                    col.rgb *= 1.0 + shade * 0.15 * (1.0 - band);
                }

                // ---------- Bottom edge: fade into the scene instead of a hard line ----------
                if (_BottomFade > 0.0)
                {
                    // Distance above the mesh bottom, from uv.y (0 at the bottom, 1 at the surface)
                    float v01 = saturate(i.uv.y);
                    float fromBottom = v01 < 0.999 ? v01 * (surface - px.y) / (1.0 - v01) : 1e4;
                    float fade = saturate(fromBottom / _BottomFade);
                    if (_BottomFadeDither > 0.5)
                    {
                        float2 cell = floor(px * ppu);
                        cell = float2(fmod(cell.x + 4096.0, 4.0), fmod(cell.y + 4096.0, 4.0));
                        clip(fade - Bayer4(cell));
                    }
                    else col.a *= fade;
                }

                return col;
            }
            ENDCG
        }
    }
}
