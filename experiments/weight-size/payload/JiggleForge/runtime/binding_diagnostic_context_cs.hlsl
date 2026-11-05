// Export-only observation. No writes to picking, geometry, or motion resources.
Texture1D<float4> IniParams : register(t120);
cbuffer ObjectTransform : register(b0) { float4 Model[29]; }
RWBuffer<float4> SearchContext : register(u3);
[numthreads(1,1,1)]
void main(uint3 tid : SV_DispatchThreadID)
{
    SearchContext[0] = IniParams[80];
    SearchContext[1] = IniParams[81];
    SearchContext[2] = IniParams[90];
    SearchContext[3] = IniParams[112];
    SearchContext[4] = IniParams[113];
    SearchContext[5] = IniParams[114];
    SearchContext[6] = IniParams[116];
    SearchContext[7] = IniParams[117];
    SearchContext[8] = IniParams[118];
    SearchContext[9] = IniParams[119];
    // Record what the GPU sees through the actual CS constant-buffer region,
    // not the first four rows of a possibly pooled, larger underlying buffer.
    SearchContext[10] = Model[0];
    SearchContext[11] = Model[1];
    SearchContext[12] = Model[2];
    SearchContext[13] = Model[3];
}
