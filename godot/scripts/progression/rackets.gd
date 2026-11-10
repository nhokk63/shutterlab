class_name EquipmentManager
extends RefCounted
## Shop + inventory. Port of ShuttleLab.Systems.EquipmentManager (Unity C#).
##
## Rackets / shoes / shirts are FICTIONAL — self-invented names (Bão Tố,
## Phong Vũ, Lôi Đình...), NO real brands (Yonex, Victor, Li-Ning...).
## The "national" rackets are inspired by real rackets, with the brand /
## model tweaked just enough that players still recognise them (Yonexx ~
## Yonex, Viktor ~ Victor, Li-Nang ~ Li-Ning, Mizuna ~ Mizuno, Apax ~
## Apacs, Kawasaky ~ Kawasaki) — the in-game names stay fictional.
## Every item has a small modifier (+1..+8, legendary max +8) and a coin
## price (bought with Career money), in 4 rarity tiers: Common / Good /
## Rare / Legendary. Modifiers are DELIBERATELY small: gear helps a
## little, never replaces skill — the "no pay-to-win" spirit of
## ARCHITECTURE.md. Stats rise with price, always with a trade-off.
##
## apply_to() returns a COPY with the modifiers added (never edits the
## base stats), clamped 1..99. Buying: buy() subtracts Career coins.
## Owned / equipped state is saved through the Career save file.
##
## NOTE (spec vs code): PROGRESSION_SPEC.md's table lists Bão Tố 900 as
## +4 power and Lôi Đình X at 1200 coins, but the Unity code
## (EquipmentManager.cs, the source of truth) has Bão Tố 900 at +3 power
## and Lôi Đình X at 1500 coins. The code values are ported here.

enum Slot { RACKET, SHOES, SHIRT }

enum Rarity { COMMON, GOOD, RARE, LEGENDARY }

const STARTER_RACKET_ID := "rkt_starter"
const STARTER_SHOES_ID := "sho_starter"
const STARTER_SHIRT_ID := "shr_starter"

const SLOT_NAMES: Array[String] = ["Racket", "Shoes", "Shirt"]

const RARITY_NAMES: Array[String] = ["Thường", "Tốt", "Hiếm", "Huyền thoại"]

# Rarity colours: Common white, Good blue, Rare purple, Legendary gold.
static func rarity_color(rarity: int) -> Color:
	match rarity:
		Rarity.GOOD:
			return Color(0.45, 0.70, 1.0)
		Rarity.RARE:
			return Color(0.75, 0.50, 1.0)
		Rarity.LEGENDARY:
			return Color(1.0, 0.80, 0.25)
		_:
			return Color.WHITE

static func rarity_name(rarity: int) -> String:
	if rarity >= 0 and rarity < RARITY_NAMES.size():
		return RARITY_NAMES[rarity]
	return RARITY_NAMES[0]

static func slot_name(slot: int) -> String:
	if slot >= 0 and slot < SLOT_NAMES.size():
		return SLOT_NAMES[slot]
	return SLOT_NAMES[0]

static func slot_from_name(slot_name_str: String) -> int:
	for i in SLOT_NAMES.size():
		if SLOT_NAMES[i] == slot_name_str:
			return i
	return Slot.RACKET

## "Sức mạnh +3, Tốc độ -1" — empty when no modifier.
static func mod_text(item: Dictionary) -> String:
	var parts: Array[String] = []
	if int(item["power"]) != 0:
		parts.append("Sức mạnh %s%d" % ["+" if int(item["power"]) > 0 else "", int(item["power"])])
	if int(item["speed"]) != 0:
		parts.append("Tốc độ %s%d" % ["+" if int(item["speed"]) > 0 else "", int(item["speed"])])
	if int(item["stamina"]) != 0:
		parts.append("Thể lực %s%d" % ["+" if int(item["stamina"]) > 0 else "", int(item["stamina"])])
	return ", ".join(parts) if parts.size() > 0 else "Không đổi chỉ số"

