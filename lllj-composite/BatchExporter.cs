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
            Directory.CreateDirectory(TargetDirectory);
            var errors = EnumerateVariants(limit).AsParallel()
                .WithDegreeOfParallelism(Math.Max(1, Environment.ProcessorCount))
                .Select(_ =>
            {
                var (character, pose, dress, face, addition) = _;
                var context = $"{character}_{pose}_{dress}_{face}_{addition}";
                try
                {
                    var pbdPath = Utils.FindFile(Path.Combine(character.Name, $"{pose.Name}.pbd"))
                                  ?? Utils.FindFile($"{pose.Name}.pbd");

                    if (pbdPath == null)
                    {
                        // also allow images to be placed in the data root
                        pbdPath = Path.Combine(WorkingDirectory, character.Name, $"{pose.Name}.pbd");
                        if (!File.Exists(pbdPath))
                        {
                            var directory = Path.GetDirectoryName(pbdPath);
                            var parent = string.IsNullOrEmpty(directory) ? null : Directory.GetParent(directory);
                            if (parent != null)
                                pbdPath = Path.Combine(parent.FullName, Path.GetFileName(pbdPath));
                        }
                    }

                    if (addition == null || addition.LayerPaths.Count == 0)
                        throw new InvalidDataException("The selected addition contains no layers.");

                    var image = new CompoundImage(pbdPath);
                    var layers = new List<string> { dress.LayerPath, addition.LayerPaths[0] };
                    layers.AddRange(face.LayerPaths);
                    layers.AddRange(addition.LayerPaths.Skip(1));

                    using (var generated = image.Generate(layers.ToArray()))
                    using (var cropped = generated.Crop())
                    {
                        BitmapSource result = cropped.ToBitmapSource();
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(result));
                        using (var file = File.Create(Path.Combine(TargetDirectory, context + ".png")))
                            encoder.Save(file);
                    }
                    return null;
                }
                catch (Exception e)
                {
                    return $"{context}: {e.GetType().Name}: {e.Message}";
                }
            }).Where(o => o != null).ToList();

            var failedLogPath = Path.Combine(TargetDirectory, "failed.log");
            if (errors.Count > 0)
            {
                using (var file = File.CreateText(failedLogPath))
                    errors.ForEach(o => file.WriteLine(o));
            }
            else if (File.Exists(failedLogPath))
            {
                File.Delete(failedLogPath);
            }

            return errors.Count;
        }

        public IEnumerable<(Character, Character.Pose, Character.Pose.Dress, Character.Pose.Face, Character.Pose.Dress.Addition)> EnumerateVariants(Limitation limit) =>
            (limit.Character != null ? new List<Character>() { limit.Character } : Characters).SelectMany(character =>
            (limit.Pose != null ? new List<Character.Pose>() { limit.Pose } : character.Poses).SelectMany(pose =>
            {
                var dresses = limit.Dress != null || limit.Addition != null ? new List<Character.Pose.Dress>() { limit.Dress } : pose.Dresses;
                return dresses.SelectMany(dress =>
                    CharacterProcessor.GetFacesForDress(pose.Faces, dress).SelectMany(preset =>
                    (limit.Addition != null ? new List<Character.Pose.Dress.Addition>() { limit.Addition } : dress.Additions).Select(addition =>
                        (character, pose, dress, preset, addition)
                    )));
            }));
    }
}
