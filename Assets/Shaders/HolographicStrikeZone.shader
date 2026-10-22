Shader "VirtualStrikeZone/HolographicZone"
{
    Properties
    {
        _MainColor ("Tint Color", Color) = (0.1, 0.6, 1.0, 0.25)
        _RimColor ("Rim Glow Color", Color) = (0.4, 0.85, 1.0, 0.9)
        _RimPower ("Rim Glow Power", Range(0.5, 8.0)) = 2.5
        _GridColor ("Grid Lines Color", Color) = (0.3, 0.8, 1.0, 0.7)
        _GridDensity ("Grid Density", Float) = 3.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 200

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
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
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float3 normal : NORMAL;
            };

            fixed4 _MainColor;
            fixed4 _RimColor;
            float _RimPower;
            fixed4 _GridColor;
            float _GridDensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Fresnel Rim glow
                float rim = 1.0 - saturate(dot(i.viewDir, normalize(i.normal)));
                float rimGlow = pow(rim, _RimPower);

                // Subtle grid pattern
                float2 grid = abs(frac(i.uv * _GridDensity - 0.5) - 0.5) / fwidth(i.uv * _GridDensity);
                float lineFactor = 1.0 - min(min(grid.x, grid.y), 1.0);

                fixed4 finalCol = _MainColor + _RimColor * rimGlow + _GridColor * lineFactor * 0.4;
                finalCol.a = saturate(_MainColor.a + rimGlow * 0.7 + lineFactor * 0.3);
                return finalCol;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
