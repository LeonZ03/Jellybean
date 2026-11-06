sampler2D Current : register(s0);
sampler2D Previous : register(s1);
float Mix : register(c0);
float Breath : register(c1);
float Ear : register(c2);
float PreviousFrozen : register(c3);
float Sway : register(c4);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float y = uv.y * 240.0;
    float chest = saturate((y - 32.0) / 132.0);
    float chestWeight = 4.0 * chest * (1.0 - chest);
    float earWeight = saturate(1.0 - y / 76.0);
    float2 warped = uv + float2(0, (Breath * chestWeight + Ear * earWeight) / 240.0);
    warped.x += (uv.y - .2) * Sway;
    float4 oldPose = tex2D(Previous, lerp(warped, uv, PreviousFrozen));
    float4 newPose = tex2D(Current, warped);
    // Both samples are premultiplied: a solid overlapping body stays solid during the blend.
    return lerp(oldPose, newPose, Mix);
}
