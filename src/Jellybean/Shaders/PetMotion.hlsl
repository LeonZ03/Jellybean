sampler2D Current : register(s0);
sampler2D Previous : register(s1);
float Mix : register(c0);
float Breath : register(c1);
float Blink : register(c2);
float PreviousFrozen : register(c3);
float2 Sway : register(c4);
float2 BreathCenter : register(c5);
float2 BreathInverseExtent : register(c6);
float4 HeadRegion : register(c7);
float4 TailRegion : register(c8);
float4 DetailShift : register(c9);
float2 Eyes : register(c10);
float EyeHeight : register(c11);
float2 PickupOffset : register(c12);

float regionWeight(float2 uv, float4 region)
{
    float2 distance = (uv - region.xy) * region.zw;
    float weight = saturate(1.0 - dot(distance, distance));
    return weight * weight;
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float2 poseUv = uv - PickupOffset;
    float y = poseUv.y * 240.0;
    float2 fromChest = (poseUv - BreathCenter) * BreathInverseExtent;
    float chestWeight = saturate(1.0 - dot(fromChest, fromChest));
    float abovePaws = saturate((160.0 - y) / 20.0);
    float belowNape = saturate((y - 48.0) / 90.0);
    // Inverse sampling lifts the ribs on inhale and displaces the hanging body below the grip.
    float2 breathWarp = float2(-fromChest.x * .45, 1.0) * (Breath * chestWeight * abovePaws);
    float2 warped = poseUv + (breathWarp - Sway * belowNape * belowNape) / 240.0;
    warped -= DetailShift.xy * regionWeight(poseUv, HeadRegion);
    warped -= DetailShift.zw * regionWeight(poseUv, TailRegion);
    float2 eyeDistance = abs(warped.x - Eyes);
    float eyeWidth = saturate(1.45 - min(eyeDistance.x, eyeDistance.y) * 44.0);
    float lid = warped.y - EyeHeight;
    float eyeHeight = saturate(1.0 - abs(lid) * 34.0);
    warped.y += Blink * eyeWidth * eyeHeight * clamp(lid, -.009, .009);
    float4 oldPose = tex2D(Previous, lerp(warped, uv, PreviousFrozen));
    float4 newPose = tex2D(Current, warped);
    // Both samples are premultiplied: a solid overlapping body stays solid during the blend.
    return lerp(oldPose, newPose, Mix);
}
