using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace atri_composite
{
    class CompoundImage
    {
        public List<Layer> Layers { get; }

        public int Width { get; }

        public int Height { get; }

        public string Name { get; }

        public CompoundImage(string descPath)
        {
            Name = Path.GetFileNameWithoutExtension(descPath);
            var imagePrefix = Path.GetFullPath(descPath);
            imagePrefix = imagePrefix.Substring(0, imagePrefix.Length - 4) + "_";

            var jArr = Utils.LoadPBDFile(descPath, false);

            int i = 0;
            Width = (int)jArr[i]["width"];
            Height = (int)jArr[i]["height"];

            var flatLayers = new List<Layer>();
            for (i++; i < jArr.Count; i++)
            {
                Layer item = jArr[i].ToObject<Layer>();
                item.Path = imagePrefix + item.LayerID;
                if (File.Exists(item.Path + ".png"))
                {
                    item.Path = imagePrefix + item.LayerID + ".png";
                }
                else
                {
                    item.Path = imagePrefix + item.LayerID + ".tlg";
                }

                flatLayers.Add(item);
            }

            flatLayers.Where(o => o.GroupLayerID != 0).ToList()
                .ForEach(o => flatLayers.First(p => p.LayerID == o.GroupLayerID).Children.Add(o));

            Layers = flatLayers.Where(o => o.GroupLayerID == 0).ToList();
        }

        public Layer GetLayer(string query)
        {
            try
            {
                var blocks = query.Split('/');
                Layer prev;
                if (blocks.Length > 1) prev = Layers.First(o => o.LayerType == LayerType.Folder && o.Name == blocks[0]);
                else return Layers.First(o => o.LayerType == LayerType.Normal && o.Name == blocks[0]);
                for (var i = 1; i < blocks.Length - 1; i++)
                    prev = prev.Children.First(o => o.LayerType == LayerType.Folder && o.Name == blocks[i]);
                return prev.Children.First(o => o.LayerType == LayerType.Normal && o.Name == blocks.Last());
            }
            catch
            {
                return null;
            }
        }

        public Bitmap Generate(params string[] layers)
        {
            var bitmap = new Bitmap(Width, Height);
            foreach (var s in layers)
            {
                if (s == "dummy") continue;
                var layer = GetLayer(s);
                if (layer == null) throw new ArgumentException();

                Bitmap layerBitmap;
                FreeMote.Tlg.TlgLoader tlgLoader = null;
                if (layer.Path.EndsWith(".png"))
                {
                    layerBitmap = new Bitmap(layer.Path);
                }
                else
                {
                    tlgLoader = new FreeMote.Tlg.TlgLoader(File.ReadAllBytes(layer.Path));
                    layerBitmap = tlgLoader.Bitmap;
                }

                try
                {
                    switch (layer.Type)
                    {
                        case KrBlendMode.ltPsNormal:
                            using (var g = Graphics.FromImage(bitmap))
                            {
                                var ia = new ImageAttributes();

                                float op = layer.Opacity / 255f;
                                ColorMatrix cm = new ColorMatrix { Matrix33 = op };

                                ia.SetColorMatrix(cm);

                                g.DrawImage(layerBitmap,
                                    new Rectangle(layer.Left, layer.Top, layer.Width, layer.Height),
                                    0, 0, layer.Width, layer.Height,
                                    GraphicsUnit.Pixel,
                                    ia);
                            }
                            break;
                        case KrBlendMode.ltPsDarken:
                            BlendPsDarken(bitmap, layerBitmap, layer.Left, layer.Top, layer.Opacity);
                            break;
                        case KrBlendMode.ltPsMultiplicative:
                            BlendPsMultiplicative(bitmap, layerBitmap, layer.Left, layer.Top, layer.Opacity);
                            break;
                        default:
                            throw new NotSupportedException($"Blend mode {layer.Type} is not supported.");
                    }
                }
                finally
                {
                    layerBitmap.Dispose();
                    tlgLoader?.Dispose();
                }
            }
            return bitmap;
        }

        private static void BlendPsDarken(Bitmap baseBmp, Bitmap topBmp, int offsetX, int offsetY, int opacity)
        {
            var rectBase = new Rectangle(0, 0, baseBmp.Width, baseBmp.Height);
            var rectTop = new Rectangle(0, 0, topBmp.Width, topBmp.Height);

            var baseData = baseBmp.LockBits(rectBase, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var topData = topBmp.LockBits(rectTop, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                int baseStride = baseData.Stride;
                int topStride = topData.Stride;

                int startX = Math.Max(0, offsetX);
                int startY = Math.Max(0, offsetY);
                int endX = Math.Min(baseBmp.Width, offsetX + topBmp.Width);
                int endY = Math.Min(baseBmp.Height, offsetY + topBmp.Height);

                if (startX >= endX || startY >= endY) return;

                unsafe
                {
                    byte* baseScan0 = (byte*)baseData.Scan0;
                    byte* topScan0 = (byte*)topData.Scan0;

                    for (int yBase = startY; yBase < endY; yBase++)
                    {
                        int yTop = yBase - offsetY;

                        byte* baseRow = baseScan0 + yBase * baseStride;
                        byte* topRow = topScan0 + yTop * topStride;

                        for (int xBase = startX; xBase < endX; xBase++)
                        {
                            int xTop = xBase - offsetX;

                            byte* basePixel = baseRow + xBase * 4;
                            byte* topPixel = topRow + xTop * 4;

                            byte tb = topPixel[0];
                            byte tg = topPixel[1];
                            byte tr = topPixel[2];
                            byte ta = topPixel[3];

                            if (ta == 0) continue;

                            byte bb = basePixel[0];
                            byte bg = basePixel[1];
                            byte br = basePixel[2];
                            byte ba = basePixel[3];

                            byte db = bb < tb ? bb : tb;
                            byte dg = bg < tg ? bg : tg;
                            byte dr = br < tr ? br : tr;

                            int a = (ta * opacity + 127) / 255;
                            int invA = 255 - a;

                            basePixel[0] = (byte)((bb * invA + db * a + 127) / 255);
                            basePixel[1] = (byte)((bg * invA + dg * a + 127) / 255);
                            basePixel[2] = (byte)((br * invA + dr * a + 127) / 255);

                            int outA = ba + ((255 - ba) * a + 127) / 255;
                            if (outA > 255) outA = 255;
                            basePixel[3] = (byte)outA;
                        }
                    }
                }
            }
            finally
            {
                baseBmp.UnlockBits(baseData);
                topBmp.UnlockBits(topData);
            }
        }

        private static void BlendPsMultiplicative(Bitmap baseBmp, Bitmap topBmp, int offsetX, int offsetY, int opacity)
        {
            var rectBase = new Rectangle(0, 0, baseBmp.Width, baseBmp.Height);
            var rectTop = new Rectangle(0, 0, topBmp.Width, topBmp.Height);

            var baseData = baseBmp.LockBits(rectBase, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var topData = topBmp.LockBits(rectTop, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                int baseStride = baseData.Stride;
                int topStride = topData.Stride;

                int startX = Math.Max(0, offsetX);
                int startY = Math.Max(0, offsetY);
                int endX = Math.Min(baseBmp.Width, offsetX + topBmp.Width);
                int endY = Math.Min(baseBmp.Height, offsetY + topBmp.Height);

                if (startX >= endX || startY >= endY)
                    return;

                unsafe
                {
                    byte* baseScan0 = (byte*)baseData.Scan0;
                    byte* topScan0 = (byte*)topData.Scan0;

                    for (int yBase = startY; yBase < endY; yBase++)
                    {
                        int yTop = yBase - offsetY;

                        byte* baseRow = baseScan0 + yBase * baseStride;
                        byte* topRow = topScan0 + yTop * topStride;

                        for (int xBase = startX; xBase < endX; xBase++)
                        {
                            int xTop = xBase - offsetX;

                            byte* basePixel = baseRow + xBase * 4;
                            byte* topPixel = topRow + xTop * 4;

                            byte pa = topPixel[3];
                            if (pa == 0) continue;
                            
                            int a = (pa * opacity + 127) / 255;
                            int invA = 255 - a;

                            byte tb = topPixel[0];
                            byte tg = topPixel[1];
                            byte tr = topPixel[2];

                            byte bb = basePixel[0];
                            byte bg = basePixel[1];
                            byte br = basePixel[2];
                            byte ba = basePixel[3];
                            
                            byte mb = (byte)((bb * tb + 127) / 255);
                            byte mg = (byte)((bg * tg + 127) / 255);
                            byte mr = (byte)((br * tr + 127) / 255);
                            
                            basePixel[0] = (byte)((bb * invA + mb * a + 127) / 255);
                            basePixel[1] = (byte)((bg * invA + mg * a + 127) / 255);
                            basePixel[2] = (byte)((br * invA + mr * a + 127) / 255);
                            
                            int outA = ba + ((255 - ba) * a + 127) / 255;
                            if (outA > 255) outA = 255;
                            basePixel[3] = (byte)outA;
                        }
                    }
                }
            }
            finally
            {
                baseBmp.UnlockBits(baseData);
                topBmp.UnlockBits(topData);
            }
        }

        public enum KrBlendMode
        {
            ltBinder = 0,
            ltCoverRect = 1,
            ltOpaque = 1, // the same as ltCoverRect
            ltTransparent = 2, // alpha blend
            ltAlpha = 2, // the same as ltTransparent
            ltAdditive = 3,
            ltSubtractive = 4,
            ltMultiplicative = 5,
            ltEffect = 6,
            ltFilter = 7,
            ltDodge = 8,
            ltDarken = 9,
            ltLighten = 10,
            ltScreen = 11,
            ltAddAlpha = 12, // additive alpha blend
            ltPsNormal = 13,
            ltPsAdditive = 14,
            ltPsSubtractive = 15,
            ltPsMultiplicative = 16,
            ltPsScreen = 17,
            ltPsOverlay = 18,
            ltPsHardLight = 19,
            ltPsSoftLight = 20,
            ltPsColorDodge = 21,
            ltPsColorDodge5 = 22,
            ltPsColorBurn = 23,
            ltPsLighten = 24,
            ltPsDarken = 25,
            ltPsDifference = 26,
            ltPsDifference5 = 27,
            ltPsExclusion = 28
        }

        public enum LayerType
        {
            Normal = 0,
            Hidden = 1,
            Folder = 2,
            Adjust = 3,
            Fill = 4
        }

        public class Layer
        {
            public string Path { get; set; }

            private string _name;

            [JsonProperty("name")]
            public string Name
            {
                get => _name;
                set => _name = value?.Replace("/", "_"); // 22　驚き/目を見開く1 -> 22　驚き_目を見開く1
            }

            [JsonProperty("type")] public KrBlendMode Type { get; set; }

            [JsonProperty("layer_type")] public LayerType LayerType { get; set; }

            [JsonProperty("layer_id")] public int LayerID { get; set; }

            [JsonProperty("group_layer_id")] public int GroupLayerID { get; set; } = 0;

            [JsonProperty("width")] public int Width { get; set; }

            [JsonProperty("height")] public int Height { get; set; }

            [JsonProperty("left")] public int Left { get; set; }

            [JsonProperty("top")] public int Top { get; set; }

            [JsonProperty("visible")] public int Visible { get; set; }

            [JsonProperty("opacity")] public int Opacity { get; set; }

            public List<Layer> Children { get; } = new List<Layer>();
        }
    }
}