# BetterWeight
A rimworld mod that makes weight *better*

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
Requires: [HugsLib](https://github.com/UnlimitedHugs/RimworldHugsLib) in order to run
Reccomends: [Minify Everything](https://github.com/erdelf/MinifyEverything) to make use of the mod. Other similar mods also work

## Compatibility
Tested to work on rimworld version 1.1  
1.0 should be supported  
Royalty should be supported  

### Known Bugs
You must restart the game after launching in order for the weights to upate (Eg. after enabling a new mod or installing this mod for the first time)
