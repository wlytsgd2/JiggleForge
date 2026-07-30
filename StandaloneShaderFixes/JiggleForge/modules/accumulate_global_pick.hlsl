// Accumulates the closest valid 1x1 native pick across every matching draw.
// Native picking uses reverse-Z and slot 0 stores 1-SV_Position.z, so the
// smallest stored depth is the closest visible surface.

Texture2D<float4> Pick0 : register(t0);
Texture2D<float4> Pick1 : register(t1);
Texture2D<float4> Pick2 : register(t2);
Texture2D<float4> Pick3 : register(t3);
Texture2D<float4> Pick4 : register(t4);
Texture2D<float4> Pick5 : register(t5);
Texture2D<float4> Pick6 : register(t6);
Texture2D<float4> Pick7 : register(t7);
RWBuffer<float4> FramePick : register(u0);

[numthreads(1, 1, 1)]
void main()
{
    uint framePickCount;
    FramePick.GetDimensions(framePickCount);
    if (framePickCount < 8u)
        return;

    float4 incoming = Pick0.Load(int3(0, 0, 0));
    if (incoming.x <= 0.0 || incoming.w <= 0.0)
        return;

    float4 incomingSource = Pick2.Load(int3(0, 0, 0));
    float4 current = FramePick[0u];
    if (current.x > 0.0 && current.w > 0.0)
    {
        float4 currentSource = FramePick[2u];
        bool samePipeline =
            incomingSource.w > 0.0
            && currentSource.w > 0.0
            && abs(incomingSource.w - currentSource.w) < 0.5;

        if (samePipeline)
        {
            // Candidates produced by one real game Draw share a pipeline
            // token. Within that Draw, prefer the exact adapted range over
            // post-Skin and pre-Skin geometry, then use depth for candidates
            // of equal quality.
            if (incoming.z < current.z)
                return;
            if (incoming.z == current.z && incoming.y > current.y)
                return;
        }
        else
        {
            // Different real Draws are different visible parts. Occlusion
            // must win across them even when one is adapted and the other
            // still uses the global fallback state.
            const float depthEpsilon = 1e-6;
            if (incoming.y > current.y + depthEpsilon)
                return;
            if (abs(incoming.y - current.y) <= depthEpsilon
                && incoming.z < current.z)
                return;
        }
    }

    FramePick[0u] = incoming;
    FramePick[1u] = Pick1.Load(int3(0, 0, 0));
    FramePick[2u] = incomingSource;
    FramePick[3u] = Pick3.Load(int3(0, 0, 0));
    FramePick[4u] = Pick4.Load(int3(0, 0, 0));
    FramePick[5u] = Pick5.Load(int3(0, 0, 0));
    FramePick[6u] = Pick6.Load(int3(0, 0, 0));
    FramePick[7u] = Pick7.Load(int3(0, 0, 0));
}
