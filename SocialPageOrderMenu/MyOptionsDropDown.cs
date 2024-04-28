using StardewValley.Menus;

namespace SocialPageOrderRedux
{
    public class MyOptionsDropDown : OptionsDropDown
    {
        public MyOptionsDropDown(string label, int whichOption, int x = -1, int y = -1) : base(label, whichOption, x, y)
        {
        }
		public override void leftClickReleased(int x, int y)
		{
			base.leftClickReleased(x, y);
            ModEntry.currentSort.Value = selectedOption;
            ModEntry.ResortSocialList();
        }
	}
}