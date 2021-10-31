# Tutorial 10: Boss Configuration

Now that we've covered how to write patterns on a basic level, we'll discuss how to add flair to your boss scripts.

![Unity_OUBCnBtSTO](../images/Unity_voPATAKULN.jpg)

## Part 1: BossConfig

Open up `BasicSceneOPENME`. Attach `DMK Tutorial Example Boss Script` to the `mokou-boss` object. If you open the script, you should see the following:

```python
<#> warnprefix
## Go to https://dmk.bagoum.com/docs/articles/tbosses.html for the tutorial. 
pattern({ 
	##boss(mynewboss)
})
phase(0)
	paction(0)
		shift-phase-to(1)
		
<!> type(non, `This is a nonspell`)
<!> hp(4000)
<!> root(0, 1)
phase(0)
	paction(0)
		async sakura-pink/w <> gcrepeat {
			wait(14)
			times(_)
			frv2(angle(cosine(8p, 800, timersec(phaset))))
		} gsr2c 3 {
		} s(rvelocity(cx(3)))

<!> type(spell, `This is a spell`)
<!> hp(4000)
<!> root(0, 2)
phase(0)
	paction(0)
		async fireball-red/w <> gcrepeat {
			wait(10)
			times(_)
			frv2(angle(cosine(9p, 800, timersec(phaset))))
		} gsr2c 5 {
		} s(rvelocity(cx(5)))
        
		
<!> type(non, `This is a nonspell`)
<!> hp(4000)
<!> root(0, 1)
phase(0)
	paction(0)
		async sakura-pink/w <> gcrepeat {
			wait(14)
			times(_)
			frv2(angle(cosine(8p, 800, timersec(phaset))))
		} gsr2c 3 {
		} s(rvelocity(cx(3)))
```

Both phases are running the basic code for Border of Wave and Particle. However, note that they use the timer `phaset`, which we never restarted. `phaset` is a special timer which is restarted automatically when using a phase that is a card type or a stage type. If you try using a different timer, the bullets will not change their angle.

Now, we want to add the following:

- Boss-specific art should appear in the sidebars.
- The boss' remaining spells should appear as stars in the lower left corner.
- The boss should have a rotating hexagram over its health bar.
- When using a spell, the boss should have another special effect.
- The secondary HP display on the UI should reflect the boss HP. 
- A highlight should appear in the bottom gutter indicating where the boss is.
- All of this should be colored according to the boss color scheme.
- The background should automatically switch between a nonspell and spell background specific to the boss.
- The boss should create a cutin before each of its spellcards (but not its nonspells).

We are going to do all of this **with one line of code**-- specifically, the line commented out at the top `boss(mynewboss)`.

Go to any folder under Assets in the Unity Project window. Right click an empty space, click "Create", then mouse over "Data" (second from the top), and click "Boss Color Scheme" (first from the top). Do it again, clicking "Boss Configuration" (second from the top). You can rename these two objects as you like, but make sure you know which is the color scheme and which is the boss configuration.

We have just created two **ScriptableObjects**. For our purposes, a ScriptableObject is just a bundle of unchanging data that we want to keep around permanently. DMK makes ample use of scriptable objects to store metadata about various engine constructs like players, shots, bosses, stages, and even games.

Click on the color scheme. In the inspector window, you should see several variables (`Ui Color`, `Ui HP Color`, `Card Color R`, etc). The usages of the variables are as follows:

- Ui Color is the color of the drop-shadow applied to the boss title (lower left of screen), card names (top), and messages (bottom right).
- Ui HP Color is the color that is multiplied against the stars in each of the four corners that display the number of remaining spellcards on the boss.
- Card Color R/G/B control the colors of the hexagram spinning around the boss. R and G are the colors of the triangles and B is the color of the shadow.
- Spell Color 1 controls the color of the rotating text effect that only appears during spell cards.

Go ahead and assign colors to each of the variables by clicking on the black bar and changing the numbers. **Make sure to set the opacity!** As a recommendation, when setting the Spell Colors, you should use opacities less than 100%. 