## Full catalog: 25 rackets + 4 shoes + 4 shirts = 33 items.
## Values are EXACT from EquipmentManager.cs (id, name, slot, rarity,
## power, speed, stamina, price, description).
static func build_catalog() -> Array:
	var c: Array = []
	# Starter racket + old fictional line (original stats/prices kept).
	c.append({"id": STARTER_RACKET_ID, "name": "Vợt Khởi Đầu", "slot": Slot.RACKET, "rarity": Rarity.COMMON, "power": 0, "speed": 0, "stamina": 0, "price": 0, "description": "Vợt tập cơ bản, cân bằng."})
	c.append({"id": "rkt_balance_5", "name": "Cân Bằng 5", "slot": Slot.RACKET, "rarity": Rarity.COMMON, "power": 1, "speed": 1, "stamina": 1, "price": 500, "description": "Mọi chỉ số +1, dễ làm quen."})
	c.append({"id": "rkt_wind_77", "name": "Phong Vũ 77", "slot": Slot.RACKET, "rarity": Rarity.COMMON, "power": 2, "speed": 2, "stamina": 0, "price": 650, "description": "Nhẹ, vung nhanh, đánh linh hoạt."})
	c.append({"id": "rkt_storm_900", "name": "Bão Tố 900", "slot": Slot.RACKET, "rarity": Rarity.GOOD, "power": 3, "speed": 0, "stamina": -1, "price": 800, "description": "Đập mạnh nhưng hơi nặng tay."})
	c.append({"id": "rkt_thunder_x", "name": "Lôi Đình X", "slot": Slot.RACKET, "rarity": Rarity.RARE, "power": 5, "speed": -2, "stamina": 0, "price": 1500, "description": "Sức mạnh tối đa, đánh đổi tốc độ."})
	# "National" rackets — inspired by real rackets, brand/model tweaked.
	# Common (150-600 coins): highest single stat +2.
	c.append({"id": "rkt_muscle_powr_29", "name": "Muscle Powr 29", "slot": Slot.RACKET, "rarity": Rarity.COMMON, "power": 1, "speed": 0, "stamina": 0, "price": 200, "description": "Vợt cổ điển bền bỉ, ai cũng từng cầm qua."})
	c.append({"id": "rkt_nanorey_10f", "name": "Nanorey 10F", "slot": Slot.RACKET, "rarity": Rarity.COMMON, "power": 0, "speed": 2, "stamina": 0, "price": 350, "description": "Nhẹ đầu, vung nhanh, hợp đánh đôi."})
	c.append({"id": "rkt_windstorme_72", "name": "Windstorme 72", "slot": Slot.RACKET, "rarity": Rarity.COMMON, "power": 0, "speed": 1, "stamina": 1, "price": 450, "description": "Nhẹ cả cây, chạy bền cả trận."})
	c.append({"id": "rkt_voltrex_7dg", "name": "Voltrex 7DG", "slot": Slot.RACKET, "rarity": Rarity.COMMON, "power": 2, "speed": -1, "stamina": 0, "price": 500, "description": "Đầu hơi nặng, đập có lực, giá mềm."})
	c.append({"id": "rkt_king_k8", "name": "King K8", "slot": Slot.RACKET, "rarity": Rarity.COMMON, "power": 1, "speed": 1, "stamina": 0, "price": 550, "description": "Cân bằng tốt, giá quốc dân."})
	# Good (700-1400 coins): highest single stat +3.
	c.append({"id": "rkt_arcsabre_7pro", "name": "Arcsabre 7 Pro", "slot": Slot.RACKET, "rarity": Rarity.GOOD, "power": 2, "speed": 1, "stamina": 0, "price": 800, "description": "Kiểm soát tốt, cầu đi đúng ý."})
	c.append({"id": "rkt_astroz_77pro", "name": "Astroz 77 Pro", "slot": Slot.RACKET, "rarity": Rarity.GOOD, "power": 3, "speed": -1, "stamina": 0, "price": 1000, "description": "Đập uy lực, hơi chậm một nhịp."})
	c.append({"id": "rkt_nanoflair_800pro", "name": "Nanoflair 800 Pro", "slot": Slot.RACKET, "rarity": Rarity.GOOD, "power": 1, "speed": 3, "stamina": 0, "price": 1100, "description": "Vợt tốc độ, thủ cầu như gió."})
	c.append({"id": "rkt_thrusta_kfalcon", "name": "Thrusta K Falcon", "slot": Slot.RACKET, "rarity": Rarity.GOOD, "power": 3, "speed": 1, "stamina": -1, "price": 1300, "description": "Cân bằng thiên công, vung đầm tay."})
	c.append({"id": "rkt_axeforce_90tiger", "name": "Axeforce 90 Tiger", "slot": Slot.RACKET, "rarity": Rarity.GOOD, "power": 3, "speed": 0, "stamina": 1, "price": 1400, "description": "Đập khỏe mà vẫn bền sức."})
	# Rare (1500-2800 coins): highest single stat +5.
	c.append({"id": "rkt_arcsabre_11pro", "name": "Arcsabre 11 Pro", "slot": Slot.RACKET, "rarity": Rarity.RARE, "power": 3, "speed": 2, "stamina": 0, "price": 1600, "description": "Chuẩn kiểm soát của dân chuyên."})
	c.append({"id": "rkt_astroz_88dpro", "name": "Astroz 88D Pro", "slot": Slot.RACKET, "rarity": Rarity.RARE, "power": 5, "speed": -1, "stamina": 0, "price": 2000, "description": "Vợt đôi công, smash sau lưới nặng trịch."})
	c.append({"id": "rkt_jetspeede_s12", "name": "Jetspeede S 12", "slot": Slot.RACKET, "rarity": Rarity.RARE, "power": 1, "speed": 4, "stamina": 0, "price": 2000, "description": "Tốc độ cực cao, đánh nhanh thắng nhanh."})
	c.append({"id": "rkt_halbertex_8000", "name": "Halbertex 8000", "slot": Slot.RACKET, "rarity": Rarity.RARE, "power": 4, "speed": 1, "stamina": 0, "price": 2400, "description": "Công thủ toàn diện, cảm giác cầu rõ."})
	c.append({"id": "rkt_auraspeede_100x", "name": "Auraspeede 100X", "slot": Slot.RACKET, "rarity": Rarity.RARE, "power": 2, "speed": 4, "stamina": -1, "price": 2800, "description": "Nhanh và hiểm, đổi lại hơi tốn sức."})
	# Legendary (3000-5000 coins): highest single stat +8.
	c.append({"id": "rkt_nanoflair_1000z", "name": "Nanoflair 1000Z", "slot": Slot.RACKET, "rarity": Rarity.LEGENDARY, "power": 2, "speed": 6, "stamina": 0, "price": 3200, "description": "Tốc độ huyền thoại, thủ cầu không góc chết."})
	c.append({"id": "rkt_astroz_99pro", "name": "Astroz 99 Pro", "slot": Slot.RACKET, "rarity": Rarity.LEGENDARY, "power": 6, "speed": -1, "stamina": 1, "price": 3800, "description": "Cây đập của các tay vợt số 1 thế giới."})
	c.append({"id": "rkt_thrusta_ryuga2", "name": "Thrusta Ryuga II", "slot": Slot.RACKET, "rarity": Rarity.LEGENDARY, "power": 7, "speed": -2, "stamina": 0, "price": 4200, "description": "Sức mạnh hủy diệt, chỉ dành cho tay to."})
	c.append({"id": "rkt_axeforce_100", "name": "Axeforce 100", "slot": Slot.RACKET, "rarity": Rarity.LEGENDARY, "power": 6, "speed": 1, "stamina": 1, "price": 4600, "description": "Đỉnh cao dòng tấn công, vừa mạnh vừa bền."})
	c.append({"id": "rkt_astroz_100zz", "name": "Astroz 100ZZ", "slot": Slot.RACKET, "rarity": Rarity.LEGENDARY, "power": 8, "speed": -2, "stamina": 0, "price": 5000, "description": "Vua của các cây vợt — smash là ghi điểm."})
	# Shoes.
	c.append({"id": STARTER_SHOES_ID, "name": "Giày Khởi Đầu", "slot": Slot.SHOES, "rarity": Rarity.COMMON, "power": 0, "speed": 0, "stamina": 0, "price": 0, "description": "Giày tập cơ bản."})
	c.append({"id": "sho_grip_pro", "name": "Bám Sân Pro", "slot": Slot.SHOES, "rarity": Rarity.GOOD, "power": 0, "speed": 3, "stamina": 0, "price": 400, "description": "Đế bám sân, đổi hướng nhanh."})
	c.append({"id": "sho_light_air", "name": "Nhẹ Nhàng Air", "slot": Slot.SHOES, "rarity": Rarity.GOOD, "power": 0, "speed": 2, "stamina": 1, "price": 550, "description": "Nhẹ chân, di chuyển bền hơn."})
	c.append({"id": "sho_court_king", "name": "Vua Sân Đấu", "slot": Slot.SHOES, "rarity": Rarity.RARE, "power": 0, "speed": 4, "stamina": -1, "price": 900, "description": "Tốc độ tối đa, hơi tốn sức."})
	# Shirts.
	c.append({"id": STARTER_SHIRT_ID, "name": "Áo Khởi Đầu", "slot": Slot.SHIRT, "rarity": Rarity.COMMON, "power": 0, "speed": 0, "stamina": 0, "price": 0, "description": "Áo tập cơ bản."})
	c.append({"id": "shr_dry_cool", "name": "Thoáng Mát Dry", "slot": Slot.SHIRT, "rarity": Rarity.COMMON, "power": 0, "speed": 0, "stamina": 2, "price": 250, "description": "Thoát mồ hôi, đỡ mệt hơn."})
	c.append({"id": "shr_aero_fit", "name": "Aero Fit", "slot": Slot.SHIRT, "rarity": Rarity.GOOD, "power": 0, "speed": 1, "stamina": 2, "price": 450, "description": "Ôm người, chạy nhanh, bền hơn."})
	c.append({"id": "shr_champion", "name": "Nhà Vô Địch", "slot": Slot.SHIRT, "rarity": Rarity.RARE, "power": 1, "speed": 0, "stamina": 3, "price": 800, "description": "Thể lực tốt, tự tin hơn khi đập."})
	return c

