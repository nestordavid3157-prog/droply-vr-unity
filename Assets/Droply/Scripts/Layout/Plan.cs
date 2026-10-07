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

        /// <summary>The character group: five unequal trees whose crowns overlap a little, with visible trunks and gaps between them.</summary>
        public static readonly Hero[] HeroGroup =
        {
            new Hero { Position = new Vector2(-9.5f, 34.5f), Height = 11.0f, Spread = 5.2f, Kind = TreeKind.Oak },
            new Hero { Position = new Vector2(-15.2f, 30.0f), Height = 8.6f, Spread = 4.2f, Kind = TreeKind.Oak },
            new Hero { Position = new Vector2(-5.4f, 39.5f), Height = 9.8f, Spread = 3.3f, Kind = TreeKind.Beech },
            new Hero { Position = new Vector2(-4.6f, 29.5f), Height = 5.6f, Spread = 2.7f, Kind = TreeKind.Oak },
            new Hero { Position = new Vector2(-12.4f, 41.5f), Height = 11.6f, Spread = 2.7f, Kind = TreeKind.Spruce },
        };

        public struct Bush { public Vector2 Position; public float Size; }

        public static readonly Bush[] GroupBushes =
        {
            new Bush { Position = new Vector2(-7.6f, 32.0f), Size = 1.4f }, new Bush { Position = new Vector2(-12.6f, 36.0f), Size = 1.2f },
            new Bush { Position = new Vector2(-2.6f, 36.5f), Size = 1.1f }, new Bush { Position = new Vector2(-8.4f, 38.4f), Size = 1.0f },
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

        /// <summary>Small deliberate tuft groups. Nothing within 9 m of the viewer, nothing on or at the path.</summary>
        public static readonly Grass[] GrassGroups =
        {
            new Grass { Center = new Vector2(-8.6f, 11.5f), Radius = 1.1f, Clumps = 6 },
            new Grass { Center = new Vector2(8.4f, 16.5f), Radius = 1.0f, Clumps = 5 },
            new Grass { Center = new Vector2(-3.6f, 24.0f), Radius = 1.0f, Clumps = 5 },
            new Grass { Center = new Vector2(13.0f, 30.0f), Radius = 1.4f, Clumps = 6 },
            new Grass { Center = new Vector2(-19.5f, 27.0f), Radius = 1.2f, Clumps = 5 },
            new Grass { Center = new Vector2(-8.4f, 28.0f), Radius = 0.9f, Clumps = 4 },
            new Grass { Center = new Vector2(8.2f, 46.0f), Radius = 1.1f, Clumps = 5 },
            new Grass { Center = new Vector2(-18.0f, 50.0f), Radius = 1.2f, Clumps = 5 },
            new Grass { Center = new Vector2(14.0f, 60.0f), Radius = 1.0f, Clumps = 5 },
        };

        public struct Island { public Vector2 Center; public float Radius; public int Flowers; public Mat Petal; }

        /// <summary>Seven small flower islands, one colour each, spread over the visible meadow; none close to the viewer.</summary>
        public static readonly Island[] FlowerIslands =
        {
            new Island { Center = new Vector2(-7.0f, 15.5f), Radius = 1.5f, Flowers = 14, Petal = Mat.FlowerLavender },
            new Island { Center = new Vector2(12.0f, 17.5f), Radius = 1.3f, Flowers = 12, Petal = Mat.FlowerWhite },
            new Island { Center = new Vector2(-17.0f, 19.0f), Radius = 1.6f, Flowers = 14, Petal = Mat.FlowerYellow },
            new Island { Center = new Vector2(13.0f, 37.0f), Radius = 1.5f, Flowers = 13, Petal = Mat.FlowerLavender },
            new Island { Center = new Vector2(-1.6f, 28.0f), Radius = 1.1f, Flowers = 10, Petal = Mat.FlowerWhite },
            new Island { Center = new Vector2(8.0f, 52.0f), Radius = 1.4f, Flowers = 11, Petal = Mat.FlowerYellow },
            new Island { Center = new Vector2(-9.5f, 52.0f), Radius = 1.6f, Flowers = 13, Petal = Mat.FlowerLavender },
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

        public struct Cloud { public float Azimuth, Distance, Altitude, Scale; }

        /// <summary>Azimuth in degrees from +z towards +x.</summary>
        public static readonly Cloud[] Clouds =
        {
            new Cloud { Azimuth = -35f, Distance = 190f, Altitude = 75f, Scale = 1.0f },
            new Cloud { Azimuth = 15f, Distance = 230f, Altitude = 98f, Scale = 1.25f },
            new Cloud { Azimuth = 58f, Distance = 200f, Altitude = 70f, Scale = 0.9f },
            new Cloud { Azimuth = -80f, Distance = 210f, Altitude = 92f, Scale = 1.1f },
            new Cloud { Azimuth = 140f, Distance = 200f, Altitude = 80f, Scale = 1.0f },
        };

        /// <summary>The path leads to this azimuth (degrees): forest line and hills dip here so the view opens along the path.</summary>
        public const float OpeningAzimuth = 4f;
    }
}
