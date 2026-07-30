RWBuffer<float4> FramePick : register(u0);

[numthreads(1, 1, 1)]
void main()
{
    if (FramePick[0u].z >= 3.0)
        return;

    [unroll]
    for (uint index = 0u; index < 8u; index++)
        FramePick[index] = 0.0;
}
