using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// One level-up round at a time; accepts only the first valid submission per round (R29, TAC-F5-5). UI state, not simulation
    /// state: the flow opens, submits and closes it outside steps. A pause keeps the round open, so the same three options return.
    public sealed class LevelUpSession
    {
        private const int OptionCount = 3;

        private readonly List<UpgradeOption> _options = new List<UpgradeOption>(OptionCount);
        private bool _submitted;

        /// Creates a closed session with no round.
        public LevelUpSession()
        {
            Reset();
        }

        /// Id of the latest round; 0 = none yet. Increments per round within a run.
        public long RoundId { get; private set; }

        /// True between Open and Close.
        public bool IsOpen { get; private set; }

        /// The three options of the latest round (empty before the first round); kept until the next Open.
        public IReadOnlyList<UpgradeOption> Options => _options;

        /// Frame the latest round was opened in; submissions need a later frame.
        public int OpenedFrame { get; private set; }

        /// Opens the next round with exactly three options at frame. Throws InvalidOperationException while a round is open,
        /// ArgumentNullException for null and ArgumentException unless options3 has exactly three entries.
        public void Open(IReadOnlyList<UpgradeOption> options3, int frame)
        {
            if (options3 == null)
            {
                throw new ArgumentNullException(nameof(options3));
            }

            if (options3.Count != OptionCount)
            {
                throw new ArgumentException("A level-up round needs exactly 3 options (got " + options3.Count + ").", nameof(options3));
            }

            if (IsOpen)
            {
                throw new InvalidOperationException("LevelUpSession.Open: round " + RoundId + " is still open.");
            }

            _options.Clear();
            for (int i = 0; i < OptionCount; i++)
            {
                _options.Add(options3[i]);
            }

            RoundId++;
            OpenedFrame = frame;
            _submitted = false;
            IsOpen = true;
        }

        /// True with the chosen option only for the first submission with IsOpen, roundId == RoundId, frame > OpenedFrame and
        /// 0 <= optionIndex < 3; every later submission of the same round is false. Does not close the round.
        public bool TrySubmit(long roundId, int optionIndex, int frame, out UpgradeOption chosen)
        {
            chosen = default;
            if (!IsOpen || roundId != RoundId || _submitted || frame <= OpenedFrame || optionIndex < 0 || optionIndex >= OptionCount)
            {
                return false;
            }

            _submitted = true;
            chosen = _options[optionIndex];
            return true;
        }

        /// Closes the current round (no-op when closed); the options stay readable until the next Open.
        public void Close()
        {
            IsOpen = false;
        }

        /// Run reset: no round (RoundId 0), closed, no options.
        public void Reset()
        {
            _options.Clear();
            _submitted = false;
            IsOpen = false;
            RoundId = 0;
            OpenedFrame = 0;
        }
    }
}