Now click on the boss configuration, In the inspector window, you should see a lot of variables. Let's go through each of them:

- Boss is the object that will be created if the player selects this boss configuration in the practice selector, or if a stage script call this boss. Set it to `mokou-boss`, since that's the boss we're using.
- Card Practice Name is the name of the boss that will be displayed in the practice selector.
- Replay Name Override is the name of the boss that will be displayed in practice mode replays. If this is not set, it defaults to Card Practice Name.
- State Machine is the script that will be analyzed and instantiated for the practice selector, or when a stage script calls this boss. Set this to `DMK Tutorial Example Boss Script`.
- Key is the string by which a stage script will call this boss and boss scripts will access their own metadata. It must be unique among all bosses in the same game. Set it to `mynewboss`. 
- Casual Name / Casual Name JP are the forms of address for the boss used in challenge descriptions for challenge-based games. For example, if the challenge is to destroy the boss, then this will be described as `Destroy {casualName}` or `{casualNameJP}を倒せ` based on locale.
- Track Name is the name of the boss as appears on the glow in the bottom gutter that shows where the boss is on the screen. I usually use Japanese text here for brevity. If you want to use English text, you should change the font asset in Assets/Danmokou/Prefab/UI/Tracker>Container>TrackedName. Set this to `妹紅` (Mokou).
- Colors is the color scheme we set up just before this. Set this to the color scheme object you created.
- Card Rotator is a function that defines the velocity of rotation, in degrees, of the hexagram on the boss. You usually don't need to fill this out, as there's a default in place (open up BossConfig.cs and look for `defaultCardRotator`). 
- Spell Rotator is a function that does the same, but for the spell circle effect.
- Profile contains the left and right sidebar images that are displayed while the boss is active. For licensing reasons the images that are actually used in my published games are not included in the repository, so instead, click the little circle on the right of the input entry field and select any image you like. 
- Default Non BG and Default Spell BG are the backgrounds that the boss will automatically use when in a nonspell-type card or a spell-type card. If you have the SiMP repository downloaded, you can set these to backgrounds from there, such as `stage ex scroll` and `space.sun`. Otherwise, you can set them to `black` and `white`. 
- Default Into Spell Transition and Default Into Non Transition are the background transitions that the boss will automatically use when moving into a spell-type card or a nonspell-type card. I recommend `WipeTex1` and `Shatter4.Normal`. 
- Boss Cutin is a special type of cutin that you should use once at the start of a boss script. It is 4.8 seconds long (configurable) and is usually used to announce the boss name and some kind of character title. If you have the SiMP repository downloaded, set this to `v2mokou`. Otherwise, set it to `Cutin Junko` (note that this will produce some artifacting due to how the background is handled in the real boss cutins).
- Spell Cutins is an array of cutins to be used before each spell. By default, the zeroeth element in this array will be spawned before each spell. However, if you want to vary your spell cutins, you can add multiple objects to the array and then use the `<!> spellcutin(INDEX)` phase property to manually select the one you want to spawn before each card. Add one object and set it to `Cutin Junko`. 

You may notice that the color of the healthbar is oddly absent from this discussion. This is because the color of the healthbar is stored on the `Enemy` component, not in the boss configuration, since any Enemy can have a healthbar. The variables in question are `Nonspell Color`, `Spell Color`, and `Unfilled Color`. When fighting a boss, the white line at the bottom of the playable area will become a healthbar, and it will automatically copy the current color of the boss healthbar.

Okay, now we're ready to get all the boss data in. Uncomment the line `boss(mynewboss)` from the script and run it. 

What happens? You should get two errors in the console. The first should say `Failed to load attached SM on startup`. Click on the second and look at the stack trace. You should see the following:

```
Frame 0: Line 3: Tried to construct PatternSM.pattern, but failed to create argument #2/2 "props" (type PatternProperties).
Line 3: Tried to construct PatternProperties, but failed to create argument #1/1 "props" (type [PatternProperty]).
Exception has been thrown by the target of an invocation.
No boss configuration exists for key mynewboss.
```

