using System;
using UnityEditor;

namespace ItchDeploy.Editor
{
    public static class WebGLHelper
    {
        public enum CompressionFormat
        {
            Disabled = 0,
            Gzip = 1,
            Brotli = 2,
        }

        public static void SetCompression(CompressionFormat format)
        {
            var unityFormat = ToUnityFormat(format);
            PlayerSettings.WebGL.compressionFormat = unityFormat;

            if (PlayerSettings.WebGL.compressionFormat != unityFormat)
                throw new InvalidOperationException(
                    $"Unity did not apply the requested WebGL compression format: {format}.");
        }

        public static CompressionFormat GetCurrentCompression()
        {
            return PlayerSettings.WebGL.compressionFormat switch
            {
                WebGLCompressionFormat.Disabled => CompressionFormat.Disabled,
                WebGLCompressionFormat.Gzip => CompressionFormat.Gzip,
                WebGLCompressionFormat.Brotli => CompressionFormat.Brotli,
                _ => throw new InvalidOperationException(
                    $"Unsupported Unity WebGL compression format: {PlayerSettings.WebGL.compressionFormat}.")
            };
        }

        private static WebGLCompressionFormat ToUnityFormat(CompressionFormat format)
        {
            return format switch
            {
                CompressionFormat.Disabled => WebGLCompressionFormat.Disabled,
                CompressionFormat.Gzip => WebGLCompressionFormat.Gzip,
                CompressionFormat.Brotli => WebGLCompressionFormat.Brotli,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
            };
        }
    }
}
