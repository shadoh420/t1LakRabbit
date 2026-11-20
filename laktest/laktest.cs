//-------------------------------------------------------------------
// LakRabbit tests in the engine. Start with: -mod lakrabbit -mod laktest
// The tests spawn bot players on a real dedicated server. They call the real
// functions (Flag::onCollision, Player::onDamage, and others) and fire real
// weapons. Each check writes "LAKTEST PASS|FAIL <name> <detail>" to
// console.log. At the end, the server stops. Use run_tests.ps1 to run them.
//-------------------------------------------------------------------

$LakTest::Enabled = 1;
$LakRabbit::AllowAI = 1;
$LakRabbit::Debug = 1;
$LakTest::Pass = 0;
$LakTest::Fail = 0;
echo("LAKTEST: harness loaded");

function LakTest::check(%name, %ok, %detail)
{
   if(%ok)
   {
      $LakTest::Pass++;
      echo("LAKTEST PASS " @ %name @ " " @ %detail);
   }
   else
   {
      $LakTest::Fail++;
      echo("LAKTEST FAIL " @ %name @ " " @ %detail);
   }
}

function LakTest::near(%a, %b, %tol)
{
   return lakAbs(%a - %b) <= %tol;
}

function LakTest::next(%step, %delay)
{
   schedule("LakTest::step" @ %step @ "();", %delay);
}

// Hit %pl with damage %type and %value from client %scl.
function LakTest::hit(%pl, %type, %value, %scl)
{
   Player::onDamage(%pl, %type, %value, getBoxCenter(%pl), "0 0 0", "0 0 0", "torso", "front_left", %scl);
}

function LakTest::lift(%pl, %h)
{
   %p = $LakTest::Home[%pl];
   GameBase::setPosition(%pl, getWord(%p,0) @ " " @ getWord(%p,1) @ " " @ getWord(%p,2) + %h);
   Item::setVelocity(%pl, "0 0 0");
}

function LakTest::ground(%pl)
{
   GameBase::setPosition(%pl, $LakTest::Home[%pl]);
   Item::setVelocity(%pl, "0 0 0");
}

function LakTest::bot(%name, %drop)
{
   %pos = GameBase::getPosition($LakDrop[%drop]);
   %rot = GameBase::getRotation($LakDrop[%drop]);
   if(AI::spawn(%name, "larmor", %pos, %rot, %name, "male2") == "false")
   {
      LakTest::check("spawn_" @ %name, false, "AI::spawn failed");
      return -1;
   }
   %cl = AI::getId(%name);
   %pl = Client::getOwnedObject(%cl);
   GameBase::setTeam(%cl, 0);
   GameBase::setTeam(%pl, 0);
   LakRabbit::addBot(%cl);
   return %cl;
}

function LakTest::start()
{
   if(!$matchStarted)
   {
      schedule("LakTest::start();", 1);
      return;
   }
   echo("LAKTEST: starting");
   // Use 3 drop points that are more than 20 m from the flag. If a bot is
   // near the flag, the engine collision gives it the flag before the test grab.
   %n = 0;
   for(%i = 0; %i < $LakDrops && %n < 3; %i++)
      if(Vector::getDistance(GameBase::getPosition($LakDrop[%i]), $LakFlag.originalPosition) > 20)
      {
         %use[%n] = %i;
         %n++;
      }
   $LakTest::A = LakTest::bot("LakA", %use[0]);
   $LakTest::B = LakTest::bot("LakB", %use[1]);
   $LakTest::C = LakTest::bot("LakC", %use[2]);
   LakTest::next(1, 1);
}

function LakTest::refs()
{
   $A = $LakTest::A;  $pA = Client::getOwnedObject($A);
   $B = $LakTest::B;  $pB = Client::getOwnedObject($B);
   $C = $LakTest::C;  $pC = Client::getOwnedObject($C);
}

