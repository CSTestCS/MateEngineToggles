# VRoid Clothing Toggles for MateEngine

Lets VRoid models use MateEngine's built-in **Clothes** menu. When a VRoid `.vrm` is
loaded, the mod finds its clothing layers (Tops, Bottoms, Onepiece, Shoes, accessories…)
and adds one toggle button per layer.

- **Your models are not changed.** Nothing is written to any `.vrm` file, and models
  import exactly as before. Everything happens in memory while the model is shown.
- **Your choices are remembered** per model (in `BepInEx/config/VRoidClothingToggles.states.txt`).
- Models that already have their own clothing setup (made with MateEngine's model
  exporter) are left alone.

## Why this is not a `.me` file

A `.me` mod is a Unity asset bundle. MateEngine only uses the scripts that are already
built into the game, and asset bundles can't carry new code. Hiding VRoid
clothing needs new code: VRoid puts most clothing inside one "Body" mesh as separate
material slots, and no built-in MateEngine component can hide those. So this mod is a
small [BepInEx](https://github.com/BepInEx/BepInEx) plugin. BepInEx is the standard mod
loader for Unity games, and you install it once.

## Install

1. **Install BepInEx 5 (one time).** Download `BepInEx_win_x64_5.4.23.x.zip` (the latest
   5.4.x release for Windows x64) from <https://github.com/BepInEx/BepInEx/releases>.
   Extract it into the MateEngine game folder: the folder containing `MateEngineX.exe`.
   In Steam: right-click MateEngine → *Manage* → *Browse local files*.
   You should now have `winhttp.dll`, `doorstop_config.ini` and a `BepInEx` folder next to
   `MateEngineX.exe`.
2. Start MateEngine once and close it, so BepInEx creates its folders.
3. **Install this mod.** Extract [`dist/VRoidClothingToggles.zip`](dist/VRoidClothingToggles.zip)
   into the same game folder. That places the DLL at
   `BepInEx/plugins/VRoidClothingToggles/VRoidClothingToggles.dll`.
   You can also copy [`dist/VRoidClothingToggles.dll`](dist/VRoidClothingToggles.dll) into
   `BepInEx/plugins` yourself.
4. Start MateEngine and load a VRoid model.

## Use

- Open the avatar's radial menu. A **Clothes** button now appears for VRoid models.
  Each button toggles one layer on or off.
- MateEngine's Clothes menu shows at most 8 buttons. Press **F9** to open a small
  window that lists every layer, with *Show all* / *Hide all* buttons.

## Settings

`BepInEx/config/mateengine.vroidclothingtoggles.cfg` (created on first run):

| Setting            | Default | What it does                                                   |
|--------------------|---------|----------------------------------------------------------------|
| `ToggleWindowKey`  | `F9`    | Key for the layer list window. `None` turns it off.            |
| `AddToClothesMenu` | `true`  | Add the layers to MateEngine's built-in Clothes menu.         |

## How layers are detected

VRoid Studio names its materials like `N00_002_01_Tops_01_CLOTH`. Each distinct
clothing material becomes a layer, named after its part ("Tops", "Shoes", …). Skin,
face, eyes and hair are never offered as toggles. If a model doesn't use VRoid naming,
the mod looks for separate objects with clothing-like names (jacket, skirt, shoes, …).

To hide a layer, the mod points the renderer at an in-memory copy of the mesh with that
material slot emptied. Showing every layer again restores the original mesh.

## Known limitations

- **Holes under clothes.** VRoid Studio often deletes the body skin that is covered by
  clothing to save polygons. Hiding a layer then shows a gap where that skin used to
  be. This comes from how the model was exported; the mod doesn't create the gap.
- Layers that VRoid merged into the same material can't be separated, and are toggled
  together.
- Not tested in a running copy of MateEngine yet. If something doesn't work, check
  `BepInEx/LogOutput.log` for lines from `VRoid Clothing Toggles`.

## Troubleshooting

- **No log lines from the mod / BepInEx doesn't start:** make sure you used the
  *Windows x64* BepInEx 5 build and extracted it next to `MateEngineX.exe`, not into a
  subfolder.
- **No Clothes button:** the log says how many layers were found. "No clothing layers
  found" means the model's materials don't follow VRoid naming. "already has its own
  clothing setup" means the model ships its own Clothes menu.

## Building from source

Requires the .NET SDK (6.0 or newer). No game files are needed.

```bash
./build.sh                                   # builds and writes dist/
dotnet run --project tests/LayerDetectorTests  # name-matching checks
```

The project compiles against public Unity reference assemblies (NuGet
`UnityEngine.Modules`) and a small stub of the BepInEx API in `stubs/BepInEx`. The stub is
only used to compile. At runtime the plugin uses the real BepInEx installed in the game,
and it reaches MateEngine's own classes (`VRMLoader`, `MEClothes`) through reflection.

## Files

| Path                              | Purpose                                               |
|-----------------------------------|-------------------------------------------------------|
| `src/VRoidClothingToggles/`       | Plugin source                                         |
| `stubs/BepInEx/`                  | Compile-only BepInEx API stub                         |
| `tests/LayerDetectorTests/`       | Checks for the material-name matching                 |
| `dist/`                           | Ready-to-install build                                |
