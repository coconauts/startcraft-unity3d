
========= The A* Pathfinding Project =========

The A* Pathfinding Project is an out-of-the-box pathfinding system
which along with ease of use has a large amount of features and blazing fast pathfinding.

The system has a Free version and a Pro version, both can found on my website (see below) and the Pro version can also be found in the Unity Asset Store
	
Documentation for the system can be found at:
	http://www.arongranberg.com/astar/docs/

Documentation can also be found in the Documentation.zip file included in the project.

A Get Started Guide can be found here:
	http://www.arongranberg.com/astar/docs/getstarted.php
	(eventually updated URL can be found by adding the Pathfinder component to some GameObject and go to Inspector -> About -> Documentation)

The A* Pathfinding Project was made by Aron Granberg
	http://www.arongranberg.com/
	
The license is the AssetStore Free License and the AssetStore Commercial License respectively for the Free and Pro versions of the project.


Using:
JsonDotNet - An awesome Json library which is used for serialization (though a bit modified to get rid of the System.Xml dependency
	which before added 1mb to the player size, a few features have been added as well)
DotNetZip - For creating zip files from serialized data (also modified to get rid of System.File calls which are not allowed in the webplayer)
