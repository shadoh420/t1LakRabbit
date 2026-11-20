# LakRabbit for Tribes 1.11

This is a server-side port of the Tribes 2 LakRabbit game mode (LakRabbitGame.cs v3.40) to Tribes 1.11.
Players do not need to install anything. Only the server needs the mod.

## Rules

- One flag is on the map. The player with the flag is the rabbit. All other players are hunters.
- Damage occurs only between the rabbit and a hunter. Hunters cannot damage other hunters.
- Score = flag grabs + kills + bonus points + (time with the flag / 3 s).
- Bonus points come from mid-air hits and long-distance hits. Distance and speed increase the points.
- The rabbit cannot get damage for 2 s after a flag grab.
- When the rabbit dies, the flag flies out. After 25 s on the ground, the flag goes back to its start position.
- A mid-air disc hit can slap the target. A slap pushes the target far away. If the target is the rabbit, the rabbit drops the flag.
- Each hunter gets one free disc jump after each spawn.
- Players spawn with the LT loadout, near the rabbit or near the flag.
- Votes: Duel Mode, No-Splash Damage, Pro Mode.
- The game ends at 2000 points or at the time limit.

## Install

1. Copy the `lakrabbit` folder into the Tribes 1.11 folder (the folder that has `Tribes.exe`).
2. Start the dedicated server:

   ```
   Tribes.exe -mod lakrabbit -dedicated Peekaboo
   ```

The mod uses only the Deathmatch maps. It puts one neutral flag on each map.
If the server tries to load a map that is not a Deathmatch map, the mod loads Peekaboo.

## Settings

Set these values in a server script before the mission starts (for example `config\autoexec.cs`):

| Setting | Default | Function |
|---|---|---|
| `$LakRabbit::DuelMode` | 0 | The rabbit must kill hunters in a time limit. |
| `$LakRabbit::NoSplashDamage` | 0 | Splash damage does not hurt hunters. |
| `$LakRabbit::PubPro` | 0 | Pro loadout (disc, blaster, plasma). Kills give 5 points. |
| `$LakRabbit::UnlimitedDJ` | 0 | Hunters get an unlimited number of free disc jumps. |
| `$LakRabbit::ShowFlagTask` | 0 | Hunters get a waypoint to the rabbit. |
| `$LakRabbit::Debug` | 0 | Write each scored hit to the console. |

### Private test server

For a test server that must not show on a master server, add these lines to `config\autoexec.cs`.
The engine writes `ServerPrefs.cs` again when it stops, and it removes blank values. Thus you must not put these values only in `ServerPrefs.cs`.

```
$Server::HostPublicGame = false;
$Server::numMasters = 0;
$Server::Master1 = "";
for(%i = 0; %i < 8; %i++)
{
   $Server::MasterAddressN[%i] = "";
   $Server::XLMasterN[%i] = "";
}
```

## Tests

The `laktest` folder has tests that operate in the real engine. The tests spawn bots on a hidden dedicated server.
They call the game functions directly and fire real weapons. Then they compare the results with the T2 rules.

1. Copy the `lakrabbit` and `laktest` folders into the Tribes 1.11 folder.
2. Run:

   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File laktest\run_tests.ps1
   ```

The script shows one `LAKTEST PASS` or `LAKTEST FAIL` line for each check. The exit code is 0 only if all checks pass.
The tests do not examine the item purchase of human players, the vote menus or network play.

## Differences from Tribes 2

T1 does not have all the T2 functions. These are the replacements:

| T2 | T1 replacement |
|---|---|
| Distance and speed from the projectile start point | A history of each player (10 samples each second). The mod finds the sample that agrees with the flight time. |
| Ground raycast (mid-air test, height) | `GameBase::getLOSInfo` with a pitch of 90 degrees down. |
| Direct-hit percent | T1 disc and plasma explode at the surface of the target. The mod uses the measured damage of a direct hit as 100%. |
| Hand grenade and grenade launcher have different damage types | They have the same type in T1. The mod records hand grenade explosions to find the weapon. |
| Shocklance | T1 does not have this weapon. |
| Missile attack on all players (rabbit hit of 75 points or more) | The rabbit cannot get damage for 5 s. |
| Fireworks (hit of 100 points or more) | A message in the center of the screen. |
| Flag icon, sensor groups | Not available. Use the waypoint setting. |
| Debrief table | The Objectives (O) screen. |
| Chaingun accuracy points | The chaingun causes damage but gives no points. T1 scripts cannot count shots. |
| Flag capture on CTF maps | Not included. |

## Notes for script authors

The tests found these 1.11 engine behaviors:

- AI client ids do not keep script fields. `Client::getFirst/getNext` do not include AI clients.
- `buyItem` stops the server when it is used for an AI client.
- Projectiles from AI players give damage source 0 to `Player::onDamage`.
- `Vector::getRotation` also sets a pitch. For a player, set the yaw only (forward is +y).

The mod contains copies of stock Tribes 1.11 script functions with changes. T1 has no `parent::`.
