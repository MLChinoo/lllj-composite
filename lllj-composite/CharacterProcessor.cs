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
        public static List<Character> Load(string fgimageDir)
        {
            var characters = new List<Character>();
            var standFiles = Directory.GetFiles(fgimageDir, "*.stand");
            foreach (var file in standFiles)
            {
                var character = new Character() { Name = Path.GetFileNameWithoutExtension(file) };
                var rtxt = Regex.Matches(File.ReadAllText(file, Encoding.Unicode), "filename:'([^']+)'");
                foreach (Match match in rtxt)
                {
                    var m = match.Groups[1].Value;
                    var name = m;

                    var pose = ProcessStandInfo(Path.Combine(fgimageDir, name + ".sinfo"));
                    pose.Name = name;
                    character.Poses.Add(pose);
                }
                characters.Add(character);
            }
            return characters;
        }

        private static Character.Pose ProcessStandInfo(string sInfoPath)
        {
            var sInfo = File.ReadAllText(sInfoPath);
            var pose = new Character.Pose();

            sInfo.Split('\n').Select(o => o.Trim()).ToList().ForEach(expression =>
            {
                var blocks = expression.Split('\t').Select(p => p.Trim()).ToList();

                var paramIndex = 0;
                switch (blocks[paramIndex++])
                {
                    case "dress":
                        var dressName = blocks[paramIndex++];
                        if (!pose.Dresses.Exists(o => o.Name == dressName)) pose.Dresses.Add(new Character.Pose.Dress() { Name = dressName });
                        var dress = pose.Dresses.First(o => o.Name == dressName);
                        string additionType = blocks[paramIndex++];
                        string additionName = blocks[paramIndex++];
                        string dressLayerPath = blocks[paramIndex++];
                        if (!dress.Additions.Exists(o => o.Name == additionName)) dress.Additions.Add(new Character.Pose.Dress.Addition() { Name = additionName });
                        var addition = dress.Additions.First(o => o.Name == additionName);
                        addition.LayerPaths.Add(dressLayerPath);
                        break;
                    case "face":
                        string faceName = blocks[paramIndex++];
                        string faceType = blocks[paramIndex++];
                        string faceLayerPath = blocks[paramIndex++];
                        if (!pose.Faces.Exists(o => o.Name == faceName)) pose.Faces.Add(new Character.Pose.Face() {Name = faceName});
                        var face = pose.Faces.First(o => o.Name == faceName);
                        face.LayerPaths.Add(faceLayerPath);
                        break;
                }
            });
            return pose;
        }
    }
}
