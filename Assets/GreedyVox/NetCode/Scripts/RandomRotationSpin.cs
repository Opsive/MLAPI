using UnityEngine;

public class RandomRotationSpin : MonoBehaviour
{
    [SerializeField] private float m_MinRotationSpeed = 10.0f;
    [SerializeField] private float m_MaxRotationSpeed = 50.0f;
    private Rigidbody m_RigidBody;
    private void Start() => m_RigidBody = GetComponent<Rigidbody>();
    private void FixedUpdate()
    {
        // Generate random rotation speed
        float rotationSpeed = Random.Range(m_MinRotationSpeed, m_MaxRotationSpeed);
        // Generate random rotation axis
        Vector3 rotationAxis = Random.onUnitSphere;
        // Apply torque to the Rigidbody to rotate the GameObject
        m_RigidBody.AddTorque(rotationAxis * rotationSpeed, ForceMode.Impulse);
    }
}
