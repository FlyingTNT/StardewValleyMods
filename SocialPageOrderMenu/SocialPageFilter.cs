using Common.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using System;

namespace SocialPageOrderRedux
{
    internal class SocialPageFilter : ClickableComponent
    {
        public readonly ISocialPageFilter Filter;
        private readonly Action<bool> onClicked;
        private readonly PerScreen<bool> isFiltering;

        public bool IsEnabled => Filter.Enabled;

        public bool IsFiltering
        {
            get
            {
                return isFiltering.Value;
            }
            set
            {
                PerPlayerConfig.SaveConfigOption(Game1.player, $"FlyingTNT.SocialPageOrderRedux.{Filter.Name}", value);
                isFiltering.Value = value;
            }
        }
        public string HoverText => Filter.HoverText(IsFiltering);

        public SocialPageFilter(int x, int y, ISocialPageFilter filter, Action<bool> onClicked = null) : base(new Rectangle(x, y, filter.SourceRectangle.Width * 4, filter.SourceRectangle.Height * 4), filter.Name)
        {
            Filter = filter;
            this.onClicked = onClicked;
            this.isFiltering = new(() => PerPlayerConfig.LoadConfigOption(Game1.player, $"FlyingTNT.SocialPageOrderRedux.{Filter.Name}", defaultValue: false));
        }

        public void receiveLeftClick(int x, int y)
        {
            if (!bounds.Contains(x, y))
            {
                return;
            }

            Game1.playSound("drumkit6");
            IsFiltering = !IsFiltering;

            // Call onClicked on the new value
            onClicked?.Invoke(IsFiltering);
        }

        public void draw(SpriteBatch b)
        {
            b.Draw(Game1.content.Load<Texture2D>(Filter.TextureName), new Vector2(bounds.X, bounds.Y), Filter.SourceRectangle, !IsFiltering ? Color.White : Color.DimGray, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.4f);
        }
    }
}
