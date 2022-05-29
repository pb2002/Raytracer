using System;
using OpenTK;

namespace Template
{
    static internal class Utils
    {
        public static Random Rng = new Random();

        public static Vector3 RandomColor()
        {
            Vector3 color = new Vector3((float) Utils.Rng.NextDouble(), (float) Utils.Rng.NextDouble(),
                (float) Utils.Rng.NextDouble());

            // this code makes the random colors look a bit nicer
            color -= Vector3.One * Math.Min(color.X, Math.Min(color.Y, color.Z));
            color /= Math.Max(color.X, Math.Max(color.Y, color.Z));
            return color;
        }
    }
}