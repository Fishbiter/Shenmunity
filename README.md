# Shenmunity

Shenmunity lets you use Shenmue assets in Unity.

It should find your Shenmue installation automatically through Steam.

Currently only MT5 models are supported.

No assets are saved in your project - they are dynamically loaded from Shenmue every time.

# Adding models

Models can be added using GameObject->Shenmunity->Model. 

Click "Choose asset" to browse a list of models in the game.

Unfortunately Shenmue only uses hashes as file identitiers so nothing is named. As a work around for this you can give assets a name using the TAG window. These will be saved. I've named a few things to get you started.

# Mesh modes

Three mesh modes are supported:

* Skinned - this will produce a skinned mesh.
* Static - this will produce a single mesh with no skinning
* Individual - this will produce a separate mesh for each model node, useful for click-selecting parts of models.

# Transforms

When Shenmunity creates a model it will create the transform hierarchy as Unity transforms. These will not be recreated, so you can move them etc. Note it currently uses the names to re-hook them, so you mustn't rename them.

Note that if you change model the hierarchy will be destroyed.

Each transform has two options: 

* Collision Type - whether to generate a collider for this object.
* Generate Geometry - whether to create the geometry for this node.

