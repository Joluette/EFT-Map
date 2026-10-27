// SPDX-License-Identifier: GPL-3.0-only
// Filename format and yaw convention verified against the-hideout/TarkovMonitor.
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace EftPosition
{
    public sealed class Position
    {
        public readonly string FileName;
        public readonly DateTime NameTime;
        public readonly double X, Y, Z, Yaw;
        public readonly bool HasHeading;
        public readonly long Counter;
        public Position(string name, DateTime time, double x, double y, double z, double yaw, bool heading, long counter)
        { FileName = name; NameTime = time; X = x; Y = y; Z = z; Yaw = yaw; HasHeading = heading; Counter = counter; }
    }

    public static class ScreenshotParser
    {
        const string P = @"(-?[0-9]+\.[0-9]{2})";
        const string Q = @"(-?[0-9]\.[0-9]{1,5})";
        // Anchors matter: a valid fragment inside a junk filename is not a position.
        static readonly Regex Pattern = new Regex(
            @"\A([0-9]{4}-[0-9]{2}-[0-9]{2}\[[0-9]{2}-[0-9]{2}\])_?" +
            P + ", " + P + ", " + P + "_?" + Q + ", " + Q + ", " + Q + ", " + Q +
            // Some game screenshots add a numeric field after the quaternion.
            // It is not a coordinate, rotation component or screenshot counter.
            @"(?:_-?[0-9]+(?:\.[0-9]{1,5})?)? \(([0-9]+)\)\.png\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(100));

        public static bool TryParse(string name, out Position result)
        {
            result = null;
            if (String.IsNullOrEmpty(name) || name.Length > 255) return false;
            Match m;
            try { m = Pattern.Match(name); }
            catch (RegexMatchTimeoutException) { return false; }
            if (!m.Success) return false;
            DateTime time;
            if (!DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd'['HH-mm']'",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out time)) return false;
            long counter;
            if (!Int64.TryParse(m.Groups[9].Value, NumberStyles.None, CultureInfo.InvariantCulture, out counter)) return false;
            double[] v = new double[7];
            for (int i = 0; i < v.Length; i++)
            {
                if (!Double.TryParse(m.Groups[i + 2].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out v[i]) || Double.IsInfinity(v[i]) || Double.IsNaN(v[i])) return false;
            }
            if (Math.Abs(v[0]) > 100000 || Math.Abs(v[1]) > 100000 || Math.Abs(v[2]) > 100000) return false;
            double norm = v[3]*v[3] + v[4]*v[4] + v[5]*v[5] + v[6]*v[6];
            if (norm < 0.8 || norm > 1.2) return false;
            double n = Math.Sqrt(norm);
            double qx = v[3]/n, qy = v[4]/n, qz = v[5]/n, qw = v[6]/n;
            // Rotate Unity's forward vector (0,0,1) by q and project it onto XZ.
            // Unlike an Euler shortcut, this remains correct when looking up/down.
            double a = 2 * (qw*qy + qx*qz);
            double b = 1 - 2 * (qx*qx + qy*qy);
            bool hasHeading = a*a + b*b > 1e-8;
            double yaw = (Math.Atan2(a, b) * 180 / Math.PI + 360) % 360;
            result = new Position(name, time, v[0], v[1], v[2], yaw, hasHeading, counter);
            return true;
        }
    }

    public struct MapPoint
    {
        public readonly double X, Y;
        public MapPoint(double x, double y) { X = x; Y = y; }
    }

    public static class CustomsMap
    {
        // tarkov.dev maps.json: bounds [[698,-307],[-372,237]], rotation 180.
        // The same bounds are used for SVGOverlay (not the satellite tile origin).
        public const double LeftX = 698, RightX = -372, TopZ = -307, BottomZ = 237;
        public const int Width = 3210, Height = 1632;
        public static MapPoint Project(double x, double z)
        { return new MapPoint((LeftX-x)/(LeftX-RightX)*Width, (z-TopZ)/(BottomZ-TopZ)*Height); }
        public static MapPoint Unproject(double px, double py)
        { return new MapPoint(LeftX-px/Width*(LeftX-RightX), TopZ+py/Height*(BottomZ-TopZ)); }
        public static bool Contains(Position p)
        { return p.X <= LeftX && p.X >= RightX && p.Z >= TopZ && p.Z <= BottomZ; }
        public static MapPoint Direction(double yaw)
        {
            // Unity +Z is yaw 0. After Customs' 180-degree rotation it points down.
            double r = yaw * Math.PI/180;
            double dx = -Math.Sin(r), dy = Math.Cos(r);
            return new MapPoint(dx, dy);
        }
    }

    public sealed class Snapshot
    {
        public readonly Position Position;
        public readonly DateTime FileTimeUtc, CreationTimeUtc, ReceivedUtc;
        public Snapshot(Position p, DateTime fileTime, DateTime creationTime, DateTime received)
        { Position = p; FileTimeUtc = fileTime; CreationTimeUtc = creationTime; ReceivedUtc = received; }
        public bool IsNewerThan(Snapshot other)
        {
            if (other == null) return true;
            int time = FileTimeUtc.CompareTo(other.FileTimeUtc);
            if (time != 0) return time > 0;
            time = CreationTimeUtc.CompareTo(other.CreationTimeUtc);
            if (time != 0) return time > 0;
            time = Position.NameTime.CompareTo(other.Position.NameTime);
            if (time != 0) return time > 0;
            int sequence = Position.Counter.CompareTo(other.Position.Counter);
            if (sequence != 0) return sequence > 0;
            return StringComparer.OrdinalIgnoreCase.Compare(Position.FileName, other.Position.FileName) > 0;
        }
    }
}
