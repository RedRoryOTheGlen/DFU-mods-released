// Project:         Daggerfall Unity
// Copyright:       Copyright (C) 2009-2016 Gavin Clayton
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Web Site:        http://www.dfworkshop.net
// Contact:         Gavin Clayton (interkarma@dfworkshop.net)
// Project Page:    https://github.com/Interkarma/daggerfall-unity

// Shader used by unmodded game or mods not wanting to use Standard shader or PBR workflow
// Mods requiring a full PBR workflow or more features should use Standard or a custom shader
Shader "Worldwalking/Splatmap" {
    Properties {
        _MainTex1("Texture 1", 2D) = "white" {}  //base
        _MainTex2("Texture 2", 2D) = "white" {}  //red
        _MainTex3("Texture 3", 2D) = "white" {}  //green
        _MainTex4("Texture 4", 2D) = "white" {}  //blue
        _MainTex5("Texture 5", 2D) = "white" {}  //alpha
        _MainMask("Mask", 2D) = "white" {}  //splatmap

        _SubTex1("Sub Texture 1", 2D) = "white" {}  //red
        _SubTex2("Sub Texture 2", 2D) = "white" {}  //green
        _SubTex3("Sub Texture 3", 2D) = "white" {}  //blue
        _SubTex4("Sub Texture 4", 2D) = "white" {}  //alpha
        _SubMask("Sub Mask", 2D) = "white" {}  //splatmap

        _HeightTex("Height Texture", 2D) = "white" {}  //white
        _HeightMask("Height Mask", 2D) = "white" {}  //heightmap

    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf BlinnPhong vertex:vert
        //#pragma surface surf Lambert
        
        sampler2D _MainTex1;
        sampler2D _MainTex2;
        sampler2D _MainTex3;
        sampler2D _MainTex4;
        sampler2D _MainTex5;
        sampler2D _MainMask;

        sampler2D _SubTex1;
        sampler2D _SubTex2;
        sampler2D _SubTex3;
        sampler2D _SubTex4;
        sampler2D _SubMask;

        sampler2D _HeightTex;
        sampler2D _HeightMask;

    	struct Input {
            float2 uv_MainTex1;
            float2 uv2_MainTex2;
            float2 uv3_MainTex3;
            float2 uv4_MainTex4;
            float2 uv2_MainMask;
    	};

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
        }

    	void surf (Input IN, inout SurfaceOutput o)
    	{
			fixed4 maskMain = tex2D( _MainMask, IN.uv2_MainMask.xy );
            float testMain = clamp(1-(maskMain.r + maskMain.g + maskMain.b),0,1);

            //first pass
			fixed3 color1 = tex2D( _MainTex1, IN.uv_MainTex1.xy ).rgb;
			fixed3 color2 = tex2D( _MainTex2, IN.uv_MainTex1.xy ).rgb;
			fixed3 color3 = tex2D( _MainTex3, IN.uv_MainTex1.xy ).rgb;
			fixed3 color4 = tex2D( _MainTex4, IN.uv_MainTex1.xy ).rgb;
			fixed3 color5 = tex2D( _MainTex5, IN.uv_MainTex1.xy ).rgb;

			fixed3 c = color2 * maskMain.r + color3 * maskMain.g + color4 * maskMain.b;
			c = lerp(c,color5,maskMain.a);
			c = lerp(c,color1,testMain);

            //second pass
			fixed3 color10 = tex2D( _MainTex1, IN.uv2_MainTex2.xy ).rgb;
			fixed3 color20 = tex2D( _MainTex2, IN.uv2_MainTex2.xy ).rgb;
			fixed3 color30 = tex2D( _MainTex3, IN.uv2_MainTex2.xy ).rgb;
			fixed3 color40 = tex2D( _MainTex4, IN.uv2_MainTex2.xy ).rgb;
			fixed3 color50 = tex2D( _MainTex5, IN.uv2_MainTex2.xy ).rgb;

			fixed3 d = color20 * maskMain.r + color30 * maskMain.g + color40 * maskMain.b;
			d = lerp(d,color50,maskMain.a);
			d = lerp(d,color10,testMain);

            //third pass
			fixed3 color100 = tex2D( _MainTex1, IN.uv3_MainTex3.xy ).rgb;
			fixed3 color200 = tex2D( _MainTex2, IN.uv3_MainTex3.xy ).rgb;
			fixed3 color300 = tex2D( _MainTex3, IN.uv3_MainTex3.xy ).rgb;
			fixed3 color400 = tex2D( _MainTex4, IN.uv3_MainTex3.xy ).rgb;
			fixed3 color500 = tex2D( _MainTex5, IN.uv3_MainTex3.xy ).rgb;

			fixed3 e = color200 * maskMain.r + color300 * maskMain.g + color400 * maskMain.b;
			e = lerp(e,color500,maskMain.a);
			e = lerp(e,color100,testMain);

            c = c + d + e;

			fixed4 maskHeight = tex2D( _HeightMask, IN.uv2_MainMask.xy );
            float testHeight = clamp(maskHeight.r + maskHeight.g + maskHeight.b,0,1);
			fixed3 colorHeight1 = tex2D( _HeightTex, IN.uv_MainTex1.xy ).rgb;
			fixed3 colorHeight2 = tex2D( _HeightTex, IN.uv2_MainTex2.xy ).rgb;
			fixed3 colorHeight3 = tex2D( _HeightTex, IN.uv3_MainTex3.xy ).rgb;
			fixed3 colorHeight = colorHeight1+colorHeight2+colorHeight3;

            fixed3 z = lerp(c,colorHeight,testHeight);
            
			fixed4 maskSub = tex2D( _SubMask, IN.uv2_MainMask.xy );
            float testSub = clamp(maskSub.r + maskSub.g + maskSub.b,0,1);
			fixed3 color1sub = tex2D( _SubTex1, IN.uv_MainTex1.xy ).rgb;
			fixed3 color2sub = tex2D( _SubTex2, IN.uv_MainTex1.xy ).rgb;
			fixed3 color3sub = tex2D( _SubTex3, IN.uv_MainTex1.xy ).rgb;
			fixed3 color4sub = tex2D( _SubTex4, IN.uv_MainTex1.xy ).rgb;
			fixed3 f = color1sub * maskSub.r + color2sub * maskSub.g + color3sub * maskSub.b;
			f = lerp(f,color4sub,maskSub.a);

            //fixed3 z = c + f;
            z = lerp(z,f,testSub);

            // output
			o.Albedo = z.rgb;
			o.Alpha = 1;
    	}
    	ENDCG
    } 
	FallBack "Diffuse"
}