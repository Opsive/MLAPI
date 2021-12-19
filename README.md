# Netcode for GameObjects
WIP for Opsive Character Controller networked using the Unity Netcode for GameObjects.

<h3>Project Setup</h3><img src='https://user-images.githubusercontent.com/69744813/146672484-3632be03-ba4c-4802-aff0-e3eccdb8027f.png' alt="project"></img>

Basic networked menu for testing, with some basic customization using unity GUIStyle.

<h3>Networked Menu</h3><img src='https://user-images.githubusercontent.com/69744813/142750865-b7422a45-492f-4666-a4c8-2f9b5b339f72.png' alt="gui"></img>

Unity sytle networking menu gui, help with testing Server/Client connection setup.

<h3>Networked GUI</h3><img src='https://user-images.githubusercontent.com/69744813/142750947-1b12a762-5455-4745-afde-ae507ad1eded.png' alt="gui"></img>

<h3>Player Tool</h3><img src='https://user-images.githubusercontent.com/69744813/140709420-5cc80801-fef9-4afa-bf31-6b57fb94b470.png' alt="project"></img>

<h5>Tools => GreedyVox => Networked => Character Inspector</h5>
Setting up the player UCC prefab, use the tool menu to open up the dialog box, drag the Player prefab into the input field and than hit the Update Character button.

<h3>Tool Setup</h3><img src='https://user-images.githubusercontent.com/69744813/140706499-77f2d1de-05ec-468e-9f92-7c4e30696076.png' alt="tool"></img>

The Player prefab will now have all the networking scripts added and ready for networking, the Player prefab should look like the screen-shot below.

<h3>Player Prefab</h3><img src='https://user-images.githubusercontent.com/69744813/146673476-198d1af1-e7b4-4ea9-be57-193cff5d6c49.png' alt="player"></img>

<h3>Ai Tool</h3><img src='https://user-images.githubusercontent.com/69744813/146673319-b41e8c94-5cde-4f60-b841-da949cadee85.png' alt="project"></img>

<h5>Tools => GreedyVox => Networked => Character Ai Inspector</h5>
Setting up the Character Ai UCC prefab, use the tool menu to open up the dialog box, drag the Character Ai prefab into the input field and than hit the Update Character button.

<h3>Tool Setup</h3><img src='https://user-images.githubusercontent.com/69744813/146673180-03ab5c7b-1695-45d0-a728-291c7140f887.png' alt="tool"></img>

The Character Ai prefab will now have all the networking scripts added and ready for networking, the Character Ai prefab should look like the screen-shot below.

<h3>Ai Prefab</h3><img src='https://user-images.githubusercontent.com/69744813/133378264-a83d806c-c78b-4c6c-8ae3-c29b77a34818.png' alt="ai"></img>

<h3>Object Setup</h3><img src='https://user-images.githubusercontent.com/69744813/133378345-393c2992-55da-49c9-b3e4-e8f401cd7143.png' alt="object"></img>

All prefabs assigned to he UCC Object Pool will automatically be pooled with the Networked Object Pool, when using the interface for spawning the prefabs over the network.

<h3>Object Pooling</h3><img src='https://user-images.githubusercontent.com/69744813/142705570-e0707d80-0df2-47bd-a097-65f56fa5947e.png' alt="pooling"></img>

Provides built-in support for Object Pooling, allows to override the default Netcode destroy and spawn handlers, for storing destroyed network objects in a pool for reuse.

```
m_SpawnObject = ObjectPool.Instantiate (m_CloneObject, Vector3.zero, Quaternion.identity);
NetworkedObjectPool.NetworkSpawn (m_CloneObject, m_SpawnObject, true);
```

<h3>Networked Pickup Item</h3><img src='https://user-images.githubusercontent.com/69744813/146672584-e2f59f4e-d8e5-42ad-8522-13b5ce3db6fd.png' alt="gui"></img>

Pickup items must be added to the networked pool and spawned in at runtime, pickup items cannot be in the OPSIVE Object Pool, otherwise they will all spawn into the game world.

<h5>Spawning the pickup item into the game world at runtime.</h5>

```
[SerializeField] private GameObject m_SwordPickup;
private void Spawn () {
    var go = ObjectPool.Instantiate (m_SwordPickup, Vector3.zero, Quaternion.identity);
    NetworkedObjectPool.NetworkSpawn (m_SwordPickup, go, true);
}
```
<h3>Pickup Tool</h3><img src='https://user-images.githubusercontent.com/69744813/146673319-b41e8c94-5cde-4f60-b841-da949cadee85.png' alt="project"></img>

<h5>Tools => GreedyVox => Networked => Item Pickup Inspector</h5>
Setting up the Pickup Item UCC prefab, use the tool menu to open up the dialog box, drag the Pickup Item prefab into the input field and than hit the Update Item button.

<h3>Pickup Tool</h3><img src='https://user-images.githubusercontent.com/69744813/146672686-87290450-af10-42e6-9d10-68787e94151d.png' alt="gui"></img>

The Pickup Item prefab will now have all the networking scripts added and ready for networking, the Pickup Item prefab should look like the screen-shot below.

<h3>Tool Setup</h3><img src='https://user-images.githubusercontent.com/69744813/146672914-8f17640e-cb3b-4c15-be16-15dd1a990676.png' alt="tool"></img>

<h3>Networked Prefabs</h3><img src='https://user-images.githubusercontent.com/69744813/146672571-0901eb29-367f-4be4-9b57-1ccccc5b82a6.png' alt="gui"></img>

Spawning prefabs into the game world must be in the network prefab list, this will insure that server will spawn automatically across networked clients.

<p align="center">Watch video below to see everything in action.</p>

<p align="center"><a href="http://www.youtube.com/watch?feature=player_embedded&v=v=mAoPNPCUn40" target="_blank"><img src="https://user-images.githubusercontent.com/69744813/146674310-14104dcd-c135-44e8-b511-cd77f043974e.jpg" 
alt="YAARRGH! Battle Island!" width="240" height="180" border="10"/></a></p>
