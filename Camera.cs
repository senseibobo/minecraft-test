using Godot;
using System;

public class Camera : Godot.Camera
{
	public override void _Ready()
	{
		Input.SetMouseMode(Input.MouseMode.Captured);
	}
	public override void _Process(float delta)
	{
		Vector2 move = new Vector2(
            Input.GetAxis("ui_left","ui_right"),
            Input.GetAxis("ui_down","ui_up")
        );
        Vector3 pos = Translation;
        Vector3 moveVec = new Vector3();
        Basis basis = GlobalTransform.basis;
        moveVec.x += basis.x.x * move.x + basis.x.z * move.y;
        moveVec.y -= basis.x.y * move.x + basis.z.y * move.y;
        moveVec.z -= basis.z.x * move.x + basis.z.z * move.y;
        moveVec = moveVec.Normalized() * delta * 10.0f;
        pos += moveVec;
        //pos.z += basis.x.z * move.x + basis.y.z * move.y;
        Translation = pos;
	}
	public override void _Input(InputEvent @event)
	{
		if(@event is InputEventMouseMotion) {
            InputEventMouseMotion mouseEvent = @event as InputEventMouseMotion;
            Vector3 rot = RotationDegrees;
            rot.y -= mouseEvent.Relative.x * 0.1f;
            rot.x -= mouseEvent.Relative.y * 0.1f;
            RotationDegrees = rot;
        }
	}
}
