Shader "AlphaSelfIllum" {
    Properties {
        _MainTex("Diffuse Color", 2D) = "white" { }
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
            struct Input {
                float2 uv_MainTex;
            };
            sampler2D _MainTex;
            float _EmisMul;
            void surf(Input IN, inout SurfaceOutput o) {
                float4 _tex2d = tex2D(_MainTex, IN.uv_MainTex);
                o.Albedo = _tex2d.rgb;
                o.Alpha = _tex2d.a;
                o.Emission = max(_tex2d.a, 0.05f) * _EmisMul;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}