from pathlib import Path


def sql_quote(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def repair_mojibake(value):
    if isinstance(value, str):
        try:
            return value.encode("latin1").decode("utf-8")
        except (UnicodeEncodeError, UnicodeDecodeError):
            return value
    if isinstance(value, list):
        return [repair_mojibake(item) for item in value]
    if isinstance(value, tuple):
        return tuple(repair_mojibake(item) for item in value)
    if isinstance(value, dict):
        return {key: repair_mojibake(item) for key, item in value.items()}
    return value


def make_translation_script(language_code: str, item: dict) -> str:
    if language_code == "vi":
        return item["script_vi"]
    if language_code == "en":
        return (
            f"{item['name']} is a local food stop at {item['address']}. "
            f"Signature dishes include {item['specialty_1']}, {item['specialty_2']}, and {item['specialty_3']}."
        )
    if language_code == "zh-CN":
        return (
            f"{item['name']} 位于{item['address']}，"
            f"招牌菜包括{item['specialty_1']}、{item['specialty_2']}和{item['specialty_3']}。"
        )
    if language_code == "ja":
        return (
            f"{item['name']} は {item['address']} にある人気店です。"
            f"おすすめは {item['specialty_1']}、{item['specialty_2']}、{item['specialty_3']} です。"
        )
    if language_code == "ko":
        return (
            f"{item['name']} 는 {item['address']} 에 있는 현지 맛집입니다. "
            f"대표 메뉴는 {item['specialty_1']}, {item['specialty_2']}, {item['specialty_3']} 입니다."
        )
    raise ValueError(f"Unsupported language code: {language_code}")


items = [
    {
        "name": "Chè Hoa Cô Lan",
        "category_slug": "desserts",
        "latitude": 10.754671,
        "longitude": 106.667296,
        "image_url": "che-hoa-co-lan.png",
        "opening_hours": "09:00-22:00",
        "rating_avg": 4.8,
        "reviews_count": 124,
        "address": "622 Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "chè hột gà trà",
        "specialty_2": "chè đậu đỏ",
        "specialty_3": "chè mè đen",
        "poi_radius_m": 28,
        "script_vi": "Nằm tại 622 Nguyễn Trãi, Chè Hoa Cô Lan là điểm đến lý tưởng cho ai mê chè Hoa Quận 5. Quán nổi tiếng với chè hột gà trà, đậu đỏ ngọt thanh, chuẩn vị. Không gian bình dân, mộc mạc tại đây chắc chắn sẽ khiến bạn hài lòng!",
    },
    {
        "name": "Hủ Tiếu - Hủ Mỳ",
        "category_slug": "noodles",
        "latitude": 10.754630,
        "longitude": 106.667263,
        "image_url": "hu-tieu-hu-my.png",
        "opening_hours": "06:30-11:30",
        "rating_avg": 4.7,
        "reviews_count": 108,
        "address": "012 Lô A, C/C Xóm Cải, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "hủ tiếu",
        "specialty_2": "hủ mì",
        "specialty_3": "sủi cảo",
        "poi_radius_m": 28,
        "script_vi": "Tọa lạc trong khu Xóm Cải, quán Hủ Tiếu - Hủ Mỳ hấp dẫn thực khách với tô mì vịt quay trứ danh. Sợi mì dai ngon, vịt quay đậm đà, nước dùng thanh nhẹ. Đây là lựa chọn tuyệt vời cho bữa sáng hoặc trưa của bạn!",
    },
    {
        "name": "Quán Cơm Phong Bình",
        "category_slug": "rice",
        "latitude": 10.754666,
        "longitude": 106.667300,
        "image_url": "quan-com-phong-binh.png",
        "opening_hours": "10:30-14:00",
        "rating_avg": 4.5,
        "reviews_count": 67,
        "address": "Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "cơm sườn",
        "specialty_2": "cơm thịt kho",
        "specialty_3": "canh cải",
        "poi_radius_m": 26,
        "script_vi": "Tọa lạc trong khu Xóm Cải, Quán Cơm Phong Bình là quán cơm bình dân được yêu thích với các món mặn quen thuộc. Quán gây ấn tượng bởi hương vị ổn định, khẩu phần đầy đặn và không khí gần gũi, đúng chất bữa cơm nhà.",
    },
    {
        "name": "Hoà Ký",
        "category_slug": "noodles",
        "latitude": 10.754628,
        "longitude": 106.667215,
        "image_url": "hoa-ky.png",
        "opening_hours": "06:30-13:30",
        "rating_avg": 4.8,
        "reviews_count": 136,
        "address": "QM38+RRV, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "mì vịt quay",
        "specialty_2": "mì xá xíu",
        "specialty_3": "hoành thánh",
        "poi_radius_m": 30,
        "script_vi": "Ẩn mình trong khu chung cư cũ trên đường Nguyễn Trãi, Hoà Ký là quán mì người Hoa quen thuộc của nhiều thực khách sành ăn. Quán nổi bật với món mì vịt quay đậm đà, sợi mì dai ngon, nước dùng trong nhưng giàu hương vị.",
    },
    {
        "name": "Minh Phát Hủ Tíu Mì Phường 8 Quận 5",
        "category_slug": "noodles",
        "latitude": 10.754615,
        "longitude": 106.667104,
        "image_url": "minh-phat-hu-tiu-mi-phuong-8-quan-5.png",
        "opening_hours": "06:30-12:00",
        "rating_avg": 4.6,
        "reviews_count": 73,
        "address": "An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "hủ tiếu nước",
        "specialty_2": "mì khô",
        "specialty_3": "hoành thánh",
        "poi_radius_m": 30,
        "script_vi": "Nằm trong khu vực Quận 5 sầm uất, Minh Phát Hủ Tíu Mì là quán ăn quen thuộc của những ai yêu thích món nước kiểu Hoa. Quán nổi bật với nước dùng ngọt thanh, topping đầy đặn và cách phục vụ nhanh nhẹn.",
    },
    {
        "name": "Hủ tiếu mì Bà Cao",
        "category_slug": "noodles",
        "latitude": 10.754606,
        "longitude": 106.666961,
        "image_url": "hu-tieu-mi-ba-cao.png",
        "opening_hours": "06:00-12:30",
        "rating_avg": 4.7,
        "reviews_count": 96,
        "address": "Chung cư Nguyễn Trãi lô A, 004 Lô A, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "hủ tiếu mì",
        "specialty_2": "xá xíu",
        "specialty_3": "nước dùng xương",
        "poi_radius_m": 30,
        "script_vi": "Nằm ở lô A chung cư Nguyễn Trãi, hủ tiếu mì Bà Cao là quán ăn sáng nổi tiếng với hương vị truyền thống. Tô hủ tiếu ở đây hấp dẫn nhờ nước dùng trong veo, topping xá xíu đậm vị và sợi mì vừa dai vừa thơm.",
    },
    {
        "name": "Chả Cuốn Cá Trích \"Tranh\"",
        "category_slug": "specialties",
        "latitude": 10.754873,
        "longitude": 106.667255,
        "image_url": "cha-cuon-ca-trich-tranh.png",
        "opening_hours": "11:00-20:00",
        "rating_avg": 4.6,
        "reviews_count": 58,
        "address": "Lô C chung cư, cầu thang/013 Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "chả cuốn cá trích",
        "specialty_2": "rau sống",
        "specialty_3": "nước chấm đậm đà",
        "poi_radius_m": 24,
        "script_vi": "Tại khu Xóm Cải nhộn nhịp, Chả Cuốn Cá Trích \"Tranh\" là một địa chỉ đặc sắc với món cá trích cuốn độc đáo. Mỗi phần ăn được cuốn khéo léo, dậy mùi thơm đặc trưng, ăn kèm rau sống tươi và nước chấm đậm đà.",
    },
    {
        "name": "Há Cảo Phánh",
        "category_slug": "dumplings",
        "latitude": 10.754861,
        "longitude": 106.667172,
        "image_url": "ha-cao-phanh.png",
        "opening_hours": "14:00-20:30",
        "rating_avg": 4.6,
        "reviews_count": 82,
        "address": "Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "há cảo",
        "specialty_2": "xíu mại",
        "specialty_3": "bánh xếp",
        "poi_radius_m": 24,
        "script_vi": "Nằm trong khu người Hoa Quận 5, Há Cảo Phánh là quán nhỏ nổi tiếng với những xửng há cảo nóng hổi thơm ngon. Vỏ mỏng, nhân đậm vị, món ăn được phục vụ nhanh và giữ trọn nét ẩm thực truyền thống.",
    },
    {
        "name": "Bánh Canh 013",
        "category_slug": "noodles",
        "latitude": 10.754996,
        "longitude": 106.667272,
        "image_url": "banh-canh-013.png",
        "opening_hours": "06:30-13:00",
        "rating_avg": 4.5,
        "reviews_count": 64,
        "address": "Chung cư, Lô C/013 Xóm Cải, Phường 9, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "bánh canh sườn sụn",
        "specialty_2": "nui",
        "specialty_3": "bún gạo",
        "poi_radius_m": 26,
        "script_vi": "Ẩn mình trong khu chung cư Xóm Cải, Bánh Canh 013 là quán ăn sáng quen thuộc của người dân địa phương. Tô bánh canh nóng hổi với nước dùng đậm đà, sợi bánh mềm dai và topping đầy đặn khiến ai thử cũng dễ nhớ.",
    },
    {
        "name": "Cơm Tấm - Bảo Nhi",
        "category_slug": "rice",
        "latitude": 10.755039,
        "longitude": 106.667484,
        "image_url": "com-tam-bao-nhi.png",
        "opening_hours": "06:00-10:30",
        "rating_avg": 4.7,
        "reviews_count": 91,
        "address": "Lô C chung cư, Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "cơm tấm sườn",
        "specialty_2": "bì chả",
        "specialty_3": "trứng ốp la",
        "poi_radius_m": 28,
        "script_vi": "Tọa lạc trên đường Nguyễn Trãi, Cơm Tấm Bảo Nhi là điểm đến quen thuộc cho bữa sáng đậm chất Sài Gòn. Quán nổi bật với miếng sườn nướng thơm lừng, cơm tơi mềm và phần ăn đầy đặn, đậm đà.",
    },
    {
        "name": "Bánh cuốn Phú Thành",
        "category_slug": "snacks",
        "latitude": 10.755199,
        "longitude": 106.667475,
        "image_url": "banh-cuon-phu-thanh.png",
        "opening_hours": "06:00-11:00",
        "rating_avg": 4.6,
        "reviews_count": 77,
        "address": "42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "bánh cuốn",
        "specialty_2": "nem chả",
        "specialty_3": "hành phi",
        "poi_radius_m": 24,
        "script_vi": "Nằm trên đường Mạc Thiên Tích, Bánh cuốn Phú Thành là quán nhỏ được nhiều người tìm đến vào buổi sáng. Lớp bánh mỏng mịn, nhân vừa ăn, ăn kèm chả lụa và nước mắm pha hài hòa khiến món ăn thêm cuốn hút.",
    },
    {
        "name": "Hủ tiếu xào 020 lô C",
        "category_slug": "noodles",
        "latitude": 10.755281,
        "longitude": 106.667491,
        "image_url": "hu-tieu-xao-020-lo-c.png",
        "opening_hours": "16:00-22:00",
        "rating_avg": 4.6,
        "reviews_count": 69,
        "address": "Chung cư Nguyễn Trãi lô A, lô C/42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "hủ tiếu xào",
        "specialty_2": "bún Singapore",
        "specialty_3": "cơm chiên",
        "poi_radius_m": 24,
        "script_vi": "Tại khu lô C Nguyễn Trãi, quán Hủ Tiếu Xào 020 là điểm hẹn chiều tối của nhiều tín đồ món xào kiểu Hoa. Sợi hủ tiếu được xào săn, thơm lửa, kết hợp cùng rau và thịt tạo nên hương vị hấp dẫn, khó quên.",
    },
    {
        "name": "Khổ Qua Cà Ớt Híng Ky",
        "category_slug": "specialties",
        "latitude": 10.754876,
        "longitude": 106.667537,
        "image_url": "kho-qua-ca-ot-hing-ky.png",
        "opening_hours": "15:00-21:00",
        "rating_avg": 4.7,
        "reviews_count": 84,
        "address": "Chung cư Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "khổ qua dồn chả cá",
        "specialty_2": "cà tím dồn",
        "specialty_3": "nước lèo sa tế",
        "poi_radius_m": 24,
        "script_vi": "Nằm trong khu Xóm Cải đậm màu sắc người Hoa, Híng Ky gây ấn tượng với món khổ qua cà ớt mang hương vị lạ miệng, đậm đà. Món ăn được chế biến cầu kỳ, vừa giữ được vị thanh tự nhiên vừa có chiều sâu hương vị.",
    },
    {
        "name": "Hủ Tiếu Mì Hồ Ký",
        "category_slug": "noodles",
        "latitude": 10.754536,
        "longitude": 106.667561,
        "image_url": "hu-tieu-mi-ho-ky.png",
        "opening_hours": "06:00-11:30",
        "rating_avg": 4.5,
        "reviews_count": 62,
        "address": "Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "hủ tiếu nước",
        "specialty_2": "mì trụng",
        "specialty_3": "xá xíu",
        "poi_radius_m": 28,
        "script_vi": "Tọa lạc trong khu Xóm Cải, Hủ Tiếu Mì Hồ Ký là quán quen thuộc của người dân yêu thích món nước kiểu Hoa. Quán có phần nước dùng thanh, topping đầy đặn và phong vị mộc mạc, dễ ăn.",
    },
    {
        "name": "Quán Ăn Phú Ký",
        "category_slug": "noodles",
        "latitude": 10.754517,
        "longitude": 106.667761,
        "image_url": "quan-an-phu-ky.png",
        "opening_hours": "06:00-12:00",
        "rating_avg": 4.6,
        "reviews_count": 71,
        "address": "598/6 Nguyễn Trãi, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "hủ tiếu gia đình",
        "specialty_2": "xương hầm",
        "specialty_3": "mì trứng",
        "poi_radius_m": 28,
        "script_vi": "Nằm tại 598/6 Nguyễn Trãi, Quán Ăn Phú Ký là địa chỉ bình dân nhưng được nhiều người yêu thích nhờ hương vị ổn định. Quán phục vụ các món mì và hủ tiếu với phần nước dùng ninh xương đậm vị, thích hợp cho bữa sáng hoặc trưa.",
    },
    {
        "name": "Mì Khô Xá Xíu",
        "category_slug": "noodles",
        "latitude": 10.754559,
        "longitude": 106.667771,
        "image_url": "mi-kho-xa-xiu.png",
        "opening_hours": "06:00-12:00",
        "rating_avg": 4.7,
        "reviews_count": 89,
        "address": "Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam",
        "specialty_1": "mì khô xá xíu",
        "specialty_2": "sủi cảo",
        "specialty_3": "hoành thánh",
        "poi_radius_m": 28,
        "script_vi": "Nằm trong khu Nguyễn Trãi sôi động, Mì Khô Xá Xíu là quán ăn hấp dẫn với món mì khô trộn đậm vị. Sợi mì dai, xá xíu thơm ngọt, ăn cùng nước lèo nóng và topping đầy đặn tạo nên trải nghiệm rất tròn vị.",
    },
]


category_names = {
    "seafood": "Hải sản",
    "grilled": "Đồ nướng",
    "noodles": "Món nước",
    "snacks": "Ăn vặt",
    "desserts": "Tráng miệng",
    "rice": "Cơm",
    "dumplings": "Há cảo",
    "specialties": "Đặc sản",
}

language_names = [
    ("vi", "Vietnamese", "Tiếng Việt", "vi-VN", 1),
    ("en", "English", "English", "en-US", 2),
    ("zh-CN", "Chinese", "中文", "zh-CN", 3),
    ("ja", "Japanese", "日本語", "ja-JP", 4),
    ("ko", "Korean", "한국어", "ko-KR", 5),
]

items = repair_mojibake(items)
category_names = repair_mojibake(category_names)
language_names = repair_mojibake(language_names)


lines = [
    "BEGIN;",
    "",
    "ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_1 TEXT;",
    "ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_2 TEXT;",
    "ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_3 TEXT;",
    "ALTER TABLE stalls ADD COLUMN IF NOT EXISTS poi_radius_m DOUBLE PRECISION DEFAULT 30;",
    "",
    "INSERT INTO languages (code, name, native_name, locale_code, sort_order) VALUES",
]

language_rows = []
for code, name, native_name, locale_code, sort_order in language_names:
    language_rows.append(
        f"({sql_quote(code)}, {sql_quote(name)}, {sql_quote(native_name)}, {sql_quote(locale_code)}, {sort_order})"
    )
lines.append(",\n".join(language_rows))
lines.append("ON CONFLICT (code) DO UPDATE SET")
lines.append("    name = EXCLUDED.name,")
lines.append("    native_name = EXCLUDED.native_name,")
lines.append("    locale_code = EXCLUDED.locale_code,")
lines.append("    sort_order = EXCLUDED.sort_order,")
lines.append("    updated_at = NOW();")
lines.append("")

lines.append("INSERT INTO categories (slug, name, icon_url) VALUES")
category_rows = [f"({sql_quote(slug)}, {sql_quote(name)}, NULL)" for slug, name in category_names.items()]
lines.append(",\n".join(category_rows))
lines.append("ON CONFLICT (slug) DO UPDATE SET")
lines.append("    name = EXCLUDED.name,")
lines.append("    updated_at = NOW();")
lines.append("")
lines.append("DELETE FROM categories WHERE slug LIKE 'cat-%';")
lines.append("")
lines.append("TRUNCATE TABLE stalls RESTART IDENTITY CASCADE;")
lines.append("")
lines.append(
    "CREATE TEMP TABLE import_stalls ("
    "name VARCHAR(200) NOT NULL, "
    "category_slug VARCHAR(50) NOT NULL, "
    "latitude DOUBLE PRECISION NOT NULL, "
    "longitude DOUBLE PRECISION NOT NULL, "
    "image_url TEXT, "
    "opening_hours VARCHAR(255), "
    "rating_avg NUMERIC(2,1) NOT NULL, "
    "reviews_count INTEGER NOT NULL, "
    "address TEXT, "
    "specialty_1 TEXT, "
    "specialty_2 TEXT, "
    "specialty_3 TEXT, "
    "poi_radius_m DOUBLE PRECISION NOT NULL DEFAULT 30, "
    "script_vi TEXT NOT NULL"
    ");"
)
lines.append("")
lines.append(
    "INSERT INTO import_stalls ("
    "name, category_slug, latitude, longitude, image_url, opening_hours, rating_avg, reviews_count, "
    "address, specialty_1, specialty_2, specialty_3, poi_radius_m, script_vi"
    ") VALUES"
)

item_rows = []
for item in items:
    item_rows.append(
        "("
        + ", ".join(
            [
                sql_quote(item["name"]),
                sql_quote(item["category_slug"]),
                f"{item['latitude']:.6f}",
                f"{item['longitude']:.6f}",
                sql_quote(item["image_url"]),
                sql_quote(item["opening_hours"]),
                f"{item['rating_avg']:.1f}",
                str(item["reviews_count"]),
                sql_quote(item["address"]),
                sql_quote(item["specialty_1"]),
                sql_quote(item["specialty_2"]),
                sql_quote(item["specialty_3"]),
                str(item["poi_radius_m"]),
                sql_quote(item["script_vi"]),
            ]
        )
        + ")"
    )
lines.append(",\n".join(item_rows) + ";")
lines.append("")
lines.append(
    "INSERT INTO stalls ("
    "category_id, name, latitude, longitude, image_url, specialty_1, specialty_2, specialty_3, poi_radius_m, "
    "opening_hours, is_open, is_active, rating_avg, reviews_count, created_at, updated_at, is_deleted"
    ")"
)
lines.append("SELECT")
lines.append("    c.id,")
lines.append("    s.name,")
lines.append("    s.latitude,")
lines.append("    s.longitude,")
lines.append("    s.image_url,")
lines.append("    s.specialty_1,")
lines.append("    s.specialty_2,")
lines.append("    s.specialty_3,")
lines.append("    s.poi_radius_m,")
lines.append("    s.opening_hours,")
lines.append("    TRUE,")
lines.append("    TRUE,")
lines.append("    s.rating_avg,")
lines.append("    s.reviews_count,")
lines.append("    NOW(),")
lines.append("    NOW(),")
lines.append("    FALSE")
lines.append("FROM import_stalls s")
lines.append("JOIN categories c ON c.slug = s.category_slug;")
lines.append("")
lines.append(
    "INSERT INTO stall_translations ("
    "stall_id, language_id, title, description, script_text, is_auto_generated, translation_status, source_version, created_at, updated_at"
    ")"
)
lines.append("SELECT")
lines.append("    st.id,")
lines.append("    l.id,")
lines.append("    src.name,")
lines.append("    src.address,")
lines.append("    CASE l.code")
for language_code in ["vi", "en", "zh-CN", "ja", "ko"]:
    sample = make_translation_script(language_code, items[0])
    del sample
    lines.append(f"        WHEN {sql_quote(language_code)} THEN")
    lines.append("            CASE src.image_url")
    for item in items:
        lines.append(
            f"                WHEN {sql_quote(item['image_url'])} THEN {sql_quote(make_translation_script(language_code, item))}"
        )
    lines.append("                ELSE src.script_vi")
    lines.append("            END")
lines.append("        ELSE src.script_vi")
lines.append("    END,")
lines.append("    CASE WHEN l.code = 'vi' THEN FALSE ELSE TRUE END,")
lines.append("    'approved',")
lines.append("    1,")
lines.append("    NOW(),")
lines.append("    NOW()")
lines.append("FROM import_stalls src")
lines.append("JOIN stalls st ON st.image_url = src.image_url")
lines.append("JOIN languages l ON l.code IN ('vi', 'en', 'zh-CN', 'ja', 'ko');")
lines.append("")
lines.append("DROP TABLE import_stalls;")
lines.append("")
lines.append("COMMIT;")
lines.append("")

output = "\n".join(lines)
Path(__file__).with_name("seed_data.sql").write_text(output, encoding="utf-8", newline="\n")
print("seed_data.sql generated")
