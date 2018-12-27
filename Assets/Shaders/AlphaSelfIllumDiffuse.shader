Shader "AlphaSelfIllumDiffuse" {
    Properties {
        _Diffuse("Diffuse Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _EmisMul("Emissive Multiplier", Float) = 3.0
    }
    Category {
        Lighting On
        ZWrite Off
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha
        Tags { Queue=Transparent }
        SubShader {
            CGPROGRAM
            #pragma surface surf Lambert

            float4 _Diffuse;
            float _EmisMul;
            struct Input {
                float2 uv_MainTex;
            };

            void surf(Input IN, inout SurfaceOutput o) {
                o.Albedo = _Diffuse.rgb;
                o.Alpha = _Diffuse.a;
                o.Emission = _Diffuse.a * _EmisMul;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}