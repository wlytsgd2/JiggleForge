#include <d3d11.h>
#include <wrl/client.h>

#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>

#pragma comment(lib, "d3d11.lib")

using Microsoft::WRL::ComPtr;

namespace
{
    std::vector<std::uint8_t> ReadAllBytes(const std::filesystem::path& path)
    {
        std::ifstream stream(path, std::ios::binary | std::ios::ate);
        if (!stream)
        {
            throw std::runtime_error("Could not open the compiled shader.");
        }

        const std::streamsize size = stream.tellg();
        if (size <= 0)
        {
            throw std::runtime_error("The compiled shader is empty.");
        }

        stream.seekg(0, std::ios::beg);
        std::vector<std::uint8_t> bytes(static_cast<std::size_t>(size));
        if (!stream.read(
                reinterpret_cast<char*>(bytes.data()),
                size))
        {
            throw std::runtime_error("Could not read the compiled shader.");
        }

        return bytes;
    }

    void ThrowIfFailed(HRESULT result, const char* operation)
    {
        if (FAILED(result))
        {
            std::string message(operation);
            message += " failed with HRESULT 0x";
            char code[16]{};
            sprintf_s(code, "%08X", static_cast<unsigned int>(result));
            message += code;
            throw std::runtime_error(message);
        }
    }

    void CreateDevice(
        ComPtr<ID3D11Device>& device,
        ComPtr<ID3D11DeviceContext>& context)
    {
        D3D_FEATURE_LEVEL featureLevel{};
        HRESULT result = D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            0,
            nullptr,
            0,
            D3D11_SDK_VERSION,
            &device,
            &featureLevel,
            &context);
        if (FAILED(result))
        {
            result = D3D11CreateDevice(
                nullptr,
                D3D_DRIVER_TYPE_WARP,
                nullptr,
                0,
                nullptr,
                0,
                D3D11_SDK_VERSION,
                &device,
                &featureLevel,
                &context);
        }

        ThrowIfFailed(result, "D3D11CreateDevice");
        if (featureLevel < D3D_FEATURE_LEVEL_11_0)
        {
            throw std::runtime_error(
                "The available D3D11 device does not support compute shader 5.0.");
        }
    }
}

int wmain(int argumentCount, wchar_t** arguments)
{
    try
    {
        if (argumentCount != 4)
        {
            std::wcerr
                << L"Usage: GpuReadback.exe <shader.cso> <output.bin> "
                << L"<float4-record-count>\n";
            return 2;
        }

        const std::filesystem::path shaderPath(arguments[1]);
        const std::filesystem::path outputPath(arguments[2]);
        const std::uint32_t recordCount =
            static_cast<std::uint32_t>(std::stoul(arguments[3]));
        if (recordCount == 0
            || recordCount
                > (1u << D3D11_REQ_BUFFER_RESOURCE_TEXEL_COUNT_2_TO_EXP))
        {
            throw std::runtime_error("The record count is outside the safe range.");
        }

        const std::vector<std::uint8_t> shaderBytes =
            ReadAllBytes(shaderPath);
        ComPtr<ID3D11Device> device;
        ComPtr<ID3D11DeviceContext> context;
        CreateDevice(device, context);

        ComPtr<ID3D11ComputeShader> computeShader;
        ThrowIfFailed(
            device->CreateComputeShader(
                shaderBytes.data(),
                shaderBytes.size(),
                nullptr,
                &computeShader),
            "CreateComputeShader");

        const std::uint32_t byteCount = recordCount * 16u;
        std::vector<std::uint8_t> zeroData(byteCount, 0u);
        D3D11_SUBRESOURCE_DATA initialData{};
        initialData.pSysMem = zeroData.data();

        D3D11_BUFFER_DESC gpuDescription{};
        gpuDescription.ByteWidth = byteCount;
        gpuDescription.Usage = D3D11_USAGE_DEFAULT;
        gpuDescription.BindFlags = D3D11_BIND_UNORDERED_ACCESS;
        gpuDescription.MiscFlags = D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;
        gpuDescription.StructureByteStride = 16u;

        ComPtr<ID3D11Buffer> gpuBuffer;
        ThrowIfFailed(
            device->CreateBuffer(
                &gpuDescription,
                &initialData,
                &gpuBuffer),
            "CreateBuffer for GPU output");

        D3D11_UNORDERED_ACCESS_VIEW_DESC viewDescription{};
        viewDescription.Format = DXGI_FORMAT_UNKNOWN;
        viewDescription.ViewDimension = D3D11_UAV_DIMENSION_BUFFER;
        viewDescription.Buffer.FirstElement = 0u;
        viewDescription.Buffer.NumElements = recordCount;

        ComPtr<ID3D11UnorderedAccessView> outputView;
        ThrowIfFailed(
            device->CreateUnorderedAccessView(
                gpuBuffer.Get(),
                &viewDescription,
                &outputView),
            "CreateUnorderedAccessView");

        ID3D11UnorderedAccessView* views[] = {outputView.Get()};
        context->CSSetShader(computeShader.Get(), nullptr, 0u);
        context->CSSetUnorderedAccessViews(0u, 1u, views, nullptr);
        context->Dispatch(1u, 1u, 1u);

        ID3D11UnorderedAccessView* emptyViews[] = {nullptr};
        context->CSSetUnorderedAccessViews(0u, 1u, emptyViews, nullptr);
        context->CSSetShader(nullptr, nullptr, 0u);

        D3D11_BUFFER_DESC stagingDescription = gpuDescription;
        stagingDescription.Usage = D3D11_USAGE_STAGING;
        stagingDescription.BindFlags = 0u;
        stagingDescription.CPUAccessFlags = D3D11_CPU_ACCESS_READ;

        ComPtr<ID3D11Buffer> stagingBuffer;
        ThrowIfFailed(
            device->CreateBuffer(
                &stagingDescription,
                nullptr,
                &stagingBuffer),
            "CreateBuffer for staging");
        context->CopyResource(stagingBuffer.Get(), gpuBuffer.Get());

        D3D11_MAPPED_SUBRESOURCE mapped{};
        ThrowIfFailed(
            context->Map(
                stagingBuffer.Get(),
                0u,
                D3D11_MAP_READ,
                0u,
                &mapped),
            "Map staging output");

        std::ofstream output(outputPath, std::ios::binary);
        if (!output)
        {
            context->Unmap(stagingBuffer.Get(), 0u);
            throw std::runtime_error("Could not create the GPU output file.");
        }

        output.write(
            static_cast<const char*>(mapped.pData),
            static_cast<std::streamsize>(byteCount));
        context->Unmap(stagingBuffer.Get(), 0u);
        if (!output)
        {
            throw std::runtime_error("Could not write the GPU output file.");
        }

        return 0;
    }
    catch (const std::exception& exception)
    {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
