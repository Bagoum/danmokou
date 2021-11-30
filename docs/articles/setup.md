# Setup

These are verbose instructions on how to set up Danmokou on your first run. 

## Part 0: Check Your Files

- Get Danmokou from [GitHub](https://github.com/Bagoum/danmokou). Details are as follows:
  - Install git if you don't have it.
  - Locate a folder in which you'd like to store the project files. The files will be stored directly in this directory. CD to the folder in your command line / terminal / bash shell.
  - Run `git init`.
  - Run `git remote add super https://github.com/Bagoum/danmokou.git`. This will allow you to reference the project repository via the alias "super".
  - Run `git pull super v8.0.0`.
    - v8.0.0 is the latest stable version. You can pick any version available [on the tags page](https://github.com/Bagoum/danmokou/tags), or you can run `git pull super master` to get the latest code, which may be less stable.
  - Run `git submodule update --init --recursive`. This will import code from several submodules, including SiMP, which is a fully structured game, and SuzunoyaUnity, which is a visual novel engine that powers the dialogue system.
    - If you do not want code from the extra submodules, then you can run `git submodule update --init --recursive Assets/SZYU` to only handle SuzunoyaUnity, which is required for the engine to work.
- If you already have a git repository with Danmokou, you can update it as follows:
  - **Assuming you have not made edits to any submodules:**
    - Run `git pull --rebase super v8.0.0`, replacing "v8.0.0" with whatever version you are updating to.
    - Run `git submodule update --recursive`.
      - Again, you can run `git submodule update --recursive Assets/SZYU` if you want to ignore the other submodules.
      - If updating from v7.0.0 or earlier, you will need to add an `--init` when updating SZYU.
  - **If you have made edits to submodules, you will need to `git pull --rebase` the individual submodules instead of running `git submodule update`**.

## Part 1: Unity Setup

- Download/Install Unity Hub (https://store.unity.com/download-nuo), then run it
- Within Unity Hub > Installs, add Unity version **2021.2** (Required for v8.0/-)
- Within Unity Hub > Projects, click ADD and browse to the root Danmokou folder. 
- Click on the project to load it. **The first time you load it, Unity may take several minutes to import resources.**
  - If you get an error that the project contains compilation errors, click "Ignore". Once the project has loaded, you should see a compilation error saying something about PanelSettings missing. If you trigger a recompile (eg. by adding code or comments to any C# file in the solution), the error will disappear.
  - If you get an error about the default layout, then close Unity, move the three `.dwlt` files in the `DMK_DEFAULTS` folder to the `Library` folder, and open Unity.
- Once you are in the Unity Editor, click on the Project tab (by default in the bottom window) and browse to Assets/Danmokou/Scenes/BasicSceneOPENME.
- (Error handling) **If you see a lot of pink squares in the game view where text should be**, then go to Window > TextMesh Pro > Import TMP Essential Resources and click Import in the popup window. If you can see the text, then you don't need to do this.

## Part 2: What is DMK?

- At the top of the game view, you should see three text boxes, saying something like: `Display 1 | Resolution | Scale ----`. Click on the second box, which shows the resolution of the game view. Either set it to the aspect ratio 16x9, or manually input a resolution at the bottom with 16x9 ratio. Also set the "Vsync" checkbox to true.
- You should still be in Assets/Danmokou/Scenes/BasicSceneOPENME. Go to the Hierarchy window (by default on the left side) and click on the "mokou-boss" object.
- The Inspector window (by default on the right side) will open up and list all the components on the object. The important component here is `Boss BEH`. Look under Boss BEH for the variable named "Behavior Script". To the right of it, you should see a text file titled "DMK Tutorial Setup 00". Double-click this file.
- Your text editor should now open the file. It should have the following contents:

```python
<#> warnprefix
## Go to https://dmk.bagoum.com/docs/articles/t01.html for the tutorial. 
pattern({ })
phase(0)
	paction(0)
		shift-phase-to(1)
		
## This is phase #1. 
<!> type(non, `Hello World`)
<!> hp(4000)
phase(0)
	paction(0)
		position(0, 1)
```

- This is a **behavior script**. Almost all interesting behavior in DMK is defined through behavior scripts, written in Bagoum Danmaku Scripting Markup (BDSM). Here are a few things to keep in mind about BDSM:
  - Comments can be placed on their own lines or at the end of lines. One hashtag makes a comment (like Python).
  - Indentation is not required, but you should do it.
  - Newlines are usually required.
  - Parentheses and argument separator commas are not required, unless you want infix mathematical operators (eg. `(1 + 2)` instead of `+ 1 2`).
- Run the scene by pressing the play button at the top of the screen. Objects should start animating and you should see the message "Hello World" at the top of the game UI. 
  - Note: you may seen cyan squares on the screen when certain objects first appear. This should only happen once per object type. For example, the first time you open the pause menu, you might see a cyan square. This is due to shader recompilation and is harmless. It does not occur in builds.
- Make sure that the FPS counter (bottom right of the game UI) is stable. If it is excessively high, then press Esc to open the in-game pause menu and turn Vsync OFF. If your computer is old, you may need to turn shaders OFF as well. These settings will be saved as soon as you close the menu.
- Because behavior in DMK is written in BDSM and not in C#, you can **recompile scripts at runtime** without significant overhead. To do this, simply press R in the game view while the game is running. If you are successful, you should see the "Hello World" message disappear and fade back in, and the boss health bar should empty out a bit. 
- That's all for setup. Feel free to move on to [the first tutorial](t01.md) once you're done with extra setup.

## Part Extra: Notepad++ Setup

You do not *need* to use Notepad++ to edit BDSM scripts, but I do not have language definitions for any other editor (though VSCode support may come in the future). As such, I recommend using Notepad++.

- Copy `npp-nautical-theme.xml` into `AppData/Roaming/Notepad++/themes`, and then select it in `Settings > Style Configurator` via the dropdown. Make sure `Global Styles > Global override > Enable global background color` is checked.
- Copy `npp-bdsm-language.xml` into `AppData/Roaming/Notepad++/userDefineLangs`, and then select "BDSM" (Bagoum Danmaku Scripting Markup) it under the Language dropdown when editing a script file.

## Part Extra: IDE Setup

When you get around to eventually extending the engine with your own functions and mechanics, you'll probably use some IDE for editing C#. The three most common are Rider, Visual Studio, and VS Code. 

If using VS Code, you may come across an issue where many references in the codebase do not compile. In this case, reinstall your C# extension to version 1.23.2. 
