using System;
using UnityEngine;

namespace Survivor.App
{
    /// Endless background (TAC-F8-11, [U29]): a Tiled SpriteRenderer larger than the view that follows the camera snapped to
    /// multiples of the tile size, so the pattern looks fixed in the world. Runs after CameraFollow.
    [DefaultExecutionOrder(100)]
    public sealed class BackgroundTiler : MonoBehaviour
    {
        /// World size of one checker tile (two squares per side).
        public const float TileSize = 2f;

        private const int TexturePixels = 32;
        private const int SortingOrder = -1000;

        private Transform _camera;
        private SpriteRenderer _renderer;

        /// World size the tiled renderer covers (view plus one tile of margin on every side).
        public Vector2 CoveredSize => _renderer != null ? _renderer.size : Vector2.zero;

        /// Builds the tiled renderer covering viewSize (world units) and follows cameraTransform; null throws ArgumentNullException.
        public void Initialize(Transform cameraTransform, Vector2 viewSize)
        {
            _camera = cameraTransform ? cameraTransform : throw new ArgumentNullException(nameof(cameraTransform));
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = CreateTileSprite();
            _renderer.drawMode = SpriteDrawMode.Tiled;
            _renderer.tileMode = SpriteTileMode.Continuous;
            _renderer.sortingOrder = SortingOrder;
            float width = Mathf.Ceil(viewSize.x / TileSize) * TileSize + 2f * TileSize;
            float height = Mathf.Ceil(viewSize.y / TileSize) * TileSize + 2f * TileSize;
            _renderer.size = new Vector2(width, height);
            Snap();
        }

        private void LateUpdate()
        {
            Snap();
        }

        private void Snap()
        {
            if (_camera == null)
            {
                return;
            }

            Vector3 c = _camera.position;
            float x = Mathf.Round(c.x / TileSize) * TileSize;
            float y = Mathf.Round(c.y / TileSize) * TileSize;
            transform.position = new Vector3(x, y, 0f);
        }

        // A two-tone checker of 2x2 squares spanning TileSize world units (FullRect mesh is required for Tiled draw mode).
        private static Sprite CreateTileSprite()
        {
            var texture = new Texture2D(TexturePixels, TexturePixels, TextureFormat.RGBA32, false)
            {
                name = "BackgroundTile",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };
            var dark = new Color32(34, 40, 34, 255);
            var light = new Color32(42, 50, 42, 255);
            var pixels = new Color32[TexturePixels * TexturePixels];
            int half = TexturePixels / 2;
            for (int y = 0; y < TexturePixels; y++)
            {
                for (int x = 0; x < TexturePixels; x++)
                {
                    pixels[y * TexturePixels + x] = (x < half) == (y < half) ? dark : light;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, TexturePixels, TexturePixels), new Vector2(0.5f, 0.5f),
                TexturePixels / TileSize, 0, SpriteMeshType.FullRect);
        }
    }
}
