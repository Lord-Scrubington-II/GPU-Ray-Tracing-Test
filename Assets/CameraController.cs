using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
	// Inspector vars: linear & angular velocity
	[SerializeField] float turnSpeed = 4.0f;
	[SerializeField] float panSpeed = 4.0f;

	private Vector3 mouseOrigin;  // This is the position of the cursor when mouse dragging starts
	private bool useMouseRotation = true; // what it says on the tin

	// - BEGIN: Control Flow
	// On mouse drag: rotate camera
	// On WASD: move forward, left, back, and right respectively
	// On Space Bar: move up
	// On CTRL: move down
	void Update()
	{
		// snapshot the mouse position at the beginning of the frame
		mouseOrigin = Input.mousePosition;

		// mouse rotation toggle
		if (!Input.GetMouseButtonUp(1)) useMouseRotation = !useMouseRotation;

		// We can rotate the camera around the y and x axis
		// adapted from a forum conversation: http://forum.unity3d.com/threads/39513-Click-drag-camera-movement
		if (useMouseRotation) {
			Vector3 pos = Camera.main.ScreenToViewportPoint(Input.mousePosition - mouseOrigin);

			transform.RotateAround(transform.position, transform.right, -pos.y * turnSpeed * Time.deltaTime);
			transform.RotateAround(transform.position, Vector3.up, pos.x * turnSpeed * Time.deltaTime);
		}

		// Move the camera upwards
		if (Input.GetKey(KeyCode.Space)) {
			Vector3 move = Vector3.up;
			transform.Translate(panSpeed * Time.deltaTime * move, Space.World);
		}

		// Move the camera downwards
		if (Input.GetKey(KeyCode.LeftControl)) {
			Vector3 move = Vector3.down;
			transform.Translate(panSpeed * Time.deltaTime * move, Space.World);
		}

		// Move the camera forward (along the local positive z-axis)
		if (Input.GetKey(KeyCode.W)) {

		}

		// Move the camera left (along the local negative y-axis)
		if (Input.GetKey(KeyCode.A)) {

		}

		// Move the camera left (along the local negative z-axis)
		if (Input.GetKey(KeyCode.S)) {

		}

		// Move the camera right (along the local positive y-axis)
		if (Input.GetKey(KeyCode.D)) {

		}
	}
}