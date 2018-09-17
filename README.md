# Shenmunity

Shenmunity lets you use Shenmue assets in Unity.

It should find your Shenmue installation automatically through Steam.

Currently only MT5 models are supported.

No assets are saved in your project - they are dynamically loaded from Shenmue every time.

# Note about submodules

This repo contains a full unity project with the actual plug in code as a git submodule. If you're not using git, you will need to download the plugin separately from https://github.com/Fishbiter/Shenmunity_plugin and ensure it's in the plugin folder. The path to Names.txt should be Assets/Plugins/Shenmunity/Names.txt

# Adding models

Models can be added using GameObject->Shenmunity->Model. 

Click "Choose asset" to browse a list of models in the game.

Unfortunately Shenmue only uses hashes as file identitiers so nothing is named. Most filenames have been filled in by comparison with the dreamcast assets, but you can give assets a name using the TAG window. These will be saved. 

# Mesh modes

Three mesh modes are supported:

* Skinned - this will produce a skinned mesh.
* Static - this will produce a single mesh with no skinning
* Individual - this will produce a separate mesh for each model node, useful for click-selecting parts of models.

# Transforms

When Shenmunity creates a model it will create the transform hierarchy as Unity transforms. These will not be recreated, so you can move them etc. Note it currently uses the names to re-hook them, so you mustn't rename them.

Note that if you change model the hierarchy will be destroyed.

Each transform has these options: 

* Collision Type - whether to generate a collider for this object.
* Generate Geometry - whether to create the geometry for this node.

# Creating avatars

Shenmue skeletons are slightly different to unity's. A Unity skeleton looks like this:

```
-Root
  |-Hips
    |-Spine
      |-Neck
        |-Head
      |-Left/Right Arm
    |-Left/Right Leg
```
    
Shenmue's normally look like this:

```
-Root
  |-Spine
      |-Neck
        |-Head
      |-Left/Right Arm
    |-Hips
      |-Left/Right Leg
```

You need to go through and label each ShenmueTransform by its bone use.

You may need to create a head transform.

The root bone (which is not marked by use) should be lifted so the character stands at 0 height and rotated 180 degrees so they look down positive Z.

The character should be put in T-Pose.

Finally, click "Create Avatar" on the Shenmue model and it should create an Animator component with an avatar. Watch the console for errors. If the transforms pop back you'll probably need to delete the Animator component and start again.

(Hopefully some of this can be automated in future.)

# Adding Scenes

Scenes can be added using GameObject->Shenmunity->Scene (CHRT). 

The format is not totally reversed, but most items should appear in the right place.
