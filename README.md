## Overview
This is a very quick and badly made program that converts momentum zones to a stripper config allowing for creation of ammo zones for Rocket Jump without decompiling<br>
This **should** be only a temporary solution until a better way is implemented so it's unlikely I will be improving it in any way

## How to use
1. Using momentum's in-game zoning tool create a start zone and an end zone anywhere on the map. End zone might not be required but I didn't test that
2. Create checkpoints in places where ammo limit should be set to 4
3. Create cancel zones in places where ammo limit should be removed ( rockets set to infinite )
4. Save the zones and provide the path to the .json file to the program ( zoneToTrigger \<filepath\> )
5. Using [Lumper](https://github.com/momentum-mod/lumper) open the bsp and go to jobs tab
6. Add a stripper (file) job and provide a path to the created .cfg
7. Save the bsp
