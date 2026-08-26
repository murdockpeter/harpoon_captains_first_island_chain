using System;
using System.Collections.Generic;

namespace Harpoon.Core
{
    [Serializable]
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Column;
        public readonly int Row;

        public HexCoord(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public int DistanceTo(HexCoord other)
        {
            var dq = other.Column - Column;
            var dr = other.Row - Row;
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
        }

        public IEnumerable<HexCoord> Neighbors()
        {
            yield return new HexCoord(Column + 1, Row);
            yield return new HexCoord(Column + 1, Row - 1);
            yield return new HexCoord(Column, Row - 1);
            yield return new HexCoord(Column - 1, Row);
            yield return new HexCoord(Column - 1, Row + 1);
            yield return new HexCoord(Column, Row + 1);
        }

        public bool IsAdjacentTo(HexCoord other) => DistanceTo(other) == 1;

        public MapPoint ToMapPoint(double radius = 1d) => new MapPoint(
            radius * 1.5d * (Column - 1),
            radius * Math.Sqrt(3d) * ((Row - 1) + (Column - 1) * 0.5d));

        public bool Equals(HexCoord other) => Column == other.Column && Row == other.Row;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => (Column * 397) ^ Row;
        public override string ToString() => $"{Column:00}{Row:00}";
        public static bool operator ==(HexCoord left, HexCoord right) => left.Equals(right);
        public static bool operator !=(HexCoord left, HexCoord right) => !left.Equals(right);
    }

    public readonly struct MapPoint
    {
        public readonly double X;
        public readonly double Y;
        public MapPoint(double x, double y) { X = x; Y = y; }
    }
}