## Last error message from buy()/equip() ("" = no error).
var last_error: String = ""

# Career reference (duck-typed: Career — set by Career, used for money
# and saving; kept untyped to avoid a parse-time class cycle).
var career = null

var _catalog: Array = []
var _owned: Dictionary = {}  # item id -> true
var _equipped: Dictionary = {}  # slot int -> item id

func _init(career_ref = null) -> void:
	career = career_ref
	_catalog = build_catalog()
	_owned[STARTER_RACKET_ID] = true
	_owned[STARTER_SHOES_ID] = true
	_owned[STARTER_SHIRT_ID] = true
	_equipped[Slot.RACKET] = STARTER_RACKET_ID
	_equipped[Slot.SHOES] = STARTER_SHOES_ID
	_equipped[Slot.SHIRT] = STARTER_SHIRT_ID

func all_items() -> Array:
	return _catalog.duplicate()

func get_item(item_id: String) -> Dictionary:
	if item_id.is_empty():
		return {}
	for item in _catalog:
		if String(item["id"]) == item_id:
			return item
	return {}

func list_by_slot(slot: int) -> Array:
	var list: Array = []
	for item in _catalog:
		if int(item["slot"]) == slot:
			list.append(item)
	return list

func is_owned(item_id: String) -> bool:
	return not item_id.is_empty() and _owned.has(item_id)

