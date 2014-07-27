﻿Shader "Hidden/TransDistort" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
        _SourceTex ("Base (RGB)", 2D) = "white" {}
		_DistortTex ("Base (RGB)", 2D) = "white" {}
	}
	
	CGINCLUDE
		#include "UnityCG.cginc"

		struct v2f {
			 half4 pos : POSITION;
			 half2 uv : TEXCOORD0;
		 };
		
		sampler2D _MainTex;
        sampler2D _SourceTex;
		sampler2D _DistortTex;
		fixed3 _Params; //[xy: force, z = distort time]
		fixed _t;
		fixed _distortT;
				
		half4 frag(v2f i) : COLOR {
			half2 distortUV = i.uv;
			fixed4 offsetColor1 = tex2D(_DistortTex, i.uv + _Time.xz*_Params.z);
			fixed4 offsetColor2 = tex2D(_DistortTex, i.uv + _Time.yx*_Params.z);
			distortUV.x += ((offsetColor1.r + offsetColor2.r) - 1) * _Params.x;
			distortUV.y += ((offsetColor1.r + offsetColor2.r) - 1) * _Params.y;
			distortUV = lerp(i.uv, distortUV, _distortT);

			return lerp(tex2D(_MainTex, i.uv), tex2D(_SourceTex, distortUV), _t);
		}

		v2f vert(appdata_img v) {
			v2f o;
			o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
			o.uv = v.texcoord.xy;
			return o;
		}
	ENDCG

	Subshader {
		Pass {
			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }      

			CGPROGRAM
			#pragma fragmentoption ARB_precision_hint_fastest 
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}
}