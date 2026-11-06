using System;

namespace Survivor.Core
{
    /// Single owner of the nine collision channels and their A/B layers (rule R5).
    public static class CollisionChannelTable
    {
        /// Number of channels (CollisionChannel values 0..8).
        public const int ChannelCount = 9;

        // Indexed by (int)CollisionChannel; the A layer is the first layer of the channel name.
        private static readonly CollisionLayer[] ALayers =
        {
            CollisionLayer.PlayerProjectile, CollisionLayer.PlayerOrb, CollisionLayer.PlayerZone,
            CollisionLayer.PlayerBody, CollisionLayer.EnemyProjectile, CollisionLayer.EnemyZone,
            CollisionLayer.PlayerMagnet, CollisionLayer.PlayerBody, CollisionLayer.PlayerBody,
        };

        private static readonly CollisionLayer[] BLayers =
        {
            CollisionLayer.Monster, CollisionLayer.Monster, CollisionLayer.Monster,
            CollisionLayer.Monster, CollisionLayer.PlayerBody, CollisionLayer.PlayerBody,
            CollisionLayer.Gem, CollisionLayer.Gem, CollisionLayer.Chest,
        };

        /// Finds the channel for a layer pair in either order; xIsA tells whether x is the A side. False when the pair has no channel.
        public static bool TryGetChannel(CollisionLayer x, CollisionLayer y, out CollisionChannel ch, out bool xIsA)
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                if (ALayers[i] == x && BLayers[i] == y)
                {
                    ch = (CollisionChannel)i;
                    xIsA = true;
                    return true;
                }

                if (ALayers[i] == y && BLayers[i] == x)
                {
                    ch = (CollisionChannel)i;
                    xIsA = false;
                    return true;
                }
            }

            ch = default;
            xIsA = false;
            return false;
        }

        /// Returns the A-side and B-side layers of a channel; throws ArgumentOutOfRangeException for an undefined channel.
        public static void GetLayers(CollisionChannel channel, out CollisionLayer a, out CollisionLayer b)
        {
            int i = (int)channel;
            if (i < 0 || i >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown CollisionChannel.");
            }

            a = ALayers[i];
            b = BLayers[i];
        }
    }
}
