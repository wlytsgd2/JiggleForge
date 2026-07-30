#ifndef JIGGLE_FORGE_RUNTIME_DEFORMATION_FIELD
#define JIGGLE_FORGE_RUNTIME_DEFORMATION_FIELD

float3 JF_SafeNormalize(float3 value, float3 fallback)
{
  float lengthSquared = dot(value, value);
  if (lengthSquared <= 1e-20)
    return fallback;

  return value * rsqrt(lengthSquared);
}

float JF_CompactSupport(float distanceFromAnchor, float radius, float falloff)
{
  float safeRadius = max(radius, 1e-6);
  float normalizedDistance = distanceFromAnchor / safeRadius;
  if (normalizedDistance >= 1.0)
    return 0.0;

  float remainingSquared = saturate(1.0 - normalizedDistance * normalizedDistance);
  return pow(remainingSquared, max(falloff, 1.0));
}

float3 JF_EvaluateGrabField(
  float3 worldPosition,
  float3 worldAnchor,
  float3 anchorDisplacement,
  float radius,
  float falloff,
  float volumeResponse)
{
  float3 relative = worldPosition - worldAnchor;
  float distanceFromAnchor = length(relative);
  float support = JF_CompactSupport(distanceFromAnchor, radius, falloff);
  if (support <= 0.0)
    return float3(0.0, 0.0, 0.0);

  // A regularized Kelvinlet translation normalized to reproduce the requested
  // displacement exactly at the anchor. The fixed Poisson ratio models a soft,
  // nearly incompressible material without exposing implementation constants
  // as user-facing parameters.
  const float poissonRatio = 0.45;
  const float directionalRatio = 1.0 / (4.0 * (1.0 - poissonRatio));
  const float centerCoefficient = 1.5 - directionalRatio;
  float epsilon = max(radius * 0.35, 1e-5);
  float epsilonSquared = epsilon * epsilon;
  float regularizedRadiusSquared = dot(relative, relative) + epsilonSquared;
  float inverseRadius = rsqrt(regularizedRadiusSquared);
  float inverseRadiusCubed = inverseRadius * inverseRadius * inverseRadius;
  float normalization = epsilon / centerCoefficient;
  float isotropicCoefficient = normalization * (
    (1.0 - directionalRatio) * inverseRadius
    + 0.5 * epsilonSquared * inverseRadiusCubed);
  float directionalCoefficient = normalization
    * directionalRatio
    * inverseRadiusCubed
    * dot(anchorDisplacement, relative)
    * max(volumeResponse, 0.0);

  return (
    anchorDisplacement * isotropicCoefficient
    + relative * directionalCoefficient)
    * support;
}

void JF_PrepareSurfaceSamples(
  float radius,
  float3 originalNormal,
  float3 originalTangent,
  float3 originalBitangent,
  out float derivativeStep,
  out float3 tangentDirection,
  out float3 bitangentDirection)
{
  derivativeStep = clamp(radius * 0.01, 0.0005, 0.005);
  tangentDirection = JF_SafeNormalize(
    originalTangent - originalNormal * dot(originalNormal, originalTangent),
    originalTangent);
  bitangentDirection = JF_SafeNormalize(
    originalBitangent - originalNormal * dot(originalNormal, originalBitangent),
    originalBitangent);
}

void JF_ReconstructSurfaceFrameFromSamples(
  float3 originalNormal,
  float3 originalTangent,
  float3 originalBitangent,
  float derivativeStep,
  float3 tangentDirection,
  float3 bitangentDirection,
  float3 centerDisplacement,
  float3 tangentDisplacement,
  float3 bitangentDisplacement,
  out float3 deformedNormal,
  out float3 deformedTangent,
  out float3 deformedBitangent)
{
  float3 displacedTangent = tangentDirection
    + (tangentDisplacement - centerDisplacement) / derivativeStep;
  float3 displacedBitangent = bitangentDirection
    + (bitangentDisplacement - centerDisplacement) / derivativeStep;
  deformedNormal = JF_SafeNormalize(
    cross(displacedTangent, displacedBitangent),
    originalNormal);
  if (dot(deformedNormal, originalNormal) < 0.0)
    deformedNormal = -deformedNormal;

  deformedTangent = JF_SafeNormalize(
    displacedTangent - deformedNormal * dot(deformedNormal, displacedTangent),
    originalTangent);
  deformedBitangent = JF_SafeNormalize(
    cross(deformedNormal, deformedTangent),
    originalBitangent);
  if (dot(deformedBitangent, originalBitangent) < 0.0)
    deformedBitangent = -deformedBitangent;
}

void JF_ReconstructSurfaceFrame(
  float3 worldPosition,
  float3 originalNormal,
  float3 originalTangent,
  float3 originalBitangent,
  float3 worldAnchor,
  float3 anchorDisplacement,
  float radius,
  float falloff,
  float volumeResponse,
  float vertexMask,
  float3 centerDisplacement,
  out float3 deformedNormal,
  out float3 deformedTangent,
  out float3 deformedBitangent)
{
  float derivativeStep;
  float3 tangentDirection;
  float3 bitangentDirection;
  JF_PrepareSurfaceSamples(
    radius,
    originalNormal,
    originalTangent,
    originalBitangent,
    derivativeStep,
    tangentDirection,
    bitangentDirection);

  float3 tangentDisplacement = JF_EvaluateGrabField(
    worldPosition + tangentDirection * derivativeStep,
    worldAnchor,
    anchorDisplacement,
    radius,
    falloff,
    volumeResponse) * vertexMask;
  float3 bitangentDisplacement = JF_EvaluateGrabField(
    worldPosition + bitangentDirection * derivativeStep,
    worldAnchor,
    anchorDisplacement,
    radius,
    falloff,
    volumeResponse) * vertexMask;

  JF_ReconstructSurfaceFrameFromSamples(
    originalNormal,
    originalTangent,
    originalBitangent,
    derivativeStep,
    tangentDirection,
    bitangentDirection,
    centerDisplacement,
    tangentDisplacement,
    bitangentDisplacement,
    deformedNormal,
    deformedTangent,
    deformedBitangent);
}

#endif
