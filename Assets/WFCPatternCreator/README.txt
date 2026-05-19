_______________________________________________________________________________________________________________________________

How to use the CozyWorldCreator plugin and things you need to know.

Important things to know:

1: When assigning tile neighbors, it must be all done from within the project window, not the heirarchy, you can use tiles dragged into the heirarchy for visual reference though.
2: On your assets import settings, you must enable Read/Write to allow mesh combining
3: If you want your tiles to have a bouncing effect as they spawn in, install the DOTweens package from the unity asset store
4: If you need help or have suggestions you can join the discord https://discord.gg/D4TV6sXpDE
5: My personal experience is that generation is about twice the speed in a build

_______________________________________________________________________________________________________________________________

How to use:

1: Create an empty GameObject and give it the WaveFunctionCollapse script
2: Create another empty GameObject and place the compass script on it to easily tell directions
3: Choose the objects you want to make a map with, give them the Tile script, and optionally use the TileRotator tool which you get from the Tools tab at the top
4: Once tile neighbors are done you can add them to the Tile Objects section of the WaveFunctionCollapse script
5: Press play

Extra: If you want, take a look at the zone templates folder and put one on the wave function object, itll help guide the generation into certain shapes.

_______________________________________________________________________________________________________________________________

Tile Neighbor assignment logic:

1: Only the tiles that you explicitly assign will spawn next to eachover
2: For example, if you wanted Trees to spawn above grass to grass, you would apply grass as a trees downward neighbor, and trees as grasses upward neighbor.
3: If you leave the applyReverse bool as true, it should automatically assign opposite neighbors, so assigning trees above grass would automatically assign grass below trees.

Important Note: All tile assigning logic should be done within the project window, not the heirarchy, you can drag tiles into the heirarchy for a visual reference though.

_______________________________________________________________________________________________________________________________

Examples:

1: Check the Sample Scenes to understand the basic setup of the tool
2: Check the Tile Examples to understand neighbor assignment

_______________________________________________________________________________________________________________________________