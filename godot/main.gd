extends Node3D
## ShuttleLab — minimal main scene (pipeline proof).
## Builds the court placeholder in code so the export can be tested
## before the full game port. Camera matches the Unity BehindPlayer
## setup: pos (0, 6.5, -10.5), looking at (0, 1, 2).

func _ready() -> void:
	# Camera
	var cam := Camera3D.new()
	cam.position = Vector3(0, 6.5, -10.5)
	cam.fov = 55.0
	add_child(cam)
	cam.look_at(Vector3(0, 1, 2), Vector3.UP)

	# Directional light (sun)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50, -30, 0)
	sun.light_energy = 1.2
	sun.shadow_enabled = true
	add_child(sun)

	# Court floor (blue, TV-broadcast look)
	var floor_mesh := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(20, 14)
	floor_mesh.mesh = plane
	var floor_mat := StandardMaterial3D.new()
	floor_mat.albedo_color = Color(0.10, 0.32, 0.62)
	floor_mat.roughness = 0.4
	floor_mesh.set_surface_override_material(0, floor_mat)
	add_child(floor_mesh)

	# Placeholder shuttlecock (white sphere)
	var ball := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.15
	sphere.height = 0.3
	ball.mesh = sphere
	ball.position = Vector3(0, 1, 0)
	var ball_mat := StandardMaterial3D.new()
	ball_mat.albedo_color = Color(1, 1, 1)
	ball.set_surface_override_material(0, ball_mat)
	add_child(ball)
