using NUnit.Framework;
using Survivor.App;
using UnityEngine;

namespace Survivor.Tests.EditMode.App
{
    /// WindowSizeSelector.Choose table (5.18, TAC-F8-5): largest 16:9 candidate whose client size + 16x39 frame fits, else 1280x720.
    public sealed class WindowSizeSelectorTests
    {
        [TestCase(1920, 1040, 1600, 900, TestName = "Choose_1080pWithTaskbar_1600x900")]
        [TestCase(1920, 1032, 1600, 900, TestName = "Choose_1080pWithTallTaskbar_1600x900")]
        [TestCase(2560, 1392, 1920, 1080, TestName = "Choose_1440p_1920x1080")]
        [TestCase(1280, 680, 1280, 720, TestName = "Choose_SmallerThan720p_FallsBackTo1280x720")]
        [TestCase(1936, 1119, 1920, 1080, TestName = "Choose_ExactFit1920_1920x1080")]
        [TestCase(1935, 1119, 1600, 900, TestName = "Choose_OnePixelNarrow_1600x900")]
        [TestCase(1936, 1118, 1600, 900, TestName = "Choose_OnePixelShort_1600x900")]
        [TestCase(1616, 939, 1600, 900, TestName = "Choose_ExactFit1600_1600x900")]
        [TestCase(1615, 939, 1280, 720, TestName = "Choose_OnePixelNarrowOf1600_1280x720")]
        [TestCase(1296, 759, 1280, 720, TestName = "Choose_ExactFit1280_1280x720")]
        [TestCase(1536, 816, 1280, 720, TestName = "Choose_Laptop125PercentLogical_1280x720")]
        [TestCase(3840, 2112, 1920, 1080, TestName = "Choose_4k_1920x1080")]
        [TestCase(0, 0, 1280, 720, TestName = "Choose_ZeroWorkArea_1280x720")]
        public void Choose_Table(int workW, int workH, int expectedW, int expectedH)
        {
            Assert.AreEqual(new Vector2Int(expectedW, expectedH), WindowSizeSelector.Choose(workW, workH));
        }

        [Test]
        public void Choose_HonoursCustomFrameAllowance()
        {
            Assert.AreEqual(new Vector2Int(1920, 1080), WindowSizeSelector.Choose(1920, 1080, 0, 0), "no frame: 1920x1080 fits exactly");
            Assert.AreEqual(new Vector2Int(1600, 900), WindowSizeSelector.Choose(1920, 1080, 1, 0));
        }

        [Test]
        public void Candidates_AreThe16By9SizesFromLargest()
        {
            CollectionAssert.AreEqual(new[] { new Vector2Int(1920, 1080), new Vector2Int(1600, 900), new Vector2Int(1280, 720) },
                WindowSizeSelector.Candidates);
            foreach (Vector2Int size in WindowSizeSelector.Candidates)
            {
                Assert.AreEqual(size.x * 9, size.y * 16, size + " is 16:9");
            }
        }
    }
}
