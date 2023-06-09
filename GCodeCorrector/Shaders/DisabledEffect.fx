sampler2D implicitInput : register(s0);
float factor : register(c0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
	float Brightness = 0.2f;
	float Contrast = 0.5f;

	float4 color = tex2D(implicitInput, uv);

	float gray = color.r * 0.3 + color.g * 0.59 + color.b * 0.11;

	float4 result;
	result.r = (color.r - gray) * factor + gray;
	result.g = (color.g - gray) * factor + gray;
	result.b = (color.b - gray) * factor + gray;
	result.a = color.a;

	result.rgb = ((result.rgb - 0.5f) * Contrast) + 0.5f;

	result.rgb += Brightness;

	result.rgb *= result.a;

	return result;
}