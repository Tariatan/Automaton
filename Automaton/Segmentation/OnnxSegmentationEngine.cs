using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using Serilog;

namespace Automaton.Segmentation;

internal sealed class OnnxSegmentationEngine : ISegmentationEngine, IDisposable
{
    private const float DefaultConfidenceThreshold = 0.5f;

    private readonly ILogger m_Logger = Log.ForContext<OnnxSegmentationEngine>();
    private readonly InferenceSession? m_Session;
    private readonly string m_InputName;
    private readonly int m_ModelInputWidth;
    private readonly int m_ModelInputHeight;

    public OnnxSegmentationEngine(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            m_Logger.Warning("ONNX model not found at {ModelPath}, AI segmentation disabled", modelPath);
            m_InputName = string.Empty;
            return;
        }

        try
        {
            var options = new SessionOptions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
            m_Session = new InferenceSession(modelPath, options);

            m_InputName = m_Session.InputMetadata.Keys.First();
            var inputShape = m_Session.InputMetadata[m_InputName].Dimensions;
            // Shape is [batch, channels, height, width]
            m_ModelInputHeight = inputShape[2];
            m_ModelInputWidth = inputShape[3];

            m_Logger.Information(
                "ONNX segmentation model loaded. ModelPath={ModelPath}, Input={InputName}, Size={Width}x{Height}",
                modelPath, m_InputName, m_ModelInputWidth, m_ModelInputHeight);
        }
        catch (Exception exception)
        {
            m_Logger.Error(exception, "Failed to load ONNX model from {ModelPath}", modelPath);
            m_Session = null;
            m_InputName = string.Empty;
        }
    }

    public bool IsAvailable => m_Session is not null;

    public SegmentationResult Segment(Mat playfieldImage)
    {
        if (m_Session is null)
        {
            return SegmentationResult.Empty;
        }

        var originalSize = playfieldImage.Size();
        using var resized = PreprocessInput(playfieldImage);

        // Perform the standard image→tensor normalization that all PyTorch/TF vision models expect.
        var inputTensor = MatToTensor(resized);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(m_InputName, inputTensor)
        };

        // Feed the tensor to the ONNX Runtime session → get output tensor
        using var results = m_Session.Run(inputs);
        var outputTensor = results[0].AsTensor<float>();
        var mask = TensorToMask(outputTensor, originalSize);
        var confidence = ComputeConfidence(outputTensor);
        var polygons = SegmentationPostProcessor.ExtractPolygons(mask);

        m_Logger.Information(
            "AI segmentation completed. Confidence={Confidence:0.000}, PolygonCount={PolygonCount}",
            confidence, polygons.Count);

        return new SegmentationResult(mask, polygons, confidence);
    }

    private Mat PreprocessInput(Mat image)
    {
        if (image.Width == m_ModelInputWidth && image.Height == m_ModelInputHeight)
        {
            return image.Clone();
        }

        var resized = new Mat();
        Cv2.Resize(image, resized, new Size(m_ModelInputWidth, m_ModelInputHeight));
        return resized;
    }

    private DenseTensor<float> MatToTensor(Mat image)
    {
        var tensor = new DenseTensor<float>([1, 3, m_ModelInputHeight, m_ModelInputWidth]);

        using var floatImage = new Mat();
        image.ConvertTo(floatImage, MatType.CV_32FC3, 1.0 / 255.0);

        var channels = Cv2.Split(floatImage);
        try
        {
            for (var channel = 0; channel < 3; channel++)
            {
                channels[channel].GetArray(out float[] channelData);
                var tensorChannel = 2 - channel; // BGR → RGB
                for (var y = 0; y < m_ModelInputHeight; y++)
                {
                    for (var x = 0; x < m_ModelInputWidth; x++)
                    {
                        tensor[0, tensorChannel, y, x] = channelData[(y * m_ModelInputWidth) + x];
                    }
                }
            }
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }

        return tensor;
    }

    private Mat TensorToMask(Tensor<float> outputTensor, Size targetSize)
    {
        var outputShape = outputTensor.Dimensions;
        var height = outputShape.Length >= 3 ? outputShape[^2] : m_ModelInputHeight;
        var width = outputShape.Length >= 3 ? outputShape[^1] : m_ModelInputWidth;

        var mask = new Mat(height, width, MatType.CV_8UC1);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = outputShape.Length switch
                {
                    4 => outputTensor[0, 0, y, x],
                    3 => outputTensor[0, y, x],
                    _ => outputTensor[(y * width) + x]
                };

                mask.Set(y, x, value >= DefaultConfidenceThreshold ? (byte)255 : (byte)0);
            }
        }

        if (mask.Width != targetSize.Width || mask.Height != targetSize.Height)
        {
            var resizedMask = new Mat();
            Cv2.Resize(mask, resizedMask, targetSize, 0, 0, InterpolationFlags.Nearest);
            mask.Dispose();
            return resizedMask;
        }

        return mask;
    }

    // If the model outputs values like 0.95, 0.92, 0.88 for the "foreground" pixels, confidence ≈ 0.92.
    // If it barely crosses 0.5 for most pixels, confidence will be ~0.55 — signaling uncertainty.
    private static float ComputeConfidence(Tensor<float> outputTensor)
    {
        var sum = 0f;
        var count = 0;
        foreach (var value in outputTensor)
        {
            if (value >= DefaultConfidenceThreshold)
            {
                sum += value;
                count++;
            }
        }

        return count > 0 ? sum / count : 0f;
    }

    public void Dispose()
    {
        m_Session?.Dispose();
    }
}
