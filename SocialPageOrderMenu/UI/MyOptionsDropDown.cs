using StardewValley.Menus;

namespace SocialPageOrderRedux.UI
{
    public class MyOptionsDropDown : OptionsDropDown
    {
        public MyOptionsDropDown(string label, int x = -1, int y = -1) : base(label, -1, x, y)
        {
        }
        public override void leftClickReleased(int x, int y)
        {
            base.leftClickReleased(x, y);
            ModEntry.CurrentSort = selectedOption;
            ModEntry.ResortSocialList();
        }


    }
}