Basically, we haven't told the engine about our boss configuration yet! If you get errors like this, it probably means that you created some metadata but forgot to link it in the appropriate place. 

Boss configurations are generally unique to a specific game, so they are stored in the `GameUniqueReferences` metadata aggregator. We will discuss the usage of GameUniqueReferences in a later tutorial, but the basic concept is that any data that is specific to a game is linked here. This prevents cross-pollution between multiple games in a single Unity project, and also allows us to avoid packing unnecessary resources into our executables when we export our projects. 

 If you click on the GameManagement object in the scene, the first variable under the component `Game Management` should be `References`, and it should point to the object `Default Game References`. Go ahead and click on this to show it in the Project window, then click it in the Project window to show all its variables. If you look at the Boss Metadata variable, you should see a list with one element, `Tutorial Boss`. Add a new element and set it to the boss configuration you created.

Now, restart the game. You should see all the changes! There should be one star in the lower left, since Mokou has 1 spell remaining when the script starts.

If you clear Mokou's nonspell, then you can see the background quickly transition, and the Junko cutin will fly across the screen. (Cutins will not appear if you set `TeleportAtPhaseStart` to true.) If you clear Mokou's spell, the background will transition back, but no cutin will appear, since the next card is a nonspell. Also, once you clear the spell, the number of stars will drop to 0. 

If we want to add a boss cutin, we must invoke it manually. Add the property `<!> bosscutin` to the first phase and run the script again. If you had set the boss cutin to `mokou3d`, then you'll see a fancy Mokou cutin. Otherwise, you'll see the same Junko cutin, but with a lot more waiting time. 

## Part 2: On Multiple Bosses

Strictly speaking, multiple boss *scripts* are not allowed in DMK. The engine is only able to handle UI modifications for one boss script at a time, and this would be fairly difficult to change. Instead, multiple bosses are handled by manipulating multiple BehaviorEntities from one script. 

Here are the current legal usages of multiple bosses:

- 1 main boss and 1+ invincible supports: Legal, do this by simply not assigning an Enemy component to the supports, and then using a structure similar to SiMP stage 4 (`SiMP/Scripts/simp.mima`):

```python
pattern { 
	bosses {
		simp.mima ## 0th element is the main boss
		simp.keine ## All other elements are spawned along with the script and destroyed when it ends
		simp.kaguya
	} {
		0 1 ## This selects which boss to use for the UI at any time. 
		5 2 ## Read this as: "From phase 5 onwards, use the boss with index #2 for the UI."
		8 0
	}
}
phase 0
	paction(0)
		@ { kaguya keine } ## BehaviorEntity IDs of the spawned support bosses
			position -15 0
```

(Note: when bosses are more than 5 units off screen, the trackers will not display. This code is in BottomTracker.cs.)

- 1 main boss and 1+ subbosses, all sharing the same health pool: Legal. Use the `diverthp` command. See the BoWaP script (`MiniProjects/jy.all`) for a reference:

```python
pattern { 
	bosses {
		jy.yukari 
		jy.junko 
	} {
		1 0
		3 1
		5 0
		7 1
		9 0
		11 1
	}
}

phase 0
	paction(0)
		@ junko
			position -15 0
		@ junko
			diverthp yukari ## `yukari` is the main boss BehaviorEntity ID
```

- 1 main boss and 1+ subbosses, all with different health pools: **Currently not supported**. This would require some rearchitecting of the HP pipeline. See the [Github ticket](https://github.com/Bagoum/danmokou/issues/2). Let me know if you have a use case.



For reference code on multiple bosses, please look at these two scripts. (Note that Junko fires zero bullets in `jy.all`, so it may not be the best reference.) There is really nothing much more to it other than using the `@` (redirection) state machine to make the secondary bosses do things, as well as using the `<!> rootother(junko, 0, 1)` command to designate the root position for the secondary bosses.

That's all for this tutorial!