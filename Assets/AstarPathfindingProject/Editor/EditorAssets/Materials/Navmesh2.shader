Shader "Transparent/Navmesh/TransparentAlwaysShow" {
Properties {
    _Color ("Main Color", Color) = (1,1,1,0.5)
    _MainTex ("Texture", 2D) = "white" { }
    _Scale ("Scale", float) = 1
    _FadeColor ("Fade Color", Color) = (1,1,1,0.3)
}
SubShader {
	
	Pass {
   		ColorMask 0
   	}
	
	Tags {"Queue"="Transparent+1" "IgnoreProjector"="True" "RenderType"="Transparent"}
	LOD 200
	
	
	
	Offset 0, -20
	Cull Off
	Lighting On
	
	Pass {
		ZWrite Off
		ZTest Greater
		Blend SrcAlpha OneMinusSrcAlpha
		
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		
		#include "UnityCG.cginc"
		
		//float4 _Color;
		sampler2D _MainTex;
		float _Scale;
		float4 _FadeColor;
		
		struct appdata_color {
		    float4 vertex : POSITION;
		    fixed4 color : COLOR;
		};

		struct v2f {
		    float4  pos : SV_POSITION;
		    float2  uv : TEXCOORD0;
		    half4 col : COLOR;
		};
		
		float4 _MainTex_ST;
		//glEnable(GL_BLEND);
		
		
		v2f vert (appdata_color v)
		{
		    v2f o;
		    o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
		    
		    float4 worldSpace = mul (_Object2World, v.vertex);
		    o.uv = float2 (worldSpace.x*_Scale,worldSpace.z*_Scale);
		    o.col = v.color*_FadeColor;
		    return o;
		}
		
		half4 frag (v2f i) : COLOR
		{
			//glBlendFunc = GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA;
		    half4 texcol = tex2D (_MainTex, i.uv) * i.col;
		    //texcol.a = 0.1;
		    return texcol;
		}
		ENDCG
	
	 }
	 
	 ZWrite Off
    Pass {
		ZTest LEqual
		//Blend One One Fog { Color (0,0,0,0) }
		
		Blend SrcAlpha OneMinusSrcAlpha
		
		ZWrite Off
		
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		
		#include "UnityCG.cginc"
		
		sampler2D _MainTex;
		float _Scale;
		
		struct appdata_color {
		    float4 vertex : POSITION;
		    fixed4 color : COLOR;
		};

		struct v2f {
		    float4  pos : SV_POSITION;
		    float2  uv : TEXCOORD0;
		    half4 col : COLOR;
		};
		
		v2f vert (appdata_color v)
		{
		    v2f o;
		    o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
		    
		    float4 worldSpace = mul (_Object2World, v.vertex);
		    o.uv = float2 (worldSpace.x*_Scale,worldSpace.z*_Scale);
		    o.col = v.color;
		    return o;
		}
		
		half4 frag (v2f i) : COLOR
		{
			//glBlendFunc = GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA;
		    half4 texcol = tex2D (_MainTex, i.uv) * i.col;
		    //texcol.a = 0.1;
		    return texcol;
		}
		ENDCG
	
	    }
	
	
	}
Fallback "VertexLit"
}