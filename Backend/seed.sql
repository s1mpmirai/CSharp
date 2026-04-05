-- Demo/sample data seed for FoodStreet Audio Guide
-- WARNING: this file resets stall sample content and should be used only for demo or a fresh database.

BEGIN;

ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_1 TEXT;
ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_2 TEXT;
ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_3 TEXT;
ALTER TABLE stalls ADD COLUMN IF NOT EXISTS poi_radius_m DOUBLE PRECISION DEFAULT 30;

INSERT INTO languages (code, name, native_name, locale_code, sort_order) VALUES
('vi', 'Vietnamese', 'Tiáº¿ng Viá»‡t', 'vi-VN', 1),
('en', 'English', 'English', 'en-US', 2),
('zh-CN', 'Chinese', 'ä¸­æ–‡', 'zh-CN', 3),
('ja', 'Japanese', 'æ—¥æœ¬èªž', 'ja-JP', 4),
('ko', 'Korean', 'í•œêµ­ì–´', 'ko-KR', 5)
ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    native_name = EXCLUDED.native_name,
    locale_code = EXCLUDED.locale_code,
    sort_order = EXCLUDED.sort_order,
    updated_at = NOW();

INSERT INTO categories (slug, name, icon_url) VALUES
('seafood', 'Háº£i sáº£n', NULL),
('grilled', 'Äá»“ nÆ°á»›ng', NULL),
('noodles', 'MÃ³n nÆ°á»›c', NULL),
('snacks', 'Ä‚n váº·t', NULL),
('desserts', 'TrÃ¡ng miá»‡ng', NULL),
('rice', 'CÆ¡m', NULL),
('dumplings', 'HÃ¡ cáº£o', NULL),
('specialties', 'Äáº·c sáº£n', NULL)
ON CONFLICT (slug) DO UPDATE SET
    name = EXCLUDED.name,
    updated_at = NOW();

DELETE FROM categories WHERE slug LIKE 'cat-%';

TRUNCATE TABLE stalls RESTART IDENTITY CASCADE;

CREATE TEMP TABLE import_stalls (name VARCHAR(200) NOT NULL, category_slug VARCHAR(50) NOT NULL, latitude DOUBLE PRECISION NOT NULL, longitude DOUBLE PRECISION NOT NULL, image_url TEXT, opening_hours VARCHAR(255), rating_avg NUMERIC(2,1) NOT NULL, reviews_count INTEGER NOT NULL, address TEXT, specialty_1 TEXT, specialty_2 TEXT, specialty_3 TEXT, poi_radius_m DOUBLE PRECISION NOT NULL DEFAULT 30, script_vi TEXT NOT NULL);

