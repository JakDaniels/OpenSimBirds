INonSharedRegion Module -  ability to control flocks of prims within an OpenSim scene.

To build from source:

Add OpenSimBirds source tree under opensim/addon-modules

e.g.

	cd ~/opensim/addon-modules
	git clone https://github.com/JakDaniels/OpenSimBirds.git

./runprebuild.sh against opensim root to build this module into the solution
then xbuild, or build within Visual Studio / Monodevelop to produce the binaries.

OpenSimBirds does not need a config in order to run, but without one it will not persist your bird flock settings from one
region restart to another!

The settings go in an .ini file in bin/addon-modules/OpenSimBirds/config/ *or* in your existing Regions.ini

OpenSimBirds has no external dependencies other than the dlls currently included in opensim.
The project generates a single dll - OpenSimBirds.Module.dll which is copied into opensim/bin as part of the build step


Configuration:

If you have the module added to or compiled with your OpenSim build then it will run a instance of itself once per region.
By default it will configure itself to some sensible defaults and will sit quietly in the background waiting for commands
from the console, or from in-world. It will not enable itself (i.e. rez some birds) until commanded to do so, or
configured to do so in the .ini file. It is possible to completely stop the module from doing anything (including
listening for commands), but you must have a particular setting in the .ini file for that region (see below) - in other
words, you must have a config for it!

To become active, the module needs to be enabled in the ini file or commanded to do so from inworld or the console.
Otherwise it does nothing on startup except listen for commands. If you are running multiple regions on one simulator you
can have different Birds settings per region in the configuration file, in the exact same way you can customize per Region
setting in Regions.ini

The configuration for this module can be placed in one of two places:

Option 1:

	bin/addon-modules/OpenSimBirds/config/OpenSimBirds.ini

This follows the similar format as a Regions.ini file, where you specify setting for
each region using the [Region Name] section heading. There is an example .ini file
provided which should be edited and copied to the correct place above.

Option 2:

	bin/Regions/Regions.ini

Add config parameters to the existing Regions.ini file under the appropriate region name heading.
The file is usually in the above location, but this could have been changed in OpenSim.ini via the
'regionload_regionsdir' parameter in the [Startup] section of OpenSim.ini


Here is an example config:

	;; Set the Birds settings per named region

	[Test Region 1]

	 BirdsModuleEnabled = True   ;this is the default and determines whether the module does anything
	 BirdsShowOnStartup = True   ;set to false to disable the birds from appearing in this region
	 BirdsFlockSize = 50         ;the number of birds to flock
	 BirdsMaxFlockSize = 100     ;the maximum flock size that can be created (keeps things sane)
	 BirdsMaxSpeed = 3           ;how far each bird can travel per update
	 BirdsMaxForce = 0.25        ;the maximum acceleration allowed to the current velocity of the bird
	 BirdsNeighbourDistance = 25 ;max distance for other birds to be considered in the same flock as us
	 BirdsDesiredSeparation = 20 ;how far away from other birds we would like to stay
	 BirdsTolerance = 5          ;how close to the edges of things can we get without being worried
	 BirdsBorderSize = 5         ;how close to the edge of a region can we get?
	 BirdsMaxHeight = 256        ;how high are we allowed to flock
	 BirdsUpdateEveryNFrames = 1 ;update bird positions every N simulator frames
	 BirdsPrim = SeaGull1        ;By default the module will create a flock of plain wooden spheres,
	                             ;however this can be overridden to the name of an existing prim that
	                             ;needs to already exist in the scene - i.e. be rezzed in the region.

         ;who is allowed to send commands via chat or script: list of UUIDs or ESTATE_OWNER or ESTATE_MANAGER
         ;or everyone if not specified
	 BirdsAllowedControllers = ESTATE_OWNER, ESTATE_MANAGER, 12345678-1234-1234-1234-123456789abc


Various runtime commands control the flocking module behaviour - described below. These can either be invoked
from the Console or in world by directing them to a chat channel either from the client's Local Chat or via a script.
You can specify which channel to use in the .ini:

	 BirdsChatChannel = 118      ;the chat channel to listen for Bird commands on



Runtime Commands:

The following commands, which can be issued on the Console or via in-world chat or scripted chat on the BirdsChatChannel
to control the birds at runtime:

	birds-stop or /118 stop                         ;stop all birds flocking
	birds-start or /118 start                       ;start all birds flocking
	birds-enable or /118 enable                     ;enable the flocking simulation if disabled and rez new birds
	birds-disable or /118 disable                   ;stop all birds and remove them from the scene
	birds-prim <name> or /118 prim <name>           ;change the bird prim to a prim already rezzed in the scene
	birds-stats or /118 stats                       ;show all the parameters and list the bird prim names and uuids
	birds-framerate <num> or /118 framerate <num>   ;only update the flock positions every <num> frames
	                                                ;only really useful for photography and debugging bird
	                                                ;behaviour


These commands are great for playing with the flock dynamics in real time:

	birds-size <num> or /118 size <num>             ;change the size of the flock
	birds-speed <num> or /118 speed <num>           ;change the maximum velocity each bird may achieve
	birds-force <num> or /118 force <num>           ;change the maximum force each bird may accelerate
	birds-distance <num> or /118 distance <num>     ;change the maximum distance that other birds are to be considered in the same flock as us
	birds-separation <num> or /118 separation <num> ;sets how far away from other birds we would like to stay
	birds-tolerance <num> or /118 tolerance <num>   ;sets how close to the edges of things can we get without being worried

