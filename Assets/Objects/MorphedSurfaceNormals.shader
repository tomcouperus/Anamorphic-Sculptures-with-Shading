Shader "Normals/Morphed Surface Normals"
{
    Properties
    {
        _Color ("Additional color to add to the shader for better contrast", Color) = (0, 0, 0, 0)
        _Mode ("Which mode to use for showing the morphed normals. 1=Normal, 2=Object relative angle. Out of range uses 1.", Integer) = 1
        _RelativePlane ("Which plane to use for angle calculation. 1=XY, 2=YZ, 3=XZ. Out of range uses 1.", Integer) = 1
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

            fixed4 _Color;
            uniform int _Mode;
            uniform int _RelativePlane;

            // Vertex shader input
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 originalObjectNormal : TEXCOORD3;
            };

            // Vertex shader output / fragment shader input
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float3 originalObjectNormal : TEXCOORD3;
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
                o.originalObjectNormal = v.originalObjectNormal;
                return o;
            }

            // Reflected mode -- fragment shader
            fixed4 reflectedMode(v2f i)
            {
                float3 normal = normalize(i.normal);
                float3 color = (normal + 1) * 0.5;
                return fixed4(color.rgb, 0);
            }

            // Object relative mode -- fragment shader 
            fixed4 objectRelativeMode(v2f i)
            {
                float angleXY = angle2(i.normal.xy, i.originalObjectNormal.xy);
                float angleYZ = angle2(i.normal.yz, i.originalObjectNormal.yz);
                float angleXZ = angle2(i.normal.xz, i.originalObjectNormal.xz);
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
                return color / PI;
            }

            // Main fragment shader 
            fixed4 frag (v2f i) : SV_Target
            {
                i.normal.z = -i.normal.z;
                fixed4 color;
                switch(_Mode)
                {
                    case 2:
                        color = objectRelativeMode(i);
                        break;
                    case 1:
                    default:
                        color = reflectedMode(i);
                        break;
                }
                color = color + _Color;
                return color;
            }
            ENDCG
        }
    }
}
