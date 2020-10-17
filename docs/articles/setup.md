# Setup

These are verbose instructions on how to set up Danmokou on your first run. 

## Part 0: Check Your Files

- Download Danmokou. You can simply run `git clone https://github.com/Bagoum/danmokou.git`, or you can (NOT RECOMMENDED) download the engine as a zip from [the repo](https://github.com/Bagoum/danmokou). 
- (Optional) If you are using git, run `git submodule update --init --recursive`. This will import code from SiMP and my challenge-scene game jam project, which are both fully structured games that serve as good reference. If you are not using git, you can download them manually from the repos and place them in [Assets/SiMP](https://github.com/Bagoum/danmokou-simp) and [Assets/BRuH](https://github.com/Bagoum/danmokou-bruh) respectively. 
  -  (Note: the Yukari/Junko script in Assets/MiniProjects has some dependencies on the SiMP repository.)

## Part 1: Unity Setup

- Download/Install Unity Hub (https://store.unity.com/download-nuo)
- Within Unity Hub > Installs, add Unity version 2020.1.9f1
- Within Unity Hub > Projects, Click ADD and browse to the root Danmokou folder. 
- Click on the project to load it. **The first time you load it, Unity may take several minutes to import resources.**
  - If you get an error about the default layout, then close Unity, download the files in the `Library` folder from [the repo](https://github.com/Bagoum/danmokou), paste them in your Library folder, and open Unity.
- Once you are in the Unity Editor, click on the Project tab (by default in the bottom window) and browse to Assets/Danmokou/Scenes/BasicSceneOPENME.
- (Error handling) **If you see a lot of pink squares in the game view where text should be**, then go to Window > TextMesh Pro > Import TMP Essential Resources and click Import in the popup window. If you can see the text, then you don't need to do this.

## Part 2: What is DMK?

- At the top of the game view, you should see three text boxes, saying something like: `Display 1 | Resolution | Scale ----`. Click on the second box, which shows the resolution of the game view. Either set it to the aspect ratio 16x9, or manually input a resolution at the bottom with 16x9 ratio. Also set the "Vsync" checkbox to true.
- You should still be in Assets/Danmokou/Scenes/BasicSceneOPENME. Go to the Hierarchy window (by default on the left side) and click on the "mokou-boss" object.
- The Inspector window (by default on the right side) will open up and list all the components on the object. The important component here is `Boss BEH`. Look under Boss BEH for the variable named "Behavior Script". To the right of it, you should see a text file titled "DMK Tutorial Setup 00". Double click this file.
- Your text editor should now open the file. It should have the following contents:

```python
<#> warnprefix
## Go to https://dmk.bagoum.com/docs/articles/t01.html for the tutorial. 
pattern({ })
phase(0)
	action(block, 0)
		shift-phase-to(1)
		
## This is phase #1. 
<!> type(non, `Hello World`)
<!> hp(4000)
phase(0)
	action(block, 0)	
	position(0, 1)
```

- This is a **behavior script**. Almost all interesting behavior in DMK is defined through behavior scripts, written in Bagoum Danmaku Scripting Markup (BDSM). Here are a few things to keep in mind about BDSM:
  - Comments can be placed on their own lines or at the end of lines. One hashtag makes a comment (like Python).
  - Indentation is not required, but you should do it.
  - Newlines are usually required.
  - Parentheses and argument separator commas are not required.
- Run the scene by pressing the play button at the top of the screen. Objects should start animating and you should see the message "Hello World" at the top of the game UI. 
  - Note: you may seen cyan squares on the screen when certain objects first appear. This should only happen once per object type. For example, the first time you open the pause menu, you might see a cyan square. This is due to shader recompilation and is harmless. It does not occur in builds.
- Make sure that the FPS counter (bottom right of the game UI) is stable. If it is excessively high, then press Esc to open the in-game pause menu and turn Vsync OFF. If your computer is old, you may need to turn shaders OFF as well. These settings will be saved as soon as you close the menu.
- Because behavior in DMK is written in BDSM and not in C#, you can **recompile scripts at runtime** without significant overhead. To do this, simply press R in the game view while the game is running. If you are successful, you should see the "Hello World" message disappear and fade back in, and the boss health bar should empty out a bit. 
- That's all for setup. Feel free to move on to [the first tutorial](t01.md) once you're done with extra setup.

## Part Extra: Notepad++ Setup

You do not *need* to use Notepad++ to edit BDSM scripts, but I do not have language definitions for any other editor (though VSCode support may come in the future). As such, I recommend using Notepad++.

- Copy `npp-nautical-theme.xml` into `AppData/Roaming/Notepad++/themes`, and then select it in `Settings > Style Configurator` via the dropdown. Make sure `Global Styles > Global override > Enable global background color` is checked.
- Copy `npp-bdsm-language.xml` into `AppData/Roaming/Notepad++/userDefineLangs`, and then select "BDSM" (Bagoum Danmaku Scripting Markup) it under the Language dropdown when editing a script file.

## Part Extra: F# Setup

Only do this if you are rebuilding the F# project. This is not necessary for the vast majority of users. If you have no idea what this means, then you don't need to do it.

- Build the F# project in the `FS` folder targeted at 4.7.2. I have provided DLLs so you don't have to do this unless you make changes to the F# code.
- Copy the output DLLs (Common, FParsec, FParsecCS, FSharp.Core, Parser, System.ValueTuple) to `Assets/Danmokou/Plugins/Self/Core`.