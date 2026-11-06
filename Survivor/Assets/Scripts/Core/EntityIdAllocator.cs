using System;
using System.Collections.Generic;

namespace Survivor.Core
{
    /// Issues EntityIds independently of any pool strategy so ids are identical across F9 strategies.
    public sealed class EntityIdAllocator
    {
        // Indexed by EntityId.Index; slot 0 is unused so Index stays 1-based.
        private readonly List<int> _generations = new List<int> { 0 };
        private readonly List<bool> _live = new List<bool> { false };
        private readonly Stack<int> _freeIndices = new Stack<int>();
        private int _liveCount;

        /// Number of ids currently allocated and not freed.
        public int LiveCount => _liveCount;

        /// Pops the most recently freed index (LIFO) at its current generation, else issues the next new index at generation 1.
        public EntityId Allocate()
        {
            int index;
            if (_freeIndices.Count > 0)
            {
                index = _freeIndices.Pop();
            }
            else
            {
                index = _generations.Count;
                _generations.Add(1);
                _live.Add(false);
            }

            _live[index] = true;
            _liveCount++;
            return new EntityId(index, _generations[index]);
        }

        /// Frees a live id and increments its slot generation; throws InvalidOperationException if !IsLive(id).
        public void Free(EntityId id)
        {
            if (!IsLive(id))
            {
                throw new InvalidOperationException($"EntityIdAllocator.Free: id {id} is not live.");
            }

            _live[id.Index] = false;
            _generations[id.Index] = id.Generation + 1;
            _liveCount--;
            _freeIndices.Push(id.Index);
        }

        /// True when the id was issued by Allocate and has not been freed since.
        public bool IsLive(EntityId id)
        {
            return id.IsValid
                && id.Index < _generations.Count
                && _live[id.Index]
                && _generations[id.Index] == id.Generation;
        }

        /// Starts a new run: next index = 1, all generations back to 1, free list empty (D26).
        public void Reset()
        {
            _generations.Clear();
            _generations.Add(0);
            _live.Clear();
            _live.Add(false);
            _freeIndices.Clear();
            _liveCount = 0;
        }
    }
}