// Spawn loadout, velocity, distance to the ground.
function LakTest::step1()
{
   LakTest::refs();
   // deleteVariables in Mission::init must not remove the rule tables or the sounds.
   LakTest::check("sensor_network_on", $SensorNetworkEnabled == 1, "value=[" @ $SensorNetworkEnabled @ "]");
   LakTest::check("tables_survive_init", $LakSplashType[$ShrapnelDamageType] == 1 && $LakBase[$ShrapnelDamageType] > 0 && $LakSnd::Bonus != "", "splash=[" @ $LakSplashType[$ShrapnelDamageType] @ "] snd=[" @ $LakSnd::Bonus @ "]");
   LakTest::check("bots_alive", lakAlive($pA) && lakAlive($pB) && lakAlive($pC), $pA @ " " @ $pB @ " " @ $pC);
   Game::playerSpawned($pA, $A, "larmor");
   Game::playerSpawned($pB, $B, "larmor");
   Game::playerSpawned($pC, $C, "larmor");
   %items = "";
   %ok = 1;
   for(%i = 0; (%it = $spawnBuyList[%i]) != ""; %i++)
   {
      if(%it == LightArmor)
         continue;
      %n = Player::getItemCount($A, %it);
      %items = %items @ %it @ "=" @ %n @ " ";
      if(%n < 1)
         %ok = 0;
   }
   LakTest::check("lt_loadout", %ok, %items @ "discammo=" @ Player::getItemCount($A, DiscAmmo) @ " grenadeammo=" @ Player::getItemCount($A, GrenadeAmmo) @ " bulletammo=" @ Player::getItemCount($A, BulletAmmo));
   LakTest::check("free_dj_on_spawn", $pA.freeDJ == 1, "freeDJ=" @ $pA.freeDJ);

   $LakTest::Home[$pA] = GameBase::getPosition($pA);
   $LakTest::Home[$pB] = GameBase::getPosition($pB);
   $LakTest::Home[$pC] = GameBase::getPosition($pC);

   Item::setVelocity($pC, "0 30 0");
   %s = lakSpeed($pC);
   LakTest::check("velocity_read", LakTest::near(%s, 108, 5), "kph=" @ %s @ " raw=" @ Item::getVelocity($pC));
   LakTest::ground($pC);
   LakTest::next(2, 1);
}

function LakTest::step2()
{
   LakTest::refs();
   %g = lakGroundDist($pB, 6);
   LakTest::check("ground_on_feet", %g < 3, "groundDist=" @ %g @ " pos=" @ GameBase::getPosition($pB));
   LakTest::check("not_ma_on_ground", !lakIsMA($pB), "");
   LakTest::lift($pB, 50);
   %g = lakGroundDist($pB, 500);
   LakTest::check("ground_at_50m", LakTest::near(%g, 50, 6), "groundDist=" @ %g);
   LakTest::check("ma_at_50m", lakIsMA($pB), "");
   LakTest::ground($pB);
   LakTest::next(3, 1);
}

// Flag grab, damage immunity, no damage between hunters.
function LakTest::step3()
{
   LakTest::refs();
   Flag::onCollision($LakFlag, $pB);
   LakTest::check("flag_grab", $LakRabbitCl == $B && $pB.carryFlag == $LakFlag && $LakPC[$B, flagGrabs] == 1, "rabbit=" @ $LakRabbitCl @ " carry=" @ $pB.carryFlag @ " grabs=" @ $LakPC[$B, flagGrabs]);
   LakTest::check("grab_mounts_flag", Player::getItemCount($pB, Flag) == 1, "count=" @ Player::getItemCount($pB, Flag));
   LakTest::check("grab_clears_dj", $pB.freeDJ == 0, "freeDJ=" @ $pB.freeDJ);

   %d0 = GameBase::getDamageLevel($pB);
   LakTest::hit($pB, $ExplosionDamageType, 0.5, $A);
   LakTest::check("rabbit_invincible_2s", GameBase::getDamageLevel($pB) == %d0, "dmg " @ %d0 @ " -> " @ GameBase::getDamageLevel($pB));

   %d0 = GameBase::getDamageLevel($pC);
   LakTest::hit($pC, $ExplosionDamageType, 0.5, $A);
   LakTest::check("hunter_vs_hunter_no_damage", GameBase::getDamageLevel($pC) == %d0, "dmg " @ %d0 @ " -> " @ GameBase::getDamageLevel($pC));
   LakTest::next(4, 2.3);
}

