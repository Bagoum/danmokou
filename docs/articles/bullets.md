# Bullet Types

The currently implemented bullet types are as follows, in increasing order of rendering priority:

| Bullet Type                                 | Notes                             |
| ------------------------------------------- | --------------------------------- |
| **Lasers** (`laser` command)                |                                   |
| Laser                                       |                                   |
| GLaser                                      | Additive rendering                |
| GDLaser                                     | Additive rendering                |
| Mulaser                                     | Should not be given a hitbox      |
| Zonelaser                                   | Should not be given a hitbox      |
| **Pathers** (`pather` command)              |                                   |
| Pather                                      |                                   |
| GPather                                     | Additive rendering                |
| Lightning                                   |                                   |
| **Simple Bullets** (`s`, `simple` commands) |                                   |
| SCircle                                     | Nonrotational                     |
| LCircle                                     |                                   |
| LEllipse                                    |                                   |
| LStar                                       |                                   |
| Fireball                                    |                                   |
| Sakura                                      |                                   |
| Amulet                                      |                                   |
| Circle                                      |                                   |
| Ellipse                                     |                                   |
| Gem                                         |                                   |
| Shell                                       |                                   |
| Arrow                                       |                                   |
| Star                                        |                                   |
| Triangle                                    | Used as suicide bullets           |
| Strip                                       |                                   |
| Sun                                         | Additive rendering, nonrotational |
| GLCircle                                    | Additive rendering                |
| Keine                                       | Additive rendering                |
| GCircle                                     | Additive rendering                |

When summoning bullets, use the format: `{STYLE}-{COLOR}{VARIANT}`, where:

- style is one of the styles listed above, but in lowercase
- color is one of: black, purple, teal, green, orange, yellow, red, pink, blue
- variant is one of: `/w`,`/`, `/b` (light, colored, and inverted colorings respectively)

The engine uses runtime texture recoloring, so you may see one lag spike the first time you use a SCircle or Sun style, as they take a while to instantiate.

The rendering order of colors is as listed: 

`black, purple, teal, green, orange, yellow, red, pink, blue`

where black is the lowest and blue is the highest.

You can add colors in Bullet Manager: Basic Gradient Palettes on the GameManagement prefab. 

Between variants, the order looks like this:

`red/b < blue/b < red/ < blue/ < red/w < blue/w`

# Miscellaneous Bullet Notes

## Culling

- Empty bullets have a significantly larger cull radius, to prevent trailing bullets from getting stuck.
- Pathers are culled automatically, but the culling is handled by a callback passed to the rendering script. Thus culling is disabled in the bullet script.
- Lasers are not culled automatically. If you need to cull lasers (instead of letting them die normally) then use a `beh-pool` command.