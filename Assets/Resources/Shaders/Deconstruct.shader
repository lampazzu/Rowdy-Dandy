// Rowdy Dandy: a sprite coming apart pixel by pixel and putting itself back together (EnemyTeleport.cs).
//   _Progress 0 = whole, 1 = gone. Each texel of the sprite gets its own moment to break off, mostly in a sweep
//   (_Sweep: +1 top first, -1 bottom first) with noise mixed in, so it crumbles in uneven pixel steps.
//   Texels about to go glow in _Edge and slide sideways with a scanline glitch; the rest keeps the sprite's colours.
//   _Rect = the sprite's frame inside its sheet (uv min xy, uv max zw), set every frame (animated sprites).
Shader "Rowdy Dandy/Deconstruct"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _Edge ("Edge Colour", Color) = (1, 0.4, 1, 1)
        _Sweep ("Sweep", Float) = 1
        _Rect ("Frame Rect", Vector) = (0, 0, 1, 1)
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float _Progress;
            float4 _Edge;
            float _Sweep;
            float4 _Rect;
            float _Seed;

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21) + _Seed);
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 texSize = _MainTex_TexelSize.zw;
                float2 texel = floor(i.uv * texSize);                  // this game pixel, in sheet texels
                float2 size = max(_Rect.zw - _Rect.xy, 1e-5);
                float v = saturate((i.uv.y - _Rect.y) / size.y);       // 0 bottom .. 1 top of the frame
                float order = _Sweep >= 0 ? 1 - v : v;                 // 0 = goes first
                float n = Hash(texel);
                float t = order * 0.6 + n * 0.4;                       // when this pixel breaks off (0..1)
                float p = _Progress * 1.15;                            // a little past 1 so the last pixels go too

                // scanline glitch: rows near the break line slide sideways a few pixels
                float row = floor(i.uv.y * texSize.y);
                float rowN = Hash(float2(row, floor(_Time.y * 24)));
                float near = saturate(1 - abs(t - p) * 6);
                float shift = (rowN > 0.55 ? (rowN - 0.55) * 18 : 0) * near * (_Progress > 0.001 ? 1 : 0);
                float2 uv = i.uv;
                uv.x += round(shift) * _MainTex_TexelSize.x * (Hash(float2(row, 7)) > 0.5 ? 1 : -1);
                uv.x = clamp(uv.x, _Rect.x, _Rect.z);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * i.color;
                if (t < p) col.a = 0;                                  // gone
                float glow = (t >= p && t < p + 0.12) ? 1 : 0;         // about to go: lit up
                col.rgb = lerp(col.rgb, _Edge.rgb, glow * _Edge.a * saturate(_Progress * 8));
                return col;
            }
            ENDHLSL
        }
    }
}
