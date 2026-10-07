using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// The composition as data: where things stand. The viewer is at the origin looking along +z (x to the right, metres).
    /// Reading order from the camera: open meadow, a few grass tufts and stones, the sand path, one character tree group on the left,
    /// loose forest clumps with a clear gap where the path goes, simplified forest, soft hills, hazy distance, sky.
    /// Random numbers only vary details (size, rotation, tint); the design is here.
    /// </summary>
    public static class Plan
    {
        public static readonly Vector2 GroupCenter = new Vector2(-9f, 35f);

        public struct Hero { public Vector2 Position; public float Height, Spread; public TreeKind Kind; }

        /// <summary>
        /// The character group: five unequal trees whose crowns overlap a little, with visible trunks and gaps between them. The slim white birch on its path side
        /// (bright trunk in front of the dark spruce, fresh yellow-green crown) is the colour accent of the picture.
        /// </summary>
        public static readonly Hero[] HeroGroup =
        {
            new Hero { Position = new Vector2(-9.5f, 34.5f), Height = 11.0f, Spread = 5.2f, Kind = TreeKind.Oak },
            new Hero { Position = new Vector2(-15.2f, 30.0f), Height = 8.6f, Spread = 4.2f, Kind = TreeKind.Oak },
            new Hero { Position = new Vector2(-5.4f, 39.5f), Height = 10.2f, Spread = 2.5f, Kind = TreeKind.Birch },
            new Hero { Position = new Vector2(-4.6f, 29.5f), Height = 5.6f, Spread = 2.7f, Kind = TreeKind.Oak },
            new Hero { Position = new Vector2(-12.4f, 41.5f), Height = 11.6f, Spread = 2.7f, Kind = TreeKind.Spruce },
        };

        public struct Bush { public Vector2 Position; public float Size; }

        public static readonly Bush[] GroupBushes =
        {
            new Bush { Position = new Vector2(-7.6f, 32.0f), Size = 1.4f }, new Bush { Position = new Vector2(-12.6f, 36.0f), Size = 1.2f },
            new Bush { Position = new Vector2(-2.6f, 36.5f), Size = 1.1f }, new Bush { Position = new Vector2(-8.4f, 38.4f), Size = 1.0f },
        };

        /// <summary>
        /// Low bush groups at the sides of the view (about 30 m out), like the shrubs that frame the concept image on both sides. Bushes do not block the view to the horizon
        /// (no trunk), so they add framing without closing the sight corridors.
        /// </summary>
        public static readonly Bush[] SideBushes =
        {
            new Bush { Position = new Vector2(23.5f, 19.0f), Size = 1.7f }, new Bush { Position = new Vector2(27.0f, 23.5f), Size = 2.1f }, new Bush { Position = new Vector2(21.0f, 24.5f), Size = 1.3f },
            new Bush { Position = new Vector2(33.0f, 38.0f), Size = 1.9f }, new Bush { Position = new Vector2(29.5f, 41.0f), Size = 1.4f },
            new Bush { Position = new Vector2(-25.0f, 22.0f), Size = 1.9f }, new Bush { Position = new Vector2(-28.5f, 26.0f), Size = 2.2f }, new Bush { Position = new Vector2(-22.0f, 27.0f), Size = 1.3f },
        };

        public struct Cluster
        {
            public Vector2 Center;
            public float RadiusX, RadiusZ, MinHeight, MaxHeight;
            public int Count;
        }

        /// <summary>Forest clumps with full-detail trees (Edge tier). Between them stays an open corridor around the path.</summary>
        public static readonly Cluster[] EdgeClusters =
        {
            new Cluster { Center = new Vector2(-34f, 58f), RadiusX = 9f, RadiusZ = 8f, MinHeight = 8f, MaxHeight = 11f, Count = 6 },
            new Cluster { Center = new Vector2(-56f, 80f), RadiusX = 10f, RadiusZ = 9f, MinHeight = 9f, MaxHeight = 12.5f, Count = 6 },
            new Cluster { Center = new Vector2(28f, 56f), RadiusX = 8f, RadiusZ = 7f, MinHeight = 8f, MaxHeight = 11f, Count = 5 },
            new Cluster { Center = new Vector2(46f, 76f), RadiusX = 10f, RadiusZ = 8f, MinHeight = 9f, MaxHeight = 12f, Count = 6 },
            new Cluster { Center = new Vector2(18f, 24f), RadiusX = 3f, RadiusZ = 2.5f, MinHeight = 4.2f, MaxHeight = 5.8f, Count = 3 },
        };

        /// <summary>Simplified groups (Mid tier) further back; the same open corridor.</summary>
        public static readonly Cluster[] MidClusters =
        {
            new Cluster { Center = new Vector2(-48f, 108f), RadiusX = 14f, RadiusZ = 10f, MinHeight = 9f, MaxHeight = 13f, Count = 9 },
            new Cluster { Center = new Vector2(-78f, 122f), RadiusX = 14f, RadiusZ = 10f, MinHeight = 10f, MaxHeight = 14f, Count = 8 },
            new Cluster { Center = new Vector2(34f, 104f), RadiusX = 12f, RadiusZ = 9f, MinHeight = 9f, MaxHeight = 12f, Count = 8 },
            new Cluster { Center = new Vector2(62f, 120f), RadiusX = 14f, RadiusZ = 10f, MinHeight = 10f, MaxHeight = 14f, Count = 9 },
            new Cluster { Center = new Vector2(-22f, 130f), RadiusX = 8f, RadiusZ = 6f, MinHeight = 8f, MaxHeight = 11f, Count = 5 },
            new Cluster { Center = new Vector2(24f, 140f), RadiusX = 8f, RadiusZ = 6f, MinHeight = 8f, MaxHeight = 11f, Count = 5 },
            new Cluster { Center = new Vector2(-70f, -55f), RadiusX = 14f, RadiusZ = 10f, MinHeight = 9f, MaxHeight = 13f, Count = 7 },
            new Cluster { Center = new Vector2(60f, -70f), RadiusX = 14f, RadiusZ = 10f, MinHeight = 9f, MaxHeight = 13f, Count = 7 },
        };

        public struct Grass { public Vector2 Center; public float Radius; public int Clumps; }

        /// <summary>
        /// Deliberate tuft groups of spiky blades. Nothing within 9 m of the viewer, nothing on or at the path. Beyond them the meadow gets its grass from small
        /// flecks in patches (<see cref="LandscapeBuilder"/>), from tufts along the path edge and at the foot of stones and trees.
        /// </summary>
        public static readonly Grass[] GrassGroups =
        {
            new Grass { Center = new Vector2(-9.6f, 7.2f), Radius = 1.2f, Clumps = 5 },
            new Grass { Center = new Vector2(8.6f, 12.5f), Radius = 1.2f, Clumps = 5 },
            new Grass { Center = new Vector2(-3.6f, 24.0f), Radius = 1.2f, Clumps = 5 },
            new Grass { Center = new Vector2(13.0f, 30.0f), Radius = 1.6f, Clumps = 7 },
            new Grass { Center = new Vector2(-19.5f, 27.0f), Radius = 1.4f, Clumps = 6 },
            new Grass { Center = new Vector2(-8.4f, 28.0f), Radius = 1.1f, Clumps = 5 },
            new Grass { Center = new Vector2(8.2f, 46.0f), Radius = 1.3f, Clumps = 6 },
            new Grass { Center = new Vector2(-18.0f, 50.0f), Radius = 1.4f, Clumps = 6 },
            new Grass { Center = new Vector2(14.0f, 60.0f), Radius = 1.2f, Clumps = 5 },
            new Grass { Center = new Vector2(21.0f, 20.0f), Radius = 1.5f, Clumps = 6 },
            new Grass { Center = new Vector2(-24.0f, 16.0f), Radius = 1.4f, Clumps = 5 },
            new Grass { Center = new Vector2(-1.0f, 41.0f), Radius = 1.2f, Clumps = 5 },
            new Grass { Center = new Vector2(24.0f, 44.0f), Radius = 1.6f, Clumps = 6 },
            new Grass { Center = new Vector2(-27.0f, 40.0f), Radius = 1.6f, Clumps = 6 },
            new Grass { Center = new Vector2(-6.0f, 58.0f), Radius = 1.2f, Clumps = 5 },
            new Grass { Center = new Vector2(30.0f, 28.0f), Radius = 1.5f, Clumps = 5 },
        };

        /// <summary>Accent = a second petal colour for about a quarter of the flowers (Mat.GroundBase means none).</summary>
        public struct Island { public Vector2 Center; public float Radius; public int Flowers; public Mat Petal; public Mat Accent; }

        /// <summary>Seven flower islands (a green mound with flowers growing out of it, one main colour and sometimes a second), spread over the visible meadow; none close to the viewer.</summary>
        public static readonly Island[] FlowerIslands =
        {
            new Island { Center = new Vector2(-6.4f, 12.6f), Radius = 1.7f, Flowers = 26, Petal = Mat.FlowerLavender, Accent = Mat.FlowerWhite },
            new Island { Center = new Vector2(12.0f, 17.5f), Radius = 1.5f, Flowers = 24, Petal = Mat.FlowerWhite, Accent = Mat.FlowerYellow },
            new Island { Center = new Vector2(-17.0f, 19.0f), Radius = 1.8f, Flowers = 26, Petal = Mat.FlowerYellow, Accent = Mat.FlowerWhite },
            new Island { Center = new Vector2(13.0f, 37.0f), Radius = 1.7f, Flowers = 24, Petal = Mat.FlowerLavender, Accent = Mat.FlowerWhite },
            new Island { Center = new Vector2(-1.6f, 28.0f), Radius = 1.3f, Flowers = 18, Petal = Mat.FlowerWhite },
            new Island { Center = new Vector2(8.0f, 52.0f), Radius = 1.6f, Flowers = 22, Petal = Mat.FlowerYellow, Accent = Mat.FlowerLavender },
            new Island { Center = new Vector2(-9.5f, 52.0f), Radius = 1.8f, Flowers = 24, Petal = Mat.FlowerLavender, Accent = Mat.FlowerYellow },
        };

        public struct Rock { public Vector2 Position; public float Size; public bool Warm; }

        /// <summary>A few stones of different size, not in a row: one large in the left foreground, the rest scattered towards the tree group.</summary>
        public static readonly Rock[] Rocks =
        {
            new Rock { Position = new Vector2(-6.2f, 8.4f), Size = 1.0f },
            new Rock { Position = new Vector2(7.6f, 9.6f), Size = 0.72f, Warm = true },
            new Rock { Position = new Vector2(-12.0f, 21.0f), Size = 0.42f },
            new Rock { Position = new Vector2(-10.8f, 22.0f), Size = 0.3f, Warm = true },
            new Rock { Position = new Vector2(12.0f, 27.0f), Size = 0.75f },
            new Rock { Position = new Vector2(-2.2f, 32.0f), Size = 0.5f, Warm = true },
        };

        /// <summary>Shades: the cloud blocks the sun for the lighting bake, so its shadow lies on the meadow (a soft dark patch). See <see cref="CloudAbove"/>.</summary>
        public struct Cloud { public float Azimuth, Distance, Altitude, Scale; public bool Shades; }

        /// <summary>
        /// Where a cloud of the given altitude has to hang so that its shadow falls on <paramref name="shadowCentre"/> (x, z on the ground): on the sun's ray through that point.
        /// Azimuth in degrees from +z towards +x, horizontal distance in metres. The cloud therefore appears next to the sun, as it does in nature.
        /// </summary>
        public static Cloud CloudAbove(Vector2 shadowCentre, float altitude, float scale)
        {
            float pitch = Palette.SunPitch * Mathf.PI / 180f, yaw = Palette.SunYaw * Mathf.PI / 180f;
            float shift = altitude / Mathf.Tan(pitch);                       // how far the shadow lies from the cloud, along the light's horizontal direction
            Vector2 cloud = shadowCentre - new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) * shift;
            return new Cloud
            {
                Azimuth = Mathf.Atan2(cloud.x, cloud.y) * 180f / Mathf.PI, Distance = Mathf.Sqrt(cloud.x * cloud.x + cloud.y * cloud.y),
                Altitude = altitude, Scale = scale, Shades = true
            };
        }

        /// <summary>Azimuth in degrees from +z towards +x.</summary>
        public static readonly Cloud[] Clouds =
        {
            CloudAbove(new Vector2(-6.5f, 18.5f), 80f, .9f),
            new Cloud { Azimuth = -35f, Distance = 190f, Altitude = 75f, Scale = 1.0f },
            new Cloud { Azimuth = 15f, Distance = 230f, Altitude = 98f, Scale = 1.25f },
            new Cloud { Azimuth = 58f, Distance = 200f, Altitude = 70f, Scale = 0.9f },
            new Cloud { Azimuth = -80f, Distance = 210f, Altitude = 92f, Scale = 1.1f },
            new Cloud { Azimuth = 140f, Distance = 200f, Altitude = 80f, Scale = 1.0f },
            new Cloud { Azimuth = 34f, Distance = 255f, Altitude = 62f, Scale = .6f },
            new Cloud { Azimuth = -8f, Distance = 275f, Altitude = 125f, Scale = 1.2f },
        };

        /// <summary>The path leads to this azimuth (degrees): forest line and hills dip here so the view opens along the path.</summary>
        public const float OpeningAzimuth = 4f;
    }
}
