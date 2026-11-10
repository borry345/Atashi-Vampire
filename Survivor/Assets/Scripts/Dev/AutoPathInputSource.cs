using System;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Dev
{
    /// Scripted circular walk (5.5, SPEC F19 "player auto path"): at step s the target is center + radius * (cos phi, sin phi)
    /// with phi = start + (speed / radius) * (s + 1) / 60; the move heads for the target at min(speed, distance * 60).
    /// Pure function of the step and the position, so a run with this input is deterministic.
    public sealed class AutoPathInputSource : IInputSource
    {
        private readonly Vector2 _center;
        private readonly float _radius;
        private readonly float _speed;
        private readonly float _startAngle;

        /// Creates the path; radius and speed must be positive and finite (ArgumentOutOfRangeException).
        public AutoPathInputSource(Vector2 center, float radius, float speedUnitsPerSecond, float startAngleRad)
        {
            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Auto path radius must be positive and finite.");
            }

            if (!(speedUnitsPerSecond > 0f) || float.IsInfinity(speedUnitsPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(speedUnitsPerSecond), speedUnitsPerSecond,
                    "Auto path speed must be positive and finite.");
            }

            _center = center;
            _radius = radius;
            _speed = speedUnitsPerSecond;
            _startAngle = startAngleRad;
        }

        /// Circle center.
        public Vector2 Center => _center;

        /// Circle radius in world units.
        public float Radius => _radius;

        /// Walking speed in units per second.
        public float Speed => _speed;

        /// Point the player heads for at step (center + radius * (cos phi, sin phi)).
        public Vector2 TargetAt(long step)
        {
            double phi = _startAngle + (double)_speed / _radius * (step + 1) / StepMath.StepsPerSecond;
            return new Vector2(_center.x + (float)(_radius * Math.Cos(phi)), _center.y + (float)(_radius * Math.Sin(phi)));
        }

        /// Unit direction to TargetAt(step) with SpeedOverride = min(speed, distance * 60); zero move when already there.
        public MoveCommand ReadMove(long step, Vector2 currentPosition)
        {
            Vector2 delta = TargetAt(step) - currentPosition;
            double distance = Math.Sqrt((double)delta.x * delta.x + (double)delta.y * delta.y);
            if (distance <= 0d)
            {
                return new MoveCommand(Vector2.zero, 0f);
            }

            var direction = new Vector2((float)(delta.x / distance), (float)(delta.y / distance));
            float speed = (float)Math.Min(_speed, distance * StepMath.StepsPerSecond);
            return new MoveCommand(direction, speed);
        }
    }
}
