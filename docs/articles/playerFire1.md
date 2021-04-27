# Player Firing Conventions

**Note: Player firing support is currently a work in progress. If you have feature requests or architectural ideas, please reach out to me.**

**Note: This is not a tutorial on how to write player shots. A tutorial does not currently exist, but you may reference the scripts in Patterns/PlayerFire or the shot objects in SO/Firing.**

Players may use simple bullets, lasers, or pathers as firables.

To use simple bullets as a fire type, use the normal name of the bullet style, and use the `simple` command with the `player` property. The arguments are, in order:

- Damage dealt to boss enemies
- Damage dealt to stage enemies
- On-hit effect linked in GameManagement/ResourceManager

Here is the example fire code for homing amulets:

```python
	async amulet-*/ <> gcr2 12 _ <> { 
		delay(3 * p)
		sfx(pc-fire)
		colorf({ red black }, p)
	} gsr {
		clip <= powerf p
	} simple(nrvelocity(
		truerotatelerprate(60,
			rotify(py 10),
			LNearestEnemy - loc)), { 
			scale(1.4)
			player(21, 21, oh1)
		})
```

To use lasers as a fire type, use the `laser` command with the `player` property. The arguments are, in order:

- Countdown frames between successive hits
- Damage dealt to boss enemies
- Damage dealt to stage enemies
- On-hit effect linked in GameManagement/ResourceManager

Lasers also have a few special complexities. They are only fired once, but they track the location and rotation of their firing options, and they are destroyed when the player stops holding down the corresponding fire key. We'll work through the example to explain how this is implemented.

Here is the example fire code for homing lasers:

```python
		async gdlaser-*/b <> gcr {
			root zero
			preloop { 
				lastActiveTime =f _
			}
			colorf({ red black }, p // 2)
		} laser(nroffset(OptionLocation),
			0, _, {
				start(30 * (t - &lastActiveTime))
				varLength(15, 30 * t)
				dynamic(nrvelocity(laserrotatelerp(lerpt(3, 8, 0.7, 0),
					rotate(OptionAngle, cy 1),
					LNearestEnemy - loc
				)))
				delete(> (t - &lastActiveTime, 1))
				deactivate(> playerUnfiringTimeFocus 0)
				player(12, 25, 15, oh1)
		})
```

`lastActiveTime` is the time at which the laser got deactivated due to the player having stopped firing. Initially, we set this to infinity (`_`).

`OptionLocation` retrieves the location of the firing option using the firing index `p`. You should not modify `p` in player laser fires.

When the laser deactivates, we will have it appear to move away from the player, as if it was a projectile. To do this, we use the laser `start` property, which is the time along the dynamic laser path that the laser starts drawing. Once `lastActiveTime` is set to an actual value (when the laser is deactivated), the start value will increase past zero and the first parts of the laser will progressively disappear.

When the laser is fired, we will have it grow out of the option. To do this, we use the laser `varLength` property, which takes a maximum length and a length function.

The laser needs to follow the existing angle of the option. By default, player fire is assumed to point upwards, so OptionAngle returns 0 when the player is firing upwards. 

The laser needs to be destroyed shortly after it is deactivated. We use the laser property `delete` and instruct it to destroy itself one second after `lastActiveTime` is set.

We need code to deactivate the laser as well. We use the laser `deactivate` property, which sets `lastActiveTime` to `t` when the condition is true. In this case, the laser is a focus-type fire, so we deactivate the laser when the player stops firing the focus fire. PlayerUnfiringTimeFocus records the amount of time since the last focus fire.  (The architecture here is kind of hacky and will probably be improved in the future.)

Finally, we add the player fire configuration with the arguments listed above.

