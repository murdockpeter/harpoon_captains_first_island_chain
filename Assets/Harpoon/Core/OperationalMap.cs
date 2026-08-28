using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public enum TerrainType { Sea, Land, RestrictedSea, NavalBase }

    public sealed class NavalBase
    {
        public string Id { get; }
        public string Name { get; }
        public Side Side { get; }
        public HexCoord Position { get; }

        public NavalBase(string id, string name, Side side, HexCoord position)
        {
            Id = id;
            Name = name;
            Side = side;
            Position = position;
        }
    }

    public sealed class OperationalMap
    {
        private readonly HashSet<HexCoord> _land;
        private readonly HashSet<HexCoord> _restricted;
        private readonly Dictionary<HexCoord, NavalBase> _bases;

        public int MinimumColumn { get; }
        public int MaximumColumn { get; }
        public int MinimumRow { get; }
        public int MaximumRow { get; }
        public IReadOnlyCollection<HexCoord> Land => _land;
        public IReadOnlyCollection<HexCoord> Restricted => _restricted;
        public IReadOnlyCollection<NavalBase> Bases => _bases.Values;

        public OperationalMap(int minimumColumn, int maximumColumn, int minimumRow, int maximumRow,
            IEnumerable<HexCoord> land, IEnumerable<HexCoord> restricted, IEnumerable<NavalBase> bases)
        {
            MinimumColumn = minimumColumn;
            MaximumColumn = maximumColumn;
            MinimumRow = minimumRow;
            MaximumRow = maximumRow;
            _land = new HashSet<HexCoord>(land ?? Array.Empty<HexCoord>());
            _restricted = new HashSet<HexCoord>(restricted ?? Array.Empty<HexCoord>());
            _bases = (bases ?? Array.Empty<NavalBase>()).ToDictionary(item => item.Position);
        }

        public IEnumerable<HexCoord> AllHexes
        {
            get
            {
                for (var column = MinimumColumn; column <= MaximumColumn; column++)
                for (var row = MinimumRow; row <= MaximumRow; row++)
                    yield return new HexCoord(column, row);
            }
        }

        public bool Contains(HexCoord hex) =>
            hex.Column >= MinimumColumn && hex.Column <= MaximumColumn &&
            hex.Row >= MinimumRow && hex.Row <= MaximumRow;

        public TerrainType TerrainAt(HexCoord hex)
        {
            if (!Contains(hex)) throw new ArgumentOutOfRangeException(nameof(hex));
            if (_bases.ContainsKey(hex)) return TerrainType.NavalBase;
            if (_land.Contains(hex)) return TerrainType.Land;
            return _restricted.Contains(hex) ? TerrainType.RestrictedSea : TerrainType.Sea;
        }

        public NavalBase BaseAt(HexCoord hex) => _bases.TryGetValue(hex, out var navalBase) ? navalBase : null;

        public bool IsNavigable(HexCoord hex, Side side)
        {
            if (!Contains(hex)) return false;
            if (_bases.TryGetValue(hex, out var navalBase)) return navalBase.Side == side;
            return !_land.Contains(hex) && !_restricted.Contains(hex);
        }

        public IEnumerable<HexCoord> NavigableNeighbors(HexCoord hex, Side side) =>
            hex.Neighbors().Where(candidate => IsNavigable(candidate, side));

        public IReadOnlyList<HexCoord> FindPath(HexCoord origin, HexCoord destination, Side side,
            int maximumSteps = int.MaxValue)
        {
            if (!Contains(origin) || !IsNavigable(destination, side) || maximumSteps < 0)
                return Array.Empty<HexCoord>();
            if (origin == destination) return new[] { origin };
            var frontier = new Queue<HexCoord>();
            var previous = new Dictionary<HexCoord, HexCoord>();
            var depth = new Dictionary<HexCoord, int> { [origin] = 0 };
            frontier.Enqueue(origin);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (depth[current] >= maximumSteps) continue;
                foreach (var next in NavigableNeighbors(current, side))
                {
                    if (depth.ContainsKey(next)) continue;
                    previous[next] = current;
                    depth[next] = depth[current] + 1;
                    if (next == destination) return ReconstructPath(origin, destination, previous);
                    frontier.Enqueue(next);
                }
            }
            return Array.Empty<HexCoord>();
        }

        private static IReadOnlyList<HexCoord> ReconstructPath(HexCoord origin, HexCoord destination,
            IReadOnlyDictionary<HexCoord, HexCoord> previous)
        {
            var path = new List<HexCoord> { destination };
            while (path[path.Count - 1] != origin) path.Add(previous[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }
    }

    public static class FirstIslandChainMap
    {
        public static OperationalMap Instance { get; } = Create();

        private static OperationalMap Create()
        {
            // The supplement's 15-by-20 scenario grid, with the coastline generalized at
            // its stated 60-mile hex scale. Major land masses are intentionally contiguous;
            // sub-hex Ryukyu islands are represented by separated island hexes instead of a
            // solid land bridge. The four-digit labels are axial column/row coordinates.
            var land = new HashSet<HexCoord>
            {
                // China coast: a continuous mainland with broader capes and readable bays.
                new HexCoord(1,1),new HexCoord(2,1),new HexCoord(3,1),
                new HexCoord(1,2),new HexCoord(2,2),new HexCoord(3,2),
                new HexCoord(1,3),new HexCoord(2,3),new HexCoord(3,3),
                new HexCoord(1,4),new HexCoord(2,4),
                new HexCoord(1,5),new HexCoord(2,5),
                new HexCoord(1,6),new HexCoord(2,6),new HexCoord(3,6),
                new HexCoord(1,7),new HexCoord(2,7),new HexCoord(3,7),
                new HexCoord(1,8),new HexCoord(2,8),new HexCoord(3,8),
                new HexCoord(1,9),new HexCoord(2,9),new HexCoord(3,9),
                new HexCoord(1,10),new HexCoord(2,10),
                new HexCoord(1,11),new HexCoord(2,11),
                // 0312 is the scenario's established offshore western exit lane.
                new HexCoord(1,12),new HexCoord(2,12),
                new HexCoord(1,13),new HexCoord(2,13),new HexCoord(3,13),
                new HexCoord(1,14),new HexCoord(2,14),new HexCoord(3,14),
                new HexCoord(1,15),new HexCoord(2,15),
                new HexCoord(1,16),new HexCoord(2,16),

                // The Qiongzhou Strait is substantially narrower than one 60-mile hex, so
                // the coast remains visually continuous at this operational map scale.
                new HexCoord(1,17),new HexCoord(2,17),

                // Hainan and the adjacent southern China coast.
                new HexCoord(1,18),new HexCoord(2,18),new HexCoord(3,18),new HexCoord(4,18),
                new HexCoord(1,19),new HexCoord(2,19),new HexCoord(3,19),new HexCoord(4,19),
                new HexCoord(1,20),new HexCoord(2,20),new HexCoord(3,20),

                // Ryukyu groups. Most islands are smaller than a 60-mile hex, so sea gaps
                // are retained between the represented groups.
                new HexCoord(11,1),new HexCoord(10,2),new HexCoord(8,6),new HexCoord(8,7),

                // Taiwan: approximately five hex lengths north to south and two hexes at
                // its broadest central shoulders at this scale.
                new HexCoord(8,8),new HexCoord(9,8),new HexCoord(8,9),new HexCoord(9,9),
                new HexCoord(8,10),new HexCoord(7,11),new HexCoord(8,11),
                new HexCoord(7,12),new HexCoord(8,12),

                // 0813 and 0714 remain open water through the Bashi Channel. Luzon sits
                // south-southeast of Taiwan with a broad South China Sea gap to Hainan.
                new HexCoord(9,15),new HexCoord(8,16),new HexCoord(9,16),
                new HexCoord(8,17),new HexCoord(9,17),new HexCoord(10,17),
                new HexCoord(8,18),new HexCoord(9,18),
                new HexCoord(8,19),new HexCoord(9,19)
            };
            var bases = new[]
            {
                new NavalBase("plan-ningbo", "Ningbo-Zhoushan", Side.Plan, new HexCoord(2,8)),
                new NavalBase("plan-xiamen", "Xiamen", Side.Plan, new HexCoord(2,10)),
                new NavalBase("plan-yulin", "Yulin / Sanya", Side.Plan, new HexCoord(2,18)),
                new NavalBase("us-kadena", "Kadena AB", Side.UsNavy, new HexCoord(9,4)),
                new NavalBase("us-taipei", "Taipei / Zuoying", Side.UsNavy, new HexCoord(8,10)),
                new NavalBase("us-subic", "Subic Bay / Clark", Side.UsNavy, new HexCoord(8,16))
            };
            // The green Hainan area is land/off-map access, not ice or restricted water.
            return new OperationalMap(1, 15, 1, 20, land, Array.Empty<HexCoord>(), bases);
        }
    }
}
