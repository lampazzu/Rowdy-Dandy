// Rowdy Dandy: crisp pixel-art shapes at ANY size (PixelShape.cs). The quad is cut into the game's real pixels
// (_Px = the shape's size in 64-per-unit pixels) and the shape is drawn per pixel, so a ring 6 units wide still has
// 1 px steps and a 3 px line - instead of a small ring texture scaled up into fat, stretched pixels.
//   _Kind 0 ring (ellipse when stretched)   1 beam (soft column, fades upward)   2 cone (light from above)
//         3 pool (flat disc with a bright rim)   4 glow (soft disc)   5 shaft (column, even top to bottom)
Shader "Rowdy Dandy/PixelShape"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
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

            float4 _Px;     // xy = size in pixels
            float _Kind;
            float _Thick;   // ring line thickness in pixels

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

            half4 frag(Varyings i) : SV_Target
            {
                float2 px = max(_Px.xy, float2(1, 1));
                float2 p = floor(i.uv * px) + 0.5;   // centre of this game pixel
                float2 c = px * 0.5;
                int k = (int)round(_Kind);
                float a = 0;
                if (k == 0)
                {
                    // distance (in pixels) inside the ellipse's edge, first order
                    float2 q = (p - c) / c;
                    float dn = length(q);
                    float2 g = float2(q.x / c.x, q.y / c.y) / max(dn, 1e-4);
                    float d = (1 - dn) / max(length(g), 1e-4);
                    a = (d >= 0 && d < _Thick) ? 1 : (d >= _Thick && d < _Thick + 2) ? 0.43 : 0;
                }
                else if (k == 1)
                {
                    float e = 1 - abs(p.x / px.x * 2 - 1);
                    float v = p.y / px.y;
                    a = (e > 0.7 ? 1 : e > 0.35 ? 0.6 : 0.25) * lerp(1, 0.15, v);
                }
                else if (k == 2)
                {
                    float v = p.y / px.y;                       // 0 bottom (wide), 1 top (the apex)
                    float halfW = lerp(1, 0.12, v);
                    float e = 1 - abs(p.x / px.x * 2 - 1);
                    float edge = (e - (1 - halfW)) / halfW;
                    a = edge > 0 ? (edge > 0.55 ? 1 : edge > 0.25 ? 0.6 : 0.3) * lerp(1, 0.3, v) : 0;
                }
                else if (k == 5)
                {
                    float e = 1 - abs(p.x / px.x * 2 - 1);
                    a = e > 0.75 ? 1 : e > 0.5 ? 0.8 : e > 0.25 ? 0.45 : 0.15;
                }
                else if (k == 3)
                {
                    float d = length((p - c) / c);
                    a = d <= 1 ? (d > 0.78 ? 1 : 0.5) : 0;
                }
                else
                {
                    float d = length((p - c) / c);
                    a = d <= 1 ? (d < 0.45 ? 1 : d < 0.75 ? 0.55 : 0.25) : 0;
                }
                half4 col = i.color;
                col.a *= a;
                return col;
            }
            ENDHLSL
        }
    }
}
