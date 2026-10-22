Shader "VirtualStrikeZone/MeasureReticle"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 0.95)
        _RingThickness ("Ring Thickness", Range(0.01, 0.2)) = 0.05
        _DotRadius ("Center Dot Radius", Range(0.01, 0.3)) = 0.08
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100

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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _RingThickness;
            float _DotRadius;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0; // Center UVs at (0,0)
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float dist = length(i.uv);

                // Center dot
                float dotAlpha = 1.0 - smoothstep(_DotRadius - 0.02, _DotRadius, dist);

                // Outer ring
                float ringOuter = 0.85;
                float ringInner = ringOuter - _RingThickness;
                float ringAlpha = smoothstep(ringInner - 0.02, ringInner, dist) * (1.0 - smoothstep(ringOuter, ringOuter + 0.02, dist));

                float totalAlpha = saturate(dotAlpha + ringAlpha) * _Color.a;
                if (totalAlpha <= 0.01) discard;

                return fixed4(_Color.rgb, totalAlpha);
            }
            ENDCG
        }
    }
}