Of course if distance is less than separation then the birds will never flock. The other way around and they will always
eventually form one or more flocks.

Security:

By default anyone can send commands to the module from within a script or via the in-world chat on the 'BirdsChatChannel' channel.
You should use a high negative value for channel if you want to allow script access, but not in-world chat. Further you can restrict
which users are allowed to control the module using the 'BirdsAllowedControllers' setting. This is a comma separated list of user UUIDs,
but it may also contain one of the pre-defined constants ESTATE_OWNER (evaluates to the UUID of the estate owner) and ESTATE_MANAGER
(evaluates to a list of estate manager UUIDS).

* For commands sent from in-world chat, it is the UUID of the avatar sending the command that is checked against the list.
* For commands sent from a script it is the UUID of the owner of the prim in which the script resides that is checked against the list.

Bird prims:

Any currently rezzed in-scene-object can be used as the bird prim. However fps is very much affected by the
complexity of the entity to use. It is easier to throw a single prim (or sculpty) around the scene than it is to
throw the constituent parts of a 200 linked prim dragon.

Tests show that <= 500 single prims can be flocked effectively - depending on system and network
However maybe <= 300 simple linksets can perform as well.

Network Traffic:

I tested the amount of network traffic generated by bird updates. 20 birds (each with 4 linked prims) takes up about 300kbps
in network position updates. 50 of the same birds generates about 750kbps traffic.
Each bird uses roughly 15kbps of network traffic. This is all measured using an update framerate of 1, i.e. birds' position
is updated every simulator frame.

Statistics:

The stats command in-world or via script returns data to BirdsChatChannel. The console command returns stats to the console.
All the the modules parameters are returned including a list of the active bird prims currently rezzed in the region,
and the UUIDs of those prims' root prim. Also included is a list of any avatar UUIDs that may be sitting on those prims. Here
is an example output:

	birds-started = False
	birds-enabled = True
	birds-prim = SeaGull1
	birds-framerate = 1
	birds-maxsize = 100
	birds-size = 20
	birds-speed = 1.5
	birds-force = 0.2
	birds-distance = 25
	birds-separation = 10
	birds-tolerance = 5
	birds-border = 5
	birds-prim0 = OpenSimBirds0 : 01abef79-7fb2-4c8d-831e-62ce1ce878f1 :
	birds-prim1 = OpenSimBirds1 : af85996d-af4d-4dda-bc89-721c51e09d0c :
	birds-prim2 = OpenSimBirds2 : ca766390-1877-4b19-a29e-4590cf40aece :
	birds-prim3 = OpenSimBirds3 : 6694bfa9-8e7f-4ac5-b336-ad13e5cfced2 :
	birds-prim4 = OpenSimBirds4 : 1c6b152d-dcca-4fef-8979-b7ccc8139e1e :
	birds-prim5 = OpenSimBirds5 : 08bba2cc-d427-4855-a7f0-57aa55109707 :
	birds-prim6 = OpenSimBirds6 : bbeb8b6d-28d8-41a9-b8ce-dab3173bd454 :
	birds-prim7 = OpenSimBirds7 : 45c73475-1f0f-487f-ac9f-87d30d0315e8 :
	birds-prim8 = OpenSimBirds8 : d5891cc8-c196-4b05-82ef-3c7d0f703963 :
	birds-prim9 = OpenSimBirds9 : 557b61e1-5fd6-4878-980e-e93cabcc078f :
	birds-prim10 = OpenSimBirds10 : 7ff2c02d-d73c-4e49-a4e9-84b652dc70a9 :
	birds-prim11 = OpenSimBirds11 : c2b0820c-ba20-4318-a0e8-ec6ad521f524 :
	birds-prim12 = OpenSimBirds12 : e8e87309-7a47-4983-89a1-4bb11d05a40c :
	birds-prim13 = OpenSimBirds13 : a351e0e3-ae99-48b8-877d-65156f437b33 :
	birds-prim14 = OpenSimBirds14 : 150f1c3b-e9d9-4cda-9e03-69fb5286e436 :
	birds-prim15 = OpenSimBirds15 : ebf63de1-d419-45d0-8eee-3db14295e401 :
	birds-prim16 = OpenSimBirds16 : faad97af-4ee6-425c-b221-99ef53650e93 :
	birds-prim17 = OpenSimBirds17 : d75ba544-bbc2-4f5a-9d7e-00e21ed6f191 :
	birds-prim18 = OpenSimBirds18 : b91e42cb-ae5b-4f03-bf6e-dc03d52858b7 : 19cc284d-11c6-4ee7-a8de-f69788d08434
	birds-prim19 = OpenSimBirds19 : 44aa3e14-56bc-43dd-afbd-7348c5dfe3a5 :

In the above example, there is one avatar sitting on bird-prim18. For more than one avatar the UUID list will be separated by spaces.

Please Note:

This module is currently only tested against opensim master.

Licence: all files released under a BSD licence
If you have any question please contact Jak Daniels, jak@ateb.co.uk
If you do fork or modify this project for the better, please let me know!! I would be interested to incorporate any enhancements you may make.

