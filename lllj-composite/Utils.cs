using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace atri_composite
{
    public static class Utils
    {
        public static BitmapSource ToBitmapSource(this Bitmap bitmap, bool disposeSource = false)
        {
            IntPtr p = bitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    p,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(bitmap.Width, bitmap.Height));
            }
            finally
            {
                DeleteObject(p);
                if (disposeSource) bitmap.Dispose();
            }
        }

        public static Bitmap Crop(this Bitmap bitmap, bool disposeSource = false)
        {
            if (bitmap.PixelFormat != PixelFormat.Format32bppArgb) throw new NotSupportedException();

            int l = -1, t = -1, r = -1, b = -1;
            var bitmapData = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                var bpp = Image.GetPixelFormatSize(bitmapData.PixelFormat) / 8;
                unsafe byte AlphaAt(int x, int y) => *((byte*)bitmapData.Scan0 + bitmapData.Stride * y + bpp * x + 3);

                Parallel.Invoke(() =>
                {
                    for (var ln = 0; ln < bitmapData.Width; ln++)
                        for (var s = 0; s < bitmapData.Height; s++)
                            if (AlphaAt(ln, s) != 0) { l = ln; return; }
                }, () =>
                {
                    for (var ln = 0; ln < bitmapData.Height; ln++)
                        for (var s = 0; s < bitmapData.Width; s++)
                            if (AlphaAt(s, ln) != 0) { t = ln; return; }
                }, () =>
                {
                    for (var ln = 0; ln < bitmapData.Width; ln++)
                        for (var s = 0; s < bitmapData.Height; s++)
                            if (AlphaAt(bitmapData.Width - 1 - ln, s) != 0) { r = ln; return; }
                }, () =>
                {
                    for (var ln = 0; ln < bitmapData.Height; ln++)
                        for (var s = 0; s < bitmapData.Width; s++)
                            if (AlphaAt(s, bitmapData.Height - 1 - ln) != 0) { b = ln; return; }
                });
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            try
            {
                if (l < 0 || t < 0 || r < 0 || b < 0)
                    throw new ArgumentException("The image contains no visible pixels.", nameof(bitmap));

                var cropBound = new Rectangle(l, t, bitmap.Width - l - r, bitmap.Height - t - b);
                var newBitmap = new Bitmap(cropBound.Width, cropBound.Height, PixelFormat.Format32bppArgb);
                try
                {
                    using (var g = Graphics.FromImage(newBitmap))
                        g.DrawImage(bitmap, new Rectangle(Point.Empty, newBitmap.Size), cropBound, GraphicsUnit.Pixel);
                    return newBitmap;
                }
                catch
                {
                    newBitmap.Dispose();
                    throw;
                }
            }
            finally
            {
                if (disposeSource) bitmap.Dispose();
            }
        }

        private static readonly HashSet<string> targetExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".stand", ".sinfo", ".txt", ".pbd", ".png", ".tlg"
        };

        private static readonly HashSet<string> excludedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "_nude",
            "裸加工"
        };

        public static readonly List<EncodingInfo> AvailableEncodings = new List<EncodingInfo>
        {
            new EncodingInfo("Shift-JIS", Encoding.GetEncoding(932)),
            new EncodingInfo("UTF-16LE BOM", Encoding.Unicode),
            new EncodingInfo("UTF-16LE", new UnicodeEncoding(false, false)),
        };

        public static Encoding StandEncoding { get; set; } = Encoding.Unicode;
        public static Encoding SinfoEncoding { get; set; } = Encoding.Unicode;
        public static Encoding PbdEncoding { get; set; } = Encoding.Unicode;

        public static readonly List<EncodingPreset> AvailablePresets = new List<EncodingPreset>
        {
            new EncodingPreset(
                "国际中文版",
                Encoding.Unicode,                    // .stand
                Encoding.Unicode,                    // .sinfo / _info.txt
                Encoding.Unicode                     // .pbd fallback .txt
            ),
            new EncodingPreset(
                "日文原版",
                Encoding.GetEncoding(932),           // .stand: Shift-JIS
                Encoding.GetEncoding(932),           // .sinfo: Shift-JIS
                new UnicodeEncoding(false, false)    // .pbd txt: UTF-16 LE no BOM
            ),
        };

        public class EncodingInfo
        {
            public string DisplayName { get; }
            public Encoding Encoding { get; }
            public EncodingInfo(string displayName, Encoding encoding)
            {
                DisplayName = displayName;
                Encoding = encoding;
            }
            public override string ToString() => DisplayName;
        }

        public class EncodingPreset
        {
            public string Name { get; }
            public Encoding StandEncoding { get; }
            public Encoding SinfoEncoding { get; }
            public Encoding PbdEncoding { get; }
            public EncodingPreset(string name, Encoding stand, Encoding sinfo, Encoding pbd)
            {
                Name = name;
                StandEncoding = stand;
                SinfoEncoding = sinfo;
                PbdEncoding = pbd;
            }
            public override string ToString() => Name;
        }

        public static List<string> WorkingDirectories { get; private set; } = new List<string>();
        private static readonly Dictionary<string, List<string>> fileLookupCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public static long EstimateCacheMemoryUsage()
        {
            long size = 0;
            size += 48; // dictionary base overhead

            foreach (var kvp in fileLookupCache)
            {
                if (kvp.Key != null)
                {
                    size += 24 + (kvp.Key.Length * 2) + 8;
                }

                var list = kvp.Value;
                if (list != null)
                {
                    size += 40 + (list.Capacity * 8) + 8;
                    foreach (var path in list)
                    {
                        if (path != null)
                        {
                            size += 24 + (path.Length * 2) + 8;
                        }
                    }
                }
            }
            return size;
        }

        public static void InitializeFileCache(IEnumerable<string> rootDirectories, Action<string, int, int, int, long> onProgress = null)
        {
            fileLookupCache.Clear();
            pbdCache.Clear();
            WorkingDirectories = rootDirectories.ToList();

            var allDirs = new List<string>();
            foreach (var root in WorkingDirectories)
            {
                if (!Directory.Exists(root)) continue;
                allDirs.Add(root);
                try
                {
                    allDirs.AddRange(Directory.GetDirectories(root, "*", SearchOption.AllDirectories));
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Failed to get subdirectories for {root}: {ex.Message}");
                }
            }
            allDirs = allDirs.Select(Path.GetFullPath)
                .Where(dir =>
                {
                    var parts = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return !parts.Any(p => excludedFolderNames.Contains(p));
                })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            int totalDirs = allDirs.Count;
            int processedDirs = 0;
            int fileCount = 0;

            foreach (var dir in allDirs)
            {
                try
                {
                    var files = Directory.GetFiles(dir);
                    foreach (var file in files)
                    {
                        var ext = Path.GetExtension(file);
                        if (!targetExtensions.Contains(ext)) continue;

                        var fileName = Path.GetFileName(file);
                        if (!fileLookupCache.TryGetValue(fileName, out var list))
                        {
                            list = new List<string>();
                            fileLookupCache[fileName] = list;
                        }
                        if (!list.Contains(file))
                        {
                            list.Add(file);
                            fileCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Failed to scan directory {dir}: {ex.Message}");
                }

                processedDirs++;
                onProgress?.Invoke(dir, processedDirs, totalDirs, fileCount, EstimateCacheMemoryUsage());
            }
        }

        public static List<string> GetStandFiles()
        {
            var stands = new List<string>();
            foreach (var kvp in fileLookupCache)
            {
                if (kvp.Key.EndsWith(".stand", StringComparison.OrdinalIgnoreCase))
                {
                    stands.AddRange(kvp.Value);
                }
            }
            return stands;
        }

        public static string FindFile(string relativeOrAbsolutePath)
        {
            if (string.IsNullOrEmpty(relativeOrAbsolutePath)) return null;

            if (Path.IsPathRooted(relativeOrAbsolutePath) && File.Exists(relativeOrAbsolutePath))
            {
                return relativeOrAbsolutePath;
            }

            var normalizedReq = relativeOrAbsolutePath.Replace('\\', '/');
            var fileName = Path.GetFileName(normalizedReq);

            if (!fileLookupCache.TryGetValue(fileName, out var paths) || paths.Count == 0)
            {
                return null;
            }

            if (paths.Count == 1)
            {
                return paths[0];
            }

            string bestMatch = null;
            int bestMatchLength = -1;

            foreach (var path in paths)
            {
                var normalizedPath = path.Replace('\\', '/');
                int matchLen = GetSuffixMatchLength(normalizedPath, normalizedReq);
                if (matchLen > bestMatchLength)
                {
                    bestMatchLength = matchLen;
                    bestMatch = path;
                }
            }

            return bestMatch ?? paths[0];
        }

        private static int GetSuffixMatchLength(string path, string suffix)
        {
            var pathParts = path.Split('/');
            var suffixParts = suffix.Split('/');
            int matchCount = 0;
            for (int i = 1; i <= Math.Min(pathParts.Length, suffixParts.Length); i++)
            {
                if (string.Equals(pathParts[pathParts.Length - i], suffixParts[suffixParts.Length - i], StringComparison.OrdinalIgnoreCase))
                {
                    matchCount++;
                }
                else
                {
                    break;
                }
            }
            return matchCount;
        }

        private static readonly ConcurrentDictionary<string, Lazy<JArray>> pbdCache =
            new ConcurrentDictionary<string, Lazy<JArray>>();
        public static JArray LoadPBDFile(string pbdPath, bool normalize = false)
        {
            var resolved = FindFile(pbdPath);
            if (resolved != null)
            {
                pbdPath = resolved;
            }
            else
            {
                if (!File.Exists(pbdPath))
                {
                    var directory = Path.GetDirectoryName(pbdPath);
                    var parent = string.IsNullOrEmpty(directory) ? null : Directory.GetParent(directory);
                    if (parent != null)
                        pbdPath = Path.Combine(parent.FullName, Path.GetFileName(pbdPath));
                }
            }

            pbdPath = Path.GetFullPath(pbdPath);
            var sourcePath = pbdPath;
            var cacheKey = sourcePath + "\0" + normalize;
            return pbdCache.GetOrAdd(cacheKey, _ => new Lazy<JArray>(() =>
            {
                if (File.Exists(sourcePath))
                {
                    var converterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pbd2json.exe");
                    if (!File.Exists(converterPath))
                        throw new FileNotFoundException("Cannot find the PBD converter.", converterPath);

                    using (var proc = new Process())
                    {
                        proc.StartInfo = new ProcessStartInfo
                        {
                            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                            Arguments = $"/D /C chcp 65001 > nul && \"{converterPath}\" \"{sourcePath}\"",
                            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8
                        };
                        proc.Start();
                        var outputTask = proc.StandardOutput.ReadToEndAsync();
                        var errorTask = proc.StandardError.ReadToEndAsync();
                        proc.WaitForExit();
                        Task.WaitAll(outputTask, errorTask);

                        if (proc.ExitCode != 0)
                            throw new InvalidDataException($"PBD conversion failed with exit code {proc.ExitCode}: {errorTask.Result}");
                        if (string.IsNullOrWhiteSpace(outputTask.Result))
                            throw new InvalidDataException("PBD conversion returned no data.");

                        var json = normalize
                            ? outputTask.Result.Normalize(NormalizationForm.FormKC)
                            : outputTask.Result;
                        return JArray.Parse(json);
                    }
                }

                // Fallback: try .txt file (older game format)
                var txtPath = Path.ChangeExtension(sourcePath, ".txt");
                var resolvedTxt = FindFile(txtPath);
                if (resolvedTxt != null) txtPath = resolvedTxt;
                if (!File.Exists(txtPath))
                    throw new FileNotFoundException("Cannot find PBD or TXT file for: " + sourcePath);
                return LoadTxtFile(txtPath);
            }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }


        private static JArray LoadTxtFile(string txtPath)
        {
            var lines = File.ReadAllLines(txtPath, PbdEncoding);
            var jArr = new JArray();

            // Line 0 is the #header comment, line 1 has canvas dimensions
            int canvasWidth = 0, canvasHeight = 0;
            if (lines.Length > 1)
            {
                var dimCols = lines[1].Split('\t');
                if (dimCols.Length > 5)
                {
                    int.TryParse(dimCols[4], out canvasWidth);
                    int.TryParse(dimCols[5], out canvasHeight);
                }
            }
            if (canvasWidth <= 0 || canvasHeight <= 0)
                throw new InvalidDataException("Invalid or missing canvas dimensions in: " + txtPath);
            jArr.Add(new JObject
            {
                ["width"] = canvasWidth,
                ["height"] = canvasHeight
            });

            // Layer data starts from line 2
            for (int i = 2; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var cols = line.Split('\t');
                if (cols.Length < 10) continue;

                int.TryParse(cols[0], out int layerType);
                var name = cols[1];
                if (string.IsNullOrEmpty(name)) continue;

                int.TryParse(cols[2], out int left);
                int.TryParse(cols[3], out int top);
                int.TryParse(cols[4], out int width);
                int.TryParse(cols[5], out int height);
                int.TryParse(cols[6], out int blendType);
                int.TryParse(cols[7], out int opacity);
                int.TryParse(cols[8], out int visible);
                int.TryParse(cols[9], out int layerId);
                int groupLayerId = 0;
                if (cols.Length > 10) int.TryParse(cols[10], out groupLayerId);

                jArr.Add(new JObject
                {
                    ["name"] = name,
                    ["layer_type"] = layerType,
                    ["left"] = left,
                    ["top"] = top,
                    ["width"] = width,
                    ["height"] = height,
                    ["type"] = blendType,
                    ["opacity"] = opacity,
                    ["visible"] = visible,
                    ["layer_id"] = layerId,
                    ["group_layer_id"] = groupLayerId
                });
            }

            return jArr;
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
