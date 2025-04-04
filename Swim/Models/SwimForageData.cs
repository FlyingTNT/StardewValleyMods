namespace Swim.Models
{
    public class SwimForageData
    {
        public const int VeryCommon = 25;
        public const int Common = 20;
        public const int Uncommon = 10;
        public const int Rare = 6;
        public const int VeryRare = 3;
        public const int Rarest = 1;

        /// <summary> A not-necessarily-qualified item id for the forage to spawn. </summary>
        public string ItemId = null;
        /// <summary> The hitpoints of the forage if it is a breakable rock, or -1 if not. </summary>
        public int Hp = -1;
        /// <summary> The </summary>
        public int Weight = 20;

        /// <summary> A unique identifier for this entry in the forage assets. Used by Content Patcher. </summary>
        public string Id = null;

        public SwimForageData() { }

        public SwimForageData(string itemId, int hp, int weight) 
        { 
            ItemId = itemId;
            Hp = hp;
            Weight = weight;
            Id = itemId;
        }
    }
}
