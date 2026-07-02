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
                var baseLayerPath = imagePrefix + item.LayerID;
                var pngPath = Utils.FindFile(baseLayerPath + ".png");
                if (pngPath != null)
                {
                    item.Path = pngPath;
                }
                else
                {
                    var tlgPath = Utils.FindFile(baseLayerPath + ".tlg");
                    if (tlgPath != null)
                    {
                        item.Path = tlgPath;
                    }
                    else
                    {
                        if (File.Exists(baseLayerPath + ".png"))
                        {
                            item.Path = baseLayerPath + ".png";
                        }
                        else
                        {
                            item.Path = baseLayerPath + ".tlg";
                        }
                    }
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
                
                // 天音a 水着帽子 这个图层混合错误，暂时没定位到原因
                if (layer.LayerID == 8320 && layer.Name.Equals("帽子埋め")) continue;

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
                            BlendKrkrzPs(bitmap, layerBitmap, layer.Left, layer.Top, layer.Opacity, PsNormalCore, updateAlpha: true);
                            break;
                        case KrBlendMode.ltPsDarken:
                            BlendKrkrzPs(bitmap, layerBitmap, layer.Left, layer.Top, layer.Opacity, PsDarkenCore, updateAlpha: false);
                            break;
                        case KrBlendMode.ltPsMultiplicative:
                            BlendKrkrzPs(bitmap, layerBitmap, layer.Left, layer.Top, layer.Opacity, PsMultiplyCore, updateAlpha: false);
                            break;
                        case KrBlendMode.ltPsColorDodge:
                            BlendKrkrzPs(bitmap, layerBitmap, layer.Left, layer.Top, layer.Opacity, PsColorDodgeCore, updateAlpha: false);
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

        private static void BlendKrkrzPs(
            Bitmap baseBmp,
            Bitmap topBmp,
            int offsetX,
            int offsetY,
            int opacity,
            Func<int, int, int> blendCore,
            bool updateAlpha)
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

                            int ta = topPixel[3];
                            if (ta == 0) continue;

                            // krkrz translucent_op: if opacity is used, a = ((srcA * opacity) >> 8).
                            // When opacity is fully opaque, use normal_op's a = srcA to avoid 255 becoming 254.
                            int a = opacity >= 255 ? ta : ((ta * opacity) >> 8);
                            if (a == 0) continue;

                            int bb = basePixel[0];
                            int bg = basePixel[1];
                            int br = basePixel[2];
                            int ba = basePixel[3];

                            int tb = topPixel[0];
                            int tg = topPixel[1];
                            int tr = topPixel[2];

                            int db = blendCore(bb, tb);
                            int dg = blendCore(bg, tg);
                            int dr = blendCore(br, tr);

                            basePixel[0] = (byte)PsAlphaBlendChannel(bb, db, a);
                            basePixel[1] = (byte)PsAlphaBlendChannel(bg, dg, a);
                            basePixel[2] = (byte)PsAlphaBlendChannel(br, dr, a);

                            // krkrz Photoshop/HDA variants keep destination alpha.
                            // Only ltPsNormal is documented as ltAlpha-equivalent, so keep alpha output usable there.
                            if (updateAlpha)
                            {
                                basePixel[3] = (byte)AlphaBlendChannel(ba, ta, opacity);
                            }
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

        private static int PsNormalCore(int dest, int src)
        {
            return src;
        }

        private static int PsDarkenCore(int dest, int src)
        {
            return dest < src ? dest : src;
        }

        private static int PsMultiplyCore(int dest, int src)
        {
            // krkrz ps_mul_blend_func uses >> 8, not / 255.
            return (dest * src) >> 8;
        }

        private static int PsColorDodgeCore(int dest, int src)
        {
            // krkrz ps_color_dodge_table: ((255-src)<=dest) ? 255 : (dest*255)/(255-src)
            return (255 - src) <= dest ? 255 : (dest * 255) / (255 - src);
        }

        private static int PsAlphaBlendChannel(int dest, int src, int alpha)
        {
            // krkrz ps_alpha_blend_func: dest + (((src - dest) * alpha) >> 8)
            return dest + (((src - dest) * alpha) >> 8);
        }

        private static int AlphaBlendChannel(int destAlpha, int srcAlpha, int opacity)
        {
            int a = opacity >= 255 ? srcAlpha : ((srcAlpha * opacity) >> 8);
            return 255 - ((255 - destAlpha) * (255 - a)) / 255;
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