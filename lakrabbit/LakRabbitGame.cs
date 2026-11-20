//-------------------------------------------------------------------
// LakRabbit for Tribes 1.11 - game rules
// This file is a port of the Tribes 2 LakRabbitGame.cs (v3.40).
// lakrabbit.cs executes this file after each mission file. Thus these
// functions replace the functions with the same names in dm.cs, game.cs,
// player.cs, item.cs and admin.cs.
// T1 has no parent::. Each replaced function is a copy of the 1.11 stock
// function, with the LakRabbit lines added.
// Units: time in seconds (T1 getSimTime), speed in kph, distance in m.
//-------------------------------------------------------------------

//--- host settings (these replace the T2 $Host::LakRabbit* settings) -----
if($LakRabbit::DuelMode == "")       $LakRabbit::DuelMode = 0;
if($LakRabbit::NoSplashDamage == "") $LakRabbit::NoSplashDamage = 0;
if($LakRabbit::PubPro == "")         $LakRabbit::PubPro = 0;
if($LakRabbit::UnlimitedDJ == "")    $LakRabbit::UnlimitedDJ = 0;
if($LakRabbit::SlapDropsFlag == "")  $LakRabbit::SlapDropsFlag = 0; // T2 is on. Default: drop only on death.
if($LakRabbit::ShowFlagTask == "")   $LakRabbit::ShowFlagTask = 0; // T2 default is off.
if($LakRabbit::Debug == "")          $LakRabbit::Debug = 0;
$LakRabbit::ScoreLimit     = 2000;
$LakRabbit::FlagReturnTime = 25;
$LakRabbit::SpawnRadius    = 250;   // T2 used a box of +-150 m around the rabbit or the flag.
$LakRabbit::SlapSpeed      = 120;   // Adjust for play. T2 used an impulse of 25000.
$LakRabbit::TossScale      = 1.36;  // T2 flag toss: impulse 75 / flag mass 55.
$LakRabbit::TickTime       = 0.1;   // Time between history samples (s).
$LakRabbit::HistSize       = 30;    // Number of samples (3 s of history).

// Stock T1 client sounds. T1 does not have the T2 sound files.
$LakSnd::Bonus   = "~wbuysellsound.wav";
$LakSnd::Special = "~wflagcapture.wav";
$LakSnd::Take    = "~wflag1.wav";
$LakSnd::Drop    = "~wflagflap.wav";
$LakSnd::Return  = "~wflagreturn.wav";
$LakSnd::Warn    = "~wLeftMissionArea.wav";

// LT loadout. Source: LT.cs in base\missions\LT.vol.
$spawnBuyList[0] = LightArmor;
$spawnBuyList[1] = EnergyPack;
$spawnBuyList[2] = GrenadeLauncher;
$spawnBuyList[3] = Chaingun;
$spawnBuyList[4] = Disclauncher;
$spawnBuyList[5] = Grenade;
$spawnBuyList[6] = RepairKit;
$spawnBuyList[7] = Beacon;
$spawnBuyList[8] = TargetingLaser;
$spawnBuyList[9] = "";

// Damage of a direct hit and muzzle speed, for each damage type.
// The T2 rules use the percent of a direct hit (98 = direct hit, 25 = glancing hit).
// In T1, disc and plasma cause radius damage. They explode at the surface of
// the target. Thus a direct hit does not cause the full damageValue.
// The disc and plasma values are direct hits that the laktest bot-fire tests
// measured on Peekaboo: disc 0.4206 of 0.5, plasma 0.3375 of 0.45.
// The other values are from baseProjData.cs.
$LakBase[$ExplosionDamageType] = 0.42;  $LakMuzzle[$ExplosionDamageType] = 65;
$LakBase[$ShrapnelDamageType]  = 0.4;   $LakMuzzle[$ShrapnelDamageType]  = 40;
$LakBase[$MortarDamageType]    = 1.0;   $LakMuzzle[$MortarDamageType]    = 40;
$LakBase[$PlasmaDamageType]    = 0.3375; $LakMuzzle[$PlasmaDamageType]   = 55;
$LakBase[$BlasterDamageType]   = 0.125; $LakMuzzle[$BlasterDamageType]   = 200;
$LakBase[$BulletDamageType]    = 0.11;  $LakMuzzle[$BulletDamageType]    = 425;
$LakBase[$MineDamageType]      = 0.65;
$LakSplashType[$ExplosionDamageType] = 1;
$LakSplashType[$ShrapnelDamageType]  = 1;
$LakSplashType[$MortarDamageType]    = 1;
$LakSplashType[$PlasmaDamageType]    = 1;
$LakSplashType[$MineDamageType]      = 1;

//--- small helpers --------------------------------------------------
function lakAbs(%x)
{
   if(%x < 0)
      return -%x;
   return %x;
}

function lakVec(%x, %y, %z)
{
   return %x @ " " @ %y @ " " @ %z;
}

function lakAdd(%a, %b)
{
   return lakVec(getWord(%a,0)+getWord(%b,0), getWord(%a,1)+getWord(%b,1), getWord(%a,2)+getWord(%b,2));
}

function lakScale(%a, %s)
{
   return lakVec(getWord(%a,0)*%s, getWord(%a,1)*%s, getWord(%a,2)*%s);
}

function lakLen(%a)
{
   return Vector::getDistance(%a, "0 0 0");
}

function lakSpeed(%obj)
{
   return floor(lakLen(Item::getVelocity(%obj)) * 3.6);
}

function lakPlural(%n)
{
   if(%n == 1)
      return "";
   return "s";
}

function lakAlive(%pl)
{
   return %pl != -1 && %pl != "" && getObjectType(%pl) == "Player" && !Player::isDead(%pl);
}

// Distance from the feet of the player down to the ground. Maximum is %range.
// T1 has no terrain-height function and no free raycast. getLOSInfo accepts
// a rotation (see ai.cs). This function tries pitch -90 and +90 degrees and
// keeps the hit below the player. The laktest tests examine this function.
function lakGroundDist(%pl, %range)
{
   %z = getWord(GameBase::getPosition(%pl), 2);
   %best = %range;
   for(%i = 0; %i < 2; %i++)
   {
      if(%i == 0) %rot = "-1.5708 0 0";
      else        %rot = "1.5708 0 0";
      if(GameBase::getLOSInfo(%pl, %range + 2, %rot))
      {
         %d = %z - getWord($los::position, 2);
         if(%d >= -0.5 && %d < %best)
            %best = %d;
      }
   }
   if(%best < 0)
      %best = 0;
   return %best;
}

function lakIsMA(%pl)
{
   return lakGroundDist(%pl, 6) >= 6;
}

function lakHeight(%pl)
{
   return floor(lakGroundDist(%pl, 500));
}

// List of human clients and registered bots.
// In 1.11, Client::getFirst/getNext do not include AI clients, and AI client
// ids do not keep script fields. Thus LakRabbit keeps the data for each
// client in $LakPC[client, field].
function lakClientList()
{
   %list = "";
   for(%cl = Client::getFirst(); %cl != -1; %cl = Client::getNext(%cl))
      %list = %list @ %cl @ " ";
   return %list @ $LakRabbit::Bots;
}

function LakRabbit::addBot(%cl)
{
   $LakRabbit::Bots = $LakRabbit::Bots @ %cl @ " ";
   LakRabbit::resetScore(%cl);
}

// Random percent test. Tests set $LakTest::Roll to get a fixed result
// (0 = always true, 100 = never true).
function lakRoll(%chance)
{
   if($LakTest::Roll != "")
      return $LakTest::Roll < %chance;
   return getRandom() * 100 < %chance;
}

function lakPlayingPlayers()
{
   %n = 0;
   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
      if(lakAlive(Client::getOwnedObject(%cl)))
         %n++;
   return %n;
}

function lakNumClients()
{
   %n = 0;
   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
      %n++;
   return %n;
}

//--- history of each player (this replaces T2 projectile.shotFrom) -----
// One loop for all players, 10 times each second. The loop also operates
// the duel timer (1 s), the rabbit score update (5 s) and the rabbit
// waypoint (24 s).
function LakRabbit::tick(%seq)
{
   if(%seq != $LakRabbit::Seq)
      return;
   schedule("LakRabbit::tick(" @ %seq @ ");", $LakRabbit::TickTime);
   $LakRabbit::Ticks++;

   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
   {
      %pl = Client::getOwnedObject(%cl);
      if(lakAlive(%pl))
      {
         %i = ($LakHI[%cl] + 1) % $LakRabbit::HistSize;
         $LakHI[%cl] = %i;
         $LakH[%cl, %i] = getBoxCenter(%pl);
         $LakV[%cl, %i] = lakSpeed(%pl);
         if($LakHN[%cl] < $LakRabbit::HistSize)
            $LakHN[%cl]++;
      }
   }

   if($LakRabbit::Ticks % 10 == 0)
      LakRabbit::duelTick();
   if($LakRabbit::Ticks % 50 == 0 && $LakRabbitCl != "")
      Game::refreshClientScore($LakRabbitCl);
   if($LakRabbit::ShowFlagTask && $LakRabbit::Ticks % 240 == 0)
      LakRabbit::showRabbitWaypoint();
   if($LakRabbit::ShowFlagTask && $LakRabbit::Ticks % 240 == 60)
      LakRabbit::hideRabbitWaypoint();
}

function LakRabbit::clearHistory(%cl)
{
   $LakHN[%cl] = 0;
}

