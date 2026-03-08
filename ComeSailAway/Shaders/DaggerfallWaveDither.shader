// Project:         Daggerfall Unity
// Copyright:       Copyright (C) 2009-2016 Gavin Clayton
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Web Site:        http://www.dfworkshop.net
// Contact:         Gavin Clayton (interkarma@dfworkshop.net)
// Project Page:    https://github.com/Interkarma/daggerfall-unity

// Shader used by all freestanding billboards including mobiles
// For wilderness foliage shader see DaggerfallBillboardBatch instead
Shader "Daggerfall/Dither/Wave" {
    Properties {
        _Color("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _MainTex("Albedo Map", 2D) = "white" {}
        _BumpMap("Normal Map", 2D) = "bump" {}
        _EmissionMap("Emission Map", 2D) = "white" {}
        _EmissionColor("Emission Color", Color) = (0,0,0)
        _DitherPattern ("Dithering Pattern", 2D) = "white" {}
        _DitherStart("Dithering Start", Range (0, 1)) = 0
        _DitherEnd("Dithering End", Range (0, 1)) = 1
    }
    SubShader {
        Tags { "IgnoreProjector" = "True" "RenderType" = "Opaque" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        LOD 200
        
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Lambert alphatest:_Cutoff addshadow
        #pragma multi_compile_local __ _NORMALMAP
        #pragma multi_compile_local __ _EMISSION

        half4 _Color;
        sampler2D _MainTex;
        #ifdef _NORMALMAP
            sampler2D _BumpMap;
        #endif
        #ifdef _EMISSION
            sampler2D _EmissionMap;
            half4 _EmissionColor;
        #endif

        sampler2D _DitherPattern;
        float4 _DitherPattern_TexelSize;
        float _DitherStart;
        float _DitherEnd;

        struct Input {
            float2 uv_MainTex;
            #ifdef _NORMALMAP
                float2 uv_BumpMap;
            #endif
            #ifdef _EMISSION
                float2 uv_EmissionMap;
            #endif
            float3 worldPos;
            float4 screenPos;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            half4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            #ifdef _NORMALMAP
                o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            #endif
            #ifdef _EMISSION
                half3 emission = tex2D(_EmissionMap, IN.uv_EmissionMap).rgb * _EmissionColor;
                o.Albedo = albedo.rgb - emission; // Emission cancels out other lights
                o.Emission = emission;
            #else
                o.Albedo = albedo.rgb;
            #endif
            o.Alpha = albedo.a;
            
            //Fade the pixels as they get closer to the camera's far clip plane (Start fading at half the distance and completely fade by the end)
            //float distanceFromCamera = distance(IN.worldPos, _WorldSpaceCameraPos);
            //float fade = 1-saturate((distanceFromCamera-(_ProjectionParams.z*_DitherStart))/(_ProjectionParams.z*(0.8-_DitherStart)));
            float fade = 1-smoothstep(_DitherStart,_DitherEnd,abs((IN.uv_MainTex.y/10)-0.5)*2);

            //value from the dither pattern
            float2 screenPos = IN.screenPos.xy / IN.screenPos.w;
            float2 ditherCoordinate = screenPos * _ScreenParams.xy * _DitherPattern_TexelSize.xy;
            float ditherValue = tex2D(_DitherPattern, ditherCoordinate).r;

            //discard pixels accordingly
            clip(fade - ditherValue);
        }
        ENDCG
    } 
    FallBack "Diffuse"
}