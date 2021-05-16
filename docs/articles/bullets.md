# Bullet Types

The currently implemented bullet types are as follows, in increasing order of rendering priority.

Note: if a bullet name starts with "G", that means it is a glowy style (usually rendered additively).

| Bullet Type                                 | Notes                             |
| ------------------------------------------- | --------------------------------- |
| **Lasers** (`laser` command)                |                                   |
| Laser                                       |                                   |
| StretchLaser                                |                                   |
| ArrowLaser                                  |                                   |
| SunnyLaser                                  |                                   |
| GLaser                                      | Additive rendering                |
| GStretchLaser                               | Additive rendering                |
| GDLaser                                     | Additive rendering                |
| MuLaser                                     | Should not be given a hitbox      |
| ZoneLaser                                   | Should not be given a hitbox      |
| **Pathers** (`pather` command)              |                                   |
| Pather                                      |                                   |
| ArrowPather                                 |                                   |
| Lightning                                   |                                   |
| GPather                                     | Additive rendering                |
| **Simple Bullets** (`s`, `simple` commands) |                                   |
| SCircle                                     | Nonrotational                     |
| EllyScythe                                  |                                   |
| StellDecagon                                |                                   |
| LCircle                                     |                                   |
| LEllipse                                    |                                   |
| LStar                                       |                                   |
| Fireball                                    |                                   |
| Sakura                                      |                                   |
| Apple                                       |                                   |
| Amulet                                      | Also AmuletReimu                  |
| Circle                                      |                                   |
| Ellipse                                     |                                   |
| Gem                                         |                                   |
| Shell                                       |                                   |
| Arrow                                       |                                   |
| Star                                        |                                   |
| Triangle                                    | Default suicide bullet style      |
| Dot                                         |                                   |
| Strip                                       |                                   |
| Sun                                         | Additive rendering, nonrotational |
| GLCircle                                    | Additive rendering                |
| Keine                                       | Additive rendering                |
| GCircle                                     | Additive rendering                |
| GDCircle                                    | Additive rendering                |

When summoning bullets, use the format: `{SHAPE}-{COLOR}{GRADIENT}`, where:

- shape is one of the styles listed above, in lowercase
- color is one of: black, purple, teal, green, orange, yellow, red, pink, blue
- gradient is one of: `/w`,`/`, `/b` (light, colored, and inverted colorings respectively)

The engine uses runtime texture recoloring, so you may see one lag spike the first time you use a SCircle or Sun style, as they take a while to instantiate.

The rendering order of colors is as listed: 

`black, purple, teal, green, orange, yellow, red, pink, blue`

where black is the lowest and blue is the highest.

You can add colors in Bullet Manager: Basic Gradient Palettes on the GameManagement prefab. 

Between gradients, the order looks like this:

`red/b < blue/b < red/ < blue/ < red/w < blue/w`

To add new bullets, create a prefab (preferably somewhere in `Danmokou/Prefab/Bullets`) and then link the prefab in the `Danmokou/SO/References/Bullet Styles` object. 

# Miscellaneous Bullet Notes

## Culling

- Empty bullets have a significantly larger cull radius, to prevent trailing bullets from getting stuck.
- Pathers are culled automatically, but the culling is handled by a callback passed to the rendering script. Thus culling is disabled in the bullet script.
- Lasers are not culled automatically. If you need to cull lasers (instead of letting them die normally) then use a `beh-pool` command.

