using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]

public class FPController : MonoBehaviour
{
    public float walkingSpeed = 7.5f;
    public float runningSpeed = 11.5f;
    public float sideSpeedMultiplier = 0.75f;
    public float backwardsSpeedMultiplier = 0.70f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public Camera playerCamera;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 45.0f;
    public MeshGenerator meshGenerator;

    CharacterController characterController;
    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;

    [HideInInspector]
    public bool canMove = true;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // We are grounded, so recalculate move direction based on axes
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);
        // Press Left Control to run
        bool isRunning = Input.GetKey(KeyCode.LeftControl);
        bool isGrounded = characterController.isGrounded;
        float curSpeedX = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedZ = canMove ? walkingSpeed * Input.GetAxis("Horizontal") : 0;
        float movementDirectionY = moveDirection.y;
        if (curSpeedX < 0) curSpeedX *= backwardsSpeedMultiplier;
        curSpeedZ *= sideSpeedMultiplier;
        moveDirection = (forward * curSpeedX) + (right * curSpeedZ);
        if (Input.GetButton("Jump") && canMove && isGrounded)
        {
            moveDirection.y = jumpSpeed;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        // Apply gravity. Gravity is multiplied by deltaTime twice (once here, and once below
        // when the moveDirection is multiplied by deltaTime). This is because gravity should be applied
        // as an acceleration (ms^-2)
        if (!isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }
        else if (!Input.GetButton("Jump"))
        {
            moveDirection.y = 0;
        }

        // Move the controller
        characterController.Move(moveDirection * Time.deltaTime);

        // Player and Camera rotation
        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }
    }

    private void OnDrawGizmos()
    {
        Vector3Int? blockPos = meshGenerator.GetRaycastedBlock();
        if (blockPos != null)
        {
            Vector3 blockSize = Vector3.one * ChunkGenerator.cellSize;
            //Gizmos.DrawWireCube(blockPos.Value + new Vector3(blockSize.x, blockSize.y, blockSize.z) / 2f, blockSize);
            Gizmos.DrawSphere(blockPos.Value, 0.1f);
        }
    }
}