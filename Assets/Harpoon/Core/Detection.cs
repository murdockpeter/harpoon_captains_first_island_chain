using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public enum ContactLevel
    {
        Undetected,
        Located,
        Classified,
        LostContact
    }

    public enum DetectionMethod
    {
        None,
        SurfaceSearchRadar,
        Esm,
        Visual,
        Sonar,
        ScenarioKnown
    }

    [Serializable]
    public sealed class ContactSnapshotData
    {
        public Side observer;
        public string targetFormationId;
        public ContactLevel level;
        public DetectionMethod method;
        public int column;
        public int row;
        public int turn;
    }

    public sealed class ContactRecord
    {
        public Side Observer { get; }
        public string TargetFormationId { get; }
        public ContactLevel Level { get; private set; }
        public DetectionMethod Method { get; private set; }
        public HexCoord LastKnownPosition { get; private set; }
        public int LastUpdatedTurn { get; private set; }
        public bool IsDetected => Level == ContactLevel.Located || Level == ContactLevel.Classified;

        internal ContactRecord(Side observer, string targetFormationId)
        {
            Observer = observer;
            TargetFormationId = targetFormationId ?? string.Empty;
            Level = ContactLevel.Undetected;
        }

        internal void Update(ContactLevel level, DetectionMethod method, HexCoord position, int turn)
        {
            Level = level;
            Method = method;
            LastKnownPosition = position;
            LastUpdatedTurn = turn;
        }

        public ContactSnapshotData ToData() => new ContactSnapshotData
        {
            observer = Observer,
            targetFormationId = TargetFormationId,
            level = Level,
            method = Method,
            column = LastKnownPosition.Column,
            row = LastKnownPosition.Row,
            turn = LastUpdatedTurn
        };
    }

    public sealed class DetectionTracker
    {
        private readonly Dictionary<string, ContactRecord> _contacts =
            new Dictionary<string, ContactRecord>(StringComparer.Ordinal);

        public IReadOnlyList<ContactRecord> Contacts => _contacts.Values.ToArray();

        public ContactRecord ContactFor(Side observer, string targetFormationId)
        {
            var key = Key(observer, targetFormationId);
            if (!_contacts.TryGetValue(key, out var contact))
            {
                contact = new ContactRecord(observer, targetFormationId);
                _contacts[key] = contact;
            }
            return contact;
        }

        public bool IsDetected(Side observer, string targetFormationId) =>
            ContactFor(observer, targetFormationId).IsDetected;
        public bool IsClassified(Side observer, string targetFormationId) =>
            ContactFor(observer, targetFormationId).Level == ContactLevel.Classified;

        public ContactRecord Detect(Side observer, TaskForceState target, DetectionMethod method,
            int turn, bool classified = true)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var contact = ContactFor(observer, target.Id);
            contact.Update(classified ? ContactLevel.Classified : ContactLevel.Located,
                method, target.Position, turn);
            return contact;
        }

        public ContactRecord Lose(Side observer, TaskForceState target, DetectionMethod method, int turn)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var contact = ContactFor(observer, target.Id);
            contact.Update(ContactLevel.LostContact, method, contact.LastKnownPosition, turn);
            return contact;
        }

        public void Restore(IEnumerable<ContactSnapshotData> snapshots)
        {
            _contacts.Clear();
            foreach (var item in snapshots ?? Array.Empty<ContactSnapshotData>())
                ContactFor(item.observer, item.targetFormationId).Update(item.level, item.method,
                    new HexCoord(item.column, item.row), item.turn);
        }

        private static string Key(Side observer, string targetFormationId) =>
            observer + "|" + (targetFormationId ?? string.Empty);
    }

    public sealed class DetectionResolver
    {
        private readonly IDieRoller _dice;
        private readonly Action<string, string> _trace;

        public DetectionResolver(IDieRoller dice, Action<string, string> trace = null)
        {
            _dice = dice ?? throw new ArgumentNullException(nameof(dice));
            _trace = trace;
        }

        public bool ResolveEsm(TaskForceState observer, TaskForceState target)
        {
            if (observer == null || target == null || !observer.CanUseEsm ||
                !target.RadarRadiating || observer.Position.DistanceTo(target.Position) != 1)
                return false;
            return Roll("ESM", observer, target, 5);
        }

        public bool ResolveVisual(TaskForceState observer, TaskForceState target, TimeOfDay timeOfDay)
        {
            if (observer == null || target == null || observer.RadarRadiating ||
                timeOfDay == TimeOfDay.Night || observer.Position != target.Position)
                return false;
            return Roll("VISUAL", observer, target, 2);
        }

        public bool ResolveSonar(TaskForceState observer, TaskForceState target, bool previouslyDetected)
        {
            if (observer == null || target == null) return false;
            var range = observer.Position.DistanceTo(target.Position);
            if (range > 2) return false;
            var sonarShips = observer.ActiveUnits.Where(unit => unit.EffectiveSonar > 0).ToArray();
            if (sonarShips.Length == 0) return false;
            var value = sonarShips.Max(unit => unit.EffectiveSonar) + (sonarShips.Length > 1 ? 1 : 0) -
                        (range == 1 ? 2 : range == 2 ? 3 : 0) +
                        Math.Max(0, target.DeclaredSpeed) - Math.Max(0, observer.DeclaredSpeed) +
                        (previouslyDetected ? 1 : 0);
            var roll = _dice.RollD6();
            var success = roll != 6 && roll < value;
            _trace?.Invoke("DIE", $"SONAR {observer.Id}->{target.Id}: D6={roll}; value={value}; " +
                $"{(success ? "DETECTED" : "NO CONTACT")}.");
            return success;
        }

        private bool Roll(string sensor, TaskForceState observer, TaskForceState target, int maximum)
        {
            var roll = _dice.RollD6();
            var success = roll <= maximum;
            _trace?.Invoke("DIE", $"{sensor} {observer.Id}->{target.Id}: D6={roll}; " +
                $"needs 1-{maximum}; {(success ? "DETECTED" : "NO CONTACT")}.");
            return success;
        }
    }
}