// Mid-air disc points, ground disc (no points), blaster damage on the ground.
function LakTest::step4()
{
   LakTest::refs();
   $LakTest::Roll = 100;  // No slap and no special.
   LakTest::lift($pB, 30);
   %dist = Vector::getDistance(getBoxCenter($pB), getBoxCenter($pA));
   %expect = floor(pow(%dist, 1.35) / 10 + 1 + pow(lakSpeed($pA), 1.125) / 10 + 1);
   %m0 = $LakPC[$A, morepoints];
   %d0 = GameBase::getDamageLevel($pB);
   LakTest::hit($pB, $ExplosionDamageType, 0.5, $A);
   %got = $LakPC[$A, morepoints] - %m0;
   LakTest::check("ma_disc_points", LakTest::near(%got, %expect, 1), "got=" @ %got @ " expect=" @ %expect @ " dist=" @ floor(%dist));
   LakTest::check("ma_disc_counts_ma", $LakPC[$A, mas] == 1, "mas=" @ $LakPC[$A, mas]);
   LakTest::check("ma_disc_damages_rabbit", GameBase::getDamageLevel($pB) > %d0, "dmg " @ %d0 @ " -> " @ GameBase::getDamageLevel($pB));
   LakTest::ground($pB);
   GameBase::setDamageLevel($pB, 0);

   %m0 = $LakPC[$A, morepoints];
   LakTest::hit($pB, $ExplosionDamageType, 0.5, $A);
   LakTest::check("ground_disc_no_points", $LakPC[$A, morepoints] == %m0, "got=" @ $LakPC[$A, morepoints] - %m0);
   GameBase::setDamageLevel($pB, 0);

   LakTest::hit($pB, $BlasterDamageType, 0.125, $A);
   %d = GameBase::getDamageLevel($pB);
   %expect = 0.125 * 0.5 * $DamageScale[larmor, $BlasterDamageType];
   LakTest::check("blaster_ground_half", LakTest::near(%d, %expect, 0.001), "dmg=" @ %d @ " expect=" @ %expect);
   GameBase::setDamageLevel($pB, 0);
   LakTest::next(5, 0.5);
}

// Free disc jump.
function LakTest::step5()
{
   LakTest::refs();
   LakTest::hit($pA, $ExplosionDamageType, 0.3, $A);
   LakTest::check("free_dj_no_damage", GameBase::getDamageLevel($pA) == 0, "dmg=" @ GameBase::getDamageLevel($pA) @ " freeDJ=" @ $pA.freeDJ);
   LakTest::hit($pA, $ExplosionDamageType, 0.3, $A);
   LakTest::check("second_dj_damages", GameBase::getDamageLevel($pA) > 0, "dmg=" @ GameBase::getDamageLevel($pA));
   GameBase::setDamageLevel($pA, 0);
   LakTest::next(6, 0.5);
}