// Find the position and speed of the shooter at the time of the shot.
// The function uses the history sample whose age is nearest to the flight
// time from that sample to the hit point.
// Result: $LakFrom (position) and $LakFromVel (speed).
function LakRabbit::shotOrigin(%cl, %hitPos, %muzzle)
{
   %n = $LakHN[%cl];
   %idx = $LakHI[%cl];
   if(%n == 0)
   {
      %pl = Client::getOwnedObject(%cl);
      $LakFrom = getBoxCenter(%pl);
      $LakFromVel = lakSpeed(%pl);
      return;
   }
   %bestJ = 0;
   %bestErr = 1000000;
   for(%j = 0; %j < %n; %j++)
   {
      %k = (%idx - %j + $LakRabbit::HistSize) % $LakRabbit::HistSize;
      %err = lakAbs(Vector::getDistance($LakH[%cl, %k], %hitPos) / %muzzle - %j * $LakRabbit::TickTime);
      if(%err < %bestErr)
      {
         %bestErr = %err;
         %bestJ = %k;
      }
   }
   $LakFrom = $LakH[%cl, %bestJ];
   $LakFromVel = $LakV[%cl, %bestJ];
}

//--- mission setup ----------------------------------------------------
function Mission::init()
{
   setTeamScoreHeading("");
   setClientScoreHeading("Player Name\t\x60Score\t\x8CTime\t\xB0Kills\t\xD8MAs\t\xEEPing\t\xFFPL");
   // Keep the sensor network on. The minimap uses it. The stock dm.cs turns it
   // off for free-for-all, but T2 LakRabbit makes all players always visible.
   $SensorNetworkEnabled = 1;
   $numTeams = getNumTeams();
   for(%i = 0; %i < $numTeams; %i++)
      $teamScore[%i] = 0;
   $dieSeqCount = 0;
   AI::setupAI();

   $LakRabbitCl = "";
   $LakRabbit::Ending = 0;
   $LakRabbit::RabbitDamageTime = 0;
   deleteVariables("$LakDamaged*");
   deleteVariables("$LakH*");
   deleteVariables("$LakV*");
   deleteVariables("$LakNade*");
   deleteVariables("$LakPC*");  // Client data. Sim time starts at 0 for each mission.
   $LakRabbit::Bots = "";
   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
      LakRabbit::resetScore(%cl);

   LakRabbit::collectDropPoints();
   LakRabbit::spawnFlag();

   $LakRabbit::Seq++;
   $LakRabbit::Ticks = 0;
   schedule("LakRabbit::tick(" @ $LakRabbit::Seq @ ");", $LakRabbit::TickTime);
   LakRabbit::updateObjectives();
   echo("LakRabbit: mission " @ $missionName @ " ready, " @ $LakDrops @ " drop points, flag " @ $LakFlag);
   if($LakTest::Enabled)
      schedule("LakTest::start();", 1);
}

function LakRabbit::collectDropPoints()
{
   $LakDrops = 0;
   %cx = 0;
   %cy = 0;
   %cz = 0;
   for(%t = 0; %t < 8; %t++)
   {
      for(%s = 0; %s < 2; %s++)
      {
         if(%s == 0) %sub = "Random";
         else        %sub = "Start";
         %group = nameToID("MissionGroup/Teams/team" @ %t @ "/DropPoints/" @ %sub);
         if(%group == -1)
            continue;
         for(%i = 0; (%obj = Group::getObject(%group, %i)) != -1; %i++)
         {
            $LakDrop[$LakDrops] = %obj;
            $LakDrops++;
            %p = GameBase::getPosition(%obj);
            %cx += getWord(%p, 0);
            %cy += getWord(%p, 1);
            %cz += getWord(%p, 2);
         }
      }
   }
   if($LakDrops)
      $LakCenter = lakVec(%cx / $LakDrops, %cy / $LakDrops, %cz / $LakDrops);
   else
      $LakCenter = "0 0 0";
}

// DM maps have no flag. Put one neutral flag on the drop point nearest to the center.
function LakRabbit::spawnFlag()
{
   %best = -1;
   %bestD = 1000000;
   for(%i = 0; %i < $LakDrops; %i++)
   {
      %d = Vector::getDistance(GameBase::getPosition($LakDrop[%i]), $LakCenter);
      if(%d < %bestD)
      {
         %bestD = %d;
         %best = $LakDrop[%i];
      }
   }
   if(%best == -1)
      %pos = "0 0 300";
   else
      %pos = GameBase::getPosition(%best);

   %flag = newObject("LakRabbitFlag", "Item", Flag, 1, false);
   addToSet("MissionCleanup", %flag);
   GameBase::setTeam(%flag, -1);
   GameBase::setPosition(%flag, %pos);
   %flag.originalPosition = %pos;
   %flag.atHome = 1;
   %flag.carrier = -1;
   %flag.pickupSequence = 0;
   %flag.objectiveName = "the flag";
   $LakFlag = %flag;
}

//--- scoring ----------------------------------------------------------
function LakRabbit::resetScore(%cl)
{
   $LakPC[%cl, score] = 0;
   $LakPC[%cl, kills] = 0;
   $LakPC[%cl, flagGrabs] = 0;
   $LakPC[%cl, flagTime] = 0;
   $LakPC[%cl, morepoints] = 0;
   $LakPC[%cl, mas] = 0;
   $LakPC[%cl, totalSpeed] = 0;
   $LakPC[%cl, totalDistance] = 0;
   $LakPC[%cl, duelSeconds] = 0;
   $LakPC[%cl, duelKills] = 0;
}

function Game::onPlayerConnected(%playerId)
{
   LakRabbit::resetScore(%playerId);
   %playerId.justConnected = 1;
   $menuMode[%playerId] = "None";
   Game::refreshClientScore(%playerId);
}

function Game::resetScores(%client)
{
   if(%client == "")
   {
      for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
         LakRabbit::resetScore(%cl);
   }
   else
      LakRabbit::resetScore(%client);
}

function LakRabbit::holdTime(%cl)
{
   %t = $LakPC[%cl, flagTime];
   if(%cl == $LakRabbitCl)
      %t += getSimTime() - $LakPC[%cl, startTime];
   return %t;
}

function LakRabbit::formatTime(%t)
{
   if(%t <= 0)
      return "";
   %s = floor(%t) % 60;
   if(%s < 10)
      %s = "0" @ %s;
   return floor(%t / 60) @ ":" @ %s;
}

// T2 recalcScore: score = grabs + kills (x5 in Pro) + bonus points + (hold time / 3 s).
function Game::refreshClientScore(%clientId)
{
   %kills = $LakPC[%clientId, kills];
   if($LakRabbit::PubPro)
      %kills = %kills * 5;
   %hold = LakRabbit::holdTime(%clientId);
   $LakPC[%clientId, score] = $LakPC[%clientId, flagGrabs] + %kills + $LakPC[%clientId, morepoints] + floor(%hold / 3);
   %name = "%n";
   if(%clientId == $LakRabbitCl)
      %name = "%n *";
   Client::setScore(%clientId, %name @ "\t" @ $LakPC[%clientId, score] @ "\t" @ LakRabbit::formatTime(%hold) @ "\t" @ $LakPC[%clientId, kills] @ "\t" @ $LakPC[%clientId, mas] @ "\t%p\t%l", $LakPC[%clientId, score]);

   if($LakPC[%clientId, score] >= $LakRabbit::ScoreLimit && $matchStarted && !$LakRabbit::Ending)
   {
      $LakRabbit::Ending = 1;
      messageAll(0, Client::getName(%clientId) @ " reached " @ $LakRabbit::ScoreLimit @ " points!");
      echo("GAME: LakRabbit score limit reached");
      LakRabbit::gameOver();
      Server::nextMission();
   }
}

function Game::checkTimeLimit()
{
   $timeLimitReached = 0;
   if(!$Server::timeLimit)
   {
      schedule("Game::checkTimeLimit();", 60);
      return;
   }
   %curTimeLeft = ($Server::timeLimit * 60) + $missionStartTime - getSimTime();
   if(%curTimeLeft <= 0 && $matchStarted)
   {
      echo("GAME: Timelimit reached.");
      $timeLimitReached = 1;
      LakRabbit::gameOver();
      Server::nextMission();
   }
   else
   {
      schedule("Game::checkTimeLimit();", 20);
      UpdateClientTimes(%curTimeLeft);
   }
}

function LakRabbit::gameOver()
{
   if($LakRabbitCl != "")
      Game::refreshClientScore($LakRabbitCl);
   LakRabbit::updateObjectives();
   echo("LakRabbit: final stats  name / score / kills / MAs / avg speed / avg dist / tot dist");
   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
      echo("LakRabbit: " @ LakRabbit::statLine(%cl));
}

function LakRabbit::statLine(%cl)
{
   %avgS = 0;
   %avgD = 0;
   if($LakPC[%cl, mas] > 0)
   {
      %avgS = floor($LakPC[%cl, totalSpeed] / $LakPC[%cl, mas]);
      %avgD = floor($LakPC[%cl, totalDistance] / $LakPC[%cl, mas]);
   }
   return Client::getName(%cl) @ "  " @ $LakPC[%cl, score] @ "  " @ $LakPC[%cl, kills] @ "  " @ $LakPC[%cl, mas] @ "  " @ %avgS @ "  " @ %avgD @ "  " @ floor($LakPC[%cl, totalDistance]);
}