func is_equipped(item_id: String) -> bool:
	if item_id.is_empty():
		return false
	for slot in _equipped:
		if String(_equipped[slot]) == item_id:
			return true
	return false

func equipped_id(slot: int) -> String:
	return String(_equipped.get(slot, ""))

func equipped_item(slot: int) -> Dictionary:
	return get_item(equipped_id(slot))

## Buy with Career coins. False + last_error when already owned or not
## enough coins. Buys save immediately.
func buy(item: Dictionary) -> bool:
	last_error = ""
	if item.is_empty():
		last_error = "Món đồ không tồn tại."
		return false
	if is_owned(String(item["id"])):
		last_error = "Bạn đã sở hữu món này."
		return false
	if career == null:
		last_error = "Chưa có sự nghiệp để dùng tiền."
		return false
	if not career.spend_money(int(item["price"])):
		last_error = "Không đủ tiền (cần %d xu)." % int(item["price"])
		return false
	_owned[String(item["id"])] = true
	if career != null:
		career.save()
	return true

## Equip an owned item (one item per slot). False when not owned.
func equip(item_id: String) -> bool:
	last_error = ""
	var item := get_item(item_id)
	if item.is_empty():
		last_error = "Món đồ không tồn tại."
		return false
	if not is_owned(item_id):
		last_error = "Bạn chưa sở hữu món này."
		return false
	_equipped[int(item["slot"])] = item_id
	if career != null:
		career.save()
	return true