// Slap: knockback, flag kept by default, flag drop if SlapDropsFlag is on,
// no fall damage, no grab for 2.5 s, flag return.
function LakTest::step6()
{
   LakTest::refs();
   $LakTest::Roll = 0;   // Make the slap occur.
   // Default ($LakRabbit::SlapDropsFlag = 0): the rabbit keeps the flag.
   LakTest::lift($pB, 30);
   LakTest::hit($pB, $ExplosionDamageType, 0.5, $A);
   LakTest::check("slap_default_keeps_flag", $LakRabbitCl == $B && $pB.carryFlag == $LakFlag && $pB.knockback == 1, "rabbit=" @ $LakRabbitCl @ " knock=" @ $pB.knockback);
   // Setting on (T2 behavior): the slap makes the rabbit drop the flag.
   $LakRabbit::SlapDropsFlag = 1;
   LakTest::lift($pB, 30);
   LakTest::hit($pB, $ExplosionDamageType, 0.5, $A);
   $LakRabbit::SlapDropsFlag = 0;
   $LakTest::Roll = "";
   LakTest::check("slap_drops_flag", $LakRabbitCl == "" && $pB.carryFlag == "" && $LakFlag.carrier == -1, "rabbit=" @ $LakRabbitCl @ " carrier=" @ $LakFlag.carrier);
   LakTest::check("slap_flag_item_removed", Player::getItemCount($pB, Flag) == 0, "count=" @ Player::getItemCount($pB, Flag));
   LakTest::check("slap_gives_dj_back", $pB.freeDJ == 1, "freeDJ=" @ $pB.freeDJ);
   LakTest::check("slap_knockback", $pB.knockback == 1 && lakSpeed($pB) > 100, "knock=" @ $pB.knockback @ " kph=" @ lakSpeed($pB));
   LakTest::check("slap_small_damage", GameBase::getDamageLevel($pB) < 0.05, "dmg=" @ GameBase::getDamageLevel($pB));
   %d0 = GameBase::getDamageLevel($pB);
   LakTest::hit($pB, $LandingDamageType, 0.3, -1);
   LakTest::check("knockback_no_fall_damage", GameBase::getDamageLevel($pB) == %d0, "dmg " @ %d0 @ " -> " @ GameBase::getDamageLevel($pB));
   LakTest::ground($pB);

   Flag::onCollision($LakFlag, $pB);
   LakTest::check("regrab_denied_2_5s", $LakFlag.carrier == -1, "carrier=" @ $LakFlag.carrier);

   %wp0 = $LakRabbit::FlagWaypointCount;
   LakRabbit::checkReturn($LakFlag, $LakFlag.pickupSequence);
   LakTest::check("flag_return_waypoint", $LakRabbit::FlagWaypointCount == %wp0 + 1, "count " @ %wp0 @ " -> " @ $LakRabbit::FlagWaypointCount);
   LakTest::check("flag_return_home", $LakFlag.atHome && Vector::getDistance(GameBase::getPosition($LakFlag), $LakFlag.originalPosition) < 1, "pos=" @ GameBase::getPosition($LakFlag));

   Game::refreshClientScore($B);
   %expect = $LakPC[$B, flagGrabs] + $LakPC[$B, kills] + $LakPC[$B, morepoints] + floor($LakPC[$B, flagTime] / 3);
   LakTest::check("score_formula", $LakPC[$B, score] == %expect, "score=" @ $LakPC[$B, score] @ " expect=" @ %expect @ " hold=" @ $LakPC[$B, flagTime]);
   LakTest::next(7, 0.5);
}

// Duel mode: timer start value, kill at time 0.
function LakTest::step7()
{
   LakTest::refs();
   $LakRabbit::DuelMode = 1;
   Flag::onCollision($LakFlag, $pC);
   %n = lakPlayingPlayers();
   %expect = 36 - %n * 4;
   if(%n == 2) %expect += 10;
   if(%expect < 20) %expect = 20;
   LakTest::check("duel_seconds", $LakPC[$C, duelSeconds] == %expect, "got=" @ $LakPC[$C, duelSeconds] @ " expect=" @ %expect @ " players=" @ %n);
   $LakPC[$C, duelSeconds] = 1;
   LakRabbit::duelTick();
   LakTest::check("duel_timeout_kills_rabbit", Player::isDead($pC) && $LakRabbitCl == "", "dead=" @ Player::isDead($pC) @ " rabbit=" @ $LakRabbitCl);
   $LakRabbit::DuelMode = 0;
   LakTest::next(8, 3);   // Wait until the 2.5 s grab block for B ends.
}

function LakTest::step8()
{
   LakTest::refs();
   LakRabbit::checkReturn($LakFlag, $LakFlag.pickupSequence);
   Flag::onCollision($LakFlag, $pB);
   LakTest::check("regrab_after_deny", $LakRabbitCl == $B, "rabbit=" @ $LakRabbitCl);
   LakTest::next(9, 2.3);
}

