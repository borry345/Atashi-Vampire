using System;
using UnityEngine;

namespace Survivor.Core
{
    /// Frozen monster positions for this step, filled in BuildIndex in ascending Id order.
    public sealed class MonsterSnapshotBuffer
    {
        private const int InitialCapacity = 512;

        private MonsterSnapshot[] _items = new MonsterSnapshot[InitialCapacity];
        private int _count;
        private float _maxHashedRadius;
        private Vector2 _min;
        private Vector2 _max;

        /// Number of snapshots added since the last Clear.
        public int Count => _count;

        /// Snapshot at index (0..Count-1, ascending Id); throws ArgumentOutOfRangeException otherwise.
        public ref readonly MonsterSnapshot this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Snapshot index out of range.");
                }

                return ref _items[index];
            }
        }

        /// Max Radius over entries that are not IsLarge; 0 when there are none.
        public float MaxHashedRadius => _maxHashedRadius;

        /// (rev 2) AABB of all positions (zero-size Rect when empty); used by Physics2DTargetProvider.
        public Rect Bounds => _count == 0 ? default : Rect.MinMaxRect(_min.x, _min.y, _max.x, _max.y);

        /// Binary search by Id; false when the id is not in this step's snapshot.
        public bool TryGetIndex(EntityHandle id, out int index)
        {
            int low = 0;
            int high = _count - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                int cmp = _items[mid].Id.CompareTo(id);
                if (cmp == 0)
                {
                    index = mid;
                    return true;
                }

                if (cmp < 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            index = -1;
            return false;
        }

        /// Removes every snapshot (capacity is kept).
        public void Clear()
        {
            _count = 0;
            _maxHashedRadius = 0f;
            _min = default;
            _max = default;
        }

        /// Appends a snapshot; throws InvalidOperationException unless its Id is greater than the last added Id.
        public void Add(in MonsterSnapshot snapshot)
        {
            if (_count > 0 && snapshot.Id.CompareTo(_items[_count - 1].Id) <= 0)
            {
                throw new InvalidOperationException(
                    $"MonsterSnapshotBuffer.Add: id {snapshot.Id} is not greater than the previous id {_items[_count - 1].Id}.");
            }

            if (_count == _items.Length)
            {
                Array.Resize(ref _items, _items.Length * 2);
            }

            _items[_count] = snapshot;
            if (_count == 0)
            {
                _min = snapshot.Position;
                _max = snapshot.Position;
            }
            else
            {
                _min = Vector2.Min(_min, snapshot.Position);
                _max = Vector2.Max(_max, snapshot.Position);
            }

            if (!snapshot.IsLarge && snapshot.Radius > _maxHashedRadius)
            {
                _maxHashedRadius = snapshot.Radius;
            }

            _count++;
        }
    }
}
