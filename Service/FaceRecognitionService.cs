using System.Text.Json;
using FaceAiSharp;
using FaceAiSharp.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebAndMobile.Services;

public interface IFaceRecognitionService
{
    FaceAnalysisResult Analyze(string imageDataUrl);
    double Compare(string storedEmbeddingJson, float[] currentEmbedding);
}

public sealed record FaceAnalysisResult(
    bool Success,
    int FaceCount,
    float[]? Embedding,
    string Message);

public class FaceRecognitionService : IFaceRecognitionService
{
    private readonly object inferenceLock = new();

    public FaceAnalysisResult Analyze(string imageDataUrl)
    {
        try
        {
            byte[] bytes = DecodeDataUrl(imageDataUrl);

            using Image<Rgb24> image = Image.Load<Rgb24>(bytes);

            lock (inferenceLock)
            {
                var detector =
                    FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();

                var embeddingsGenerator =
                    FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();

                var faces = detector.DetectFaces(image).ToList();

                if (faces.Count == 0)
                {
                    return new(false, 0, null,
                        "No face detected. Please look at the camera.");
                }

                if (faces.Count > 1)
                {
                    return new(false, faces.Count, null,
                        "Multiple faces detected. Only one person is allowed.");
                }

                var face = faces[0];

                if (face.Landmarks == null)
                {
                    return new(false, 1, null,
                        "Face landmarks could not be detected. Please face forward.");
                }

                embeddingsGenerator.AlignFaceUsingLandmarks(
                    image,
                    face.Landmarks);

                float[] embedding =
                    embeddingsGenerator.GenerateEmbedding(image).ToArray();

                return new(true, 1, embedding, "Face detected.");
            }
        }
        catch (Exception exception)
        {
            return new(false, 0, null,
                "Unable to analyse the camera frame. " + exception.Message);
        }
    }

    public double Compare(
        string storedEmbeddingJson,
        float[] currentEmbedding)
    {
        float[]? stored =
            JsonSerializer.Deserialize<float[]>(storedEmbeddingJson);

        if (stored == null || stored.Length != currentEmbedding.Length)
        {
            return 0;
        }

        double dot = 0;
        double storedMagnitude = 0;
        double currentMagnitude = 0;

        for (int index = 0; index < stored.Length; index++)
        {
            dot += stored[index] * currentEmbedding[index];
            storedMagnitude += stored[index] * stored[index];
            currentMagnitude += currentEmbedding[index] * currentEmbedding[index];
        }

        if (storedMagnitude == 0 || currentMagnitude == 0)
        {
            return 0;
        }

        return dot /
            (Math.Sqrt(storedMagnitude) * Math.Sqrt(currentMagnitude));
    }

    private static byte[] DecodeDataUrl(string imageDataUrl)
    {
        if (string.IsNullOrWhiteSpace(imageDataUrl))
        {
            throw new ArgumentException("Image data is empty.");
        }

        int commaIndex = imageDataUrl.IndexOf(',');
        string base64 = commaIndex >= 0
            ? imageDataUrl[(commaIndex + 1)..]
            : imageDataUrl;

        return Convert.FromBase64String(base64);
    }
}
