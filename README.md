# BetterWeight
A Rimworld mod that makes weight *better*

## Features
* Adds weights to all buildings that don't normally have them (Balances Minify Everything)
* Calculates weights for all buildings for all mods by using the weights of their components
* Option to determine how "efficiently" the buildings are made (Percentage mass lost to make the building as no construction is 100% efficient).
* Configurable settings menu for efficiency and rounding (Based on batteries the default should be a 55% efficiency)
* Made it work for doors and other buildings that can have multiple building materials (Assumes other materials weigh 1.00kg)

## Planned Features
* Allow each building to have a weight manually assigned
* Allow each building to have an efficiency manually assigned that may be different than the default

## Possible Features
* Make it work for all objects. Allow everything to have it's weight easily edited and this carry a ripple effect in order to make the game harder/easier
What if steel weighed double of what it does now? How would moving around the map with steel mortars be effected?

## Requirements
* Harmony 

Recommends:
* [Minify Everything](https://github.com/erdelf/MinifyEverything) to make use of the mod. Other similar mods also work

## Compatibility
Tested to work on rimworld version 1.1, 1.2, 1.3, 1.4, 1.5, 1.6  
1.0 is supported in previous versions but not the current build  
All DLC should be supported though I have only tested on Ideology/Royalty  

### Known Bugs
None

### Previously Fixed Bugs
- Pre v2.1.8 in the 1.5 version the default list of buildings to edit the mass of is incorrect
  - To fix you can just delete the `1.5` folder and rename the `1.6` folder to `1.5` or update to v2.1.8
- Pre v2.1.8 the mod incorrectly states that it requires `HugsLib`
  - v2.1.8 fixes this for 1.5 and 1.6
- Pre v2.1.8 versions the mod incorrectly includes `Harmony` in the distribution files
  - v2.1.8 fixes this for 1.5 and 1.6
