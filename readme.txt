This repo contains various C# files for extracting from or transforming game archives or assets.

It's not one whole app, but a collection of code of v


* Everblue (PS2) - Code to dump the synthesis table from the PS2 exe, and the item table from PCSX2 (this part probably won't work for you without finding the proper address in memory on your computer). 

* Everblue.xls - The finished dump of the synthesis table, item, buy/sell prices, how diving experience works, chests and items in ships, all for both the PAL/EU version and the JP version. Also contains a not-quite-completed speedrun route. 

* Hooters Road Trip (PS1) - Code to explode the main archive into its constituent files

* Jagged Alliance 1 /Deadly Games - Some code to extract assets from the game archives, lots more code to turn the audio files and premade videos into these two youtube videos
https://www.youtube.com/watch?v=p6fRdjjYOOY
https://www.youtube.com/watch?v=lssdkrbQrTQ

* Kula World - Code to extract the level files from each PAK

* Taito Legends 2 (PS2) - Code to explode the gamebin.gzh files into the constituent files

* WWF Wrestlemania The Arcade Game (Arcade) - Code to convert the IMG files in the Arcade version source code (https://github.com/historicalsource/wwf-wrestlemania) to PNG. Note, that I couldn't figure out how the game knows which palette goes with which image, so for IMG files containing multiple palettes, each image is dumped once with each palette.