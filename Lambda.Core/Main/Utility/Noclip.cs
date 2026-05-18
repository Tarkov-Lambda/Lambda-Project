using UnityEngine;

public static class Noclip
{
    public static bool IsEnabled { get; private set; } = false;

    // Use a Vector3 for velocity instead of a float for speed
    private static Vector3 _currentVelocity = Vector3.zero;

    public static void ProcessNoclipFrame()
    {
        var movementContext = H.MainPlayer.MovementContext;

        movementContext.ResetFlying();
        movementContext.FreefallTime = 0f;
        movementContext.IsGrounded = true;

        Transform camTransform = CameraClass.Instance.Camera.transform;
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) moveDirection += camTransform.forward;
        if (Input.GetKey(KeyCode.S)) moveDirection -= camTransform.forward;
        if (Input.GetKey(KeyCode.A)) moveDirection -= camTransform.right;
        if (Input.GetKey(KeyCode.D)) moveDirection += camTransform.right;

        if (Input.GetKey(KeyCode.Space)) moveDirection += Vector3.up;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) moveDirection -= Vector3.up;

        if (moveDirection.sqrMagnitude > 0f)
        {
            moveDirection.Normalize();
        }

        float noiseLevel = Mathf.Clamp01(movementContext.CovertNoiseLevel);
        float targetSpeed = Mathf.Lerp(5f, 30f, noiseLevel);

        bool isShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (isShift) targetSpeed *= 4f;

        Vector3 targetVelocity = moveDirection * targetSpeed;

        float acceleration = 12f; 

        _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, acceleration * Time.deltaTime);

        Vector3 newPosition = H.MainPlayer.Transform.position + (_currentVelocity * Time.deltaTime);
        H.MainPlayer.Teleport(newPosition);
    }

    public static void ToggleNoclip()
    {
        IsEnabled = !IsEnabled;
        
        if (!IsEnabled)
        {
            _currentVelocity = Vector3.zero;
        }
    }
}