# BVHTools for Unity

A Unity tool for importing and playing BVH motion capture files on 3D characters.

## Features

- Import and parse BVH motion capture files
- Apply BVH animations to rigged characters in Unity
- Support for mirroring animations (left/right)
- Visual debugging with skeleton visualization
- Adjustable rotation mapping and smoothing
- Runtime BVH file loading and playback control

## Installation

1. Clone this repository or download the scripts
2. Import the scripts into your Unity project
3. Place [`BVHParser.cs`](BVHParser.cs) and [`BVHPlayer.cs`](BVHPlayer.cs) into your project's Scripts folder

## Setup

1. Add the `BVHParser` component to your character's root GameObject
2. Configure the `BVHParser` settings:
   - Set the `Root Transform` to your character's root bone
   - Set `Bones Prefix` for your model (e.g., "mixamo:" for Mixamo models)
   - Adjust `Scale Factor` if needed (default: 0.01)
   - Configure rotation mapping if required
   - Enable/disable `Mirror Left To Right` as needed
   - Set `Rotation Smooth Factor` for animation smoothing

3. Add the `BVHPlayer` component to the same GameObject
4. Link the `BVHParser` reference in the BVHPlayer component

## Model Requirements

### Compatible Models
- Models from [Mixamo](https://www.mixamo.com/#/) (set bones_prefix to "mixamo:")(set bones_prefix to "mixamo:")
- Models with initial bone rotations set to 0
- Properly rigged humanoid characters

### Important Notes
1. For Mixamo models:
   - The script will automatically handle bone names like "mixamo:hips" by using the prefix setting
   - Set `bones_prefix = "mixamo:"` in the BVHParser component

2. Model Requirements:
   - Initial bone rotations should be 0 for best results
   - If your model has non-zero initial rotations:
     - Use a 3D modeling software (like Blender or Maya) to reset bone rotations
     - Export the model with zeroed rotations
     - Re-import into Unity

3. Bone Naming:
   - The script identifies bones by removing the prefix (e.g., "mixamo:hips" → "hips")
   - Ensure your BVH file uses matching bone names (after prefix removal)
   - Common bone names should follow standard conventions (hips, spine, head, etc.)

## Usage

### Playing BVH Animations

```csharp
// Play animation from code
BVHPlayer player = GetComponent<BVHPlayer>();
player.Play("path/to/your/animation.bvh");

// Stop animation
player.Stop();
```

### Inspector Controls

- Check `Play On Start` to automatically play a BVH file
- Check `Stop Animation` to stop the current animation
- Adjust `Rotation Smooth Factor` to control animation smoothness
- Toggle `Show Skeleton` for visual debugging

## Configuration

### BVHParser Settings

- `Scale Factor`: Adjusts the overall scale of the animation (default: 0.01)
- `Show Skeleton`: Enables visual debugging of the skeleton structure
- `Mirror Left To Right`: Mirrors the animation between left and right body parts
- `Rotation Smooth Factor`: Controls the smoothness of rotation transitions
- `Rotation Mapping`: Configures how rotations are mapped from BVH to Unity

### BVHPlayer Settings

- `BVH Parser`: Reference to the BVHParser component
- `BVH File Path`: Path to the BVH file
- `Play On Start`: Automatically plays the animation on start
- `Stop Animation`: Stops the current animation

