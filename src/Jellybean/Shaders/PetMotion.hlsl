sampler2D Current : register(s0);
sampler2D Previous : register(s1);
float Mix : register(c0);
float Breath : register(c1);
float Ear : register(c2);
float PreviousFrozen : register(c3);
float2 Sway : register(c4);
float2 BreathCenter : register(c5);
float2 BreathInverseExtent : register(c6);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float y = uv.y * 240.0;
    float2 fromChest = (uv - BreathCenter) * BreathInverseExtent;
    float chestWeight = saturate(1.0 - dot(fromChest, fromChest));
    float abovePaws = saturate((160.0 - y) / 20.0);
    float earWeight = saturate(1.0 - y / 76.0);
    float belowNape = saturate((y - 48.0) / 90.0);
    // Inverse sampling lifts the ribs on inhale and displaces the hanging body below the grip.
    float2 breathWarp = float2(-fromChest.x * .45, 1.0) * (Breath * chestWeight * abovePaws);
    breathWarp.y += Ear * earWeight;
    float2 warped = uv + (breathWarp - Sway * belowNape * belowNape) / 240.0;
    float4 oldPose = tex2D(Previous, lerp(warped, uv, PreviousFrozen));
    float4 newPose = tex2D(Current, warped);
    // Both samples are premultiplied: a solid overlapping body stays solid during the blend.
    return lerp(oldPose, newPose, Mix);
}
