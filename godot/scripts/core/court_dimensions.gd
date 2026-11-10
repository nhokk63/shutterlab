class_name CourtDimensions
## Single source of truth for all court geometry (BWF Laws of Badminton).
## Port of ShuttleLab.Core.CourtDimensions (Unity C#).
## Units are metres. Net plane is z = 0. Near side (user) is z < 0,
## far side (AI) is z > 0. X is court width, Y is up.

const COURT_LENGTH := 13.40
const COURT_HALF_LENGTH := 6.70

const DOUBLES_WIDTH := 6.10
const DOUBLES_HALF_WIDTH := 3.05

const SINGLES_WIDTH := 5.18
const SINGLES_HALF_WIDTH := 2.59

const NET_HEIGHT_CENTER := 1.524
const NET_HEIGHT_POST := 1.55
const POST_HEIGHT := 1.55

const LINE_WIDTH := 0.04

## Distance from the net to the short service line.
const SHORT_SERVICE_DISTANCE := 1.98

## Distance from the net to the doubles long service line.
const DOUBLES_LONG_SERVICE_DISTANCE := 5.94

## Distance from the net to the singles long service line (= back boundary).
const SINGLES_LONG_SERVICE_DISTANCE := 6.70

## Unity's Mathf.Sign: 1 when v >= 0, -1 when v < 0 (never 0).
static func sign_of(v: float) -> int:
	return 1 if v >= 0.0 else -1

## Net height at a given x (linear taper centre -> posts).
static func net_height_at(x: float) -> float:
	var t := clampf(absf(x) / DOUBLES_HALF_WIDTH, 0.0, 1.0)
	return lerpf(NET_HEIGHT_CENTER, NET_HEIGHT_POST, t)

static func is_in_singles(p: Vector3) -> bool:
	return absf(p.x) <= SINGLES_HALF_WIDTH + LINE_WIDTH * 0.5 \
		and absf(p.z) <= COURT_HALF_LENGTH + LINE_WIDTH * 0.5

static func is_in_doubles(p: Vector3) -> bool:
	return absf(p.x) <= DOUBLES_HALF_WIDTH + LINE_WIDTH * 0.5 \
		and absf(p.z) <= COURT_HALF_LENGTH + LINE_WIDTH * 0.5

## Is a point inside a service box? side_sign: -1 = near side (z < 0),
## +1 = far side. x_sign: +1 = box with x > 0, -1 = box with x < 0.
## doubles: use doubles long line.
static func is_in_service_box(p: Vector3, side_sign: int, x_sign: int, doubles: bool) -> bool:
	var z_min := SHORT_SERVICE_DISTANCE
	var z_max := DOUBLES_LONG_SERVICE_DISTANCE if doubles else SINGLES_LONG_SERVICE_DISTANCE
	var az := absf(p.z)
	if sign_of(p.z) != side_sign:
		return false
	if az < z_min or az > z_max:
		return false
	if sign_of(p.x) != x_sign:
		return false
	if absf(p.x) > SINGLES_HALF_WIDTH:
		return false
	return true

## Clamp a position to the playable court (singles or doubles).
static func clamp_to_court(p: Vector3, doubles: bool) -> Vector3:
	var hw := DOUBLES_HALF_WIDTH if doubles else SINGLES_HALF_WIDTH
	p.x = clampf(p.x, -hw, hw)
	p.z = clampf(p.z, -COURT_HALF_LENGTH, COURT_HALF_LENGTH)
	return p

## Service-court X sign for a server standing on the given side.
## Even server score -> server's RIGHT court; odd -> LEFT court.
## Near server faces +z, so their right is x < 0. Far server faces -z, right is x > 0.
static func service_court_x_sign(server_side_sign: int, server_score_even: bool) -> int:
	# right court: near side -> x < 0 (-1); far side -> x > 0 (+1)
	var right_sign := -1 if server_side_sign < 0 else 1
	return right_sign if server_score_even else -right_sign

## Where the server stands (just behind the short service line, centre of their box).
static func server_position(server_side_sign: int, court_x_sign: int) -> Vector3:
	var z := server_side_sign * (SHORT_SERVICE_DISTANCE + 1.6)
	var x := court_x_sign * SINGLES_HALF_WIDTH * 0.5
	return Vector3(x, 0.0, z)

## Where the receiver waits (diagonal box, ~1 m behind the short service line).
static func receiver_position(receiver_side_sign: int, court_x_sign: int) -> Vector3:
	var z := receiver_side_sign * (SHORT_SERVICE_DISTANCE + 1.2)
	var x := court_x_sign * SINGLES_HALF_WIDTH * 0.5
	return Vector3(x, 0.0, z)

## Centre of a service box (for serve targeting).
static func service_box_centre(side_sign: int, x_sign: int, doubles: bool) -> Vector3:
	var z_max := DOUBLES_LONG_SERVICE_DISTANCE if doubles else SINGLES_LONG_SERVICE_DISTANCE
	var z := side_sign * (SHORT_SERVICE_DISTANCE + z_max) * 0.5
	var x := x_sign * SINGLES_HALF_WIDTH * 0.5
	return Vector3(x, 0.0, z)
