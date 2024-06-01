Shader "Custom/Image Comparison Shader"
{
    Properties
    {
        _OriginalObjTex ("Original Object Texture", 2D) = "white" {}
        _MappedObjTex ("Mapped Object Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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
                float2 uvOriginal : TEXCOORD0;
                float2 uvMapped : TEXCOORD3;
                float4 vertex : SV_POSITION;
            };

            sampler2D _OriginalObjTex;
            float4 _OriginalObjTex_ST; // Tiling information. Generally unused though
            sampler2D _MappedObjTex;
            float4 _MappedObjTex_ST; // Tiling information. Generally unused though

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uvOriginal = TRANSFORM_TEX(v.uv, _OriginalObjTex);
                o.uvMapped = TRANSFORM_TEX(v.uv, _MappedObjTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 colOriginal = tex2D(_OriginalObjTex, i.uvOriginal);
                fixed4 colMapped = tex2D(_MappedObjTex, i.uvMapped);
                return abs(colOriginal - colMapped);
            }
            ENDCG
        }
    }
}