// The Objectives (O) screen. In T1, it replaces the T2 flag status HUD
// and the T2 debrief table.
function LakRabbit::updateObjectives()
{
   if($LakRabbitCl != "")
      %status = Client::getName($LakRabbitCl) @ " is the rabbit";
   else if($LakFlag.atHome)
      %status = "The flag is at home";
   else
      %status = "The flag is in the field";
   %modes = "";
   if($LakRabbit::DuelMode)       %modes = %modes @ " Duel";
   if($LakRabbit::NoSplashDamage) %modes = %modes @ " No-Splash";
   if($LakRabbit::PubPro)         %modes = %modes @ " Pro";
   if(%modes == "")
      %modes = " none";

   %line[0] = "<f5>LakRabbit";
   %line[1] = "<f1>Grab the flag and stay alive. Points for holding, kills and mid-air hits.";
   %line[2] = "<f1>" @ %status @ ".   Score limit " @ $LakRabbit::ScoreLimit @ ".   Modes:" @ %modes;
   %line[3] = " ";
   %line[4] = "<f0>Player / Score / Kills / MAs / Avg speed / Avg dist / Total dist";
   %n = 5;
   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1 && %n < 20; %lw++)
   {
      %line[%n] = "<f1>" @ LakRabbit::statLine(%cl);
      %n++;
   }
   for(%n = %n; %n < 22; %n++)
      %line[%n] = " ";
   // Send only the lines that changed. Each line is one network update for each team.
   for(%i = 0; %i < 22; %i++)
   {
      if($LakObj[%i] == %line[%i] && $LakObjSeq == $LakRabbit::Seq)
         continue;
      $LakObj[%i] = %line[%i];
      for(%t = -1; %t < getNumTeams(); %t++)
         Team::setObjective(%t, %i, %line[%i]);
   }
   $LakObjSeq = $LakRabbit::Seq;
}

//--- teams, spawning, loadout ------------------------------------------
function Game::assignClientTeam(%playerId)
{
   GameBase::setTeam(%playerId, 0);
}

// T2 pickTeamSpawn: spawn near the rabbit. If there is no rabbit, spawn near
// the flag. T1 has no ground raycast. Thus this function uses a random drop
// point in SpawnRadius. If no drop point is in SpawnRadius, it uses the
// nearest drop point.
function Game::pickPlayerSpawn(%clientId, %respawn)
{
   $LakDamaged[%clientId] = 0;
   $LakPC[%clientId, duelSeconds] = 0;
   $LakPC[%clientId, duelSecondsCounted] = 0;
   $LakPC[%clientId, duelKills] = 0;
   LakRabbit::clearHistory(%clientId);

   %anchor = -1;
   if($LakRabbitCl != "")
      %anchor = Client::getOwnedObject($LakRabbitCl);
   if(!lakAlive(%anchor))
      %anchor = $LakFlag;
   if(%anchor == "" || %anchor == -1 || $LakDrops == 0)
      return Game::pickRandomSpawn(0);

   %apos = GameBase::getPosition(%anchor);
   %n = 0;
   %near = -1;
   %nearD = 1000000;
   for(%i = 0; %i < $LakDrops; %i++)
   {
      %d = Vector::getDistance(GameBase::getPosition($LakDrop[%i]), %apos);
      if(%d <= $LakRabbit::SpawnRadius)
      {
         %cand[%n] = $LakDrop[%i];
         %n++;
      }
      if(%d < %nearD)
      {
         %nearD = %d;
         %near = $LakDrop[%i];
      }
   }
   if(%n > 0)
      return %cand[floor(getRandom() * (%n - 0.01))];
   return %near;
}

// Spawn loadout: the LT loadout (as game.cs Game::playerSpawned with
// $spawnBuyList), or the T2 Pro loadout.
function Game::playerSpawned(%pl, %clientId, %armor)
{
   Client::setSkin(%clientId, $Client::info[%clientId, 0]);
   if($LakRabbit::PubPro)
   {
      Player::setItemCount(%clientId, $ArmorName[%armor], 1);
      Player::setItemCount(%clientId, DiscLauncher, 1);
      Player::setItemCount(%clientId, Blaster, 1);
      Player::setItemCount(%clientId, PlasmaGun, 1);
      Player::setItemCount(%clientId, DiscAmmo, 30);
      Player::setItemCount(%clientId, PlasmaAmmo, 40);
      Player::setItemCount(%clientId, MineAmmo, 6);
      Player::setItemCount(%clientId, RepairKit, 1);
      Player::setItemCount(%clientId, EnergyPack, 1);
      Player::useItem(%pl, EnergyPack);
      Player::useItem(%pl, DiscLauncher);
   }
   else if(Player::isAIControlled(%pl))
   {
      // In 1.11, buyItem stops the server for AI clients. Give the LT items to bots directly.
      for(%i = 0; (%item = $spawnBuyList[%i]) != ""; %i++)
      {
         if(%item.className == Armor)
            continue;
         %n = $ItemMax[larmor, %item];
         if(%n < 1)
            %n = 1;
         Player::setItemCount(%clientId, %item, %n);
         %ammo = %item.imageType.ammoType;
         if(%ammo != "")
            Player::setItemCount(%clientId, %ammo, $ItemMax[larmor, %ammo]);
      }
   }
   else
   {
      %clientId.spawn = 1;
      for(%i = 0; (%item = $spawnBuyList[%i]) != ""; %i++)
      {
         buyItem(%clientId, %item);
         if(%item.className == Weapon)
            %clientId.spawnWeapon = %item;
      }
      %clientId.spawn = "";
      if(%clientId.spawnWeapon != "")
      {
         Player::useItem(%pl, %clientId.spawnWeapon);
         %clientId.spawnWeapon = "";
      }
   }
   %pl.freeDJ = 1;
   %pl.knockback = 0;
   %pl.lakInvUntil = 0;
   // Show the flag home position (also at the start of the mission).
   if($LakRabbit::Debug)
      echo("LakRabbit spawn: client " @ %clientId @ " flagAtHome=" @ $LakFlag.atHome @ " carrier=" @ $LakFlag.carrier @ " matchStarted=" @ $matchStarted @ " sensorNet=" @ $SensorNetworkEnabled);
   if($LakFlag.atHome && $LakFlag.carrier == -1)
      LakRabbit::showFlagWaypoint(%clientId, "");
}

//--- flag ---------------------------------------------------------------
function Flag::onCollision(%this, %object)
{
   if(%this.isFake || %this != $LakFlag)
      return;
   if(getObjectType(%object) != "Player" || Player::isDead(%object))
      return;
   if(%this.carrier != -1)
      return;
   if(Player::isAIControlled(%object) && !$LakRabbit::AllowAI)
      return;
   %cl = Player::getClient(%object);
   if($LakPC[%cl, flagDenyUntil] > getSimTime())
      return;

   // A player cannot pick up the flag until 2 players are in the game.
   if(lakPlayingPlayers() < 2)
   {
      if(getSimTime() - $LakPC[%cl, pickUpTime] > 5 || $LakPC[%cl, pickUpTime] == "")
      {
         Client::sendMessage(%cl, 1, "You can't pick up the flag until another person joins.");
         $LakPC[%cl, pickUpTime] = getSimTime();
      }
      return;
   }

   Player::setItemCount(%object, Flag, 1);
   Player::mountItem(%object, Flag, $FlagSlot, -1);
   Item::hide(%this, true);
   %this.atHome = 0;
   %this.carrier = %object;
   %this.pickupSequence++;
   %object.carryFlag = %this;
   %object.freeDJ = 0;
   LakRabbit::hideFlagWaypoint();
   $LakRabbitCl = %cl;
   $LakRabbit::RabbitDamageTime = 0;
   $LakPC[%cl, startTime] = getSimTime();
   $LakPC[%cl, rabbitSplash] = getSimTime();
   $LakPC[%cl, flagGrabs]++;

   // Mid-air flag grab: there is no ground in the 10 m below the player.
   if(lakGroundDist(%object, 10) >= 10)
   {
      %speed = lakSpeed(%object);
      %height = lakHeight(%object);
      %points = floor(%speed / 6.3 + %height / 3.3);
      if(%points < 5)
         %points = 5;
      $LakPC[%cl, morepoints] += %points;
      messageAll(0, Client::getName(%cl) @ " gets " @ %points @ " points for a Mid-Air flag grab! [Speed:" @ %speed @ "] [Height:" @ %height @ "]" @ $LakSnd::Special);
   }
   else
      messageAll(0, Client::getName(%cl) @ " has taken the flag!" @ $LakSnd::Take);
   echo("LakRabbit: " @ Client::getName(%cl) @ " took flag");

   // The rabbit cannot get damage for 2 seconds.
   %object.lakInvUntil = getSimTime() + 2;

   if($LakRabbit::DuelMode)
   {
      %n = lakPlayingPlayers();
      $LakPC[%cl, duelSeconds] = 36 - %n * 4;
      if(%n == 2 || $LakRabbit::NoSplashDamage)
         $LakPC[%cl, duelSeconds] += 10;
      if($LakPC[%cl, duelSeconds] < 20)
         $LakPC[%cl, duelSeconds] = 20;
      $LakPC[%cl, duelKills] = 0;
      $LakPC[%cl, duelSecondsCounted] = 0;
   }
   Game::refreshClientScore(%cl);
   LakRabbit::updateObjectives();
}

// The rabbit cannot throw the flag. The rabbit loses the flag only on death,
// on a slap, or on disconnect.
function Flag::onDrop(%player, %type)
{
   if(!Player::isDead(%player))
      return;
   LakRabbit::dropFlag(%player);
}

