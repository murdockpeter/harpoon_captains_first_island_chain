using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public interface IRandomSource
    {
        int Next(int maximumExclusive);
    }

    public enum TimeOfDay { Am, Pm, Night }

    [Serializable]
    public sealed class MovementChitData
    {
        public string formationId;
        public Side side;
    }

    public sealed class MovementChit
    {
        public string FormationId { get; }
        public Side Side { get; }

        public MovementChit(string formationId, Side side)
        {
            FormationId = formationId ?? string.Empty;
            Side = side;
        }

        public MovementChitData ToData() => new MovementChitData
        {
            formationId = FormationId,
            side = Side
        };
    }

    public sealed class MovementChitCup
    {
        private readonly IRandomSource _random;
        private readonly List<MovementChit> _remaining = new List<MovementChit>();
        private readonly List<MovementChit> _drawn = new List<MovementChit>();

        public IReadOnlyList<MovementChit> Remaining => _remaining;
        public IReadOnlyList<MovementChit> Drawn => _drawn;
        public int TotalCount => _remaining.Count + _drawn.Count;
        public bool IsEmpty => _remaining.Count == 0;
        public bool FirstDrawPending => _drawn.Count == 0 && _remaining.Count > 0;

        public MovementChitCup(IRandomSource random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public void Reset(IEnumerable<TaskForceState> formations)
        {
            Reset((formations ?? Array.Empty<TaskForceState>())
                .Where(force => !force.IsDestroyed)
                .Select(force => new MovementChit(force.Id, force.Side)));
        }

        public void Reset(IEnumerable<MovementChit> chits)
        {
            _remaining.Clear();
            _drawn.Clear();
            _remaining.AddRange(chits ?? Array.Empty<MovementChit>());
        }

        public MovementChit Draw()
        {
            if (_remaining.Count == 0) throw new InvalidOperationException("The movement chit cup is empty.");
            var index = _random.Next(_remaining.Count);
            var chit = _remaining[index];
            _remaining.RemoveAt(index);
            _drawn.Add(chit);
            return chit;
        }

        public bool RemoveUndrawnFormation(string formationId)
        {
            var index = _remaining.FindIndex(item => string.Equals(item.FormationId,
                formationId, StringComparison.Ordinal));
            if (index < 0) return false;
            _remaining.RemoveAt(index);
            return true;
        }

        internal void Restore(IEnumerable<MovementChitData> remaining, IEnumerable<MovementChitData> drawn)
        {
            _remaining.Clear();
            _drawn.Clear();
            _remaining.AddRange((remaining ?? Array.Empty<MovementChitData>())
                .Select(item => new MovementChit(item.formationId, item.side)));
            _drawn.AddRange((drawn ?? Array.Empty<MovementChitData>())
                .Select(item => new MovementChit(item.formationId, item.side)));
        }
    }
}
