// using System.Collections.Generic;
// using Opsive.Shared.Audio;
// using Opsive.Shared.Game;
// using Opsive.UltimateCharacterController.Events;
// using Opsive.UltimateCharacterController.Game;
// using Opsive.UltimateCharacterController.Networking.Game;
// using Opsive.UltimateCharacterController.Objects;
// using Opsive.UltimateCharacterController.Traits;
// using Opsive.UltimateCharacterController.Utility;
// using UnityEngine;
// using EventHandler = Opsive.Shared.Events.EventHandler;

// namespace GreedyVox.NetCode
// {
//     /// <summary>
//     /// Creates an explosion which applies a force and damage to any object that is within the specified radius.
//     /// </summary>
//     public class NetCodeExplosion : MonoBehaviour
//     {
//         [Tooltip("Should the object explode when the object is enabled?")]
//         [SerializeField] protected bool m_ExplodeOnEnable;
//         [Tooltip("Determines how far out the explosion affects other objects.")]
//         [SerializeField] protected float m_Radius = 5;
//         [Tooltip("The maximum amount of damage the explosion applies to objects with the Health component.")]
//         [SerializeField] protected float m_DamageAmount = 10;
//         [Tooltip("The maximum amount of force the explosion applies to nearby Rigidbody/IForceObject objects.")]
//         [SerializeField] protected float m_ImpactForce = 2;
//         [Tooltip("The number of frames to add the impact force to.")]
//         [SerializeField] protected int m_ImpactForceFrames = 1;
//         [Tooltip("The layers that the explosion can affect.")]
//         [SerializeField]
//         protected LayerMask m_ImpactLayers = ~(1 << LayerManager.IgnoreRaycast | 1 << LayerManager.Water | 1 << LayerManager.SubCharacter | 1 << LayerManager.Overlay |
//             1 << LayerManager.VisualEffect);
//         [Tooltip("Does the explosion require line of sight in order to damage the hit object?")]
//         [SerializeField] protected bool m_LineOfSight;
//         [Tooltip("The duration of the explosion.")]
//         [SerializeField] protected float m_Lifespan = 3;
//         [Tooltip("The maximum number of objects that the explosions can detect.")]
//         [SerializeField] protected int m_MaxCollisionCount = 100;
//         [Tooltip("A set of AudioClips that can be played when the explosion occurs.")]
//         [SerializeField] protected AudioClipSet m_ExplosionAudioClipSet = new AudioClipSet();
//         [Tooltip("Unity event invoked when the explosion hits another object.")]
//         [SerializeField] protected UnityFloatVector3Vector3GameObjectEvent m_OnImpactEvent;
//         public bool ExplodeOnEnable { get { return m_ExplodeOnEnable; } set { m_ExplodeOnEnable = value; } }
//         public float Radius { get { return m_Radius; } set { m_Radius = value; } }
//         public float DamageAmount { get { return m_DamageAmount; } set { m_DamageAmount = value; } }
//         public float ImpactForce { get { return m_ImpactForce; } set { m_ImpactForce = value; } }
//         public int ImpactForceFrames { get { return m_ImpactForceFrames; } set { m_ImpactForceFrames = value; } }
//         public LayerMask ImpactLayers { get { return m_ImpactLayers; } set { m_ImpactLayers = value; } }
//         public bool LineOfSight { get { return m_LineOfSight; } set { m_LineOfSight = value; } }
//         public float Lifespan { get { return m_Lifespan; } set { m_Lifespan = value; } }
//         public AudioClipSet ExplosionAudioClipSet { get { return m_ExplosionAudioClipSet; } set { m_ExplosionAudioClipSet = value; } }
//         public UnityFloatVector3Vector3GameObjectEvent OnImpactEvent { get { return m_OnImpactEvent; } set { m_OnImpactEvent = value; } }
//         private GameObject m_GameObject;
//         private Transform m_Transform;
//         private HashSet<object> m_ObjectExplosions = new HashSet<object>();
//         private Collider[] m_CollidersHit;
//         private RaycastHit m_RaycastHit;
//         private ScheduledEventBase m_DestructionEvent;
//         /// <summary>
//         /// Initialize the default values.
//         /// </summary>
//         private void Awake()
//         {
//             m_GameObject = gameObject;
//             m_Transform = transform;
//             m_CollidersHit = new Collider[m_MaxCollisionCount];
//             // AudioManager.Register (m_GameObject);
//         }
//         /// <summary>
//         /// Explode if requested when the component is enabled.
//         /// </summary>
//         private void OnEnable()
//         {
//             if (m_ExplodeOnEnable)
//             {
//                 Explode(m_DamageAmount, m_ImpactForce, m_ImpactForceFrames, null);
//             }
//         }
//         /// <summary>
//         /// Do the explosion.
//         /// </summary>
//         public void Explode()
//         {
//             Explode(m_DamageAmount, m_ImpactForce, m_ImpactForceFrames, null);
//         }
//         /// <summary>
//         /// Do the explosion.
//         /// </summary>
//         /// <param name="damageAmount">The amount of damage to apply to the hit objects.</param>
//         public void Explode(GameObject originator)
//         {
//             Explode(m_DamageAmount, m_ImpactForce, m_ImpactForceFrames, originator);
//         }
//         /// <summary>
//         /// Do the explosion.
//         /// </summary>
//         /// <param name="damageAmount">The amount of damage to apply to the hit objects.</param>
//         /// <param name="impactForce">The amount of force to apply to the hit object.</param>
//         /// <param name="impactForceFrames">The number of frames to add the force to.</param>
//         /// <param name="originator">The originator of the object.</param>
//         public void Explode(float damageAmount, float impactForce, int impactForceFrames, GameObject originator)
//         {
//             Health health = null;
//             Rigidbody colliderRigidbody = null;
//             IForceObject forceObject = null;
//             var hitCount = Physics.OverlapSphereNonAlloc(m_Transform.position, m_Radius, m_CollidersHit, m_ImpactLayers, QueryTriggerInteraction.Ignore);
// #if UNITY_EDITOR
//             if (hitCount == m_MaxCollisionCount)
//             {
//                 Debug.LogWarning("Warning: The maximum number of colliders have been hit by " + m_GameObject.name + ". Consider increasing the Max Collision Count value.");
//             }
// #endif
//             for (int i = 0; i < hitCount; ++i)
//             {
//                 // A GameObject can contain multiple colliders. Prevent the explosion from occurring on the same GameObject multiple times.
//                 if (m_ObjectExplosions.Contains(m_CollidersHit[i].gameObject))
//                 {
//                     continue;
//                 }
//                 m_ObjectExplosions.Add(m_CollidersHit[i].gameObject);
//                 // The base character GameObject should only be checked once.
//                 if ((forceObject = m_CollidersHit[i].gameObject.GetCachedParentComponent<IForceObject>()) != null)
//                 {
//                     if (m_ObjectExplosions.Contains(forceObject))
//                     {
//                         continue;
//                     }
//                     m_ObjectExplosions.Add(forceObject);
//                 }
//                 // OverlapSphere can return objects that are in a different room. Perform a cast to ensure the object is within the explosion range.
//                 if (m_LineOfSight)
//                 {
//                     // Add a slight vertical offset to prevent a floor collider from getting in the way of the cast.
//                     var position = m_Transform.TransformPoint(0, 0.1f, 0);
//                     var direction = m_CollidersHit[i].transform.position - position;
//                     if (Physics.Raycast(position - direction.normalized * 0.1f, direction, out m_RaycastHit, direction.magnitude, m_ImpactLayers, QueryTriggerInteraction.Ignore) &&
//                         !(m_RaycastHit.transform.IsChildOf(m_CollidersHit[i].transform)
// #if FIRST_PERSON_CONTROLLER
//                             // The cast should not hit any colliders who are a child of the camera.
//                             ||
//                             m_RaycastHit.transform.gameObject.GetCachedParentComponent<Opsive.UltimateCharacterController.FirstPersonController.Character.FirstPersonObjects>() != null
// #endif
//                         ))
//                     {
//                         // If the collider is part of a character then ensure the head can't be hit.
//                         var parentAnimator = m_CollidersHit[i].transform.gameObject.GetCachedParentComponent<Animator>();
//                         if (parentAnimator != null && parentAnimator.isHuman)
//                         {
//                             var head = parentAnimator.GetBoneTransform(HumanBodyBones.Head);
//                             direction = head.position - position;
//                             if (Physics.Raycast(position, direction, out m_RaycastHit, direction.magnitude, m_ImpactLayers, QueryTriggerInteraction.Ignore) &&
//                                 !m_RaycastHit.transform.IsChildOf(m_CollidersHit[i].transform) && !m_CollidersHit[i].transform.IsChildOf(m_RaycastHit.transform) &&
//                                 m_RaycastHit.transform.IsChildOf(m_Transform)
// #if FIRST_PERSON_CONTROLLER
//                                 // The cast should not hit any colliders who are a child of the camera.
//                                 &&
//                                 m_RaycastHit.transform.gameObject.GetCachedParentComponent<Opsive.UltimateCharacterController.FirstPersonController.Character.FirstPersonObjects>() == null
// #endif
//                             )
//                             {
//                                 continue;
//                             }
//                         }
//                         else
//                         {
//                             continue;
//                         }
//                     }
//                 }
//                 // The shield can absorb some (or none) of the damage from the explosion.
//                 var hitDamageAmount = damageAmount;
// #if ULTIMATE_CHARACTER_CONTROLLER_MELEE
//                 ShieldCollider shieldCollider;
//                 if ((shieldCollider = m_CollidersHit[i].transform.gameObject.GetCachedComponent<ShieldCollider>()) != null)
//                 {
//                     hitDamageAmount = shieldCollider.Shield.Damage(this, hitDamageAmount);
//                 }
// #endif
//                 // ClosestPoint only works with a subset of collider types.
//                 Vector3 closestPoint;
//                 if (m_CollidersHit[i] is BoxCollider || m_CollidersHit[i] is SphereCollider || m_CollidersHit[i] is CapsuleCollider || (m_CollidersHit[i] is MeshCollider && (m_CollidersHit[i] as MeshCollider).convex))
//                 {
//                     closestPoint = m_CollidersHit[i].ClosestPoint(m_Transform.position);
//                 }
//                 else
//                 {
//                     closestPoint = m_CollidersHit[i].ClosestPointOnBounds(m_Transform.position);
//                 }
//                 var hitDirection = closestPoint - m_Transform.position;
//                 // Allow a custom event to be received.
//                 EventHandler.ExecuteEvent<float, Vector3, Vector3, GameObject, object, Collider>(m_CollidersHit[i].transform.gameObject, "OnObjectImpact", hitDamageAmount, closestPoint, hitDirection * m_ImpactForce, originator, this, m_CollidersHit[i]);
//                 if (m_OnImpactEvent != null)
//                 {
//                     m_OnImpactEvent.Invoke(hitDamageAmount, closestPoint, hitDirection * m_ImpactForce, originator);
//                 }
//                 // If the shield didn't absorb all of the damage then it should be applied to the character.
//                 if (hitDamageAmount > 0)
//                 {
//                     // If the Health component exists it will apply an explosive force to the character/character in addition to deducting the health.
//                     // Otherwise just apply the force to the character/rigidbody. 
//                     if ((health = m_CollidersHit[i].gameObject.GetCachedParentComponent<Health>()) != null)
//                     {
//                         // The further out the collider is, the less it is damaged.
//                         var damageModifier = Mathf.Max(1 - (hitDirection.magnitude / m_Radius), 0.01f);
//                         health.Damage(hitDamageAmount * damageModifier, m_Transform.position, hitDirection.normalized, impactForce * damageModifier, impactForceFrames, m_Radius, originator, this, null);
//                     }
//                     else if (forceObject != null)
//                     {
//                         var damageModifier = Mathf.Max(1 - (hitDirection.magnitude / m_Radius), 0.01f);
//                         forceObject.AddForce(hitDirection.normalized * impactForce * damageModifier);
//                     }
//                     else if ((colliderRigidbody = m_CollidersHit[i].gameObject.GetCachedComponent<Rigidbody>()) != null)
//                     {
//                         colliderRigidbody.AddExplosionForce(impactForce * MathUtility.RigidbodyForceMultiplier, m_Transform.position, m_Radius);
//                     }
//                 }
//             }
//             m_ObjectExplosions.Clear();
//             // An audio clip can play when the object explodes.
//             m_ExplosionAudioClipSet.PlayAudioClip(m_GameObject);
//             m_DestructionEvent = Scheduler.Schedule(m_Lifespan, Destroy);
//         }
//         /// <summary>
//         /// The object has been disabled.
//         /// </summary>
//         public void OnDisable()
//         {
//             if (m_DestructionEvent != null)
//             {
//                 Scheduler.Cancel(m_DestructionEvent);
//                 m_DestructionEvent = null;
//             }
//         }
//         /// <summary>
//         /// Place the object back in the ObjectPool.
//         /// </summary>
//         private void Destroy()
//         {
//             NetworkObjectPool.Destroy(gameObject);
//             m_DestructionEvent = null;
//         }
//     }
// }