function LakRabbit::dropFlag(%player)
{
   %flag = %player.carryFlag;
   if(%flag == "")
      return;
   %cl = Player::getClient(%player);
   $LakPC[%cl, flagTime] += getSimTime() - $LakPC[%cl, startTime];
   %player.carryFlag = "";
   Player::setItemCount(%player, "Flag", 0);
   %flag.carrier = -1;
   %flag.pickupSequence++;
   $LakRabbitCl = "";

   // T2 toss: a random direction, an upward push, and the velocity of the rabbit.
   %vec = lakVec(-10 + getRandom() * 20, -10 + getRandom() * 20, getRandom() * 10 + 12);
   %vec = lakAdd(%vec, Item::getVelocity(%player));
   if($LakRabbit::DuelMode && getWord(%vec, 2) < 35)
      %vec = lakVec(getWord(%vec, 0), getWord(%vec, 1), 35);
   %vec = lakScale(%vec, $LakRabbit::TossScale);
   GameBase::setPosition(%flag, getBoxCenter(%player));
   Item::hide(%flag, false);
   Item::setVelocity(%flag, %vec);

   MessageAllExcept(%cl, 0, Client::getName(%cl) @ " dropped the flag!" @ $LakSnd::Drop);
   Client::sendMessage(%cl, 0, "You dropped the flag!" @ $LakSnd::Drop);
   echo("LakRabbit: " @ Client::getName(%cl) @ " dropped flag");
   schedule("LakRabbit::checkReturn(" @ %flag @ ", " @ %flag.pickupSequence @ ");", $LakRabbit::FlagReturnTime);

   $LakPC[%cl, flagDenyUntil] = getSimTime() + 2.5;
   LakRabbit::hideRabbitWaypoint();

   if($LakRabbit::DuelMode)
      LakRabbit::duelEndBonus(%player, %cl);
   Game::refreshClientScore(%cl);
   LakRabbit::updateObjectives();
}

function LakRabbit::checkReturn(%flag, %seq)
{
   if(%flag.pickupSequence != %seq || %flag.carrier != -1)
      return;
   messageAll(0, "The flag was returned to its starting point." @ $LakSnd::Return);
   echo("LakRabbit: flag return (timeout)");
   GameBase::setPosition(%flag, %flag.originalPosition);
   Item::setVelocity(%flag, "0 0 0");
   %flag.atHome = 1;
   LakRabbit::showFlagWaypointAll();
   LakRabbit::updateObjectives();
}

//--- duel mode ------------------------------------------------------------
function LakRabbit::duelBonus(%cl)
{
   $LakPC[%cl, duelSeconds] += 12;
   if($LakRabbit::NoSplashDamage)
      $LakPC[%cl, duelSeconds] += 5;
   $LakPC[%cl, duelKills]++;
   Client::sendMessage(%cl, 0, "You now have " @ $LakPC[%cl, duelSeconds] @ " seconds to live.");
}

function LakRabbit::duelTick()
{
   if(!$LakRabbit::DuelMode || $LakRabbitCl == "" || lakPlayingPlayers() < 2)
      return;
   %cl = $LakRabbitCl;
   $LakPC[%cl, duelSeconds]--;
   $LakPC[%cl, duelSecondsCounted]++;
   if($LakPC[%cl, duelSeconds] <= 0)
   {
      messageAll(1, Client::getName(%cl) @ " failed to kill within the alloted time!" @ $LakSnd::Warn);
      LakRabbit::kill(%cl, 0, $ExplosionDamageType, true);
   }
   else if($LakPC[%cl, duelSeconds] == 10 || $LakPC[%cl, duelSeconds] == 5 || $LakPC[%cl, duelSeconds] <= 3)
      Client::sendMessage(%cl, 1, "You have " @ $LakPC[%cl, duelSeconds] @ " second" @ lakPlural($LakPC[%cl, duelSeconds]) @ " to kill someone, or die!" @ $LakSnd::Warn);
}

// T2 playerDroppedFlag duel bonus.
// T2 has a minimum time of 4 s for each kill, but the T2 code reads a field
// that is never set (%player.duelKills). Thus the minimum does not operate in T2.
// This port applies the minimum. This limits the bonus for fast kill series.
function LakRabbit::duelEndBonus(%player, %cl)
{
   if(%player.lakSuicide)
   {
      messageAll(1, Client::getName(%cl) @ " suicides and gets no points!");
      return;
   }
   %k = $LakPC[%cl, duelKills];
   if(%k == 0)
   {
      messageAll(1, Client::getName(%cl) @ " kills nobody and gets nothing!  Better luck next time....");
      return;
   }
   %points = 0;
   for(%i = 1; %i <= %k; %i++)
      %points += %i * 4;
   %seconds = $LakPC[%cl, duelSecondsCounted];
   if(%seconds < %k * 4)
      %seconds = %k * 4;
   %points = floor(%points * (%k * 12 + 12) / %seconds);
   if(%k == 1) %who = "person";
   else        %who = "people";
   messageAll(1, Client::getName(%cl) @ " gets " @ %points @ " bonus point" @ lakPlural(%points) @ " for killing " @ %k @ " " @ %who @ " in " @ $LakPC[%cl, duelSecondsCounted] @ " second" @ lakPlural($LakPC[%cl, duelSecondsCounted]) @ ".");
   $LakPC[%cl, morepoints] += %points;
}

//--- kills ----------------------------------------------------------------
// Script kill (T2 blowup + scriptKill).
// If %killer is 0, the message is "dies." and there is no score penalty.
function LakRabbit::kill(%cl, %killer, %type, %blowUp)
{
   %pl = Client::getOwnedObject(%cl);
   if(!lakAlive(%pl))
      return;
   if(%blowUp)
      Player::blowUp(%pl);
   playNextAnim(%cl);
   Player::kill(%pl);
   Client::onKilled(%cl, %killer, %type);
}

// game.cs Client::onKilled calls this function at its end, after its kill and score updates.
function Game::clientKilled(%playerId, %killerId)
{
   if(%killerId != %playerId && %killerId)
   {
      // Count the kill here. The stock scoreKills++ in Client::onKilled does not operate on bots.
      $LakPC[%killerId, kills]++;
      %killer = $LakDamaged[%playerId];
      if(%killer == "" || %killer == 0)
         %killer = %killerId;
      if($LakRabbit::DuelMode && %killer == $LakRabbitCl)
         LakRabbit::duelBonus(%killer);
      Game::refreshClientScore(%killerId);
   }
   $LakPC[%playerId, flagDenyUntil] = getSimTime() + 2.5;
   LakRabbit::clearHistory(%playerId);
   Game::refreshClientScore(%playerId);
   LakRabbit::updateObjectives();
}

function remoteKill(%client)
{
   if(!$matchStarted)
      return;
   %player = Client::getOwnedObject(%client);
   if(%player != -1 && getObjectType(%player) == "Player" && !Player::isDead(%player))
   {
      %player.lakSuicide = 1;
      playNextAnim(%client);
      Player::kill(%client);
      Client::onKilled(%client, %client);
   }
}

//--- out of bounds (T2 boundary bounce and Arena damage) ----------------------
function Player::leaveMissionArea(%player)
{
   %cl = Player::getClient(%player);
   %player.outArea = 1;
   %player.lakOutSeq++;
   Client::sendMessage(%cl, 1, "You have left the mission area." @ $LakSnd::Warn);
   LakRabbit::bounce(%player, %player.lakOutSeq, 0);
   schedule("leaveMissionAreaDamage(" @ %cl @ ", " @ %player.lakOutSeq @ ");", 3.5, %cl);
}

function Player::enterMissionArea(%player)
{
   if(%player.outArea == 1)
      Client::sendMessage(Player::getClient(%player), 1, "You are back in the mission area.");
   %player.outArea = "";
   %player.lakOutSeq++;
}

// Push the player back to the center of the map, 4 times each second.
// After 2 s outside, kill the player.
function LakRabbit::bounce(%player, %seq, %count)
{
   if(%player.outArea != 1 || %player.lakOutSeq != %seq || !lakAlive(%player))
      return;
   if(%count >= 8)
   {
      LakRabbit::kill(Player::getClient(%player), 0, $LandingDamageType, false);
      return;
   }
   %old = Item::getVelocity(%player);
   %oldSpeed = lakLen(%old);
   %dir = Vector::sub($LakCenter, GameBase::getPosition(%player));
   %dir = Vector::normalize(lakVec(getWord(%dir, 0), getWord(%dir, 1), 0));
   %h = lakLen(lakVec(getWord(%old, 0), getWord(%old, 1), 0));
   if(%oldSpeed < 25)       %h = 25;
   else if(%oldSpeed < 75)  %h = %h * 0.95;
   else if(%oldSpeed < 100) %h = %h * 0.85;
   else if(%oldSpeed < 125) %h = %h * 0.75;
   else if(%oldSpeed < 150) %h = %h * 0.65;
   else                     %h = %h * 0.55;
   %vel = lakScale(%dir, %h);
   Item::setVelocity(%player, lakVec(getWord(%vel, 0), getWord(%vel, 1), getWord(%old, 2)));
   schedule("LakRabbit::bounce(" @ %player @ ", " @ %seq @ ", " @ %count + 1 @ ");", 0.25, %player);
}

// This replaces dm.cs leaveMissionAreaDamage.
// player.cs Player::onKilled also calls this function, with one argument,
// for each player who dies outside the area. That call does nothing here.
function leaveMissionAreaDamage(%client, %seq)
{
   %player = Client::getOwnedObject(%client);
   if(%seq == "" || !lakAlive(%player) || %player.outArea != 1 || %player.lakOutSeq != %seq)
      return;
   Player::setDamageFlash(%client, 0.1);
   GameBase::setDamageLevel(%player, GameBase::getDamageLevel(%player) + 0.09);
   if(Player::isDead(%player))
   {
      playNextAnim(%client);
      Client::onKilled(%client, 0);
   }
   else
      schedule("leaveMissionAreaDamage(" @ %client @ ", " @ %seq @ ");", 1, %client);
}

