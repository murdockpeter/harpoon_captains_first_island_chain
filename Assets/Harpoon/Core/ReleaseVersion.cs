using System;

namespace Harpoon.Core
{
    public readonly struct ReleaseVersion : IComparable<ReleaseVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }

        public ReleaseVersion(int major, int minor, int patch)
        {
            Major = Math.Max(0, major);
            Minor = Math.Max(0, minor);
            Patch = Math.Max(0, patch);
        }

        public int CompareTo(ReleaseVersion other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0) return major;
            var minor = Minor.CompareTo(other.Minor);
            return minor != 0 ? minor : Patch.CompareTo(other.Patch);
        }

        public override string ToString() => $"{Major}.{Minor}.{Patch}";

        public static bool TryParse(string value, out ReleaseVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var clean = value.Trim();
            if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(1);
            var suffix = clean.IndexOfAny(new[] { '-', '+' });
            if (suffix >= 0) clean = clean.Substring(0, suffix);
            var parts = clean.Split('.');
            var patch = 0;
            if (parts.Length < 2 || parts.Length > 3 ||
                !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor) ||
                (parts.Length == 3 && !int.TryParse(parts[2], out patch)) ||
                major < 0 || minor < 0 || patch < 0) return false;
            version = new ReleaseVersion(major, minor, parts.Length == 3 ? patch : 0);
            return true;
        }

        public static bool IsNewer(string candidate, string installed) =>
            TryParse(candidate, out var next) && TryParse(installed, out var current) &&
            next.CompareTo(current) > 0;
    }
}
