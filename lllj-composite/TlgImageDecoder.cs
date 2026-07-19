using FreeMote.Tlg.Managed;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace atri_composite
{
    internal static class TlgImageDecoder
    {
        private static readonly byte[] TlgMuxHeader = Encoding.ASCII.GetBytes("TLGmux\0idx\x1a");

        public static DecodedTlgImage Decode(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            if (HasTlgMuxHeader(path))
            {
                try
                {
                    var decoded = TlgQoiCodec.Decode(path);
                    return new DecodedTlgImage(CreateBitmap(decoded), null);
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    throw new InvalidDataException($"Failed to decode TLGmux image: {path}", ex);
                }
            }

            FreeMote.Tlg.TlgLoader loader = null;
            try
            {
                loader = new FreeMote.Tlg.TlgLoader(File.ReadAllBytes(path));
                return new DecodedTlgImage(loader.Bitmap, loader);
            }
            catch
            {
                loader?.Dispose();
                throw;
            }
        }

        internal static bool HasTlgMuxHeader(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length < TlgMuxHeader.Length) return false;

                for (var i = 0; i < TlgMuxHeader.Length; i++)
                {
                    if (stream.ReadByte() != TlgMuxHeader[i]) return false;
                }

                return true;
            }
        }

        private static Bitmap CreateBitmap(TlgDecodedImage image)
        {
            var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, image.Width, image.Height);
            BitmapData bitmapData = null;
            try
            {
                bitmapData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                var rowBytes = checked(image.Width * 4);
                for (var y = 0; y < image.Height; y++)
                    Marshal.Copy(image.Bgra32, y * rowBytes, IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride), rowBytes);

                bitmap.UnlockBits(bitmapData);
                bitmapData = null;
                return bitmap;
            }
            catch
            {
                if (bitmapData != null) bitmap.UnlockBits(bitmapData);
                bitmap.Dispose();
                throw;
            }
        }
    }

    internal sealed class DecodedTlgImage : IDisposable
    {
        private IDisposable _owner;

        public Bitmap Bitmap { get; private set; }

        internal DecodedTlgImage(Bitmap bitmap, IDisposable owner)
        {
            Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
            _owner = owner;
        }

        public void Dispose()
        {
            Bitmap?.Dispose();
            Bitmap = null;
            _owner?.Dispose();
            _owner = null;
        }
    }
}
