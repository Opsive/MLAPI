# Netcode for GameObjects
WIP for Opsive Character Controller networked using the Unity Netcode for GameObjects.

<h3>Project Setup</h3><img src='https://user-images.githubusercontent.com/69744813/140709420-5cc80801-fef9-4afa-bf31-6b57fb94b470.png' alt="project"></img>

<h3>Tools => GreedyVox => Networked => Character Inspector</h3>
Setting up the player UCC prefab, use the tool menu to open up the dialog box, drag the Player prefab into the input field and than hit the Update Character button.

<h3>Tool Setup</h3><img src='https://user-images.githubusercontent.com/69744813/140706499-77f2d1de-05ec-468e-9f92-7c4e30696076.png' alt="tool"></img>

The Player prefab will now have all the networking scripts added and ready for networking, the Player prefab should look like the screen-shot below.

<h3>Character Setup</h3><img src='https://user-images.githubusercontent.com/69744813/133378417-48d0e5ac-444a-4a30-a4a8-dd7e955da06e.png' alt="player"></img>

Currently the Ai prefab has no tool for adding the scripts, will require to manually add the networking scripts, check the screen-shot below for setting up the Ai prefab for networking.

<h3>Ai Setup</h3><img src='https://user-images.githubusercontent.com/69744813/133378264-a83d806c-c78b-4c6c-8ae3-c29b77a34818.png' alt="ai"></img>

Currently the Object prefab has no tool for adding the scripts, will also require to manually add.

<h3>Object Setup</h3><img src='https://user-images.githubusercontent.com/69744813/133378345-393c2992-55da-49c9-b3e4-e8f401cd7143.png' alt="object"></img>

All prefabs assigned to he UCC Object Pool will automatically be pooled with the Networked Object Pool, when using the interface for spawning the prefabs over the network.

<h3>Object Pooling</h3><img src='https://user-images.githubusercontent.com/69744813/142705570-e0707d80-0df2-47bd-a097-65f56fa5947e.png' alt="pooling"></img>

Provides built-in support for Object Pooling, allows to override the default Netcode destroy and spawn handlers, for storing destroyed network objects in a pool for reuse.

<h3>Networked Pooling</h3><img src='https://user-images.githubusercontent.com/69744813/142705903-6912521e-5aaa-41d3-804f-670a5b062375.png' alt="spawning"></img>

```
m_SpawnObject = ObjectPool.Instantiate (m_CloneObject, Vector3.zero, Quaternion.identity);
NetworkedObjectPool.NetworkSpawn (m_CloneObject, m_SpawnObject, true);
```

Basic networked menu for testing, with some basic customization using unity GUIStyle.

<h3>Networked Menu</h3><img src='https://user-images.githubusercontent.com/69744813/142750865-b7422a45-492f-4666-a4c8-2f9b5b339f72.png' alt="gui"></img>

Unity sytle networking menu gui, help with testing Server/Client connection setup.

<h3>Networked GUI</h3><img src='https://user-images.githubusercontent.com/69744813/142750947-1b12a762-5455-4745-afde-ae507ad1eded.png' alt="gui"></img>