// Hand grenade and grenade launcher, no-splash mode, mortar half damage.
function LakTest::step9()
{
   LakTest::refs();
   LakTest::lift($pB, 30);
   $LakNadeT[$A] = getSimTime();
   $LakNadeFrom[$A] = getBoxCenter($pA);
   %m0 = $LakPC[$A, morepoints];
   LakTest::hit($pB, $ShrapnelDamageType, 0.5, $A);
   %expect = floor((100 / 10 + 1) * 2 * 1.5);
   LakTest::check("hand_nade_ma_full", $LakPC[$A, morepoints] - %m0 == %expect, "got=" @ $LakPC[$A, morepoints] - %m0 @ " expect=" @ %expect);
   LakTest::ground($pB);
   GameBase::setDamageLevel($pB, 0);

   $LakNadeT[$A] = 0;
   %m0 = $LakPC[$A, morepoints];
   LakTest::hit($pB, $ShrapnelDamageType, 0.4, $A);
   LakTest::check("gl_ground_no_points", $LakPC[$A, morepoints] == %m0, "got=" @ $LakPC[$A, morepoints] - %m0);
   GameBase::setDamageLevel($pB, 0);

   $LakRabbit::NoSplashDamage = 1;
   LakTest::hit($pA, $ShrapnelDamageType, 0.2, $B);
   LakTest::check("no_splash_on_hunter", GameBase::getDamageLevel($pA) == 0, "dmg=" @ GameBase::getDamageLevel($pA));
   $LakRabbit::NoSplashDamage = 0;
   GameBase::setDamageLevel($pA, 0);

   LakTest::hit($pA, $MortarDamageType, 0.6, $B);
   %expect = 0.3 * $DamageScale[larmor, $MortarDamageType];
   LakTest::check("mortar_half_damage", LakTest::near(GameBase::getDamageLevel($pA), %expect, 0.001), "dmg=" @ GameBase::getDamageLevel($pA) @ " expect=" @ %expect);
   GameBase::setDamageLevel($pA, 0);

   // Duel mode: when there are more players, the splash damage from a hunter
   // to the rabbit is less. The splash damage from the rabbit does not change.
   // (T2: holdingFlag ? 1 : mult)
   $LakRabbit::DuelMode = 1;
   GameBase::setDamageLevel($pB, 0);
   LakTest::hit($pB, $ShrapnelDamageType, 0.2, $A);
   %mult = 1 - (lakNumClients() - 1) / 10;
   if(%mult < 0.1) %mult = 0.1;
   %expect = 0.2 * %mult * $DamageScale[larmor, $ShrapnelDamageType];
   LakTest::check("duel_hunter_splash_scaled", LakTest::near(GameBase::getDamageLevel($pB), %expect, 0.001), "dmg=" @ GameBase::getDamageLevel($pB) @ " expect=" @ %expect @ " clients=" @ lakNumClients());
   LakTest::hit($pA, $ShrapnelDamageType, 0.2, $B);
   %expect = 0.2 * $DamageScale[larmor, $ShrapnelDamageType];
   LakTest::check("duel_rabbit_splash_full", LakTest::near(GameBase::getDamageLevel($pA), %expect, 0.001), "dmg=" @ GameBase::getDamageLevel($pA) @ " expect=" @ %expect);
   $LakRabbit::DuelMode = 0;
   GameBase::setDamageLevel($pA, 0);
   GameBase::setDamageLevel($pB, 0);

   LakTest::hit($pA, $LaserDamageType, 0.5, $B);
   LakTest::check("laser_banned", GameBase::getDamageLevel($pA) == 0, "dmg=" @ GameBase::getDamageLevel($pA));
   LakTest::next(10, 0.5);
}

// Kill of the rabbit: the kill counts, the flag drops.
function LakTest::step10()
{
   LakTest::refs();
   %k0 = $LakPC[$A, kills];
   GameBase::setDamageLevel($pB, 0.6);
   LakTest::hit($pB, $ExplosionDamageType, 0.5, $A);
   LakTest::check("rabbit_killed", Player::isDead($pB), "dmg=" @ GameBase::getDamageLevel($pB));
   LakTest::check("kill_credit", $LakPC[$A, kills] == %k0 + 1, "kills " @ %k0 @ " -> " @ $LakPC[$A, kills]);
   LakTest::check("death_drops_flag", $LakRabbitCl == "" && $LakFlag.carrier == -1 && !$LakFlag.atHome, "rabbit=" @ $LakRabbitCl @ " carrier=" @ $LakFlag.carrier);
   LakTest::next(11, 0.5);
}

// Out of bounds push, mode changes.
function LakTest::step11()
{
   LakTest::refs();
   Item::setVelocity($pA, "0 0 0");
   Player::leaveMissionArea($pA);
   %v = Item::getVelocity($pA);
   %h = lakLen(getWord(%v,0) @ " " @ getWord(%v,1) @ " 0");
   %toC = Vector::sub($LakCenter, GameBase::getPosition($pA));
   %dot = getWord(%v,0) * getWord(%toC,0) + getWord(%v,1) * getWord(%toC,1);
   LakTest::check("oob_push_to_centre", LakTest::near(%h, 25, 1) && %dot > 0, "hspeed=" @ %h @ " dot=" @ %dot);
   Player::enterMissionArea($pA);
   LakTest::check("oob_reentry", $pA.outArea == "", "outArea=" @ $pA.outArea);

   LakRabbit::toggleMode("splash", -1);
   %on = $LakRabbit::NoSplashDamage;
   LakRabbit::toggleMode("splash", -1);
   LakTest::check("toggle_splash", %on && !$LakRabbit::NoSplashDamage, "");
   LakTest::next(12, 0.5);
}

