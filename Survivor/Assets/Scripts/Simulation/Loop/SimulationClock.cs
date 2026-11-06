using System;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Owns the step counter and the run gate; the only source of game time (5.3).
    public sealed class SimulationClock : IStepClockReader
    {
        private const long NoJump = -1;

        private long _step;
        private bool _isRunning;
        private bool _stepActive;
        private SimulationPhase _phase;
        private StepContext _context;
        private int _stepsThisFrame;
        private GateCloseReason _closeReason;
        private int _closeSequence;
        private bool _closedThisStep;
        private bool _singleStepPending;
        private long _jumpTarget = NoJump;

        /// Completed steps in the current run.
        public long Step => _step;

        /// Gate for the next FixedUpdate; false after any close.
        public bool IsRunning => _isRunning;

        /// True between Begin and End of an executing step.
        public bool StepActive => _stepActive;

        /// Phase currently executing (the last phase set when no step is active).
        public SimulationPhase CurrentPhase => _phase;

        /// Context of the current step; valid only while StepActive and phase >= MonsterTick.
        public StepContext CurrentContext => _context;

        /// Steps begun since the last SimulationFrameCounter.Update (order -10000).
        public int StepsThisFrame => _stepsThisFrame;

        /// Game seconds = StepMath.StepsToSeconds(Step) (R2).
        public double GameSeconds => StepMath.StepsToSeconds(_step);

        /// Reason of the latest close.
        public GateCloseReason CloseReason => _closeReason;

        /// Increments on every close; the flow polls it in Update.
        public int CloseSequence => _closeSequence;

        /// Profiler marker state shared by the three drivers.
        internal StepMarkerTracker Markers { get; } = new StepMarkerTracker();

        /// Opens the gate (flow only); throws InvalidOperationException while a step is active.
        public void Open()
        {
            ThrowIfStepActive(nameof(Open));
            _isRunning = true;
        }

        /// Closes the gate outside a step (pause, focus loss, run reset); throws InvalidOperationException while a step is active.
        public void Close(GateCloseReason reason)
        {
            ThrowIfStepActive(nameof(Close));
            _singleStepPending = false;
            CloseGate(reason);
        }

        /// Closes the gate from inside a step (Lifecycle handlers, EndStep); the step still runs to End.
        /// The first close in a step wins; throws InvalidOperationException when no step is active.
        public void CloseAfterCurrentStep(GateCloseReason reason)
        {
            if (!_stepActive)
            {
                throw new InvalidOperationException("SimulationClock.CloseAfterCurrentStep is only valid inside a step.");
            }

            if (_closedThisStep)
            {
                return;
            }

            _closedThisStep = true;
            CloseGate(reason);
        }

        /// Dev: opens the gate for exactly one step, after which EndStep closes it with SingleStepDone.
        /// Throws InvalidOperationException while a step is active.
        public void RequestSingleStep()
        {
            ThrowIfStepActive(nameof(RequestSingleStep));
            _singleStepPending = true;
            _isRunning = true;
        }

        /// Dev: Step becomes targetStep inside the next TryBeginStep, before Begin. Forward only:
        /// throws ArgumentOutOfRangeException when targetStep is less than Step (TAC-F19-3).
        public void RequestJump(long targetStep)
        {
            if (targetStep < _step)
            {
                throw new ArgumentOutOfRangeException(nameof(targetStep), targetStep, "Time jumps must not go backwards.");
            }

            _jumpTarget = targetStep;
        }

        /// Run reset: Step = 0, gate closed with RunReset, pending single step and jump dropped.
        /// Throws InvalidOperationException while a step is active.
        public void Reset()
        {
            ThrowIfStepActive(nameof(Reset));
            _step = 0;
            _phase = SimulationPhase.Begin;
            _context = default;
            _singleStepPending = false;
            _jumpTarget = NoJump;
            _stepsThisFrame = 0;
            CloseGate(GateCloseReason.RunReset);
        }

        // SimulationDriverEarly: starts a step when IsRunning && Step < stopAtStep. A pending jump is applied first;
        // a jump that lands on or past stopAtStep closes the gate with StopAtStep instead of starting a step.
        internal bool TryBeginStep(long stopAtStep)
        {
            if (_stepActive)
            {
                throw new InvalidOperationException("SimulationClock.TryBeginStep called while a step is active.");
            }

            if (!_isRunning)
            {
                return false;
            }

            if (_jumpTarget != NoJump)
            {
                _step = Math.Max(_step, _jumpTarget);
                _jumpTarget = NoJump;
            }

            if (_step >= stopAtStep)
            {
                _singleStepPending = false;
                CloseGate(GateCloseReason.StopAtStep);
                return false;
            }

            _stepActive = true;
            _closedThisStep = false;
            _phase = SimulationPhase.Begin;
            _context = default;
            _stepsThisFrame++;
            return true;
        }

        internal void SetPhase(SimulationPhase phase)
        {
            _phase = phase;
        }

        internal void SetContext(in StepContext context)
        {
            _context = context;
        }

        // SimulationDriverLate after Lifecycle: Step++, then closes with StopAtStep / SingleStepDone unless the step already closed.
        internal void EndStep(long stopAtStep)
        {
            if (!_stepActive)
            {
                throw new InvalidOperationException("SimulationClock.EndStep called outside a step.");
            }

            _phase = SimulationPhase.End;
            _step++;
            if (_step >= stopAtStep)
            {
                CloseAfterCurrentStep(GateCloseReason.StopAtStep);
            }
            else if (_singleStepPending)
            {
                CloseAfterCurrentStep(GateCloseReason.SingleStepDone);
            }

            _singleStepPending = false;
            _stepActive = false;
        }

        // Driver exception handler only: the step stops where it is, Step is NOT incremented, gate closed with Error.
        internal void AbortStep()
        {
            Markers.EndAll();
            _stepActive = false;
            _singleStepPending = false;
            CloseGate(GateCloseReason.Error);
        }

        // SimulationFrameCounter.Update.
        internal void ResetStepsThisFrame()
        {
            _stepsThisFrame = 0;
        }

        private void CloseGate(GateCloseReason reason)
        {
            _isRunning = false;
            _closeReason = reason;
            _closeSequence++;
        }

        private void ThrowIfStepActive(string operation)
        {
            if (_stepActive)
            {
                throw new InvalidOperationException("SimulationClock." + operation + " is not allowed while a step is active.");
            }
        }
    }
}