//--- rabbit waypoint (T2 ShowFlagTask, default is off) -------------------------
function LakRabbit::showRabbitWaypoint()
{
   if($LakRabbitCl == "")
      return;
   if($LakRabbit::RabbitDamageTime && getSimTime() - $LakRabbit::RabbitDamageTime < 24)
      return;
   %rpl = Client::getOwnedObject($LakRabbitCl);
   if(!lakAlive(%rpl))
      return;
   %rpos = GameBase::getPosition(%rpl);
   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
   {
      %pl = Client::getOwnedObject(%cl);
      if(%cl == $LakRabbitCl || !lakAlive(%pl))
         continue;
      if(Vector::getDistance(%rpos, GameBase::getPosition(%pl)) < 100)
         continue;
      issueCommand(%cl, %cl, 0, "Kill the Rabbit!", getWord(%rpos, 0), getWord(%rpos, 1));
      $LakPC[%cl, waypoint] = 1;
   }
}

function LakRabbit::hideRabbitWaypoint()
{
   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
      if($LakPC[%cl, waypoint])
      {
         setCommandStatus(%cl, 0, "");
         $LakPC[%cl, waypoint] = "";
      }
}

//--- flag home waypoint (T2 "Flag Home" waypoint) ------------------------------
// Show a waypoint to the flag home position. %sound is "" or a sound tag.
// Bots do not get waypoints.
function LakRabbit::showFlagWaypoint(%cl, %sound)
{
   if(Player::isAIControlled(Client::getOwnedObject(%cl)))
      return;
   %p = $LakFlag.originalPosition;
   issueCommand(%cl, %cl, 0, "The flag is at home." @ %sound, getWord(%p, 0), getWord(%p, 1));
   if($LakRabbit::Debug)
      echo("LakRabbit waypoint: flag home to client " @ %cl @ " at " @ getWord(%p, 0) @ " " @ getWord(%p, 1) @ " matchStarted=" @ $matchStarted @ " sensorNet=" @ $SensorNetworkEnabled @ " team=" @ Client::getTeam(%cl));
   $LakPC[%cl, flagWaypoint] = 1;
}

// Show the waypoint to all players. The flag went back to its home position.
function LakRabbit::showFlagWaypointAll()
{
   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
      if(lakAlive(Client::getOwnedObject(%cl)))
         LakRabbit::showFlagWaypoint(%cl, "~wcapobj");
   $LakRabbit::FlagWaypointCount++;
}

// Remove the waypoint from all players. A player took the flag.
function LakRabbit::hideFlagWaypoint()
{
   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
      if($LakPC[%cl, flagWaypoint])
      {
         setCommandStatus(%cl, 0, "");
         $LakPC[%cl, flagWaypoint] = "";
      }
}

//--- false flags thrown out (T2 throwDummyFlag, Hunters createDroppedFlag) ------
function LakRabbit::spewFlags(%player, %count)
{
   %pos = getBoxCenter(%player);
   %pvel = Item::getVelocity(%player);
   for(%i = 0; %i < %count; %i++)
   {
      %f = newObject("", "Item", Flag, 1, false);
      addToSet("MissionCleanup", %f);
      %f.isFake = 1;
      GameBase::setTeam(%f, floor(getRandom() * 2.99));
      GameBase::setPosition(%f, %pos);
      %vec = lakVec(-1 + getRandom() * 2, -1 + getRandom() * 2, getRandom());
      %vec = lakAdd(lakScale(%vec, 5 + getRandom() * 19), %pvel);
      Item::setVelocity(%f, %vec);
      schedule("Item::pop(" @ %f @ ");", 1.5 + getRandom() * 4, %f);
   }
}

//--- awards ----------------------------------------------------------------------
// T2 missileEveryone. T1 has no missile launcher for players.
// Replacement: the rabbit cannot get damage for 5 s. In duel mode, the
// rabbit also gets 15 more seconds.
function LakRabbit::heist(%player, %cl)
{
   messageAll(1, Client::getName(%cl) @ " pulls off a heist!" @ $LakSnd::Special);
   %player.lakInvUntil = getSimTime() + 5;
   if($LakRabbit::DuelMode)
      $LakPC[%cl, duelSeconds] += 15;
}

// T2 fireworks. T1 scripts cannot make particle emitters. Replacement: a center message.
function LakRabbit::fireworks(%cl, %points)
{
   centerprintall("<jc><f1>" @ Client::getName(%cl) @ "<f0> scores <f1>" @ %points @ "<f0> points in one hit!", 4);
}

