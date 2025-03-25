using System;
using GreedyVox.NetCode.Data;
using GreedyVox.NetCode.Interfaces;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Items.Actions;
using Opsive.UltimateCharacterController.Networking.Objects;
using Opsive.UltimateCharacterController.Objects.ItemAssist;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace GreedyVox.NetCode.Objects
{
    /// <summary>
    /// Initializes the magic particle over the network.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetCodeMagicParticle : MonoBehaviour, INetworkMagicObject, IPayload
    {
        private GameObject m_Character;
        private MagicAction m_MagicAction;
        private int m_ActionIndex;
        private uint m_CastID;
        private PayloadMagicParticle m_Data;
        public int NetworkID { get; set; }
        /// <summary>
        /// Returns the initialization data that is required when the object spawns. This allows the remote players to initialize the object correctly.
        /// </summary>
        /// <returns>The initialization data that is required when the object spawns.</returns>
        private void Start()
        {
            var net = m_Character.GetCachedComponent<NetworkObject>();
            m_Data = new PayloadMagicParticle()
            {
                OwnerID = net == null ? -1L : (long)net.OwnerClientId,
                SlotID = m_MagicAction.CharacterItem.SlotID,
                ActionID = m_MagicAction.ID,
                ActionIndex = m_ActionIndex,
                CastID = m_CastID
            };
        }
        /// <summary>
        /// Sets the spawn data.
        /// </summary>
        /// <param name="character">The character that is instantiating the object.</param>
        /// <param name="magicAction">The MagicAction that the object belongs to.</param>
        /// <param name="actionIndex">The index of the action that is instantiating the object.</param>
        /// <param name="castID">The ID of the cast that is instantiating the object.</param>
        public void Instantiate(GameObject character, MagicAction magicAction, int actionIndex, uint castID)
        {
            m_Character = character;
            m_MagicAction = magicAction;
            m_ActionIndex = actionIndex;
            m_CastID = castID;
        }
        /// <summary>
        /// Initializes the object. This will be called from an object creating the projectile (such as a weapon).
        /// </summary>
        /// <param name="id">The id used to differentiate this projectile from others.</param>
        /// <param name="owner">The object that instantiated the trajectory object.</param>
        public void Initialize(uint id, GameObject own) { }
        /// <summary>
        /// Returns the maximus size for the fast buffer writer
        /// </summary>
        public int MaxBufferSize()
        {
            return
                   FastBufferWriter.GetWriteSize<int>() +
                   FastBufferWriter.GetWriteSize(NetworkID) +
                   FastBufferWriter.GetWriteSize(m_Data.OwnerID) +
                   FastBufferWriter.GetWriteSize(m_Data.CastID) +
                   FastBufferWriter.GetWriteSize(m_Data.SlotID) +
                   FastBufferWriter.GetWriteSize(m_Data.ActionID) +
                   FastBufferWriter.GetWriteSize(m_Data.ActionIndex);
        }
        /// <summary>
        /// The object has been spawned, write the payload data.
        /// </summary>
        public bool PayLoad(ref int idx, out FastBufferWriter writer)
        {
            try
            {
                using (writer = new FastBufferWriter(MaxBufferSize(), Allocator.Temp))
                {
                    writer.WriteValueSafe(idx);
                    writer.WriteValueSafe(m_Data);
                }
                return true;
            }
            catch (Exception e)
            {
                NetworkLog.LogErrorServer(e.Message);
                return false;
            }
        }
        /// <summary>
        /// The object has been spawned, read the payload data.
        /// Initialize the particle.
        public void PayLoad(in FastBufferReader reader, GameObject go = default)
        {
            if (go == null) return;
            reader.ReadValueSafe(out m_Data);
            m_ActionIndex = m_Data.ActionIndex;
            var inventory = go.GetCachedComponent<Inventory>();
            if (inventory == null) return;
            var item = inventory.GetActiveCharacterItem(m_Data.SlotID);
            if (item == null) return;
            var magicAction = item.GetItemAction(m_Data.ActionID) as MagicAction;
            if (magicAction == null) return;
            var magicParticle = gameObject.GetCachedComponent<MagicParticle>();
            magicParticle?.Initialize(magicAction, m_Data.CastID);
        }
    }
}