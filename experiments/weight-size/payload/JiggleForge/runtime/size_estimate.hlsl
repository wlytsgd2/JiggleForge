#ifndef JF_WEIGHT_SIZE_ESTIMATE
#define JF_WEIGHT_SIZE_ESTIMATE

// One full, single-bone scan in the existing pending binding-resolve dispatch.
// These are views already prepared for the nearest binding search; no new GPU
// allocation or dispatch is needed. The scope is the bound Position view, NOT
// a promise that every vertex belongs to the visible draw or one anatomical part.
StructuredBuffer<JF_SkinnedVertex> SizePositions : register(t4);
Buffer<float> SizePositionsTyped : register(t5);

static const uint JF_SIZE_THREADS = 128u;
static const uint JF_SIZE_MAX_VERTICES = 262144u;
static const float JF_SIZE_MAX_LENGTH = 0.04;

// Exactly 4096 bytes of reduction storage. M0 = (sum(w*r), sum(w));
// M1.x = sum(w*dot(r,r)); M1.w = accepted point count.
groupshared float4 SizeM0[128];
groupshared float4 SizeM1[128];

float JF_EstimateSize(uint lane, JF_BlendVertex seed, uint blendCount, bool enabled)
{
    float reference = IniParams[115].w; // Installed default is 0.03.
    uint selectedBone = 0xffffffffu;
    float strongestWeight = 0.0;

    // Merge duplicate IDs before choosing the greatest RAW captured weight.
    // An exact tie uses the smaller ID, independently of the slot ordering.
    [unroll] for (uint candidate = 0u; candidate < 4u; ++candidate)
    {
        float mergedWeight = 0.0;
        [unroll] for (uint slot = 0u; slot < 4u; ++slot)
            if (seed.indices[slot] == seed.indices[candidate])
                mergedWeight += seed.weights[slot];
        if (isfinite(mergedWeight) && mergedWeight > 0.0
            && (mergedWeight > strongestWeight
                || (mergedWeight == strongestWeight && seed.indices[candidate] < selectedBone)))
        {
            strongestWeight = mergedWeight;
            selectedBone = seed.indices[candidate];
        }
    }

    uint count = 0u;
    uint offset = 0u;
    float offsetValue = IniParams[114].x;
    // Do not query or load position data for invalid / disabled captures. All
    // lanes still initialize the reduction and pass every barrier below.
    if (enabled && strongestWeight > 0.0 && reference > 0.0 && isfinite(reference)
        && isfinite(offsetValue) && offsetValue >= 0.0 && offsetValue < (float)blendCount)
    {
        uint positionCount = 0u, stride = 40u;
        if (IniParams[114].w > 0.5)
        {
            SizePositionsTyped.GetDimensions(positionCount);
            positionCount /= 10u;
        }
        else SizePositions.GetDimensions(positionCount, stride);

        // Reject an oversized VIEW rather than silently sampling or truncating
        // its region, even when the matching Blend view is shorter.
        if (stride == 40u && positionCount <= JF_SIZE_MAX_VERTICES)
        {
            offset = (uint)offsetValue;
            count = min(positionCount, blendCount - offset);
        }
    }

    float4 m0 = 0.0;
    float4 m1 = 0.0;
    [loop] for (uint index = lane; index < count; index += JF_SIZE_THREADS)
    {
        // Position index is relative to its current view; the Blend index
        // includes the existing VB byte-offset / 40 conversion from the INI.
        uint vertex = index + offset;
        JF_BlendVertex v;
        if (IniParams[114].y > 0.5)
        {
            v.weights = asfloat(BlendTyped[vertex * 2u]);
            v.indices = BlendTyped[vertex * 2u + 1u];
        }
        else v = Blend[vertex];
        if (any(!isfinite(v.weights)) || any(v.weights < 0.0)) continue;

        float weight = 0.0;
        [unroll] for (uint bone = 0u; bone < 4u; ++bone)
            if (v.indices[bone] == selectedBone) weight += v.weights[bone];
        // Linear raw weights, threshold zero: no normalization, squaring,
        // candidate ranking, nearby radius, or grab-position dependent region.
        if (!(weight > 0.0) || !isfinite(weight)) continue;

        float3 p;
        if (IniParams[114].w > 0.5)
            p = float3(SizePositionsTyped[index * 10u],
                SizePositionsTyped[index * 10u + 1u],
                SizePositionsTyped[index * 10u + 2u]);
        else p = SizePositions[index].position;

        // Compute in world-length units but cancel translations before adding
        // small vertex terms. Subtracting the weighted centroid below removes
        // the chosen grab origin mathematically; it is not the RMS center.
        float3 r = Model[0].xyz * p.x + Model[1].xyz * p.y
            + Model[2].xyz * p.z + (Model[3].xyz - CapturedPick[0].xyz);
        if (any(!isfinite(r))) continue;
        m0 += float4(r * weight, weight);
        m1.x += dot(r, r) * weight;
        m1.w += 1.0;
    }

    SizeM0[lane] = m0;
    SizeM1[lane] = m1;
    GroupMemoryBarrierWithGroupSync();
    [unroll] for (uint step = 64u; step > 0u; step >>= 1u)
    {
        if (lane < step)
        {
            SizeM0[lane] += SizeM0[lane + step];
            SizeM1[lane] += SizeM1[lane + step];
        }
        GroupMemoryBarrierWithGroupSync();
    }

    float scale = 1.0;
    if (lane == 0u)
    {
        float4 total = SizeM0[0];
        float secondMoment = SizeM1[0].x;
        // One point has exactly zero geometric RMS. Do not let cancellation
        // roundoff turn it into a positive measurement and the minimum scale.
        if (SizeM1[0].w >= 2.0 && total.w > 0.0 && all(isfinite(total)) && isfinite(secondMoment))
        {
            float3 weightedCenter = total.xyz / total.w;
            float variance = max(secondMoment / total.w - dot(weightedCenter, weightedCenter), 0.0);
            float rawLength = sqrt(variance);
            if (isfinite(rawLength) && rawLength > 0.0)
            {
                float usedLength = min(rawLength, JF_SIZE_MAX_LENGTH);
                scale = clamp(usedLength / reference, 0.25, 4.0);
            }
        }
    }
    return scale;
}
#endif
