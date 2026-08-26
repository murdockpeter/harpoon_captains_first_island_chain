using Harpoon.Core;
using UnityEngine;

namespace Harpoon.Runtime
{
    public sealed class HexTileView : MonoBehaviour
    {
        public HexCoord Coordinate { get; private set; }
        public void Initialize(HexCoord coordinate) => Coordinate = coordinate;
    }
}
