using UnityEngine;

namespace Survivor.App
{
    /// Picks the startup window size (5.18, TAC-F8-5): the largest 16:9 candidate whose client area plus frame fits the work area.
    public static class WindowSizeSelector
    {
        /// Candidates from largest to smallest; the last one is also the fallback.
        public static readonly Vector2Int[] Candidates = { new Vector2Int(1920, 1080), new Vector2Int(1600, 900), new Vector2Int(1280, 720) };

        /// First candidate whose client size plus frame allowance fits the work area (inclusive); otherwise 1280x720 (clipping allowed).
        public static Vector2Int Choose(int workAreaWidth, int workAreaHeight, int frameAllowanceW = 16, int frameAllowanceH = 39)
        {
            for (int i = 0; i < Candidates.Length; i++)
            {
                Vector2Int size = Candidates[i];
                if (size.x + frameAllowanceW <= workAreaWidth && size.y + frameAllowanceH <= workAreaHeight)
                {
                    return size;
                }
            }

            return Candidates[Candidates.Length - 1];
        }
    }
}