//--- real projectiles: bot D fires at the rabbit bot E ---------------------------
// Each line: weapon, ammo, expected damage type, time to hold the trigger (s).
$LakShot[0] = "DiscLauncher DiscAmmo 4 0.5";
$LakShot[1] = "PlasmaGun PlasmaAmmo 3 0.5";
$LakShot[2] = "Blaster none 8 0.3";
$LakShot[3] = "Chaingun BulletAmmo 1 0.6";
$LakShotCount = 4;

function LakTest::step12()
{
   %n = 0;
   for(%i = 0; %i < $LakDrops && %n < 5; %i++)
      if(Vector::getDistance(GameBase::getPosition($LakDrop[%i]), $LakFlag.originalPosition) > 20)
      {
         %use[%n] = %i;
         %n++;
      }
   $LakTest::D = LakTest::bot("LakD", %use[3]);
   $LakTest::E = LakTest::bot("LakE", %use[4]);
   LakTest::next(13, 1);
}

function LakTest::refs2()
{
   $D = $LakTest::D;  $pD = Client::getOwnedObject($D);
   $E = $LakTest::E;  $pE = Client::getOwnedObject($E);
}

function LakTest::step13()
{
   LakTest::refs2();
   Game::playerSpawned($pD, $D, "larmor");
   Game::playerSpawned($pE, $E, "larmor");
   Player::setItemCount($D, PlasmaGun, 1);
   Player::setItemCount($D, PlasmaAmmo, 30);
   Player::setItemCount($D, Blaster, 1);
   $LakTest::Home[$pD] = GameBase::getPosition($pD);
   LakRabbit::checkReturn($LakFlag, $LakFlag.pickupSequence);
   Flag::onCollision($LakFlag, $pE);
   LakTest::check("shot_rabbit_ready", $LakRabbitCl == $E, "rabbit=" @ $LakRabbitCl);
   $LakTest::Shot = 0;
   LakTest::next("Aim", 2.3);   // Wait until the 2 s rabbit immunity ends.
}

// Put D 30 m above its drop point. Put E 25 m from D, at the same height. Turn D to E.
function LakTest::placeShooters()
{
   %p = $LakTest::Home[$pD];
   %up = getWord(%p,0) @ " " @ getWord(%p,1) @ " " @ getWord(%p,2) + 30;
   GameBase::setPosition($pD, %up);
   GameBase::setPosition($pE, getWord(%p,0) + 25 @ " " @ getWord(%p,1) @ " " @ getWord(%p,2) + 30);
   Item::setVelocity($pD, "0 0 0");
   Item::setVelocity($pE, "0 0 0");
   // Set the yaw only. In Darkstar, forward is +y. Thus -pi/2 is the +x direction.
   // Do not use Vector::getRotation: it also sets a pitch.
   GameBase::setRotation($pD, "0 0 -1.5708");
}

function LakTest::stepAim()
{
   LakTest::refs2();
   if($LakTest::Shot >= $LakShotCount)
   {
      LakTest::next("AIFight", 0.5);
      return;
   }
   %s = $LakShot[$LakTest::Shot];
   Player::useItem($pD, getWord(%s, 0));
   GameBase::setDamageLevel($pE, 0);
   GameBase::setEnergy($pD, 100);
   LakTest::placeShooters();
   LakTest::next("Fire", 0.6);  // Wait for the weapon to become ready.
}

