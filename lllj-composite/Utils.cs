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
                if (disposeSource) try { bitmap.Dispose(); } catch { }
            }
        }

        public static Bitmap Crop(this Bitmap bitmap, bool disposeSource = false)
        {
            if (bitmap.PixelFormat != PixelFormat.Format32bppArgb) throw new NotSupportedException();

            var bitmapData = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            var bpp = Image.GetPixelFormatSize(bitmapData.PixelFormat) / 8;

            unsafe byte AlphaAt(int x, int y) => *((byte*)bitmapData.Scan0 + bitmapData.Stride * y + bpp * x + 3);

            int l = -1, t = -1, r = -1, b = -1;
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

            bitmap.UnlockBits(bitmapData);

            if (l < 0 || t < 0 || r < 0 || b < 0) throw new ArgumentException();

            var cropBound = new Rectangle(l, t, bitmap.Width - l - r, bitmap.Height - t - b);
            var newBitmap = new Bitmap(cropBound.Width, cropBound.Height);
            using (var g = Graphics.FromImage(newBitmap)) g.DrawImage(bitmap, new Rectangle(Point.Empty, newBitmap.Size), cropBound, GraphicsUnit.Pixel);

            if (disposeSource) try { bitmap.Dispose(); } catch { }
            return newBitmap;
        }

        private static readonly HashSet<string> targetExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".stand", ".sinfo", ".txt", ".pbd", ".png", ".tlg"
        };

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
                // riddle_steam_dumps/fgimage/_nude文件夹下的png会干扰读取同名tlg
                .Where(dir => !dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("_nude", StringComparer.OrdinalIgnoreCase))
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

        private static readonly ConcurrentDictionary<string, JArray> pbdCache = new ConcurrentDictionary<string, JArray>();
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
                    pbdPath = Path.Combine(Directory.GetParent(Path.GetDirectoryName(pbdPath)).FullName, Path.GetFileName(pbdPath));
                }
            }

            return pbdCache.GetOrAdd(pbdPath, o =>
              {
                  var proc = Process.Start(new ProcessStartInfo()
                  {
                      FileName = "cmd.exe",
                      Arguments = $"/C chcp 65001 > nul && pbd2json.exe \"{pbdPath}\"",
                      UseShellExecute = false,
                      RedirectStandardOutput = true,
                      CreateNoWindow = true,
                      StandardOutputEncoding = Encoding.UTF8
                  });
                  var json = proc.StandardOutput.ReadToEnd();
                  proc.WaitForExit();
                  return JArray.Parse(normalize ? json.Normalize(NormalizationForm.FormKC) : json);
              });
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
