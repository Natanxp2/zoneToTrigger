## Overview
This is a very quick and badly made program that converts momentum zones to a stripper config allowing for creation of triggers without decompiling<br>
This **should** be only a temporary solution until a better way is implemented so it's unlikely I will be improving it in any way<br><br>
ONLY BOX ZONES ARE SUPPORTED. CREATING POLYGONAL TRIGGERS IS NOT POSSIBLE WITH THIS METHOD

## How to use
1. Using momentum's in-game zoning tool create a start zone and an end zone anywhere on the map. End zone might not be required but I didn't test that
2. Create checkpoints and cancel zones where desired triggers should be (according to chosen .ini)
3. Save the zones and run the program ( zoneToTrigger \<config file name\> \<.json filepath\>)
4. Using [Lumper](https://github.com/momentum-mod/lumper) open the bsp and go to jobs tab
5. Add a stripper (file) job, provide a path to the created .cfg and run it
6. Save the bsp
