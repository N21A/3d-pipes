# Testing the classic fixed-camera renderer

This branch replaces the CPU-heavy GDI+ projection renderer with a Windows OpenGL renderer.

## What changed

- fixed camera during each generated scene
- screen-aspect-aware procedural volume instead of a small cube
- smooth, time-based pipe extension at approximately 60 FPS
- cylindrical pipes and rounded multi-section elbows
- pure black background and a restrained classic colour palette
- three simultaneous pipes by default, with longer straight runs
- hardware depth buffering instead of manually sorting every object

The viewpoint may change slightly when the entire scene regenerates, but it does not move while the pipes grow.

## Build and run

From PowerShell in the repository root:

```powershell
.\Build.ps1
.\dist\3DPipes.exe /w
```

Open the settings window with:

```powershell
.\dist\3DPipes.exe /c
```

Test full-screen screensaver mode with:

```powershell
.\dist\3DPipes.exe /s
```

## Suggested first test

Use the defaults:

- 145 ms per grid section
- 3 simultaneous pipes
- 420 completed sections

Check the following:

1. The viewpoint remains stationary while the scene grows
2. Pipes are spread across the full width and height rather than forming one central cube
3. Corners appear rounded rather than using oversized balls
4. Motion remains smooth in a 1280 x 720 window and at full-screen resolution
5. Closing the application restores normal display and sleep behaviour

## Useful diagnostics

If the OpenGL context cannot initialise, Windows shows an error instead of silently falling back to the old renderer. Update the graphics driver and test again.

To compare CPU and GPU use, run the windowed preview and inspect Task Manager while the scene has reached roughly 300 sections.
