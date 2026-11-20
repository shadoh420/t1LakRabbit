# LakRabbit for Tribes 1.11

Server-side port of the Tribes 2 LakRabbit game mode (v3.40). Clients need nothing.

## Rules

- One flag. Whoever holds it is the rabbit; everyone else hunts the rabbit.
- Damage only happens between the rabbit and hunters.
- Score: flag grabs + kills + bonus points + 1 point per 3 s holding the flag.
- Bonus points for mid-air and long-distance hits, scaled by distance and speed.
- The rabbit is invulnerable for 2 s after a grab and drops the flag only on death.
- Some mid-air disc hits slap the target far away.
- Hunters get one free disc jump per life.
- LT loadout. Spawns are near the rabbit or the flag.
- Votes: Duel Mode, No-Splash, Pro Mode. Ends at 2000 points or the time limit.

## Install

Copy `lakrabbit` into the Tribes folder, then:

```
Tribes.exe -mod lakrabbit -dedicated Peekaboo
```

Only Deathmatch maps are used; each gets one neutral flag.

## Settings

| Setting | Default | Function |
|---|---|---|
| `$LakRabbit::DuelMode` | 0 | The rabbit must kill hunters in a time limit. |
| `$LakRabbit::NoSplashDamage` | 0 | Splash damage does not hurt hunters. |
| `$LakRabbit::PubPro` | 0 | Pro loadout (disc, blaster, plasma). Kills give 5 points. |
| `$LakRabbit::UnlimitedDJ` | 0 | Unlimited free disc jumps for hunters. |
| `$LakRabbit::SlapDropsFlag` | 0 | A slap makes the rabbit drop the flag (T2 behavior). |
| `$LakRabbit::ShowFlagTask` | 0 | Hunters get a waypoint to the rabbit. |
| `$LakRabbit::Debug` | 0 | Log each scored hit to the console. |

## Differences from Tribes 2

- No shocklance in T1.
- Rabbit hit of 75+ points: 5 s of invulnerability instead of the missile attack.
- Hit of 100+ points: a center-screen message instead of fireworks.
- Chaingun does damage but gives no points (T1 scripts cannot count shots).
- No flag icon; the flag home has a waypoint. Stats are on the Objectives screen.
- No flag capture on CTF maps.
