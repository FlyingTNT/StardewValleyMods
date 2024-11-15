**Longer Seasons** is a Stardew Valley mod that allows you to change the number of days per season and add multiple months to each season.

## For players
### Config
#### Normal Config Options
 - **Extend Berry Seasons**: Will allow the berry bushes to grow and the bookseller to come in the extended parts of the seasons.
 - **Distribute Birthdays**: Will distribute the NPCs' birthdays throughought the extended seasons. They will still be in the same places relative to the length of the season.
 - **Avoid Birthday Overlap**: Will prevent multiple birthdays from being placed on the same day.
 - **Days per Month**: Lets you set the number of days per month.
 - **Months per {season}**: Lets you set the number of months in each season. Adding multiple months basically just makes a season repeat.
#### Advanced Billboard Options
Longer Seasons attempts to add support for mods that edit the billboard texture. To achieve this, it procedurally generates the new billboard textures by erasing the old numbers and then adding on the new numbers. Normally, it gets the new numbers from its mod files, but these options allow it to pull them from the old billboard texture. IDK if that's something anyone needs. I just added it :)
 - **Get Numbers from Billboard**: If disabled, will just get the numbers from the mod file and none of the below options will have any effect.
 - **Number Width/Height**: The width/height of the numbers in the billboard, in pixels.
 - **{number} x/y Offset**: The number of pixels that number is offset from the top-left corner of its respective spot in the billboard texture. 

### Multiplayer
Longer Seasons should work in multiplayer. It will automatically sync the number of days per month, number of months per season, and current season month. The days per month and months per season will be defined by the host's config.

## For developers
### CP Tokens
Starting in version 1.2.0, Longer Seasons adds three Content Patcher tokens:
 - DaysPerMonth: Returns the number of days in each month.
 - MonthsInCurrentSeason: Returns the number of months in the current season.
 - CurrentSeasonMonth: Returns the current month number. This is one-indexed.<br>

You might use this query to get the overall day in the season:
```
({{Day}} + (({{FlyingTNT.LongerSeasons/CurrentSeasonMonth}} - 1) * {{FlyingTNT.LongerSeasons/DaysPerMonth}}))
```
Or this query to get the total number of days per seaon:
```
({{FlyingTNT.LongerSeasons/MonthsInCurrentSeason}} * {{FlyingTNT.LongerSeasons/DaysPerMonth}})
```
### API
Starting in version 1.2.0, Longer Seasons has an API.
You can find it at [LongerSeasons/ILongerSeasonsAPI.cs](LongerSeasons/ILongerSeasonsAPI.cs)
