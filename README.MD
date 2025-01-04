# LeakNinja

Tool for auto-detecting leaked unity objects, use for self control when writing code. Monitor & detection is done in runtime. Results are output in unity log console.
Note: it checks whether destroyed unity objects (game objects and assets) are garbage collected. Works on regular interval.

!! this tool not find holders. To find holders you need to remember what code your wrote, what changes did to assets (self-control during feature development). Or use additional profiler / write your own.

# Installation

There are ways to install this plugin:

- clone/download this repository and move the "Assets/LeakNinja" folder to your Unity project's "Assets" folder
- (via Package Manager) add the following line to Packages/manifest.json:
-- "com.deepscorn.leakninja": "https://github.com/Deepscorn/LeakNinja.git?path=Assets/LeakNinja",

# Usage

!! do not use in editor because objects are cached for inspector, project view, etc. For many leaked object, it's holders will be editor irrelevant stuff.
!! do not use in production builds because it's cpu expensive and lead to microfreezes.

To use just create gameobject and add LeakNinja component.
Or you can add it through code:

```csharp
        private LeakNinja.LeakNinja _leakNinja;

        private void OnEnable()
        {
            _leakNinja = LeakNinja.LeakNinja.Create(); // just create LeakNinja and it will work till your app quit
        }

        private void OnDisable()
        {
            Destroy(_leakNinja.gameObject); // if you want to stop LeakNinja, destroy it
        }
```

# How to read output

![Example log image](Images/OutputExample.png?raw=true "Example log")

Note: leaks are being outputed after each check. Sometimes check happen too early leading to false detection. If after second check you do not see leaks log, than this means no leaks (all objects are garbage collected).

# Tuning

Default LeakNinja behaviour is an expensive full search on each scene load. And comparably fast checking for leaks periodically. So this is trade-off between quality of detection and speed. 
This is how it's set up:

![Default setup image](Images/DefaultSetup.png?raw=true "Default setup")

This mode wount find spawned prefabs in runtime.
If you need e.g. to find them too, you need setup. Setup consists from behaviours to choose on callbacks: start, scene load/unload and periodic. Setup to find prefabs:

![Full expensive setup image](Images/FullExpensiveSetup.png?raw=true "Full expensive setup")

Note: this is most freezing setup, but give you all the objects (meshes textures etc) whether referenced from scene or not, even created programmatically.

To make tool work smoother you need to adopt to setup based on you app structure. Or do not use Watch behaviours and add watches manually:

```csharp
LeakNinja.Manual.Watch(<your object>)
```

# Tests
Tool can be opened as project in unity and checked that it works with Test Runner (Window -> General -> Test Runner).
Other way (windows only) is to edit file "run_tests_win.bat", replace unity version with your own:
"%ProgramFiles%\Unity\Hub\Editor\2023.2.17f1\Editor\Unity.exe"

And run it. Test will be saved in ".test" directory in repo root
