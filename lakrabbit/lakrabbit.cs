//-------------------------------------------------------------------
// LakRabbit for Tribes 1.11 - loader
// The engine executes this file when you start with "-mod lakrabbit"
// (console.cs ExecModScripts). This occurs before createServer() executes
// Player, Item and Admin again, and before each .mis file executes dm.cs.
// T1 has no packages. Thus Server::finishMissionLoad executes the game file
// after each mission script.
//-------------------------------------------------------------------

$Console::logMode = 1;  // Write console.log in the install folder. The server window is hidden.
$MODInfo = "LakRabbit (T2 port)";
$LakRabbit::FirstMission = "Peekaboo";
echo("LakRabbit: loader executed");

// LakRabbit uses only Deathmatch maps. This function removes all other maps
// from the vote list, the admin list and the rotation.
// missionList.cs makes the full list before this file executes.
function LakRabbit::filterMissionList()
{
   for(%t = 1; %t < $MLIST::TypeCount; %t++)
      if($MLIST::Type[%t] == "Deathmatch")
         break;
   if(%t >= $MLIST::TypeCount)
   {
      echo("LakRabbit: no Deathmatch missions found");
      return;
   }
   %list = $MLIST::MissionList[%t];
   $MLIST::TypeCount = 2;
   $MLIST::Type[1] = "LakRabbit";
   $MLIST::MissionList[1] = %list;
   $MLIST::MissionList[0] = %list;
   for(%i = 0; (%m = getWord(%list, %i)) != -1; %i++)
      $LakRabbit::IsMap[$MLIST::EName[%m]] = 1;
   MissionList::initNextMission();
}
LakRabbit::filterMissionList();

// Copy of base server.cs Server::loadMission.
// Added: if the map is not a LakRabbit map, load the first LakRabbit map.
function Server::loadMission(%missionName, %immed)
{
   if($loadingMission)
      return;

   if(!$LakRabbit::IsMap[%missionName])
   {
      echo("LakRabbit: " @ %missionName @ " is not a LakRabbit map, loading " @ $LakRabbit::FirstMission);
      %missionName = $LakRabbit::FirstMission;
   }

   %missionFile = "missions\\" $+ %missionName $+ ".mis";
   if(File::FindFirst(%missionFile) == "")
   {
      %missionName = $firstMission;
      %missionFile = "missions\\" $+ %missionName $+ ".mis";
      if(File::FindFirst(%missionFile) == "")
      {
         echo("invalid nextMission and firstMission...");
         echo("aborting mission load.");
         return;
      }
   }
   echo("Notfifying players of mission change: ", getNumClients(), " in game");
   for(%cl = Client::getFirst(); %cl != -1; %cl = Client::getNext(%cl))
   {
      Client::setGuiMode(%cl, $GuiModeVictory);
      %cl.guiLock = true;
      %cl.nospawn = true;
      remoteEval(%cl, missionChangeNotify, %missionName);
   }

   $loadingMission = true;
   $missionName = %missionName;
   $missionFile = %missionFile;
   $prevNumTeams = getNumTeams();

   deleteObject("MissionGroup");
   deleteObject("MissionCleanup");
   deleteObject("ConsoleScheduler");
   resetPlayerManager();
   resetGhostManagers();
   $matchStarted = false;
   $countdownStarted = false;
   $ghosting = false;

   resetSimTime(); // deal with time imprecision

   newObject(ConsoleScheduler, SimConsoleScheduler);
   if(!%immed)
      schedule("Server::finishMissionLoad();", 18);
   else
      Server::finishMissionLoad();
}

// Copy of base server.cs Server::finishMissionLoad.
// Added: execute LakRabbitGame.cs immediately after the mission file.
// Thus the LakRabbit functions replace the dm.cs functions for each mission.
function Server::finishMissionLoad()
{
   $loadingMission = false;
	$TestMissionType = "";
   // instant off of the manager
   setInstantGroup(0);
   newObject(MissionCleanup, SimGroup);

   exec($missionFile);
   exec("LakRabbitGame.cs");
   $Game::missionType = "LakRabbit";
   Mission::init();
	Mission::reinitData();
   if($prevNumTeams != getNumTeams())
   {
      // loop thru clients and setTeam to -1;
      messageAll(0, "New teamcount - resetting teams.");
      for(%cl = Client::getFirst(); %cl != -1; %cl = Client::getNext(%cl))
         GameBase::setTeam(%cl, -1);
   }

   $ghosting = true;
   for(%cl = Client::getFirst(); %cl != -1; %cl = Client::getNext(%cl))
   {
      if(!%cl.svNoGhost)
      {
         %cl.ghostDoneFlag = true;
         startGhosting(%cl);
      }
   }
   if($SinglePlayer)
      Game::startMatch();
   else if($Server::warmupTime && !$Server::TourneyMode)
      Server::Countdown($Server::warmupTime);
   else if(!$Server::TourneyMode)
      Game::startMatch();

   // LakRabbit is always free-for-all.
   $teamplay = false;
   purgeResources(true);

   // make sure the match happens within 5-10 hours.
   schedule("Server::CheckMatchStarted();", 3600);
   schedule("Server::nextMission();", 18000);

   return "True";
}
