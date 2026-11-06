using System;
using System.Globalization;

namespace Survivor.Data
{
    /// Holds the current GameData; hot reload swaps it atomically at a step boundary (the SwapData command at Begin is LOOP's).
    /// Readers that look up data on every use read Current; owned/active objects keep the definition they were created with (5c.3 #10).
    public sealed class GameDataRegistry
    {
        private bool _initialized;

        /// Current data set; null until the first playable load.
        public GameData Current { get; private set; }

        /// Report of the latest Initialize or TrySwap call (kept even when it was refused).
        public DataValidationReport LastReport { get; private set; }

        /// Raised after a successful TrySwap with the new data.
        public event Action<GameData> Swapped;

        /// Boot only, once: always stores the report, sets Current only when report.IsPlayable. Throws InvalidOperationException on a second call.
        public void Initialize(GameData data, DataValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (_initialized)
            {
                throw new InvalidOperationException("GameDataRegistry.Initialize must be called only once (boot).");
            }

            _initialized = true;
            LastReport = report;
            if (report.IsPlayable)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }

                Current = data;
            }
        }

        /// Hot reload: replaces Current and raises Swapped when report.IsPlayable; otherwise keeps the existing data and returns false (TAC-F11-5). Stores the report either way.
        public bool TrySwap(GameData data, DataValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (!_initialized)
            {
                throw new InvalidOperationException("GameDataRegistry.TrySwap requires Initialize first.");
            }

            LastReport = report;
            if (!report.IsPlayable)
            {
                DataLog.Warning("핫 리로드 거부: 새 데이터가 플레이 불가(차단 " + report.BlockingCount.ToString(CultureInfo.InvariantCulture)
                    + "건)라 기존 데이터를 유지합니다.");
                return false;
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Current = data;
            DataLog.Info("핫 리로드 적용: 무기 " + data.Weapons.Count.ToString(CultureInfo.InvariantCulture)
                + ", 몬스터 " + data.Monsters.Count.ToString(CultureInfo.InvariantCulture)
                + ", 경고 " + report.WarningCount.ToString(CultureInfo.InvariantCulture));
            Swapped?.Invoke(data);
            return true;
        }
    }
}
