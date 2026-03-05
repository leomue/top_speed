using System;

namespace TopSpeed.Core.Updates
{
    internal readonly struct GameVersion : IComparable<GameVersion>, IEquatable<GameVersion>
    {
        public GameVersion(int year, int month, int day)
        {
            Year = year;
            Month = month;
            Day = day;
        }

        public int Year { get; }
        public int Month { get; }
        public int Day { get; }

        public static bool TryParse(string? value, out GameVersion version)
        {
            version = default;
            if (value == null)
                return false;
            var text = value.Trim();
            if (text.Length == 0)
                return false;

            var parts = text.Split('.');
            if (parts.Length != 3)
                return false;
            if (!int.TryParse(parts[0], out var year))
                return false;
            if (!int.TryParse(parts[1], out var month))
                return false;
            if (!int.TryParse(parts[2], out var day))
                return false;
            if (year < 2000 || year > 9999)
                return false;
            if (month < 1 || month > 12)
                return false;
            if (day < 1 || day > 31)
                return false;

            version = new GameVersion(year, month, day);
            return true;
        }

        public int CompareTo(GameVersion other)
        {
            var cmp = Year.CompareTo(other.Year);
            if (cmp != 0)
                return cmp;
            cmp = Month.CompareTo(other.Month);
            if (cmp != 0)
                return cmp;
            return Day.CompareTo(other.Day);
        }

        public bool Equals(GameVersion other)
        {
            return Year == other.Year && Month == other.Month && Day == other.Day;
        }

        public override bool Equals(object? obj)
        {
            return obj is GameVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Year;
                hash = (hash * 397) ^ Month;
                hash = (hash * 397) ^ Day;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Year}.{Month}.{Day}";
        }
    }
}
