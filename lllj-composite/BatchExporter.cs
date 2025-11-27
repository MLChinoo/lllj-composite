using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace atri_composite
{
    internal class BatchExporter
    {
        internal struct Limitation
        {
            public Character Character;
            public Character.Pose Pose;
            public Character.Pose.Dress Dress;
            public Character.Pose.Dress.Addition Addition;
        }

        List<Character> Characters { get; }

        string WorkingDirectory { get; }

        string TargetDirectory { get; }

        public BatchExporter(List<Character> characters, string workingDirectory, string targetDirectory)
        {
            Characters = characters;
            WorkingDirectory = workingDirectory;
            TargetDirectory = targetDirectory;
        }

        public int Run(Limitation limit)
        {
            var errors = EnumerateVariants(limit).AsParallel().WithDegreeOfParallelism(Environment.ProcessorCount * 4).Select(_ =>
            {
                var (character, pose, dress, face, addition) = _;
                var pbdPath = Path.Combine(WorkingDirectory, character.Name, $"{pose.Name}.pbd");

                // also allow images to be placed in the data root
                if (!File.Exists(pbdPath))
                {
                    pbdPath = Path.Combine(Directory.GetParent(Path.GetDirectoryName(pbdPath)).FullName, Path.GetFileName(pbdPath));
                }

                var image = new CompoundImage(pbdPath);
                var layers = new List<string>();
                layers.Add(dress.LayerPath);
                layers.Add(addition.LayerPaths[0]);
                layers.AddRange(face.LayerPaths);
                layers.AddRange(addition.LayerPaths.GetRange(1, addition.LayerPaths.Count - 1));

                BitmapSource result;
                try
                {
                    result = image.Generate(layers.ToArray()).Crop(true).ToBitmapSource(true);
                }
                catch (Exception e)
                {
                    return $"{character}_{pose}_{dress}_{face}_{addition}: {e.Message}";
                }

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(result));
                using (var file = File.Create(Path.Combine(TargetDirectory, $"{character}_{pose}_{dress}_{face}_{addition}.png")))
                    encoder.Save(file);
                return null;
            }).Where(o => o != null).ToList();

            if (errors.Count > 0)
            {
                using (var file = File.CreateText(Path.Combine(TargetDirectory, $"failed.log")))
                    errors.ForEach(o => file.WriteLine(o));
            }

            return errors.Count;
        }

        public IEnumerable<(Character, Character.Pose, Character.Pose.Dress, Character.Pose.Face, Character.Pose.Dress.Addition)> EnumerateVariants(Limitation limit) =>
            (limit.Character != null ? new List<Character>() { limit.Character } : Characters).SelectMany(character =>
            (limit.Pose != null ? new List<Character.Pose>() { limit.Pose } : character.Poses).SelectMany(pose =>
            {
                var dresses = limit.Dress != null || limit.Addition != null ? new List<Character.Pose.Dress>() { limit.Dress } : pose.Dresses;
                var faces = pose.Faces;
                return dresses.SelectMany(dress =>
                    faces.SelectMany(preset =>
                    (limit.Addition != null ? new List<Character.Pose.Dress.Addition>() { limit.Addition } : dress.Additions).Select(addition =>
                        (character, pose, dress, preset, addition)
                    )));
            }));
    }
}
