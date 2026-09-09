using Microsoft.Xna.Framework.Graphics;

namespace Murder.Core.Graphics
{
    /// <summary>
    /// Shared resources used per game instance.
    /// TODO: Move to RenderContext?
    /// </summary>
    public static class SharedResources
    {
        /// Textures 2D do not need to be created for each spritebatch, only for different graphics devices.
        private static Texture2D _pixel = null!;
        
        public static readonly BlendState MaskBlendState = new()
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.SourceAlpha,

            AlphaBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.SourceAlpha
        };

        public static readonly BlendState SourceInBlendState = new()
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorSourceBlend = Blend.DestinationAlpha,
            ColorDestinationBlend = Blend.Zero,

            AlphaBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.DestinationAlpha,
            AlphaDestinationBlend = Blend.Zero
        };

        public static Texture2D GetOrCreatePixel()
        {
            if (_pixel == null)
            {
                _pixel = CreatePixel(Color.White);
            }

            return _pixel;
        }

        /// <summary>
        /// Creates a new 1x1 pixel texture with a given color
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static Texture2D CreatePixel(Color color)
        {
            var pixel = new Texture2D(Game.GraphicsDevice, 1, 1);
            pixel.Name = "White Pixel";
            pixel.SetData(new Microsoft.Xna.Framework.Color[] { color });

            return pixel;
        }
    }
}