//--- damage: the T2 Armor::damageObject rules ------------------------------------
// The function returns the damage to apply (0 = no damage).
function LakRabbit::onHit(%pl, %cl, %scl, %type, %value, %pos)
{
   // Damage to yourself: a hunter gets one free disc jump.
   if(%scl == %cl)
   {
      if(%type == $ExplosionDamageType && %pl.carryFlag == "" && (%pl.freeDJ > 0 || $LakRabbit::UnlimitedDJ))
      {
         %pl.freeDJ--;
         return 0;
      }
      return %value;
   }
   if(%scl <= 0 || %scl == "")
      return %value;  // Damage from the world (fall, out of bounds).

   // Damage occurs only between the rabbit and a hunter.
   // In T2, the hunters are one team and team damage is off.
   %tRabbit = (%pl.carryFlag != "");
   %sRabbit = ($LakRabbitCl != "" && %scl == $LakRabbitCl);
   if(!%tRabbit && !%sRabbit)
      return 0;
   // T2 does not permit these weapons: sniper rifle, ELF.
   if(%type == $LaserDamageType || %type == $ElectricityDamageType)
      return 0;

   %shooter = Client::getOwnedObject(%scl);
   %shooterOK = lakAlive(%shooter);
   %now = getSimTime();
   if(%tRabbit)
      $LakRabbit::RabbitDamageTime = %now;
   $LakDamaged[%cl] = %scl;

   %tpos = getBoxCenter(%pl);
   %base = $LakBase[%type];
   %nade = 0;
   if(%type == $ShrapnelDamageType && %now - $LakNadeT[%scl] < 0.3)
   {
      %nade = 1;
      %base = 0.5;
   }
   if(%nade)
   {
      %from = $LakNadeFrom[%scl];
      %vel = 0;
      if(%shooterOK)
         %vel = lakSpeed(%shooter);
   }
   else if(%type == $MineDamageType || !%shooterOK || $LakMuzzle[%type] == "")
   {
      %from = $LakMineFrom[%scl];
      if(%from == "" && %shooterOK)
         %from = getBoxCenter(%shooter);
      if(%from == "")
         %from = %tpos;
      %vel = 0;
      if(%shooterOK)
         %vel = lakSpeed(%shooter);
   }
   else
   {
      LakRabbit::shotOrigin(%scl, %tpos, $LakMuzzle[%type]);
      %from = $LakFrom;
      %vel = $LakFromVel;
   }

   %pct = 0;
   if(%base > 0)
      %pct = floor(%value / %base * 100);
   %distance = Vector::getDistance(%tpos, %from);
   %velTarget = lakSpeed(%pl);
   %distanceBonus = pow(%distance, 1.35) / 10 + 1;
   %longDistanceBonus = pow(%distance, 1.10) / 10 + 1;
   %velBonus = pow(%vel, 1.125) / 10 + 1;
   %ma = lakIsMA(%pl);
   %points = 0;
   %long = 0;
   %sound = "";
   %special = "";
   %accuracy = "";
   %weapon = "";
   %splash = (%pct < 98 && $LakSplashType[%type]);

   // Last hit, for the laktest tests: type, value, pct, distance, ma, nade.
   $LakRabbit::LastHit = %type @ " " @ %value @ " " @ %pct @ " " @ floor(%distance) @ " " @ %ma @ " " @ %nade;
   if($LakRabbit::Debug)
      echo("LakRabbit hit: type " @ %type @ " val " @ %value @ " pct " @ %pct @ " dist " @ floor(%distance) @ " vel " @ %vel @ " tvel " @ %velTarget @ " ma " @ %ma @ " ground " @ lakGroundDist(%pl, 6) @ " nade " @ %nade @ " splash " @ %splash @ " nosplash " @ $LakRabbit::NoSplashDamage @ " tRabbit " @ %tRabbit @ " sRabbit " @ %sRabbit);

   // Duel mode: when there are more players, the splash damage from a hunter
   // to the rabbit is less. The splash damage from the rabbit does not change.
   if($LakRabbit::DuelMode && %splash && !%sRabbit)
   {
      %mult = 1 - (lakNumClients() - 1) / 10;
      if(%mult < 0.1)
         %mult = 0.1;
      %value = %value * %mult;
   }
   // No-splash mode. Mines and hand grenades continue to cause splash damage.
   if($LakRabbit::NoSplashDamage && %splash && !%nade && %type != $MineDamageType)
   {
      if(!%tRabbit)
         %value = 0;
      else if(%now - $LakPC[%cl, rabbitSplash] < 15)
         %value = 0;
   }

   if(%type == $ExplosionDamageType)
   {
      %weapon = "Disc";
      if(%ma && %pct >= 98)
      {
         %points = %distanceBonus;
         %sound = $LakSnd::Bonus;
      }
      if(%pct >= 25 && !%ma && %distance >= 80 && %velTarget > 20)
      {
         %points = %longDistanceBonus;
         %velBonus = %velBonus / 2;
         %long = 1;
         %sound = $LakSnd::Bonus;
      }
      if(%ma)
      {
         %h = 0;
         if(%tRabbit)
            %h = lakHeight(%pl);
         if(%tRabbit && %h >= 100)
         {
            // TR2 style: a rabbit high in the air explodes, and false flags fly out.
            %chance = floor(20 - %distance / %h);
            if(%chance <= 0) %chance = 1;
            if(lakRoll(%chance))
            {
               LakRabbit::dropFlag(%pl);
               LakRabbit::spewFlags(%pl, 40);
               messageAll(1, Client::getName(%cl) @ " is blown out of the sky!" @ $LakSnd::Special);
               LakRabbit::kill(%cl, %scl, $ExplosionDamageType, true);
               if(%points > 0)
                  LakRabbit::award(%scl, %shooter, %points, %velBonus, %vel, %distance, %ma, %long, "Disc", "", "");
               return 0;
            }
         }
         else
         {
            // Slap.
            %chance = floor(15 - %distance / 3);
            if(%chance <= 0) %chance = 1;
            if(lakRoll(%chance))
            {
               // T2 drops the flag on a slap. Here the default is off.
               if(%tRabbit && $LakRabbit::SlapDropsFlag)
               {
                  LakRabbit::dropFlag(%pl);
                  %pl.freeDJ = 1;
               }
               if(%sRabbit && $LakRabbit::DuelMode)
               {
                  LakRabbit::duelBonus(%scl);
                  $LakDamaged[%cl] = 0;
               }
               %value = 0.01;
               %pl.knockback = 1;
               %pl.lakKnockSeq++;
               schedule("LakRabbit::endKnockback(" @ %pl @ ", " @ %pl.lakKnockSeq @ ");", 15, %pl);
               if(%shooterOK)
                  Item::setVelocity(%pl, Vector::getFromRot(GameBase::getRotation(%shooter), $LakRabbit::SlapSpeed, $LakRabbit::SlapSpeed / 3));
               %sound = $LakSnd::Special;
               %r = floor(getRandom() * 2.99);
               if(%r == 0)      messageAll(0, Client::getName(%cl) @ " wonders what the five fingers said to the face.");
               else if(%r == 1) messageAll(0, Client::getName(%cl) @ " gets slapped the heck out!");
               else             messageAll(0, Client::getName(%cl) @ " is taking a short tour around the map.");
            }
         }
      }
   }
   else if(%type == $ShrapnelDamageType && %nade)
   {
      %weapon = "Hand-Nade";
      if(%pct > 20)
      {
         %accuracy = " [Accuracy:" @ %pct @ "%]";
         %points = %pct / 10 + 1;
         %velBonus = 0;
         %sound = $LakSnd::Bonus;
      }
      if(%pct >= 99)
      {
         %sound = $LakSnd::Special;
         %points = %points * 2;
      }
      if(%ma)
         %points = %points * 1.5;
   }
   else if(%type == $ShrapnelDamageType)
   {
      %weapon = "Grenade-Launcher";
      if(%ma && %pct >= 98)
      {
         %points = %distanceBonus / 1.85;
         %sound = $LakSnd::Bonus;
      }
      if(%pct >= 25 && !%ma && %distance >= 100 && %velTarget > 30)
      {
         %points = %longDistanceBonus / 1.85;
         %velBonus = %velBonus / 4;
         %long = 1;
         %sound = $LakSnd::Bonus;
      }
   }
   else if(%type == $MortarDamageType)
   {
      %weapon = "Mortar";
      if(%ma && %pct >= 98)
      {
         %points = %distanceBonus * 2.66;
         %sound = $LakSnd::Bonus;
      }
      if(%pct >= 25 && !%ma && %distance >= 100 && %velTarget > 30)
      {
         %points = %longDistanceBonus;
         %velBonus = %velBonus / 2;
         %long = 1;
         %sound = $LakSnd::Bonus;
      }
      if(!$LakRabbit::DuelMode)
         %value = %value / 2;
   }
   else if(%type == $MineDamageType)
   {
      %weapon = "MINE";
      %value = %value / 3;
      if(%ma)
      {
         %accuracy = " [Accuracy:" @ %pct @ "%]";
         %points = %pct / 7 + 2;
         %velBonus = 0;
         %sound = $LakSnd::Bonus;
         if(%pct >= 99)
         {
            %sound = $LakSnd::Special;
            %points = %points * 2;
         }
      }
      else
         %value = 0;
   }
   else if(%type == $BlasterDamageType)
   {
      %weapon = "Blaster";
      if(%ma)
      {
         %points = %distanceBonus / 2.5;
         %velBonus = %velBonus / 2;
         %sound = $LakSnd::Bonus;
         %value = %value * 0.7;
      }
      else
         %value = %value * 0.5;
   }
   else if(%type == $PlasmaDamageType)
   {
      %weapon = "Plasma";
      if(%ma && %pct >= 98)
      {
         %points = %distanceBonus / 1.3 + 2;
         %sound = $LakSnd::Bonus;
      }
      if(%pct >= 25 && !%ma && %distance >= 40 && %velTarget > 30)
      {
         %points = %longDistanceBonus * 1.5;
         %velBonus = %velBonus / 1.75;
         %long = 1;
         %sound = $LakSnd::Bonus;
      }
   }
   // Chaingun ($BulletDamageType) causes damage but gives no points.
   // The T2 accuracy award (each 5 hits) needs the number of shots.
   // T1 scripts cannot get that number.

   if(%points > 0)
      LakRabbit::award(%scl, %shooter, %points, %velBonus, %vel, %distance, %ma, %long, %weapon, %special, %accuracy);
   else if(%sound != "")
      messageAll(0, %sound);
   if(%sound != "" && %sRabbit)
      $LakPC[%scl, rabbitSplash] = %now;
   return %value;
}

function LakRabbit::endKnockback(%pl, %seq)
{
   if(%pl.lakKnockSeq == %seq)
      %pl.knockback = 0;
}

function LakRabbit::award(%scl, %shooter, %points, %velBonus, %vel, %distance, %ma, %long, %weapon, %special, %accuracy)
{
   %distance = floor(%distance);
   %points = floor(%points + %velBonus);
   if(%points <= 0)
      return;
   $LakPC[%scl, morepoints] += %points;
   if(%ma)
   {
      $LakPC[%scl, mas]++;
      $LakPC[%scl, totalSpeed] += %vel;
      $LakPC[%scl, totalDistance] += %distance;
      %hitType = "Mid-Air ";
   }
   else if(%long)
      %hitType = "Long-Distance ";
   else
      %hitType = "";
   %sound = $LakSnd::Bonus;
   %info = " point" @ lakPlural(%points) @ "! [" @ %hitType @ %weapon @ %special @ "] [Distance:" @ %distance @ "m] [Speed:" @ %vel @ "kph]" @ %accuracy;
   Client::sendMessage(%scl, 0, "You receive " @ %points @ %info @ %sound);
   MessageAllExcept(%scl, 0, Client::getName(%scl) @ " receives " @ %points @ %info @ %sound);
   bottomprint(%scl, "<jc><f1>+" @ %points @ " <f0>" @ %hitType @ %weapon, 3);

   if(%scl == $LakRabbitCl && %points >= 75 && lakAlive(%shooter))
      LakRabbit::heist(%shooter, %scl);
   else if(%points >= 100)
      LakRabbit::fireworks(%scl, %points);
   Game::refreshClientScore(%scl);
   LakRabbit::updateObjectives();
}

