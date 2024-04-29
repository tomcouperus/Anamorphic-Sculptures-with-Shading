Shader "Normals/Morphed Surface Normals"
{
    Properties
    {
        _RelativePlane ("Which plane to use for angle calculation. 1=XY, 2=YZ, 3=XZ. Out of range uses XY plane.", Integer) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
// Upgrade NOTE: excluded shader from DX11; has structs without semantics (struct v2f members normal)
#pragma exclude_renderers d3d11
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #define PI 3.14159265358

            uniform int _RelativePlane;

            // Vertex shader input
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 originalNormal : TEXCOORD3;
            };

            // Vertex shader output / fragment shader input
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float3 originalNormal : TEXCOORD3;
            };

            // Return the angle in radians between two 2D vectors
            float angle2(float2 a, float2 b)
            {
                return acos(dot(a,b) / (length(a) * length(b)));
            }

            // Vertex shader
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = v.normal;
                o.originalNormal = v.originalNormal;
                return o;
            }

            // Fragment shader
            fixed4 frag (v2f i) : SV_Target
            {
                float angleXY = angle2(i.normal.xy, i.originalNormal.xy);
                float angleYZ = angle2(i.normal.yz, i.originalNormal.yz);
                float angleXZ = angle2(i.normal.xz, i.originalNormal.xz);
                fixed4 color;
                switch (_RelativePlane) 
                {
                    case 3:
                        color = fixed4(angleXZ, angleXZ, angleXZ, 0);
                        break;
                    case 2:
                        color = fixed4(angleYZ, angleYZ, angleYZ, 0);
                        break;
                    case 1:
                    default:
                        color = fixed4(angleXY, angleXY, angleXY, 0);
                        break;
                }
                return color / (2*PI);
            }
            ENDCG
        }
    }
}
