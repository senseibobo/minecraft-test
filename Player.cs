using Godot;
using System;

public class Player : KinematicBody
{

    Camera camera;
	public override void _Ready()
	{
		camera = GetNode<Camera>("Camera");
        Input.SetMouseMode(Input.MouseMode.Captured);
	}
    Vector3 velocity = new Vector3();
	public override void _PhysicsProcess(float delta)
	{
		Vector2 move = new Vector2(
            Input.GetAxis("ui_left","ui_right"),
            Input.GetAxis("ui_down","ui_up")
        );
        Vector3 pos = Translation;
        Basis basis = camera.GlobalTransform.basis;
        velocity.x = 0;
        velocity.z = 0;
        velocity.y -= 100*delta;
        velocity.x += basis.x.x * move.x * 100 + basis.x.z * move.y * 100;
        velocity.z -= basis.z.x * move.x * 100 + basis.z.z * move.y * 100;
        velocity = MoveAndSlide(velocity);
	}
	public override void _Input(InputEvent @event)
	{
		if(@event is InputEventMouseMotion) {
            InputEventMouseMotion mouseEvent = @event as InputEventMouseMotion;
            Vector3 rot = camera.RotationDegrees;
            rot.y -= mouseEvent.Relative.x * 0.1f;
            rot.x -= mouseEvent.Relative.y * 0.1f;
            camera.RotationDegrees = rot;
        }
	}
}
