using System.Collections.Generic;

namespace atri_composite
{
    public class Character
    {
        public string Name { get; set; }

        public List<Pose> Poses { get; } = new List<Pose>();

        public override string ToString() => Name;

        public class Pose
        {
            public string Name { get; set; }
            
            public List<Dress> Dresses { get; } = new List<Dress>();

            public List<Face> Faces { get; } = new List<Face>();
            
            public override string ToString() => Name;

            public class Dress
            {
                public string Name { get; set; }

                public string LayerPath { get; set; } = "dummy";

                public List<Addition> Additions { get; } = new List<Addition>();

                public override string ToString() => Name;

                public class Addition
                {
                    public string Name { get; set; }
                    
                    public List<string> LayerPaths { get; } = new List<string>();

                    public override string ToString() => Name;
                }
            }

            public class Face
            {
                public string Name { get; set; }
                
                public string LayerPath { get; set; }

                public override string ToString() => Name;
            }
        }
    }
}
