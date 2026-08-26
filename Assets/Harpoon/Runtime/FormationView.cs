using Harpoon.Core;
using UnityEngine;

namespace Harpoon.Runtime
{
    public sealed class FormationView : MonoBehaviour
    {
        public Side Side { get; private set; }
        public void Initialize(Side side) => Side = side;
    }
}
