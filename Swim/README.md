**Swim** is a Stardew Valley mod that allows the player to swim in the various bodies of water throughout Stardew Valley.

## For Map Makers
### General
This mod determines which tiles may be swam in based on the presence of the 'water' tile or tile index property. The player transitions between walking and swimming by 'jumping' at the edge of a water-land boundary.  
In general, this mod will work perfectly fine with custom maps as long as there are impassible Buildings tiles to prevent the player from walking on any water they shouldn't be able to walk on. There may be some quirks when using numbered Buildings tiles, but I've tried my best to come up with a solution that works with as many maps as possible. 
### Jumping Details
By default, the player's jump distance is ~2 tiles.  
Jumping out of the water is blocked by Buildings tiles (ignoring Buildings1 and the such).  
Jumping into the water is blocked by Buildings tiles unless they have water tiles on all four sides. It is also blocked by Buildings1, Buildings-1... tiles if there is *not* an impassable buildings tile between them and the player (as it it somewhat common to use Buildings1 tiles to make bridges over water). 
### Collission
While swimming, the player is considered to be colliding if not all four of the corners of their hitbox are in tiles with the 'water' property on the back layer. The one exception to this is that if one or two corners are out of bounds, only one corner needs to be in water for the player to not be colliding.
### Disabling Swimming
If a location has the ```ProhibitSwimming``` property (any value), this mod will be disabled in that location.  
Additionally, this mod will be disabled if the player walks through a tile with the ```PoolEntrance``` or ```ChangeIntoSwimsuit``` properties as this mod doesn't work with ```PoolEntrance``` areas if the accessable tiles don't all have the water property.
## For Developers
### API
Swim's API can be found in [ISwimModAPI](Swim/ISwimModAPI.cs).
### Content Patcher
Swim adds a couple of resources that can be modified with content patcher.
#### Ocean Forage Items
Path: ```FlyingTNT.Swim/OceanForage```  
This resource defines what forage items spawn in underwater maps with the ```OceanForage``` property and at what rates. It should generally contain ocean-themed items.
It is a list of models with the following fields:  
```ItemId``` (string): The item id of the forage.  
```Hp``` (integer): -1 for forage items, or the hp of the stone for breakable stones.  
```Weight``` (integer): How much to weight the item when spawning it. I have used 20-25 for the most common items and 1-3 for the rarest.  
```Id``` (string): An identifier for the entry in the list. We don't use this, but it is used by CP.  
#### Minerals
Path: ```FlyingTNT.Swim/Minerals```  
This resource defines what forage items or breakable rocks spawn in underwater maps with the ```Minerals``` property and at what rates. It should generally contain items you might find in the mines.  
It is a list of models with the same format as Ocean Forage Items.
