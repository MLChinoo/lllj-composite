using FreeMote.Tlg.Managed;
using System;
using System.Drawing;
using System.IO;
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
                    return new DecodedTlgImage(decoded.CreateBitmap(), null);
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
