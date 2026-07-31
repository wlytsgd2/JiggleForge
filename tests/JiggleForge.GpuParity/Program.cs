using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace JiggleForge.Core.Tests;

internal static class Program
{
    private const int MotionScenarioCount = 6;
    private const int MotionRecordsPerScenario = 7;
    private const int InputScenarioCount = 5;
    private const int InputRecordsPerScenario = 9;
    private const int ComponentsPerRecord = 4;
    private static readonly Vector2 Viewport = new(1000.0f, 1000.0f);

    public static int Main(string[] arguments)
    {
        try
        {
            if (arguments.Length != 3)
            {
                Console.Error.WriteLine(
                    "Usage: JiggleForge.GpuParity <gpu-runner.exe> "
                    + "<motion.cso> <input-controller.cso>");
                return 2;
            }

            string motionOutputPath = Path.Combine(
                Path.GetTempPath(),
                $"JiggleForge-MotionGpu-{Guid.NewGuid():N}.bin");
            string inputOutputPath = Path.Combine(
                Path.GetTempPath(),
                $"JiggleForge-InputGpu-{Guid.NewGuid():N}.bin");
            try
            {
                RunGpu(
                    arguments[0],
                    arguments[1],
                    motionOutputPath,
                    MotionScenarioCount * MotionRecordsPerScenario);
                Compare(
                    "motion",
                    MotionScenarioCount,
                    MotionRecordsPerScenario,
                    BuildExpectedMotionOutput(),
                    ReadGpuOutput(
                        motionOutputPath,
                        MotionScenarioCount * MotionRecordsPerScenario));

                RunGpu(
                    arguments[0],
                    arguments[2],
                    inputOutputPath,
                    InputScenarioCount * InputRecordsPerScenario);
                Compare(
                    "input",
                    InputScenarioCount,
                    InputRecordsPerScenario,
                    BuildExpectedInputOutput(),
                    ReadGpuOutput(
                        inputOutputPath,
                        InputScenarioCount * InputRecordsPerScenario));
            }
            finally
            {
                if (File.Exists(motionOutputPath))
                {
                    File.Delete(motionOutputPath);
                }

                if (File.Exists(inputOutputPath))
                {
                    File.Delete(inputOutputPath);
                }
            }

            Console.WriteLine(
                "Runtime CPU/GPU parity passed for 11 scenarios and 87 float4 records.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void RunGpu(
        string runnerPath,
        string shaderPath,
        string outputPath,
        int recordCount)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = runnerPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(shaderPath);
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add(
            recordCount.ToString(CultureInfo.InvariantCulture));

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the GPU runner.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"GPU runner failed with exit code {process.ExitCode}.\n"
                + standardOutput
                + standardError);
        }
    }

    private static float[] ReadGpuOutput(string path, int recordCount)
    {
        byte[] bytes = File.ReadAllBytes(path);
        int expectedBytes = recordCount * ComponentsPerRecord * sizeof(float);
        if (bytes.Length != expectedBytes)
        {
            throw new InvalidDataException(
                $"GPU output has {bytes.Length} bytes; expected {expectedBytes}.");
        }

        float[] values = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return values;
    }

    private static float[] BuildExpectedMotionOutput()
    {
        List<CpuMotionState> scenarios =
        [
            RunIdleScenario(),
            RunPlanarDragScenario(),
            RunWheelScenario(),
            RunReleaseScenario(),
            RunClampScenario(),
            RunTapScenario(),
        ];
        List<float> values = new(
            MotionScenarioCount
            * MotionRecordsPerScenario
            * ComponentsPerRecord);
        foreach (CpuMotionState state in scenarios)
        {
            foreach (Vector4 record in Encode(state))
            {
                values.Add(record.X);
                values.Add(record.Y);
                values.Add(record.Z);
                values.Add(record.W);
            }
        }

        return values.ToArray();
    }

    private static float[] BuildExpectedInputOutput()
    {
        List<CpuCaptureState> scenarios =
        [
            RunIdleInputScenario(),
            RunValidPressInputScenario(),
            RunHeldChangedPickInputScenario(),
            RunReleaseInputScenario(),
            RunInvalidRepressInputScenario(),
        ];
        List<float> values = new(
            InputScenarioCount
            * InputRecordsPerScenario
            * ComponentsPerRecord);
        foreach (CpuCaptureState state in scenarios)
        {
            foreach (Vector4 record in EncodeInputState(state))
            {
                values.Add(record.X);
                values.Add(record.Y);
                values.Add(record.Z);
                values.Add(record.W);
            }
        }

        return values.ToArray();
    }

    private static CpuCaptureState RunIdleInputScenario()
    {
        CpuCaptureState state = new();
        state.Step(
            Input(false, new Vector2(50.0f, 60.0f)),
            InputPick(false, 0, 0));
        return state;
    }

    private static CpuCaptureState RunValidPressInputScenario()
    {
        CpuCaptureState state = new();
        state.Step(
            Input(true, new Vector2(100.0f, 200.0f)),
            InputPick(true, 17, 4));
        return state;
    }

    private static CpuCaptureState RunHeldChangedPickInputScenario()
    {
        CpuCaptureState state = new();
        state.Step(
            Input(true, new Vector2(100.0f, 200.0f)),
            InputPick(true, 17, 4));
        state.Step(
            Input(true, new Vector2(300.0f, 400.0f), 7, 2),
            InputPick(true, 99, 8));
        return state;
    }

    private static CpuCaptureState RunReleaseInputScenario()
    {
        CpuCaptureState state = new();
        state.Step(
            Input(true, new Vector2(100.0f)),
            InputPick(true, 17, 4));
        state.Step(
            Input(false, new Vector2(120.0f)),
            InputPick(true, 99, 8));
        return state;
    }

    private static CpuCaptureState RunInvalidRepressInputScenario()
    {
        CpuCaptureState state = new();
        state.Step(
            Input(true, new Vector2(100.0f)),
            InputPick(true, 17, 4));
        state.Step(
            Input(false, new Vector2(120.0f)),
            InputPick(true, 17, 4));
        state.Step(
            Input(true, new Vector2(200.0f)),
            InputPick(false, 0, 0));
        return state;
    }

    private static CpuMotionState RunIdleScenario()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        Step(
            state,
            capture,
            DefaultParameters(),
            false,
            new Vector2(500.0f),
            InvalidPick());
        return state;
    }

    private static CpuMotionState RunPlanarDragScenario()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = DefaultParameters();
        CpuPick pick = Pick();
        Step(state, capture, parameters, true, new Vector2(500.0f), pick);
        Step(
            state,
            capture,
            parameters,
            true,
            new Vector2(620.0f, 540.0f),
            pick);
        return state;
    }

    private static CpuMotionState RunWheelScenario()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = DefaultParameters();
        CpuPick pick = Pick();
        Step(state, capture, parameters, true, new Vector2(500.0f), pick);
        Step(
            state,
            capture,
            parameters,
            true,
            new Vector2(500.0f),
            pick,
            towardSequence: 3);
        return state;
    }

    private static CpuMotionState RunReleaseScenario()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = DefaultParameters();
        CpuPick pick = Pick();
        Step(state, capture, parameters, true, new Vector2(500.0f), pick);
        Step(state, capture, parameters, true, new Vector2(650.0f, 500.0f), pick);
        Step(state, capture, parameters, false, new Vector2(650.0f, 500.0f), pick);
        return state;
    }

    private static CpuMotionState RunClampScenario()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = DefaultParameters();
        parameters.Strength = 10.0f;
        parameters.DragScale = 100.0f;
        parameters.MaxOffset = 0.05f;
        parameters.TargetFollowSeconds = 0.0f;
        parameters.HoldFrequencyHz = 60.0f;
        CpuPick pick = Pick(pressWorldPosition: Vector3.Zero);
        Step(state, capture, parameters, true, Vector2.Zero, pick);
        for (int index = 0; index < 4; index++)
        {
            Step(state, capture, parameters, true, new Vector2(1000.0f), pick);
        }

        return state;
    }

    private static CpuMotionState RunTapScenario()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = DefaultParameters();
        CpuPick pick = Pick() with
        {
            SurfaceNormal = new Vector3(0.0f, 0.6f, 0.8f),
        };
        Step(state, capture, parameters, true, new Vector2(500.0f), pick);
        Step(state, capture, parameters, false, new Vector2(504.0f, 500.0f), pick);
        return state;
    }

    private static void Step(
        CpuMotionState state,
        CpuCaptureState capture,
        CpuParameters parameters,
        bool held,
        Vector2 cursor,
        CpuPick pick,
        int towardSequence = 0,
        int awaySequence = 0)
    {
        CpuInput input = new(
            cursor,
            Viewport,
            held,
            towardSequence,
            awaySequence,
            1.0f / 60.0f);
        capture.Step(input, pick);
        CpuMotionSolver.Step(state, 17, input, capture, parameters);
    }

    private static CpuParameters DefaultParameters() => new()
    {
        Strength = 0.7f,
        DragScale = 0.75f,
        MaxOffset = 0.15f,
        TargetFollowSeconds = 0.02f,
        HoldFrequencyHz = 10.0f,
        HoldDampingRatio = 0.84f,
        ReleaseFrequencyHz = 2.2f,
        ReleaseDampingRatio = 0.9f,
        ReleaseImpulse = 0.12f,
        WheelDepthStep = 0.02f,
        WheelMinDepth = 0.0f,
        WheelMaxDepth = 0.15f,
        MouseXSign = 1.0f,
        MouseYSign = -1.0f,
    };

    private static CpuPick Pick(
        Vector3? pressWorldPosition = null) =>
        new(
            true,
            17,
            pressWorldPosition ?? new Vector3(1.0f, 2.0f, 3.0f),
            Vector3.UnitX,
            Vector3.UnitY,
            4);

    private static CpuPick InvalidPick() =>
        new(false, 17, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 4);

    private static CpuInput Input(
        bool held,
        Vector2 cursor,
        int toward = 0,
        int away = 0) =>
        new(
            cursor,
            Viewport,
            held,
            toward,
            away,
            1.0f / 60.0f);

    private static CpuPick InputPick(
        bool valid,
        int objectId,
        int sourceDraw) =>
        new(
            valid,
            objectId,
            new Vector3(1.0f, 2.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 0.0f),
            new Vector3(2.0f, 3.0f, 0.0f),
            sourceDraw)
        {
            Depth = 0.42f,
            Priority = 3.0f,
            PipelineToken = 91.0f,
            TriangleOrdinal = 7,
            TriangleIndices = new Vector3(10.0f, 20.0f, 30.0f),
            Barycentric = new Vector3(0.2f, 0.3f, 0.5f),
            SurfaceNormal = new Vector3(0.0f, 0.6f, 0.8f),
        };

    private static Vector4[] Encode(CpuMotionState state)
    {
        float holdMetadata =
            (Math.Min(state.CaptureGeneration, 0x007fffffu) * 2u)
            + (state.WasHeld ? 1u : 0u);

        return
        [
            new Vector4(state.Position, state.Active ? 1.0f : 0.0f),
            new Vector4(state.Velocity, state.SleepFrames),
            new Vector4(state.Anchor, state.OwnerObjectId),
            new Vector4(state.ScreenRight, state.SourceDraw),
            new Vector4(state.ScreenUp, state.DepthTarget),
            new Vector4(state.FilteredTarget, state.LastWheelSequenceCode),
            new Vector4(state.PreviousFilteredTarget, holdMetadata),
        ];
    }

    private static Vector4[] EncodeInputState(CpuCaptureState state)
    {
        CpuPick pick = state.Pick;
        return
        [
            new Vector4(
                state.PressCursorPixels,
                state.PreviousHeld ? 1.0f : 0.0f,
                state.Generation),
            new Vector4(
                state.CurrentCursorPixels,
                state.WheelSequenceCode,
                state.CurrentPickValid ? 1.0f : 0.0f),
            new Vector4(
                pick.WorldPosition,
                state.Valid ? 1.0f : 0.0f),
            new Vector4(pick.ScreenRight, pick.ObjectId),
            new Vector4(pick.ScreenUp, pick.SourceDraw),
            new Vector4(
                pick.Depth,
                pick.Priority,
                pick.TriangleOrdinal,
                state.Generation),
            new Vector4(
                pick.TriangleIndices,
                state.PressCursorPixels.X),
            new Vector4(
                pick.Barycentric,
                state.PressCursorPixels.Y),
            new Vector4(
                pick.SurfaceNormal,
                state.HoldSeconds),
        ];
    }

    private static void Compare(
        string label,
        int scenarioCount,
        int recordsPerScenario,
        float[] expected,
        float[] actual)
    {
        if (expected.Length != actual.Length)
        {
            throw new InvalidDataException("CPU and GPU output sizes differ.");
        }

        float largestDifference = 0.0f;
        for (int index = 0; index < expected.Length; index++)
        {
            float expectedValue = expected[index];
            float actualValue = actual[index];
            float difference = MathF.Abs(expectedValue - actualValue);
            largestDifference = MathF.Max(largestDifference, difference);
            float tolerance = 5.0e-5f
                * MathF.Max(1.0f, MathF.Abs(expectedValue));
            if (!float.IsFinite(actualValue) || difference > tolerance)
            {
                throw Difference(
                    label,
                    recordsPerScenario,
                    index,
                    expectedValue,
                    actualValue);
            }
        }

        Console.WriteLine(
            $"{label} largest finite component difference: "
            + $"{largestDifference:G9} across {scenarioCount} scenarios.");
    }

    private static InvalidDataException Difference(
        string label,
        int recordsPerScenario,
        int index,
        float expected,
        float actual)
    {
        int scenario = index
            / (recordsPerScenario * ComponentsPerRecord);
        int withinScenario = index
            % (recordsPerScenario * ComponentsPerRecord);
        int record = withinScenario / ComponentsPerRecord;
        int component = withinScenario % ComponentsPerRecord;
        return new InvalidDataException(
            $"CPU/GPU {label} mismatch at scenario {scenario}, "
            + $"record {record}[{component}]: "
            + $"expected {expected:G9}, actual {actual:G9}.");
    }
}
