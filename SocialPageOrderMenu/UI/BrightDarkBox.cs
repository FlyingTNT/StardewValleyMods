using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;
using StardewValley;
using Microsoft.Xna.Framework;
using System;

namespace SocialPageOrderRedux.UI
{
    /// <summary>
    /// A lot of this is copied from OptionsCheckBox
    /// </summary>
    internal class BrightDarkBox : ClickableComponent
    {
        public bool isBright;

        private readonly string textureName;
        private Rectangle sourceRect;
        private readonly Action<bool> onClicked;

        public BrightDarkBox(string name, int x, int y, string textureName, Rectangle sourceRect, Action<bool> onClicked = null) : base(new Rectangle(x, y, sourceRect.Width * 4, sourceRect.Height * 4), name)
        {
            this.textureName = textureName;
            this.sourceRect = sourceRect;
            this.onClicked = onClicked;
        }

        public void receiveLeftClick(int x, int y)
        {
            if (!bounds.Contains(x, y))
            {
                return;
            }

            Game1.playSound("drumkit6");
            isBright = !isBright;

            // Call onClicked on the new value
            onClicked?.Invoke(isBright);
        }

        public void draw(SpriteBatch b)
        {
            b.Draw(Game1.content.Load<Texture2D>(textureName), new Vector2(bounds.X, bounds.Y), sourceRect, isBright ? Color.White : Color.DimGray, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.4f);
        }
    }
}
