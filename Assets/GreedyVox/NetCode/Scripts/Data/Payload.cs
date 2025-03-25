// using Unity.Netcode;

// namespace GreedyVox.NetCode.Data
// {
//     public static class Payload
//     {
//         public static void WriteValueSafe(this FastBufferWriter writer, in PayloadItemPickup value)
//         {
//             writer.WriteValueSafe(value.OwnerID);
//             writer.WriteValueSafe(value.ItemCount);
//             writer.WriteValueSafe(value.Torque);
//             writer.WriteValueSafe(value.Velocity);
//             writer.WriteValueSafe(value.ItemID);
//             writer.WriteValueSafe(value.ItemAmounts);
//         }
//         public static void ReadValueSafe(this FastBufferReader reader, out PayloadItemPickup value)
//         {
//             value = new PayloadItemPickup();
//             reader.ReadValueSafe(out value.OwnerID);
//             reader.ReadValueSafe(out value.ItemCount);
//             reader.ReadValueSafe(out value.Torque);
//             reader.ReadValueSafe(out value.Velocity);
//             reader.ReadValueSafe(out value.ItemID);
//             reader.ReadValueSafe(out value.ItemAmounts);
//         }
//         public static void WriteValueSafe(this FastBufferWriter writer, in PayloadGrenado value)
//         {
//             writer.WriteValueSafe(value.ImpactStateName);
//             writer.WriteValueSafe(value.Torque);
//             writer.WriteValueSafe(value.Velocity);
//             writer.WriteValueSafe(value.ImpactFrames);
//             writer.WriteValueSafe(value.ImpactLayers);
//             writer.WriteValueSafe(value.ImpactForce);
//             writer.WriteValueSafe(value.DamageAmount);
//             writer.WriteValueSafe(value.ScheduledDeactivation);
//             writer.WriteValueSafe(value.ImpactStateDisableTimer);
//         }
//         public static void ReadValueSafe(this FastBufferReader reader, out PayloadGrenado value)
//         {
//             value = new PayloadGrenado();
//             reader.ReadValueSafe(out value.ImpactStateName);
//             reader.ReadValueSafe(out value.Torque);
//             reader.ReadValueSafe(out value.Velocity);
//             reader.ReadValueSafe(out value.ImpactFrames);
//             reader.ReadValueSafe(out value.ImpactLayers);
//             reader.ReadValueSafe(out value.ImpactForce);
//             reader.ReadValueSafe(out value.DamageAmount);
//             reader.ReadValueSafe(out value.ScheduledDeactivation);
//             reader.ReadValueSafe(out value.ImpactStateDisableTimer);
//         }
//     }
// }