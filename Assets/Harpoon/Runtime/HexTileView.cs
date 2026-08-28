using Harpoon.Core;
using UnityEngine;

namespace Harpoon.Runtime
{
    public sealed class HexTileView : MonoBehaviour
    {
        public HexCoord Coordinate { get; private set; }
        public Color BaseColor { get; private set; }
        public bool IsLand { get; private set; }
        private Material _material;
        private Vector2 _textureOrigin;

        public void Initialize(HexCoord coordinate, Color baseColor, bool isLand)
        {
            Coordinate = coordinate;
            BaseColor = baseColor;
            IsLand = isLand;
            _material = GetComponent<Renderer>().material;
            _textureOrigin = _material.mainTextureOffset;
        }

        private void Update()
        {
            if (IsLand || _material == null || _material.mainTexture == null) return;
            var drift = Time.time * 0.006f;
            _material.mainTextureOffset = _textureOrigin + new Vector2(drift, drift * 0.42f);
        }
    }
}