## Unequip back to the starter item of that slot.
func unequip(slot: int) -> void:
	var starter := STARTER_RACKET_ID
	if slot == Slot.SHOES:
		starter = STARTER_SHOES_ID
	elif slot == Slot.SHIRT:
		starter = STARTER_SHIRT_ID
	_equipped[slot] = starter
	if career != null:
		career.save()

## Returns a COPY of base_stats with the equipped items' modifiers added
## (clamped 1..99). Base stats are never edited.
func apply_to(base_stats: PlayerStats) -> PlayerStats:
	if base_stats == null:
		return null
	var result := base_stats.clone()
	for slot in _equipped:
		var item := get_item(String(_equipped[slot]))
		if item.is_empty():
			continue
		result.power += int(item["power"])
		result.speed += int(item["speed"])
		result.stamina += int(item["stamina"])
	result.clamp_all()
	return result

## Reset the shop to starters only — used when starting a new career, so
## items bought in the old career don't carry over.
func reset_to_starters() -> void:
	_owned.clear()
	_owned[STARTER_RACKET_ID] = true
	_owned[STARTER_SHOES_ID] = true
	_owned[STARTER_SHIRT_ID] = true
	_equipped[Slot.RACKET] = STARTER_RACKET_ID
	_equipped[Slot.SHOES] = STARTER_SHOES_ID
	_equipped[Slot.SHIRT] = STARTER_SHIRT_ID
	if career != null:
		career.save()

# --- Save / load ----------------------------------------------------------

func load_from_data(data: Dictionary) -> void:
	_owned.clear()
	_owned[STARTER_RACKET_ID] = true
	_owned[STARTER_SHOES_ID] = true
	_owned[STARTER_SHIRT_ID] = true
	var owned_ids: Array = data.get("owned_equipment_ids", [])
	for item_id in owned_ids:
		if not get_item(String(item_id)).is_empty():
			_owned[String(item_id)] = true
	_equipped[Slot.RACKET] = STARTER_RACKET_ID
	_equipped[Slot.SHOES] = STARTER_SHOES_ID
	_equipped[Slot.SHIRT] = STARTER_SHIRT_ID
	var equipped_list: Array = data.get("equipped", [])
	for entry in equipped_list:
		if not (entry is Dictionary):
			continue
		var slot := slot_from_name(String(entry.get("slot", "")))
		var item_id := String(entry.get("item_id", ""))
		if not get_item(item_id).is_empty() and _owned.has(item_id):
			_equipped[slot] = item_id

func write_to_data(data: Dictionary) -> void:
	data["owned_equipment_ids"] = _owned.keys()
	var equipped_list: Array = []
	for slot in _equipped:
		equipped_list.append({"slot": slot_name(int(slot)), "item_id": String(_equipped[slot])})
	data["equipped"] = equipped_list