// Copy of player.cs Player::onDamage.
// Added: LakRabbit::onHit, before the damage is applied.
// Removed: the team damage part ($teamplay is always off).
function Player::onDamage(%this,%type,%value,%pos,%vec,%mom,%vertPos,%quadrant,%object)
{
	if (Player::isExposed(%this)) {
      %damagedClient = Player::getClient(%this);
      %shooterClient = %object;

		Player::applyImpulse(%this,%mom);
		if (Player::isDead(%this))
			return;
		// T2 setInvincible (flag grab, heist). No fall damage after a slap.
		if (%this.lakInvUntil > getSimTime())
			return;
		if (%this.knockback && (%type == $LandingDamageType || %type == $ImpactDamageType))
			return;
		$LakRabbit::LastRawSrc[%damagedClient] = %object;  // Damage source from the engine, for the tests.
		if($LakRabbit::Debug)
			echo("LakRabbit raw damage: target " @ %this @ " type " @ %type @ " value " @ %value @ " object [" @ %object @ "] objtype [" @ getObjectType(%object) @ "] targetClient " @ %damagedClient);
		if(%type == $ImpactDamageType && %object.clLastMount != "")
			%shooterClient = %object.clLastMount;
		// Test only: the engine gives source 0 for projectiles from AI
		// players. The laktest harness sets the shooter.
		if(%shooterClient == 0 && $LakTest::Shooter != "")
			%shooterClient = $LakTest::Shooter;
		%value = LakRabbit::onHit(%this, %damagedClient, %shooterClient, %type, %value, %pos);
		if (%value <= 0)
			return;
		%friendFire = 1.0;

		%armor = Player::getArmor(%this);
		//More damage applyed to head shots
		if(%vertPos == "head" && %type == $LaserDamageType) {
			if(%armor == "harmor") {
				if(%quadrant == "middle_back" || %quadrant == "middle_front" || %quadrant == "middle_middle") {
					%value += (%value * 0.3);
				}
			}
			else {
				%value += (%value * 0.3);
			}
		}
		// T2: heavy armor and medium armor get more damage.
		if(%armor == "harmor" || %armor == "marmor" || %armor == "mfemale") {
			if($LakRabbit::DuelMode && %shooterClient == $LakRabbitCl)
				%value = %value * 2;
			else
				%value = %value * 1.5;
		}
		//If Shield Pack is on
		if (%type != -1 && %this.shieldStrength) {
			%energy = GameBase::getEnergy(%this);
			%strength = %this.shieldStrength;
			if (%type == $ShrapnelDamageType || %type == $MortarDamageType)
				%strength *= 0.75;
			%absorb = %energy * %strength;
			if (%value < %absorb) {
				GameBase::setEnergy(%this,%energy - ((%value / %strength)*%friendFire));
				%thisPos = getBoxCenter(%this);
				%offsetZ =((getWord(%pos,2))-(getWord(%thisPos,2)));
				GameBase::activateShield(%this,%vec,%offsetZ);
				%value = 0;
			}
			else {
				GameBase::setEnergy(%this,0);
				%value = %value - %absorb;
			}
		}
		if (%value) {
			%value = $DamageScale[%armor, %type] * %value * %friendFire;
			%dlevel = GameBase::getDamageLevel(%this) + %value;
			%spillOver = %dlevel - %armor.maxDamage;
			GameBase::setDamageLevel(%this,%dlevel);
			%flash = Player::getDamageFlash(%this) + %value * 2;
			if (%flash > 0.75)
				%flash = 0.75;
			Player::setDamageFlash(%this,%flash);
			//If player not dead then play a random hurt sound
			if(!Player::isDead(%this)) {
				if(%damagedClient.lastDamage < getSimTime()) {
					%sound = radnomItems(3,injure1,injure2,injure3);
					playVoice(%damagedClient,%sound);
					%damagedClient.lastdamage = getSimTime() + 1.5;
				}
			}
			else {
				if(%spillOver > 0.5 && (%type== $ExplosionDamageType || %type == $ShrapnelDamageType || %type== $MortarDamageType|| %type == $MissileDamageType)) {
					Player::trigger(%this, $WeaponSlot, false);
					%weaponType = Player::getMountedItem(%this,$WeaponSlot);
					if(%weaponType != -1)
						Player::dropItem(%this,%weaponType);
					Player::blowUp(%this);
				}
				else
				{
					if ((%value > 0.40 && (%type== $ExplosionDamageType || %type == $ShrapnelDamageType || %type== $MortarDamageType || %type == $MissileDamageType )) || (Player::getLastContactCount(%this) > 6) ) {
						if(%quadrant == "front_left" || %quadrant == "front_right")
							%curDie = $PlayerAnim::DieBlownBack;
						else
							%curDie = $PlayerAnim::DieForward;
					}
					else if( Player::isCrouching(%this) )
						%curDie = $PlayerAnim::Crouching;
					else if(%vertPos=="head") {
						if(%quadrant == "front_left" ||	%quadrant == "front_right"	)
							%curDie = radnomItems(2, $PlayerAnim::DieHead, $PlayerAnim::DieBack);
						else
							%curDie = radnomItems(2, $PlayerAnim::DieHead, $PlayerAnim::DieForward);
					}
					else if (%vertPos == "torso") {
						if(%quadrant == "front_left" )
							%curDie = radnomItems(3, $PlayerAnim::DieLeftSide, $PlayerAnim::DieChest, $PlayerAnim::DieForwardKneel);
						else if(%quadrant == "front_right")
							%curDie = radnomItems(3, $PlayerAnim::DieChest, $PlayerAnim::DieRightSide, $PlayerAnim::DieSpin);
						else if(%quadrant == "back_left" )
							%curDie = radnomItems(4, $PlayerAnim::DieLeftSide, $PlayerAnim::DieGrabBack, $PlayerAnim::DieForward, $PlayerAnim::DieForwardKneel);
						else if(%quadrant == "back_right")
							%curDie = radnomItems(4, $PlayerAnim::DieGrabBack, $PlayerAnim::DieRightSide, $PlayerAnim::DieForward, $PlayerAnim::DieForwardKneel);
					}
					else if (%vertPos == "legs") {
						if(%quadrant == "front_left" ||	%quadrant == "back_left")
							%curDie = $PlayerAnim::DieLegLeft;
						if(%quadrant == "front_right" ||	%quadrant == "back_right")
							%curDie = $PlayerAnim::DieLegRight;
					}
					Player::setAnimation(%this, %curDie);
				}
				Client::onKilled(%damagedClient,%shooterClient, %type);
			}
		}
	}
}

//--- grenades and mines -------------------------------------------------------
// Copy of item.cs Grenade::onUse.
// Added: mark the grenade. Hand grenades and grenade launcher shells both
// use $ShrapnelDamageType. The mark shows which weapon caused the damage.
function Grenade::onUse(%player,%item)
{
	if($matchStarted) {
		if(%player.throwTime < getSimTime() ) {
			Player::decItemCount(%player,%item);
			%obj = newObject("","Mine","Handgrenade");
 	 	 	addToSet("MissionCleanup", %obj);
			%client = Player::getClient(%player);
			GameBase::throw(%obj,%player,9 * %client.throwStrength,false);
			%player.throwTime = getSimTime() + 0.5;
			%obj.lakOwner = %client;
			%obj.lakFrom = getBoxCenter(%player);
			%player.lakInvUntil = 0;  // T2: a hand grenade throw stops the damage immunity.
		}
	}
}

// Copy of item.cs MineAmmo::onUse.
// Added (T2): a mine explodes 2 s after it is deployed.
function MineAmmo::onUse(%player,%item)
{
	if($matchStarted) {
		if(%player.throwTime < getSimTime() ) {
			Player::decItemCount(%player,%item);
			%obj = newObject("","Mine","antipersonelMine");
		 	addToSet("MissionCleanup", %obj);
			%client = Player::getClient(%player);
			GameBase::throw(%obj,%player,15 * %client.throwStrength,false);
			%player.throwTime = getSimTime() + 0.5;
			$LakMineFrom[%client] = getBoxCenter(%player);
			schedule("Mine::Detonate(" @ %obj @ ");", 3, %obj);
		}
	}
}

// Copy of Mine.cs Mine::Detonate.
// Added: record the time and the owner of each hand grenade explosion.
function Mine::Detonate(%this)
{
	%data = GameBase::getDataName(%this);
	if(%this.lakOwner != "")
	{
		$LakNadeT[%this.lakOwner] = getSimTime();
		$LakNadeFrom[%this.lakOwner] = %this.lakFrom;
		$LakNadeAny = getSimTime();
	}
	GameBase::setDamageLevel(%this, %data.maxDamage);
}

// Copy of Mine.cs Mine::onDamage.
// Added (T2): in duel mode, hand grenades do not make mines explode.
function Mine::onDamage(%this,%type,%value,%pos,%vec,%mom,%object)
{
   if($LakRabbit::DuelMode && %type == $ShrapnelDamageType && getSimTime() - $LakNadeAny < 0.3)
      return;
   if (%type == $MineDamageType)
      %value = %value * 0.25;

	%damageLevel = GameBase::getDamageLevel(%this);
	GameBase::setDamageLevel(%this,%damageLevel + %value);
}

//--- votes (T2 VoteDuelMode / VoteSplashDamage / VotePro) -----------------------
function LakRabbit::killEveryone()
{
   messageAll(0, "Resetting...");
   for(%lw = 0; (%cl = getWord(lakClientList(), %lw)) != -1; %lw++)
      LakRabbit::kill(%cl, 0, $ExplosionDamageType, true);
}

function LakRabbit::toggleMode(%mode, %admin)
{
   %msg = "The Admin has chosen to " @ LakRabbit::modeName(%mode) @ ".";
   if(%admin <= 0)
      %msg = "Vote passed: " @ LakRabbit::modeName(%mode) @ ".";
   if(%mode == "duel")
   {
      LakRabbit::killEveryone();
      $LakRabbit::DuelMode = !$LakRabbit::DuelMode;
   }
   else if(%mode == "splash")
      $LakRabbit::NoSplashDamage = !$LakRabbit::NoSplashDamage;
   else if(%mode == "pro")
   {
      LakRabbit::killEveryone();
      $LakRabbit::PubPro = !$LakRabbit::PubPro;
   }
   else
      return;
   messageAll(1, %msg);
   LakRabbit::updateObjectives();
}

function LakRabbit::modeName(%mode)
{
   if(%mode == "duel")
   {
      if($LakRabbit::DuelMode) return "disable Duel Mode";
      return "enable Duel Mode";
   }
   if(%mode == "splash")
   {
      if($LakRabbit::NoSplashDamage) return "enable Splash Damage";
      return "disable Splash Damage";
   }
   if($LakRabbit::PubPro) return "disable Pro Mode (Disc, Blaster, Plasma only)";
   return "enable Pro Mode (Disc, Blaster, Plasma only)";
}

// Copy of admin.cs Admin::voteSucceded. Added: the LakRabbit modes.
function Admin::voteSucceded()
{
   $curVoteInitiator.numVotesFailed = "";
   if($curVoteAction == "kick")
   {
      if($curVoteOption.voteTarget)
         Admin::kick(-1, $curVoteOption);
   }
   else if($curVoteAction == "admin")
   {
      if($curVoteOption.voteTarget)
      {
         $curVoteOption.isAdmin = 1;
         messageAll(0, Client::getName($curVoteOption) @ " has become an administrator.");
         if($curVoteOption.menuMode == "options")
            Game::menuRequest($curVoteOption);
      }
      $curVoteOption.voteTarget = 0;
   }
   else if($curVoteAction == "cmission")
   {
      messageAll(0, "Changing to mission " @ $curVoteOption @ ".");
		Vote::changeMission();
      Server::loadMission($curVoteOption);
   }
   else if($curVoteAction == "tourney")
      Admin::setModeTourney(-1);
   else if($curVoteAction == "ffa")
      Admin::setModeFFA(-1);
   else if($curVoteAction == "etd")
      Admin::setTeamDamageEnable(-1, true);
   else if($curVoteAction == "dtd")
      Admin::setTeamDamageEnable(-1, false);
   else if($curVoteOption == "smatch")
      Admin::startMatch(-1);
   else if($curVoteAction == "lakmode")
      LakRabbit::toggleMode($curVoteOption, -1);
}

