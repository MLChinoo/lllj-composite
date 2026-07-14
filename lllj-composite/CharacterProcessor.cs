using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace atri_composite
{
    public class CharacterProcessor
    {
        public static List<Character> Load(string fgimageDir = null)
        {
            var characters = new List<Character>();
            var standFiles = Utils.GetStandFiles();
            foreach (var file in standFiles)
            {
                var character = new Character() { Name = Path.GetFileNameWithoutExtension(file) };
                MatchCollection rtxt;
                try
                {
                    rtxt = Regex.Matches(File.ReadAllText(file, Utils.StandEncoding), "filename:'([^']+)'");
                }
                catch (System.Exception ex)
                {
                    Trace.TraceError($"Failed to read stand file {file}: {ex.Message}");
                    continue;
                }

                foreach (Match match in rtxt)
                {
                    var m = match.Groups[1].Value;
                    var name = m;

                    try
                    {
                        var pose = new Character.Pose();
                        var infoPath = Utils.FindFile(name + "_info.txt") ?? Utils.FindFile(name + ".sinfo");
                        if (infoPath != null)
                        {
                            pose = ProcessStandInfo(infoPath);
                        }
                        else
                        {
                            var fallbackDir = fgimageDir ?? (Utils.WorkingDirectories.FirstOrDefault() ?? "");
                            var fallbackTxt = Path.Combine(fallbackDir, name + "_info.txt");
                            var fallbackSinfo = Path.Combine(fallbackDir, name + ".sinfo");

                            if (File.Exists(fallbackTxt))
                            {
                                pose = ProcessStandInfo(fallbackTxt);
                            }
                            else if (File.Exists(fallbackSinfo))
                            {
                                pose = ProcessStandInfo(fallbackSinfo);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        pose.Name = name;
                        character.Poses.Add(pose);
                    }
                    catch (System.Exception ex)
                    {
                        Trace.TraceError($"Failed to load pose {name} referenced by {file}: {ex.Message}");
                    }
                }
                if (character.Poses.Count > 0) characters.Add(character);
            }
            return characters;
        }

        private static Character.Pose ProcessStandInfo(string sInfoPath)
        {
            var sInfo = File.ReadAllText(sInfoPath, Utils.SinfoEncoding);
            var pose = new Character.Pose();

            foreach (var expression in sInfo.Split('\n').Select(o => o.Trim()))
            {
                if (string.IsNullOrEmpty(expression)) continue;
                var blocks = expression.Split('\t').Select(p => p.Trim()).ToList();

                var paramIndex = 0;
                switch (blocks[paramIndex++])
                {
                    case "dress":
                        if (blocks.Count < 5)
                        {
                            Trace.TraceWarning($"Ignored malformed dress row in {sInfoPath}: {expression}");
                            continue;
                        }
                        var dressName = blocks[paramIndex++];
                        if (!pose.Dresses.Exists(o => o.Name == dressName))
                            pose.Dresses.Add(new Character.Pose.Dress() { Name = dressName });
                        var dress = pose.Dresses.First(o => o.Name == dressName);
                        string additionType = blocks[paramIndex++];
                        string additionName = blocks[paramIndex++];
                        string dressLayerPath = blocks[paramIndex++];
                        if (!dress.Additions.Exists(o => o.Name == additionName))
                            dress.Additions.Add(new Character.Pose.Dress.Addition() { Name = additionName });
                        var addition = dress.Additions.First(o => o.Name == additionName);
                        addition.LayerPaths.Add(dressLayerPath);
                        break;
                    case "face":
                        if (blocks.Count < 4)
                        {
                            Trace.TraceWarning($"Ignored malformed face row in {sInfoPath}: {expression}");
                            continue;
                        }
                        string faceName = blocks[paramIndex++];
                        string faceType = blocks[paramIndex++];
                        string faceLayerPath = blocks[paramIndex++];
                        if (!pose.Faces.Exists(o => o.Name == faceName))
                            pose.Faces.Add(new Character.Pose.Face() {Name = faceName});
                        var face = pose.Faces.First(o => o.Name == faceName);
                        face.LayerPaths.Add(faceLayerPath);
                        break;
                }
            }
            return pose;
        }

        public static IEnumerable<Character.Pose.Face> GetFacesForDress(
            IEnumerable<Character.Pose.Face> faces,
            Character.Pose.Dress dress)
        {
            if (faces == null) return Enumerable.Empty<Character.Pose.Face>();

            return faces.Where(face =>
            {
                if (string.IsNullOrEmpty(face.Name)) return false;
                if (!face.Name.Contains("@")) return true;
                var parts = face.Name.Split('@');
                return parts.Length > 1 && parts[1] == dress?.Name;
            });
        }
    }
}