INSERT INTO import_stalls (name, category_slug, latitude, longitude, image_url, opening_hours, rating_avg, reviews_count, address, specialty_1, specialty_2, specialty_3, poi_radius_m, script_vi) VALUES
('ChÃ¨ Hoa CÃ´ Lan', 'desserts', 10.754671, 106.667296, 'che-hoa-co-lan.png', '09:00-22:00', 4.8, 124, '622 Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'chÃ¨ há»™t gÃ  trÃ ', 'chÃ¨ Ä‘áº­u Ä‘á»', 'chÃ¨ mÃ¨ Ä‘en', 28, 'Náº±m táº¡i 622 Nguyá»…n TrÃ£i, ChÃ¨ Hoa CÃ´ Lan lÃ  Ä‘iá»ƒm Ä‘áº¿n lÃ½ tÆ°á»Ÿng cho ai mÃª chÃ¨ Hoa Quáº­n 5. QuÃ¡n ná»•i tiáº¿ng vá»›i chÃ¨ há»™t gÃ  trÃ , Ä‘áº­u Ä‘á» ngá»t thanh, chuáº©n vá»‹. KhÃ´ng gian bÃ¬nh dÃ¢n, má»™c máº¡c táº¡i Ä‘Ã¢y cháº¯c cháº¯n sáº½ khiáº¿n báº¡n hÃ i lÃ²ng!'),
('Há»§ Tiáº¿u - Há»§ Má»³', 'noodles', 10.754630, 106.667263, 'hu-tieu-hu-my.png', '06:30-11:30', 4.7, 108, '012 LÃ´ A, C/C XÃ³m Cáº£i, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'há»§ tiáº¿u', 'há»§ mÃ¬', 'sá»§i cáº£o', 28, 'Tá»a láº¡c trong khu XÃ³m Cáº£i, quÃ¡n Há»§ Tiáº¿u - Há»§ Má»³ háº¥p dáº«n thá»±c khÃ¡ch vá»›i tÃ´ mÃ¬ vá»‹t quay trá»© danh. Sá»£i mÃ¬ dai ngon, vá»‹t quay Ä‘áº­m Ä‘Ã , nÆ°á»›c dÃ¹ng thanh nháº¹. ÄÃ¢y lÃ  lá»±a chá»n tuyá»‡t vá»i cho bá»¯a sÃ¡ng hoáº·c trÆ°a cá»§a báº¡n!'),
('QuÃ¡n CÆ¡m Phong BÃ¬nh', 'rice', 10.754666, 106.667300, 'quan-com-phong-binh.png', '10:30-14:00', 4.5, 67, 'Khu XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'cÆ¡m sÆ°á»n', 'cÆ¡m thá»‹t kho', 'canh cáº£i', 26, 'Tá»a láº¡c trong khu XÃ³m Cáº£i, QuÃ¡n CÆ¡m Phong BÃ¬nh lÃ  quÃ¡n cÆ¡m bÃ¬nh dÃ¢n Ä‘Æ°á»£c yÃªu thÃ­ch vá»›i cÃ¡c mÃ³n máº·n quen thuá»™c. QuÃ¡n gÃ¢y áº¥n tÆ°á»£ng bá»Ÿi hÆ°Æ¡ng vá»‹ á»•n Ä‘á»‹nh, kháº©u pháº§n Ä‘áº§y Ä‘áº·n vÃ  khÃ´ng khÃ­ gáº§n gÅ©i, Ä‘Ãºng cháº¥t bá»¯a cÆ¡m nhÃ .'),
('HoÃ  KÃ½', 'noodles', 10.754628, 106.667215, 'hoa-ky.png', '06:30-13:30', 4.8, 136, 'QM38+RRV, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'mÃ¬ vá»‹t quay', 'mÃ¬ xÃ¡ xÃ­u', 'hoÃ nh thÃ¡nh', 30, 'áº¨n mÃ¬nh trong khu chung cÆ° cÅ© trÃªn Ä‘Æ°á»ng Nguyá»…n TrÃ£i, HoÃ  KÃ½ lÃ  quÃ¡n mÃ¬ ngÆ°á»i Hoa quen thuá»™c cá»§a nhiá»u thá»±c khÃ¡ch sÃ nh Äƒn. QuÃ¡n ná»•i báº­t vá»›i mÃ³n mÃ¬ vá»‹t quay Ä‘áº­m Ä‘Ã , sá»£i mÃ¬ dai ngon, nÆ°á»›c dÃ¹ng trong nhÆ°ng giÃ u hÆ°Æ¡ng vá»‹.'),
('Minh PhÃ¡t Há»§ TÃ­u MÃ¬ PhÆ°á»ng 8 Quáº­n 5', 'noodles', 10.754615, 106.667104, 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png', '06:30-12:00', 4.6, 73, 'An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'há»§ tiáº¿u nÆ°á»›c', 'mÃ¬ khÃ´', 'hoÃ nh thÃ¡nh', 30, 'Náº±m trong khu vá»±c Quáº­n 5 sáº§m uáº¥t, Minh PhÃ¡t Há»§ TÃ­u MÃ¬ lÃ  quÃ¡n Äƒn quen thuá»™c cá»§a nhá»¯ng ai yÃªu thÃ­ch mÃ³n nÆ°á»›c kiá»ƒu Hoa. QuÃ¡n ná»•i báº­t vá»›i nÆ°á»›c dÃ¹ng ngá»t thanh, topping Ä‘áº§y Ä‘áº·n vÃ  cÃ¡ch phá»¥c vá»¥ nhanh nháº¹n.'),
('Há»§ tiáº¿u mÃ¬ BÃ  Cao', 'noodles', 10.754606, 106.666961, 'hu-tieu-mi-ba-cao.png', '06:00-12:30', 4.7, 96, 'Chung cÆ° Nguyá»…n TrÃ£i lÃ´ A, 004 LÃ´ A, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'há»§ tiáº¿u mÃ¬', 'xÃ¡ xÃ­u', 'nÆ°á»›c dÃ¹ng xÆ°Æ¡ng', 30, 'Náº±m á»Ÿ lÃ´ A chung cÆ° Nguyá»…n TrÃ£i, há»§ tiáº¿u mÃ¬ BÃ  Cao lÃ  quÃ¡n Äƒn sÃ¡ng ná»•i tiáº¿ng vá»›i hÆ°Æ¡ng vá»‹ truyá»n thá»‘ng. TÃ´ há»§ tiáº¿u á»Ÿ Ä‘Ã¢y háº¥p dáº«n nhá» nÆ°á»›c dÃ¹ng trong veo, topping xÃ¡ xÃ­u Ä‘áº­m vá»‹ vÃ  sá»£i mÃ¬ vá»«a dai vá»«a thÆ¡m.'),
('Cháº£ Cuá»‘n CÃ¡ TrÃ­ch "Tranh"', 'specialties', 10.754873, 106.667255, 'cha-cuon-ca-trich-tranh.png', '11:00-20:00', 4.6, 58, 'LÃ´ C chung cÆ°, cáº§u thang/013 XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'cháº£ cuá»‘n cÃ¡ trÃ­ch', 'rau sá»‘ng', 'nÆ°á»›c cháº¥m Ä‘áº­m Ä‘Ã ', 24, 'Táº¡i khu XÃ³m Cáº£i nhá»™n nhá»‹p, Cháº£ Cuá»‘n CÃ¡ TrÃ­ch "Tranh" lÃ  má»™t Ä‘á»‹a chá»‰ Ä‘áº·c sáº¯c vá»›i mÃ³n cÃ¡ trÃ­ch cuá»‘n Ä‘á»™c Ä‘Ã¡o. Má»—i pháº§n Äƒn Ä‘Æ°á»£c cuá»‘n khÃ©o lÃ©o, dáº­y mÃ¹i thÆ¡m Ä‘áº·c trÆ°ng, Äƒn kÃ¨m rau sá»‘ng tÆ°Æ¡i vÃ  nÆ°á»›c cháº¥m Ä‘áº­m Ä‘Ã .'),
('HÃ¡ Cáº£o PhÃ¡nh', 'dumplings', 10.754861, 106.667172, 'ha-cao-phanh.png', '14:00-20:30', 4.6, 82, 'Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'hÃ¡ cáº£o', 'xÃ­u máº¡i', 'bÃ¡nh xáº¿p', 24, 'Náº±m trong khu ngÆ°á»i Hoa Quáº­n 5, HÃ¡ Cáº£o PhÃ¡nh lÃ  quÃ¡n nhá» ná»•i tiáº¿ng vá»›i nhá»¯ng xá»­ng hÃ¡ cáº£o nÃ³ng há»•i thÆ¡m ngon. Vá» má»ng, nhÃ¢n Ä‘áº­m vá»‹, mÃ³n Äƒn Ä‘Æ°á»£c phá»¥c vá»¥ nhanh vÃ  giá»¯ trá»n nÃ©t áº©m thá»±c truyá»n thá»‘ng.'),
('BÃ¡nh Canh 013', 'noodles', 10.754996, 106.667272, 'banh-canh-013.png', '06:30-13:00', 4.5, 64, 'Chung cÆ°, LÃ´ C/013 XÃ³m Cáº£i, PhÆ°á»ng 9, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'bÃ¡nh canh sÆ°á»n sá»¥n', 'nui', 'bÃºn gáº¡o', 26, 'áº¨n mÃ¬nh trong khu chung cÆ° XÃ³m Cáº£i, BÃ¡nh Canh 013 lÃ  quÃ¡n Äƒn sÃ¡ng quen thuá»™c cá»§a ngÆ°á»i dÃ¢n Ä‘á»‹a phÆ°Æ¡ng. TÃ´ bÃ¡nh canh nÃ³ng há»•i vá»›i nÆ°á»›c dÃ¹ng Ä‘áº­m Ä‘Ã , sá»£i bÃ¡nh má»m dai vÃ  topping Ä‘áº§y Ä‘áº·n khiáº¿n ai thá»­ cÅ©ng dá»… nhá»›.'),
('CÆ¡m Táº¥m - Báº£o Nhi', 'rice', 10.755039, 106.667484, 'com-tam-bao-nhi.png', '06:00-10:30', 4.7, 91, 'LÃ´ C chung cÆ°, Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'cÆ¡m táº¥m sÆ°á»n', 'bÃ¬ cháº£', 'trá»©ng á»‘p la', 28, 'Tá»a láº¡c trÃªn Ä‘Æ°á»ng Nguyá»…n TrÃ£i, CÆ¡m Táº¥m Báº£o Nhi lÃ  Ä‘iá»ƒm Ä‘áº¿n quen thuá»™c cho bá»¯a sÃ¡ng Ä‘áº­m cháº¥t SÃ i GÃ²n. QuÃ¡n ná»•i báº­t vá»›i miáº¿ng sÆ°á»n nÆ°á»›ng thÆ¡m lá»«ng, cÆ¡m tÆ¡i má»m vÃ  pháº§n Äƒn Ä‘áº§y Ä‘áº·n, Ä‘áº­m Ä‘Ã .'),
('BÃ¡nh cuá»‘n PhÃº ThÃ nh', 'snacks', 10.755199, 106.667475, 'banh-cuon-phu-thanh.png', '06:00-11:00', 4.6, 77, '42 Máº¡c ThiÃªn TÃ­ch, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'bÃ¡nh cuá»‘n', 'nem cháº£', 'hÃ nh phi', 24, 'Náº±m trÃªn Ä‘Æ°á»ng Máº¡c ThiÃªn TÃ­ch, BÃ¡nh cuá»‘n PhÃº ThÃ nh lÃ  quÃ¡n nhá» Ä‘Æ°á»£c nhiá»u ngÆ°á»i tÃ¬m Ä‘áº¿n vÃ o buá»•i sÃ¡ng. Lá»›p bÃ¡nh má»ng má»‹n, nhÃ¢n vá»«a Äƒn, Äƒn kÃ¨m cháº£ lá»¥a vÃ  nÆ°á»›c máº¯m pha hÃ i hÃ²a khiáº¿n mÃ³n Äƒn thÃªm cuá»‘n hÃºt.'),
('Há»§ tiáº¿u xÃ o 020 lÃ´ C', 'noodles', 10.755281, 106.667491, 'hu-tieu-xao-020-lo-c.png', '16:00-22:00', 4.6, 69, 'Chung cÆ° Nguyá»…n TrÃ£i lÃ´ A, lÃ´ C/42 Máº¡c ThiÃªn TÃ­ch, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'há»§ tiáº¿u xÃ o', 'bÃºn Singapore', 'cÆ¡m chiÃªn', 24, 'Táº¡i khu lÃ´ C Nguyá»…n TrÃ£i, quÃ¡n Há»§ Tiáº¿u XÃ o 020 lÃ  Ä‘iá»ƒm háº¹n chiá»u tá»‘i cá»§a nhiá»u tÃ­n Ä‘á»“ mÃ³n xÃ o kiá»ƒu Hoa. Sá»£i há»§ tiáº¿u Ä‘Æ°á»£c xÃ o sÄƒn, thÆ¡m lá»­a, káº¿t há»£p cÃ¹ng rau vÃ  thá»‹t táº¡o nÃªn hÆ°Æ¡ng vá»‹ háº¥p dáº«n, khÃ³ quÃªn.'),
('Khá»• Qua CÃ  á»št HÃ­ng Ky', 'specialties', 10.754876, 106.667537, 'kho-qua-ca-ot-hing-ky.png', '15:00-21:00', 4.7, 84, 'Chung cÆ° XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'khá»• qua dá»“n cháº£ cÃ¡', 'cÃ  tÃ­m dá»“n', 'nÆ°á»›c lÃ¨o sa táº¿', 24, 'Náº±m trong khu XÃ³m Cáº£i Ä‘áº­m mÃ u sáº¯c ngÆ°á»i Hoa, HÃ­ng Ky gÃ¢y áº¥n tÆ°á»£ng vá»›i mÃ³n khá»• qua cÃ  á»›t mang hÆ°Æ¡ng vá»‹ láº¡ miá»‡ng, Ä‘áº­m Ä‘Ã . MÃ³n Äƒn Ä‘Æ°á»£c cháº¿ biáº¿n cáº§u ká»³, vá»«a giá»¯ Ä‘Æ°á»£c vá»‹ thanh tá»± nhiÃªn vá»«a cÃ³ chiá»u sÃ¢u hÆ°Æ¡ng vá»‹.'),
('Há»§ Tiáº¿u MÃ¬ Há»“ KÃ½', 'noodles', 10.754536, 106.667561, 'hu-tieu-mi-ho-ky.png', '06:00-11:30', 4.5, 62, 'Khu XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'há»§ tiáº¿u nÆ°á»›c', 'mÃ¬ trá»¥ng', 'xÃ¡ xÃ­u', 28, 'Tá»a láº¡c trong khu XÃ³m Cáº£i, Há»§ Tiáº¿u MÃ¬ Há»“ KÃ½ lÃ  quÃ¡n quen thuá»™c cá»§a ngÆ°á»i dÃ¢n yÃªu thÃ­ch mÃ³n nÆ°á»›c kiá»ƒu Hoa. QuÃ¡n cÃ³ pháº§n nÆ°á»›c dÃ¹ng thanh, topping Ä‘áº§y Ä‘áº·n vÃ  phong vá»‹ má»™c máº¡c, dá»… Äƒn.'),
('QuÃ¡n Ä‚n PhÃº KÃ½', 'noodles', 10.754517, 106.667761, 'quan-an-phu-ky.png', '06:00-12:00', 4.6, 71, '598/6 Nguyá»…n TrÃ£i, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'há»§ tiáº¿u gia Ä‘Ã¬nh', 'xÆ°Æ¡ng háº§m', 'mÃ¬ trá»©ng', 28, 'Náº±m táº¡i 598/6 Nguyá»…n TrÃ£i, QuÃ¡n Ä‚n PhÃº KÃ½ lÃ  Ä‘á»‹a chá»‰ bÃ¬nh dÃ¢n nhÆ°ng Ä‘Æ°á»£c nhiá»u ngÆ°á»i yÃªu thÃ­ch nhá» hÆ°Æ¡ng vá»‹ á»•n Ä‘á»‹nh. QuÃ¡n phá»¥c vá»¥ cÃ¡c mÃ³n mÃ¬ vÃ  há»§ tiáº¿u vá»›i pháº§n nÆ°á»›c dÃ¹ng ninh xÆ°Æ¡ng Ä‘áº­m vá»‹, thÃ­ch há»£p cho bá»¯a sÃ¡ng hoáº·c trÆ°a.'),
('MÃ¬ KhÃ´ XÃ¡ XÃ­u', 'noodles', 10.754559, 106.667771, 'mi-kho-xa-xiu.png', '06:00-12:00', 4.7, 89, 'Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam', 'mÃ¬ khÃ´ xÃ¡ xÃ­u', 'sá»§i cáº£o', 'hoÃ nh thÃ¡nh', 28, 'Náº±m trong khu Nguyá»…n TrÃ£i sÃ´i Ä‘á»™ng, MÃ¬ KhÃ´ XÃ¡ XÃ­u lÃ  quÃ¡n Äƒn háº¥p dáº«n vá»›i mÃ³n mÃ¬ khÃ´ trá»™n Ä‘áº­m vá»‹. Sá»£i mÃ¬ dai, xÃ¡ xÃ­u thÆ¡m ngá»t, Äƒn cÃ¹ng nÆ°á»›c lÃ¨o nÃ³ng vÃ  topping Ä‘áº§y Ä‘áº·n táº¡o nÃªn tráº£i nghiá»‡m ráº¥t trÃ²n vá»‹.');

INSERT INTO stalls (category_id, name, latitude, longitude, image_url, specialty_1, specialty_2, specialty_3, poi_radius_m, opening_hours, is_open, is_active, rating_avg, reviews_count, created_at, updated_at, is_deleted)
SELECT
    c.id,
    s.name,
    s.latitude,
    s.longitude,
    s.image_url,
    s.specialty_1,
    s.specialty_2,
    s.specialty_3,
    s.poi_radius_m,
    s.opening_hours,
    TRUE,
    TRUE,
    s.rating_avg,
    s.reviews_count,
    NOW(),
    NOW(),
    FALSE
FROM import_stalls s
JOIN categories c ON c.slug = s.category_slug;

INSERT INTO stall_translations (stall_id, language_id, title, description, script_text, is_auto_generated, translation_status, source_version, created_at, updated_at)
SELECT
    st.id,
    l.id,
    src.name,
    src.address,
    CASE l.code
        WHEN 'vi' THEN
            CASE src.image_url
                WHEN 'che-hoa-co-lan.png' THEN 'Náº±m táº¡i 622 Nguyá»…n TrÃ£i, ChÃ¨ Hoa CÃ´ Lan lÃ  Ä‘iá»ƒm Ä‘áº¿n lÃ½ tÆ°á»Ÿng cho ai mÃª chÃ¨ Hoa Quáº­n 5. QuÃ¡n ná»•i tiáº¿ng vá»›i chÃ¨ há»™t gÃ  trÃ , Ä‘áº­u Ä‘á» ngá»t thanh, chuáº©n vá»‹. KhÃ´ng gian bÃ¬nh dÃ¢n, má»™c máº¡c táº¡i Ä‘Ã¢y cháº¯c cháº¯n sáº½ khiáº¿n báº¡n hÃ i lÃ²ng!'
                WHEN 'hu-tieu-hu-my.png' THEN 'Tá»a láº¡c trong khu XÃ³m Cáº£i, quÃ¡n Há»§ Tiáº¿u - Há»§ Má»³ háº¥p dáº«n thá»±c khÃ¡ch vá»›i tÃ´ mÃ¬ vá»‹t quay trá»© danh. Sá»£i mÃ¬ dai ngon, vá»‹t quay Ä‘áº­m Ä‘Ã , nÆ°á»›c dÃ¹ng thanh nháº¹. ÄÃ¢y lÃ  lá»±a chá»n tuyá»‡t vá»i cho bá»¯a sÃ¡ng hoáº·c trÆ°a cá»§a báº¡n!'
                WHEN 'quan-com-phong-binh.png' THEN 'Tá»a láº¡c trong khu XÃ³m Cáº£i, QuÃ¡n CÆ¡m Phong BÃ¬nh lÃ  quÃ¡n cÆ¡m bÃ¬nh dÃ¢n Ä‘Æ°á»£c yÃªu thÃ­ch vá»›i cÃ¡c mÃ³n máº·n quen thuá»™c. QuÃ¡n gÃ¢y áº¥n tÆ°á»£ng bá»Ÿi hÆ°Æ¡ng vá»‹ á»•n Ä‘á»‹nh, kháº©u pháº§n Ä‘áº§y Ä‘áº·n vÃ  khÃ´ng khÃ­ gáº§n gÅ©i, Ä‘Ãºng cháº¥t bá»¯a cÆ¡m nhÃ .'
                WHEN 'hoa-ky.png' THEN 'áº¨n mÃ¬nh trong khu chung cÆ° cÅ© trÃªn Ä‘Æ°á»ng Nguyá»…n TrÃ£i, HoÃ  KÃ½ lÃ  quÃ¡n mÃ¬ ngÆ°á»i Hoa quen thuá»™c cá»§a nhiá»u thá»±c khÃ¡ch sÃ nh Äƒn. QuÃ¡n ná»•i báº­t vá»›i mÃ³n mÃ¬ vá»‹t quay Ä‘áº­m Ä‘Ã , sá»£i mÃ¬ dai ngon, nÆ°á»›c dÃ¹ng trong nhÆ°ng giÃ u hÆ°Æ¡ng vá»‹.'
                WHEN 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png' THEN 'Náº±m trong khu vá»±c Quáº­n 5 sáº§m uáº¥t, Minh PhÃ¡t Há»§ TÃ­u MÃ¬ lÃ  quÃ¡n Äƒn quen thuá»™c cá»§a nhá»¯ng ai yÃªu thÃ­ch mÃ³n nÆ°á»›c kiá»ƒu Hoa. QuÃ¡n ná»•i báº­t vá»›i nÆ°á»›c dÃ¹ng ngá»t thanh, topping Ä‘áº§y Ä‘áº·n vÃ  cÃ¡ch phá»¥c vá»¥ nhanh nháº¹n.'
                WHEN 'hu-tieu-mi-ba-cao.png' THEN 'Náº±m á»Ÿ lÃ´ A chung cÆ° Nguyá»…n TrÃ£i, há»§ tiáº¿u mÃ¬ BÃ  Cao lÃ  quÃ¡n Äƒn sÃ¡ng ná»•i tiáº¿ng vá»›i hÆ°Æ¡ng vá»‹ truyá»n thá»‘ng. TÃ´ há»§ tiáº¿u á»Ÿ Ä‘Ã¢y háº¥p dáº«n nhá» nÆ°á»›c dÃ¹ng trong veo, topping xÃ¡ xÃ­u Ä‘áº­m vá»‹ vÃ  sá»£i mÃ¬ vá»«a dai vá»«a thÆ¡m.'
                WHEN 'cha-cuon-ca-trich-tranh.png' THEN 'Táº¡i khu XÃ³m Cáº£i nhá»™n nhá»‹p, Cháº£ Cuá»‘n CÃ¡ TrÃ­ch "Tranh" lÃ  má»™t Ä‘á»‹a chá»‰ Ä‘áº·c sáº¯c vá»›i mÃ³n cÃ¡ trÃ­ch cuá»‘n Ä‘á»™c Ä‘Ã¡o. Má»—i pháº§n Äƒn Ä‘Æ°á»£c cuá»‘n khÃ©o lÃ©o, dáº­y mÃ¹i thÆ¡m Ä‘áº·c trÆ°ng, Äƒn kÃ¨m rau sá»‘ng tÆ°Æ¡i vÃ  nÆ°á»›c cháº¥m Ä‘áº­m Ä‘Ã .'
                WHEN 'ha-cao-phanh.png' THEN 'Náº±m trong khu ngÆ°á»i Hoa Quáº­n 5, HÃ¡ Cáº£o PhÃ¡nh lÃ  quÃ¡n nhá» ná»•i tiáº¿ng vá»›i nhá»¯ng xá»­ng hÃ¡ cáº£o nÃ³ng há»•i thÆ¡m ngon. Vá» má»ng, nhÃ¢n Ä‘áº­m vá»‹, mÃ³n Äƒn Ä‘Æ°á»£c phá»¥c vá»¥ nhanh vÃ  giá»¯ trá»n nÃ©t áº©m thá»±c truyá»n thá»‘ng.'
                WHEN 'banh-canh-013.png' THEN 'áº¨n mÃ¬nh trong khu chung cÆ° XÃ³m Cáº£i, BÃ¡nh Canh 013 lÃ  quÃ¡n Äƒn sÃ¡ng quen thuá»™c cá»§a ngÆ°á»i dÃ¢n Ä‘á»‹a phÆ°Æ¡ng. TÃ´ bÃ¡nh canh nÃ³ng há»•i vá»›i nÆ°á»›c dÃ¹ng Ä‘áº­m Ä‘Ã , sá»£i bÃ¡nh má»m dai vÃ  topping Ä‘áº§y Ä‘áº·n khiáº¿n ai thá»­ cÅ©ng dá»… nhá»›.'
                WHEN 'com-tam-bao-nhi.png' THEN 'Tá»a láº¡c trÃªn Ä‘Æ°á»ng Nguyá»…n TrÃ£i, CÆ¡m Táº¥m Báº£o Nhi lÃ  Ä‘iá»ƒm Ä‘áº¿n quen thuá»™c cho bá»¯a sÃ¡ng Ä‘áº­m cháº¥t SÃ i GÃ²n. QuÃ¡n ná»•i báº­t vá»›i miáº¿ng sÆ°á»n nÆ°á»›ng thÆ¡m lá»«ng, cÆ¡m tÆ¡i má»m vÃ  pháº§n Äƒn Ä‘áº§y Ä‘áº·n, Ä‘áº­m Ä‘Ã .'
                WHEN 'banh-cuon-phu-thanh.png' THEN 'Náº±m trÃªn Ä‘Æ°á»ng Máº¡c ThiÃªn TÃ­ch, BÃ¡nh cuá»‘n PhÃº ThÃ nh lÃ  quÃ¡n nhá» Ä‘Æ°á»£c nhiá»u ngÆ°á»i tÃ¬m Ä‘áº¿n vÃ o buá»•i sÃ¡ng. Lá»›p bÃ¡nh má»ng má»‹n, nhÃ¢n vá»«a Äƒn, Äƒn kÃ¨m cháº£ lá»¥a vÃ  nÆ°á»›c máº¯m pha hÃ i hÃ²a khiáº¿n mÃ³n Äƒn thÃªm cuá»‘n hÃºt.'
                WHEN 'hu-tieu-xao-020-lo-c.png' THEN 'Táº¡i khu lÃ´ C Nguyá»…n TrÃ£i, quÃ¡n Há»§ Tiáº¿u XÃ o 020 lÃ  Ä‘iá»ƒm háº¹n chiá»u tá»‘i cá»§a nhiá»u tÃ­n Ä‘á»“ mÃ³n xÃ o kiá»ƒu Hoa. Sá»£i há»§ tiáº¿u Ä‘Æ°á»£c xÃ o sÄƒn, thÆ¡m lá»­a, káº¿t há»£p cÃ¹ng rau vÃ  thá»‹t táº¡o nÃªn hÆ°Æ¡ng vá»‹ háº¥p dáº«n, khÃ³ quÃªn.'
                WHEN 'kho-qua-ca-ot-hing-ky.png' THEN 'Náº±m trong khu XÃ³m Cáº£i Ä‘áº­m mÃ u sáº¯c ngÆ°á»i Hoa, HÃ­ng Ky gÃ¢y áº¥n tÆ°á»£ng vá»›i mÃ³n khá»• qua cÃ  á»›t mang hÆ°Æ¡ng vá»‹ láº¡ miá»‡ng, Ä‘áº­m Ä‘Ã . MÃ³n Äƒn Ä‘Æ°á»£c cháº¿ biáº¿n cáº§u ká»³, vá»«a giá»¯ Ä‘Æ°á»£c vá»‹ thanh tá»± nhiÃªn vá»«a cÃ³ chiá»u sÃ¢u hÆ°Æ¡ng vá»‹.'
                WHEN 'hu-tieu-mi-ho-ky.png' THEN 'Tá»a láº¡c trong khu XÃ³m Cáº£i, Há»§ Tiáº¿u MÃ¬ Há»“ KÃ½ lÃ  quÃ¡n quen thuá»™c cá»§a ngÆ°á»i dÃ¢n yÃªu thÃ­ch mÃ³n nÆ°á»›c kiá»ƒu Hoa. QuÃ¡n cÃ³ pháº§n nÆ°á»›c dÃ¹ng thanh, topping Ä‘áº§y Ä‘áº·n vÃ  phong vá»‹ má»™c máº¡c, dá»… Äƒn.'
                WHEN 'quan-an-phu-ky.png' THEN 'Náº±m táº¡i 598/6 Nguyá»…n TrÃ£i, QuÃ¡n Ä‚n PhÃº KÃ½ lÃ  Ä‘á»‹a chá»‰ bÃ¬nh dÃ¢n nhÆ°ng Ä‘Æ°á»£c nhiá»u ngÆ°á»i yÃªu thÃ­ch nhá» hÆ°Æ¡ng vá»‹ á»•n Ä‘á»‹nh. QuÃ¡n phá»¥c vá»¥ cÃ¡c mÃ³n mÃ¬ vÃ  há»§ tiáº¿u vá»›i pháº§n nÆ°á»›c dÃ¹ng ninh xÆ°Æ¡ng Ä‘áº­m vá»‹, thÃ­ch há»£p cho bá»¯a sÃ¡ng hoáº·c trÆ°a.'
                WHEN 'mi-kho-xa-xiu.png' THEN 'Náº±m trong khu Nguyá»…n TrÃ£i sÃ´i Ä‘á»™ng, MÃ¬ KhÃ´ XÃ¡ XÃ­u lÃ  quÃ¡n Äƒn háº¥p dáº«n vá»›i mÃ³n mÃ¬ khÃ´ trá»™n Ä‘áº­m vá»‹. Sá»£i mÃ¬ dai, xÃ¡ xÃ­u thÆ¡m ngá»t, Äƒn cÃ¹ng nÆ°á»›c lÃ¨o nÃ³ng vÃ  topping Ä‘áº§y Ä‘áº·n táº¡o nÃªn tráº£i nghiá»‡m ráº¥t trÃ²n vá»‹.'
                ELSE src.script_vi
            END
        WHEN 'en' THEN
            CASE src.image_url
                WHEN 'che-hoa-co-lan.png' THEN 'ChÃ¨ Hoa CÃ´ Lan is a local food stop at 622 Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include chÃ¨ há»™t gÃ  trÃ , chÃ¨ Ä‘áº­u Ä‘á», and chÃ¨ mÃ¨ Ä‘en.'
                WHEN 'hu-tieu-hu-my.png' THEN 'Há»§ Tiáº¿u - Há»§ Má»³ is a local food stop at 012 LÃ´ A, C/C XÃ³m Cáº£i, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include há»§ tiáº¿u, há»§ mÃ¬, and sá»§i cáº£o.'
                WHEN 'quan-com-phong-binh.png' THEN 'QuÃ¡n CÆ¡m Phong BÃ¬nh is a local food stop at Khu XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include cÆ¡m sÆ°á»n, cÆ¡m thá»‹t kho, and canh cáº£i.'
                WHEN 'hoa-ky.png' THEN 'HoÃ  KÃ½ is a local food stop at QM38+RRV, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include mÃ¬ vá»‹t quay, mÃ¬ xÃ¡ xÃ­u, and hoÃ nh thÃ¡nh.'
                WHEN 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png' THEN 'Minh PhÃ¡t Há»§ TÃ­u MÃ¬ PhÆ°á»ng 8 Quáº­n 5 is a local food stop at An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include há»§ tiáº¿u nÆ°á»›c, mÃ¬ khÃ´, and hoÃ nh thÃ¡nh.'
                WHEN 'hu-tieu-mi-ba-cao.png' THEN 'Há»§ tiáº¿u mÃ¬ BÃ  Cao is a local food stop at Chung cÆ° Nguyá»…n TrÃ£i lÃ´ A, 004 LÃ´ A, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include há»§ tiáº¿u mÃ¬, xÃ¡ xÃ­u, and nÆ°á»›c dÃ¹ng xÆ°Æ¡ng.'
                WHEN 'cha-cuon-ca-trich-tranh.png' THEN 'Cháº£ Cuá»‘n CÃ¡ TrÃ­ch "Tranh" is a local food stop at LÃ´ C chung cÆ°, cáº§u thang/013 XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include cháº£ cuá»‘n cÃ¡ trÃ­ch, rau sá»‘ng, and nÆ°á»›c cháº¥m Ä‘áº­m Ä‘Ã .'
                WHEN 'ha-cao-phanh.png' THEN 'HÃ¡ Cáº£o PhÃ¡nh is a local food stop at Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include hÃ¡ cáº£o, xÃ­u máº¡i, and bÃ¡nh xáº¿p.'
                WHEN 'banh-canh-013.png' THEN 'BÃ¡nh Canh 013 is a local food stop at Chung cÆ°, LÃ´ C/013 XÃ³m Cáº£i, PhÆ°á»ng 9, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include bÃ¡nh canh sÆ°á»n sá»¥n, nui, and bÃºn gáº¡o.'
                WHEN 'com-tam-bao-nhi.png' THEN 'CÆ¡m Táº¥m - Báº£o Nhi is a local food stop at LÃ´ C chung cÆ°, Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include cÆ¡m táº¥m sÆ°á»n, bÃ¬ cháº£, and trá»©ng á»‘p la.'
                WHEN 'banh-cuon-phu-thanh.png' THEN 'BÃ¡nh cuá»‘n PhÃº ThÃ nh is a local food stop at 42 Máº¡c ThiÃªn TÃ­ch, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include bÃ¡nh cuá»‘n, nem cháº£, and hÃ nh phi.'
                WHEN 'hu-tieu-xao-020-lo-c.png' THEN 'Há»§ tiáº¿u xÃ o 020 lÃ´ C is a local food stop at Chung cÆ° Nguyá»…n TrÃ£i lÃ´ A, lÃ´ C/42 Máº¡c ThiÃªn TÃ­ch, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include há»§ tiáº¿u xÃ o, bÃºn Singapore, and cÆ¡m chiÃªn.'
                WHEN 'kho-qua-ca-ot-hing-ky.png' THEN 'Khá»• Qua CÃ  á»št HÃ­ng Ky is a local food stop at Chung cÆ° XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include khá»• qua dá»“n cháº£ cÃ¡, cÃ  tÃ­m dá»“n, and nÆ°á»›c lÃ¨o sa táº¿.'
                WHEN 'hu-tieu-mi-ho-ky.png' THEN 'Há»§ Tiáº¿u MÃ¬ Há»“ KÃ½ is a local food stop at Khu XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include há»§ tiáº¿u nÆ°á»›c, mÃ¬ trá»¥ng, and xÃ¡ xÃ­u.'
                WHEN 'quan-an-phu-ky.png' THEN 'QuÃ¡n Ä‚n PhÃº KÃ½ is a local food stop at 598/6 Nguyá»…n TrÃ£i, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include há»§ tiáº¿u gia Ä‘Ã¬nh, xÆ°Æ¡ng háº§m, and mÃ¬ trá»©ng.'
                WHEN 'mi-kho-xa-xiu.png' THEN 'MÃ¬ KhÃ´ XÃ¡ XÃ­u is a local food stop at Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam. Signature dishes include mÃ¬ khÃ´ xÃ¡ xÃ­u, sá»§i cáº£o, and hoÃ nh thÃ¡nh.'
                ELSE src.script_vi
            END
        WHEN 'zh-CN' THEN
            CASE src.image_url
                WHEN 'che-hoa-co-lan.png' THEN 'ChÃ¨ Hoa CÃ´ Lan ä½äºŽ622 Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬chÃ¨ há»™t gÃ  trÃ ã€chÃ¨ Ä‘áº­u Ä‘á»å’ŒchÃ¨ mÃ¨ Ä‘enã€‚'
                WHEN 'hu-tieu-hu-my.png' THEN 'Há»§ Tiáº¿u - Há»§ Má»³ ä½äºŽ012 LÃ´ A, C/C XÃ³m Cáº£i, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬há»§ tiáº¿uã€há»§ mÃ¬å’Œsá»§i cáº£oã€‚'
                WHEN 'quan-com-phong-binh.png' THEN 'QuÃ¡n CÆ¡m Phong BÃ¬nh ä½äºŽKhu XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬cÆ¡m sÆ°á»nã€cÆ¡m thá»‹t khoå’Œcanh cáº£iã€‚'
                WHEN 'hoa-ky.png' THEN 'HoÃ  KÃ½ ä½äºŽQM38+RRV, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬mÃ¬ vá»‹t quayã€mÃ¬ xÃ¡ xÃ­uå’ŒhoÃ nh thÃ¡nhã€‚'
                WHEN 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png' THEN 'Minh PhÃ¡t Há»§ TÃ­u MÃ¬ PhÆ°á»ng 8 Quáº­n 5 ä½äºŽAn ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬há»§ tiáº¿u nÆ°á»›cã€mÃ¬ khÃ´å’ŒhoÃ nh thÃ¡nhã€‚'
                WHEN 'hu-tieu-mi-ba-cao.png' THEN 'Há»§ tiáº¿u mÃ¬ BÃ  Cao ä½äºŽChung cÆ° Nguyá»…n TrÃ£i lÃ´ A, 004 LÃ´ A, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬há»§ tiáº¿u mÃ¬ã€xÃ¡ xÃ­uå’ŒnÆ°á»›c dÃ¹ng xÆ°Æ¡ngã€‚'
                WHEN 'cha-cuon-ca-trich-tranh.png' THEN 'Cháº£ Cuá»‘n CÃ¡ TrÃ­ch "Tranh" ä½äºŽLÃ´ C chung cÆ°, cáº§u thang/013 XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬cháº£ cuá»‘n cÃ¡ trÃ­chã€rau sá»‘ngå’ŒnÆ°á»›c cháº¥m Ä‘áº­m Ä‘Ã ã€‚'
                WHEN 'ha-cao-phanh.png' THEN 'HÃ¡ Cáº£o PhÃ¡nh ä½äºŽNguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬hÃ¡ cáº£oã€xÃ­u máº¡iå’ŒbÃ¡nh xáº¿pã€‚'
                WHEN 'banh-canh-013.png' THEN 'BÃ¡nh Canh 013 ä½äºŽChung cÆ°, LÃ´ C/013 XÃ³m Cáº£i, PhÆ°á»ng 9, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬bÃ¡nh canh sÆ°á»n sá»¥nã€nuiå’ŒbÃºn gáº¡oã€‚'
                WHEN 'com-tam-bao-nhi.png' THEN 'CÆ¡m Táº¥m - Báº£o Nhi ä½äºŽLÃ´ C chung cÆ°, Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬cÆ¡m táº¥m sÆ°á»nã€bÃ¬ cháº£å’Œtrá»©ng á»‘p laã€‚'
                WHEN 'banh-cuon-phu-thanh.png' THEN 'BÃ¡nh cuá»‘n PhÃº ThÃ nh ä½äºŽ42 Máº¡c ThiÃªn TÃ­ch, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬bÃ¡nh cuá»‘nã€nem cháº£å’ŒhÃ nh phiã€‚'
                WHEN 'hu-tieu-xao-020-lo-c.png' THEN 'Há»§ tiáº¿u xÃ o 020 lÃ´ C ä½äºŽChung cÆ° Nguyá»…n TrÃ£i lÃ´ A, lÃ´ C/42 Máº¡c ThiÃªn TÃ­ch, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬há»§ tiáº¿u xÃ oã€bÃºn Singaporeå’ŒcÆ¡m chiÃªnã€‚'
                WHEN 'kho-qua-ca-ot-hing-ky.png' THEN 'Khá»• Qua CÃ  á»št HÃ­ng Ky ä½äºŽChung cÆ° XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬khá»• qua dá»“n cháº£ cÃ¡ã€cÃ  tÃ­m dá»“nå’ŒnÆ°á»›c lÃ¨o sa táº¿ã€‚'
                WHEN 'hu-tieu-mi-ho-ky.png' THEN 'Há»§ Tiáº¿u MÃ¬ Há»“ KÃ½ ä½äºŽKhu XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬há»§ tiáº¿u nÆ°á»›cã€mÃ¬ trá»¥ngå’ŒxÃ¡ xÃ­uã€‚'
                WHEN 'quan-an-phu-ky.png' THEN 'QuÃ¡n Ä‚n PhÃº KÃ½ ä½äºŽ598/6 Nguyá»…n TrÃ£i, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬há»§ tiáº¿u gia Ä‘Ã¬nhã€xÆ°Æ¡ng háº§må’ŒmÃ¬ trá»©ngã€‚'
                WHEN 'mi-kho-xa-xiu.png' THEN 'MÃ¬ KhÃ´ XÃ¡ XÃ­u ä½äºŽNguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Namï¼Œæ‹›ç‰ŒèœåŒ…æ‹¬mÃ¬ khÃ´ xÃ¡ xÃ­uã€sá»§i cáº£oå’ŒhoÃ nh thÃ¡nhã€‚'
                ELSE src.script_vi
            END
        WHEN 'ja' THEN
            CASE src.image_url
                WHEN 'che-hoa-co-lan.png' THEN 'ChÃ¨ Hoa CÃ´ Lan ã¯ 622 Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ chÃ¨ há»™t gÃ  trÃ ã€chÃ¨ Ä‘áº­u Ä‘á»ã€chÃ¨ mÃ¨ Ä‘en ã§ã™ã€‚'
                WHEN 'hu-tieu-hu-my.png' THEN 'Há»§ Tiáº¿u - Há»§ Má»³ ã¯ 012 LÃ´ A, C/C XÃ³m Cáº£i, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ há»§ tiáº¿uã€há»§ mÃ¬ã€sá»§i cáº£o ã§ã™ã€‚'
                WHEN 'quan-com-phong-binh.png' THEN 'QuÃ¡n CÆ¡m Phong BÃ¬nh ã¯ Khu XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ cÆ¡m sÆ°á»nã€cÆ¡m thá»‹t khoã€canh cáº£i ã§ã™ã€‚'
                WHEN 'hoa-ky.png' THEN 'HoÃ  KÃ½ ã¯ QM38+RRV, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ mÃ¬ vá»‹t quayã€mÃ¬ xÃ¡ xÃ­uã€hoÃ nh thÃ¡nh ã§ã™ã€‚'
                WHEN 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png' THEN 'Minh PhÃ¡t Há»§ TÃ­u MÃ¬ PhÆ°á»ng 8 Quáº­n 5 ã¯ An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ há»§ tiáº¿u nÆ°á»›cã€mÃ¬ khÃ´ã€hoÃ nh thÃ¡nh ã§ã™ã€‚'
                WHEN 'hu-tieu-mi-ba-cao.png' THEN 'Há»§ tiáº¿u mÃ¬ BÃ  Cao ã¯ Chung cÆ° Nguyá»…n TrÃ£i lÃ´ A, 004 LÃ´ A, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ há»§ tiáº¿u mÃ¬ã€xÃ¡ xÃ­uã€nÆ°á»›c dÃ¹ng xÆ°Æ¡ng ã§ã™ã€‚'
                WHEN 'cha-cuon-ca-trich-tranh.png' THEN 'Cháº£ Cuá»‘n CÃ¡ TrÃ­ch "Tranh" ã¯ LÃ´ C chung cÆ°, cáº§u thang/013 XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ cháº£ cuá»‘n cÃ¡ trÃ­chã€rau sá»‘ngã€nÆ°á»›c cháº¥m Ä‘áº­m Ä‘Ã  ã§ã™ã€‚'
                WHEN 'ha-cao-phanh.png' THEN 'HÃ¡ Cáº£o PhÃ¡nh ã¯ Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ hÃ¡ cáº£oã€xÃ­u máº¡iã€bÃ¡nh xáº¿p ã§ã™ã€‚'
                WHEN 'banh-canh-013.png' THEN 'BÃ¡nh Canh 013 ã¯ Chung cÆ°, LÃ´ C/013 XÃ³m Cáº£i, PhÆ°á»ng 9, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ bÃ¡nh canh sÆ°á»n sá»¥nã€nuiã€bÃºn gáº¡o ã§ã™ã€‚'
                WHEN 'com-tam-bao-nhi.png' THEN 'CÆ¡m Táº¥m - Báº£o Nhi ã¯ LÃ´ C chung cÆ°, Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ cÆ¡m táº¥m sÆ°á»nã€bÃ¬ cháº£ã€trá»©ng á»‘p la ã§ã™ã€‚'
                WHEN 'banh-cuon-phu-thanh.png' THEN 'BÃ¡nh cuá»‘n PhÃº ThÃ nh ã¯ 42 Máº¡c ThiÃªn TÃ­ch, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ bÃ¡nh cuá»‘nã€nem cháº£ã€hÃ nh phi ã§ã™ã€‚'
                WHEN 'hu-tieu-xao-020-lo-c.png' THEN 'Há»§ tiáº¿u xÃ o 020 lÃ´ C ã¯ Chung cÆ° Nguyá»…n TrÃ£i lÃ´ A, lÃ´ C/42 Máº¡c ThiÃªn TÃ­ch, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ há»§ tiáº¿u xÃ oã€bÃºn Singaporeã€cÆ¡m chiÃªn ã§ã™ã€‚'
                WHEN 'kho-qua-ca-ot-hing-ky.png' THEN 'Khá»• Qua CÃ  á»št HÃ­ng Ky ã¯ Chung cÆ° XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ khá»• qua dá»“n cháº£ cÃ¡ã€cÃ  tÃ­m dá»“nã€nÆ°á»›c lÃ¨o sa táº¿ ã§ã™ã€‚'
                WHEN 'hu-tieu-mi-ho-ky.png' THEN 'Há»§ Tiáº¿u MÃ¬ Há»“ KÃ½ ã¯ Khu XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ há»§ tiáº¿u nÆ°á»›cã€mÃ¬ trá»¥ngã€xÃ¡ xÃ­u ã§ã™ã€‚'
                WHEN 'quan-an-phu-ky.png' THEN 'QuÃ¡n Ä‚n PhÃº KÃ½ ã¯ 598/6 Nguyá»…n TrÃ£i, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ há»§ tiáº¿u gia Ä‘Ã¬nhã€xÆ°Æ¡ng háº§mã€mÃ¬ trá»©ng ã§ã™ã€‚'
                WHEN 'mi-kho-xa-xiu.png' THEN 'MÃ¬ KhÃ´ XÃ¡ XÃ­u ã¯ Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ã«ã‚ã‚‹äººæ°—åº—ã§ã™ã€‚ãŠã™ã™ã‚ã¯ mÃ¬ khÃ´ xÃ¡ xÃ­uã€sá»§i cáº£oã€hoÃ nh thÃ¡nh ã§ã™ã€‚'
                ELSE src.script_vi
            END
        WHEN 'ko' THEN
            CASE src.image_url
                WHEN 'che-hoa-co-lan.png' THEN 'ChÃ¨ Hoa CÃ´ Lan ëŠ” 622 Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” chÃ¨ há»™t gÃ  trÃ , chÃ¨ Ä‘áº­u Ä‘á», chÃ¨ mÃ¨ Ä‘en ìž…ë‹ˆë‹¤.'
                WHEN 'hu-tieu-hu-my.png' THEN 'Há»§ Tiáº¿u - Há»§ Má»³ ëŠ” 012 LÃ´ A, C/C XÃ³m Cáº£i, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” há»§ tiáº¿u, há»§ mÃ¬, sá»§i cáº£o ìž…ë‹ˆë‹¤.'
                WHEN 'quan-com-phong-binh.png' THEN 'QuÃ¡n CÆ¡m Phong BÃ¬nh ëŠ” Khu XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” cÆ¡m sÆ°á»n, cÆ¡m thá»‹t kho, canh cáº£i ìž…ë‹ˆë‹¤.'
                WHEN 'hoa-ky.png' THEN 'HoÃ  KÃ½ ëŠ” QM38+RRV, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” mÃ¬ vá»‹t quay, mÃ¬ xÃ¡ xÃ­u, hoÃ nh thÃ¡nh ìž…ë‹ˆë‹¤.'
                WHEN 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png' THEN 'Minh PhÃ¡t Há»§ TÃ­u MÃ¬ PhÆ°á»ng 8 Quáº­n 5 ëŠ” An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” há»§ tiáº¿u nÆ°á»›c, mÃ¬ khÃ´, hoÃ nh thÃ¡nh ìž…ë‹ˆë‹¤.'
                WHEN 'hu-tieu-mi-ba-cao.png' THEN 'Há»§ tiáº¿u mÃ¬ BÃ  Cao ëŠ” Chung cÆ° Nguyá»…n TrÃ£i lÃ´ A, 004 LÃ´ A, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” há»§ tiáº¿u mÃ¬, xÃ¡ xÃ­u, nÆ°á»›c dÃ¹ng xÆ°Æ¡ng ìž…ë‹ˆë‹¤.'
                WHEN 'cha-cuon-ca-trich-tranh.png' THEN 'Cháº£ Cuá»‘n CÃ¡ TrÃ­ch "Tranh" ëŠ” LÃ´ C chung cÆ°, cáº§u thang/013 XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” cháº£ cuá»‘n cÃ¡ trÃ­ch, rau sá»‘ng, nÆ°á»›c cháº¥m Ä‘áº­m Ä‘Ã  ìž…ë‹ˆë‹¤.'
                WHEN 'ha-cao-phanh.png' THEN 'HÃ¡ Cáº£o PhÃ¡nh ëŠ” Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” hÃ¡ cáº£o, xÃ­u máº¡i, bÃ¡nh xáº¿p ìž…ë‹ˆë‹¤.'
                WHEN 'banh-canh-013.png' THEN 'BÃ¡nh Canh 013 ëŠ” Chung cÆ°, LÃ´ C/013 XÃ³m Cáº£i, PhÆ°á»ng 9, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” bÃ¡nh canh sÆ°á»n sá»¥n, nui, bÃºn gáº¡o ìž…ë‹ˆë‹¤.'
                WHEN 'com-tam-bao-nhi.png' THEN 'CÆ¡m Táº¥m - Báº£o Nhi ëŠ” LÃ´ C chung cÆ°, Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” cÆ¡m táº¥m sÆ°á»n, bÃ¬ cháº£, trá»©ng á»‘p la ìž…ë‹ˆë‹¤.'
                WHEN 'banh-cuon-phu-thanh.png' THEN 'BÃ¡nh cuá»‘n PhÃº ThÃ nh ëŠ” 42 Máº¡c ThiÃªn TÃ­ch, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” bÃ¡nh cuá»‘n, nem cháº£, hÃ nh phi ìž…ë‹ˆë‹¤.'
                WHEN 'hu-tieu-xao-020-lo-c.png' THEN 'Há»§ tiáº¿u xÃ o 020 lÃ´ C ëŠ” Chung cÆ° Nguyá»…n TrÃ£i lÃ´ A, lÃ´ C/42 Máº¡c ThiÃªn TÃ­ch, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” há»§ tiáº¿u xÃ o, bÃºn Singapore, cÆ¡m chiÃªn ìž…ë‹ˆë‹¤.'
                WHEN 'kho-qua-ca-ot-hing-ky.png' THEN 'Khá»• Qua CÃ  á»št HÃ­ng Ky ëŠ” Chung cÆ° XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” khá»• qua dá»“n cháº£ cÃ¡, cÃ  tÃ­m dá»“n, nÆ°á»›c lÃ¨o sa táº¿ ìž…ë‹ˆë‹¤.'
                WHEN 'hu-tieu-mi-ho-ky.png' THEN 'Há»§ Tiáº¿u MÃ¬ Há»“ KÃ½ ëŠ” Khu XÃ³m Cáº£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” há»§ tiáº¿u nÆ°á»›c, mÃ¬ trá»¥ng, xÃ¡ xÃ­u ìž…ë‹ˆë‹¤.'
                WHEN 'quan-an-phu-ky.png' THEN 'QuÃ¡n Ä‚n PhÃº KÃ½ ëŠ” 598/6 Nguyá»…n TrÃ£i, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” há»§ tiáº¿u gia Ä‘Ã¬nh, xÆ°Æ¡ng háº§m, mÃ¬ trá»©ng ìž…ë‹ˆë‹¤.'
                WHEN 'mi-kho-xa-xiu.png' THEN 'MÃ¬ KhÃ´ XÃ¡ XÃ­u ëŠ” Nguyá»…n TrÃ£i, PhÆ°á»ng 7, An ÄÃ´ng, Há»“ ChÃ­ Minh, Viá»‡t Nam ì— ìžˆëŠ” í˜„ì§€ ë§›ì§‘ìž…ë‹ˆë‹¤. ëŒ€í‘œ ë©”ë‰´ëŠ” mÃ¬ khÃ´ xÃ¡ xÃ­u, sá»§i cáº£o, hoÃ nh thÃ¡nh ìž…ë‹ˆë‹¤.'
                ELSE src.script_vi
            END
        ELSE src.script_vi
    END,
    CASE WHEN l.code = 'vi' THEN FALSE ELSE TRUE END,
    'approved',
    1,
    NOW(),
    NOW()
FROM import_stalls src
JOIN stalls st ON st.image_url = src.image_url
JOIN languages l ON l.code IN ('vi', 'en', 'zh-CN', 'ja', 'ko');

DROP TABLE import_stalls;

COMMIT;


BEGIN;

INSERT INTO users (
    role_id,
    username,
    password_hash,
    full_name,
    email,
    is_active,
    created_at,
    updated_at
)
SELECT r.id, 'admin', 'pbkdf2_sha256$390000$SHGU1N51AvWzZqNDolhXeA$JKlMGBHNFqXIXeM2SJU08lbOJneu_JUwqq9tg-K_aRs',
       'Qu?n tr? h? th?ng', 'admin@streetfeast.local', TRUE, NOW(), NOW()
FROM roles r
WHERE r.name = 'super_admin'
ON CONFLICT (username) DO NOTHING;

COMMIT;