// Copy of admin.cs Game::menuRequest. Added: the LakRabbit mode items.
// Removed: the team damage and tournament items (LakRabbit does not use them).
function Game::menuRequest(%clientId)
{
   %curItem = 0;
   Client::buildMenu(%clientId, "Options", "options", true);
   if(!$matchStarted || !$Server::TourneyMode)
   {
      Client::addMenuItem(%clientId, %curItem++ @ "Change Teams/Observe", "changeteams");
   }
   if(%clientId.selClient)
   {
      %sel = %clientId.selClient;
      %name = Client::getName(%sel);

      if($curVoteTopic == "" && !%clientId.isAdmin)
      {
         Client::addMenuItem(%clientId, %curItem++ @ "Vote to admin " @ %name, "vadmin " @ %sel);
         Client::addMenuItem(%clientId, %curItem++ @ "Vote to kick " @ %name, "vkick " @ %sel);
      }
      if(%clientId.isAdmin)
      {
         Client::addMenuItem(%clientId, %curItem++ @ "Kick " @ %name, "kick " @ %sel);
         if(%clientId.isSuperAdmin)
         {
            Client::addMenuItem(%clientId, %curItem++ @ "Ban " @ %name, "ban " @ %sel);
            Client::addMenuItem(%clientId, %curItem++ @ "Admin " @ %name, "admin " @ %sel);
         }
         Client::addMenuItem(%clientId, %curItem++ @ "Change " @ %name @ "'s team", "fteamchange " @ %sel);
      }
      if(%clientId.muted[%sel])
         Client::addMenuItem(%clientId, %curItem++ @ "Unmute " @ %name, "unmute " @ %sel);
      else
         Client::addMenuItem(%clientId, %curItem++ @ "Mute " @ %name, "mute " @ %sel);
      if(%clientId.observerMode == "observerOrbit")
         Client::addMenuItem(%clientId, %curItem++ @ "Observe " @ %name, "observe " @ %sel);
   }
   if($curVoteTopic != "" && %clientId.vote == "")
   {
      Client::addMenuItem(%clientId, %curItem++ @ "Vote YES to " @ $curVoteTopic, "voteYes " @ $curVoteCount);
      Client::addMenuItem(%clientId, %curItem++ @ "Vote NO to " @ $curVoteTopic, "voteNo " @ $curVoteCount);
   }
   else if($curVoteTopic == "" && !%clientId.isAdmin)
   {
      Client::addMenuItem(%clientId, %curItem++ @ "Vote to change mission", "vcmission");
      Client::addMenuItem(%clientId, %curItem++ @ "Vote to " @ LakRabbit::modeName("duel"), "vlak duel");
      Client::addMenuItem(%clientId, %curItem++ @ "Vote to " @ LakRabbit::modeName("splash"), "vlak splash");
      Client::addMenuItem(%clientId, %curItem++ @ "Vote to " @ LakRabbit::modeName("pro"), "vlak pro");
   }
   else if(%clientId.isAdmin)
   {
      Client::addMenuItem(%clientId, %curItem++ @ "Change mission", "cmission");
      Client::addMenuItem(%clientId, %curItem++ @ "Set Time Limit", "ctimelimit");
      Client::addMenuItem(%clientId, %curItem++ @ "Admin: " @ LakRabbit::modeName("duel"), "lak duel");
      Client::addMenuItem(%clientId, %curItem++ @ "Admin: " @ LakRabbit::modeName("splash"), "lak splash");
      Client::addMenuItem(%clientId, %curItem++ @ "Admin: " @ LakRabbit::modeName("pro"), "lak pro");
      Client::addMenuItem(%clientId, %curItem++ @ "Reset Server Defaults", "reset");
   }
}

// Copy of admin.cs processMenuOptions. Added: the LakRabbit mode options.
function processMenuOptions(%clientId, %option)
{
   %opt = getWord(%option, 0);
   %cl = getWord(%option, 1);

   if(%opt == "vlak")
   {
      Admin::startVote(%clientId, LakRabbit::modeName(%cl), "lakmode", %cl);
      Game::menuRequest(%clientId);
      return;
   }
   else if(%opt == "lak")
   {
      if(%clientId.isAdmin)
         LakRabbit::toggleMode(%cl, %clientId);
      Game::menuRequest(%clientId);
      return;
   }

   if(%opt == "fteamchange")
   {
      %clientId.ptc = %cl;
      Client::buildMenu(%clientId, "Pick a team:", "FPickTeam", true);
      Client::addMenuItem(%clientId, "0Observer", -2);
      Client::addMenuItem(%clientId, "1Automatic", -1);
      for(%i = 0; %i < getNumTeams(); %i = %i + 1)
         Client::addMenuItem(%clientId, (%i+2) @ getTeamName(%i), %i);
      return;
   }
   else if(%opt == "changeteams")
   {
      if(!$matchStarted || !$Server::TourneyMode)
      {
         Client::buildMenu(%clientId, "Pick a team:", "PickTeam", true);
         Client::addMenuItem(%clientId, "0Observer", -2);
         Client::addMenuItem(%clientId, "1Automatic", -1);
         for(%i = 0; %i < getNumTeams(); %i = %i + 1)
            Client::addMenuItem(%clientId, (%i+2) @ getTeamName(%i), %i);
         return;
      }
   }
   else if(%opt == "mute")
      %clientId.muted[%cl] = 1;
   else if(%opt == "unmute")
      %clientId.muted[%cl] = "";
   else if(%opt == "vkick")
   {
      %cl.voteTarget = 1;
      Admin::startVote(%clientId, "kick " @ Client::getName(%cl), "kick", %cl);
   }
   else if(%opt == "vadmin")
   {
      %cl.voteTarget = 1;
      Admin::startVote(%clientId, "admin " @ Client::getName(%cl), "admin", %cl);
   }
   else if(%opt == "vsmatch")
      Admin::startVote(%clientId, "start the match", "smatch", 0);
   else if(%opt == "voteYes" && %cl == $curVoteCount)
   {
      %clientId.vote = "yes";
      centerprint(%clientId, "", 0);
   }
   else if(%opt == "voteNo" && %cl == $curVoteCount)
   {
      %clientId.vote = "no";
      centerprint(%clientId, "", 0);
   }
   else if(%opt == "kick")
   {
      Client::buildMenu(%clientId, "Confirm kick:", "kaffirm", true);
      Client::addMenuItem(%clientId, "1Kick " @ Client::getName(%cl), "yes " @ %cl);
      Client::addMenuItem(%clientId, "2Don't kick " @ Client::getName(%cl), "no " @ %cl);
      return;
   }
   else if(%opt == "admin")
   {
      Client::buildMenu(%clientId, "Confirm admim:", "aaffirm", true);
      Client::addMenuItem(%clientId, "1Admin " @ Client::getName(%cl), "yes " @ %cl);
      Client::addMenuItem(%clientId, "2Don't admin " @ Client::getName(%cl), "no " @ %cl);
      return;
   }
   else if(%opt == "ban")
   {
      Client::buildMenu(%clientId, "Confirm Ban:", "baffirm", true);
      Client::addMenuItem(%clientId, "1Ban " @ Client::getName(%cl), "yes " @ %cl);
      Client::addMenuItem(%clientId, "2Don't ban " @ Client::getName(%cl), "no " @ %cl);
      return;
   }
   else if(%opt == "smatch")
      Admin::startMatch(%clientId);
   else if(%opt == "vcmission" || %opt == "cmission")
   {
      Admin::changeMissionMenu(%clientId, %opt == "cmission");
      return;
   }
   else if(%opt == "ctimelimit")
   {
      Client::buildMenu(%clientId, "Change Time Limit:", "ctlimit", true);
      Client::addMenuItem(%clientId, "110 Minutes", 10);
      Client::addMenuItem(%clientId, "215 Minutes", 15);
      Client::addMenuItem(%clientId, "320 Minutes", 20);
      Client::addMenuItem(%clientId, "425 Minutes", 25);
      Client::addMenuItem(%clientId, "530 Minutes", 30);
      Client::addMenuItem(%clientId, "645 Minutes", 45);
      Client::addMenuItem(%clientId, "760 Minutes", 60);
      Client::addMenuItem(%clientId, "8No Time Limit", 0);
      return;
   }
   else if(%opt == "reset")
   {
      Client::buildMenu(%clientId, "Confirm Reset:", "raffirm", true);
      Client::addMenuItem(%clientId, "1Reset", "yes");
      Client::addMenuItem(%clientId, "2Don't Reset", "no");
      return;
   }
   else if(%opt == "observe")
   {
      Observer::setTargetClient(%clientId, %cl);
      return;
   }
   Game::menuRequest(%clientId);
}

// Start check. These engine functions operate only when players fight.
// This line shows in console.log if they do not operate.
echo("LakRabbit: selfcheck pow(2,3)=" @ pow(2, 3) @ " sub=" @ Vector::sub("3 2 1", "1 1 1") @ " norm=" @ Vector::normalize("3 0 4") @ " rot=" @ Vector::getFromRot("0 0 0", 10, 2) @ " abs=" @ lakAbs(-4) @ " scale=" @ lakScale("1 2 3", 2));
echo("LakRabbit: game rules loaded");