function LakTest::stepFire()
{
   LakTest::refs2();
   %s = $LakShot[$LakTest::Shot];
   LakTest::placeShooters();
   $LakRabbit::LastHit = "";
   $LakTest::M0 = $LakPC[$D, morepoints];
   $LakTest::Ammo0 = Player::getItemCount($D, getWord(%s, 1));
   $LakTest::MountedAtFire = Player::getMountedItem($pD, $WeaponSlot);
   $LakTest::Shooter = $D;   // The engine gives source 0 for projectiles from AI players.
   Player::trigger($pD, $WeaponSlot, 1);
   schedule("Player::trigger(" @ $pD @ ", " @ $WeaponSlot @ ", 0);", getWord(%s, 3));
   // Keep the two bots in position while the shot flies.
   for(%t = 0.05; %t < 1.1; %t += 0.05)
      schedule("LakTest::placeShooters();", %t);
   LakTest::next("Check", 1.2);
}

function LakTest::stepCheck()
{
   LakTest::refs2();
   %s = $LakShot[$LakTest::Shot];
   %w = getWord(%s, 0);
   %h = $LakRabbit::LastHit;
   %pts = $LakPC[$D, morepoints] - $LakTest::M0;
   $LakTest::Shooter = "";
   %detail = "lastHit=[" @ %h @ "] (type value pct dist ma nade) points=" @ %pts @ " mounted=" @ $LakTest::MountedAtFire @ " ammo " @ $LakTest::Ammo0 @ "->" @ Player::getItemCount($D, getWord(%s, 1)) @ " dmgE=" @ GameBase::getDamageLevel($pE);
   if(%w == "Chaingun")
   {
      // At 25 m, chaingun bullets spread. A hit is possible but not sure. The chaingun never gives points.
      LakTest::check("fire_Chaingun_no_points", %pts == 0, %detail);
   }
   else
   {
      LakTest::check("fire_" @ %w @ "_hits", getWord(%h, 0) == getWord(%s, 2), %detail);
      LakTest::check("fire_" @ %w @ "_direct_pct", getWord(%h, 2) >= 98, "pct=" @ getWord(%h, 2));
      LakTest::check("fire_" @ %w @ "_midair", getWord(%h, 4) == 1, "ma=" @ getWord(%h, 4));
      LakTest::check("fire_" @ %w @ "_distance", LakTest::near(getWord(%h, 3), 25, 4), "dist=" @ getWord(%h, 3));
      LakTest::check("fire_" @ %w @ "_points", %pts > 0, "points=" @ %pts);
   }
   $LakTest::Shot++;
   LakTest::next("Aim", 0.3);
}

//--- engine AI combat (as Spoonbot): does the engine give the bot client as the source? ---
// The engine AI attacks only enemies. Thus E goes to team 1 for this step.
// The step reports the result. It does not fail if the bot does not fire.
function LakTest::stepAIFight()
{
   LakTest::refs2();
   GameBase::setTeam($E, 1);
   GameBase::setTeam($pE, 1);
   GameBase::setDamageLevel($pE, 0);
   $LakRabbit::LastRawSrc[$E] = "none";
   %p = $LakTest::Home[$pD];
   GameBase::setPosition($pD, %p);
   GameBase::setPosition($pE, getWord(%p,0) + 20 @ " " @ getWord(%p,1) @ " " @ getWord(%p,2) + 2);
   Item::setVelocity($pD, "0 0 0");
   Item::setVelocity($pE, "0 0 0");
   Player::useItem($pD, DiscLauncher);
   // Spoonbot combat settings (BotGear.cs AI::setWeapons, spoonbot.cs IQ)
   AI::SetVar("LakD", iq, 240);
   AI::SetVar("LakD", triggerPct, 1000);
   AI::SetVar("LakD", attackMode, 1);
   AI::setAutomaticTargets("LakD");
   LakTest::next("AIFightCheck", 8);
}

function LakTest::stepAIFightCheck()
{
   LakTest::refs2();
   %src = $LakRabbit::LastRawSrc[$E];
   echo("LAKTEST INFO ai_fight source=[" @ %src @ "] botClient=" @ $D @ " dmgE=" @ GameBase::getDamageLevel($pE) @ " ammo=" @ Player::getItemCount($D, DiscAmmo) @ " lastHit=[" @ $LakRabbit::LastHit @ "]");
   LakTest::next("Done", 0.5);
}

function LakTest::stepDone()
{
   echo("LAKTEST DONE pass=" @ $LakTest::Pass @ " fail=" @ $LakTest::Fail);
   schedule("quit();", 1);
}
