using System;
using NUnit.Framework;
using Survivor.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R04_VisualScaleTests
    {
        private Texture2D _texture;
        private Sprite _square;
        private Sprite _wide;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(200, 100);
            // 100 pixels per unit: square sprite = 1x1 units, wide sprite = 2x1 units.
            _square = Sprite.Create(_texture, new Rect(0, 0, 100, 100), new Vector2(0.5f, 0.5f), 100f);
            _wide = Sprite.Create(_texture, new Rect(0, 0, 200, 100), new Vector2(0.5f, 0.5f), 100f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_square);
            Object.DestroyImmediate(_wide);
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void Scale_MatchesDiameterToLargerSide()
        {
            Assert.AreEqual(1f, CircleMath.VisualScaleFor(_square, 0.5f), 1e-6f);
            Assert.AreEqual(4f, CircleMath.VisualScaleFor(_square, 2f), 1e-6f);
            Assert.AreEqual(1f, CircleMath.VisualScaleFor(_wide, 1f), 1e-6f);
            Assert.AreEqual(0f, CircleMath.VisualScaleFor(_square, 0f));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => CircleMath.VisualScaleFor(null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CircleMath.VisualScaleFor(_square, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CircleMath.VisualScaleFor(_square, float.NaN));
        }
    }
}
