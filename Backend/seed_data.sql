BEGIN;

ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_1 TEXT;
ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_2 TEXT;
ALTER TABLE stalls ADD COLUMN IF NOT EXISTS specialty_3 TEXT;
ALTER TABLE stalls ADD COLUMN IF NOT EXISTS poi_radius_m DOUBLE PRECISION DEFAULT 30;

INSERT INTO languages (code, name, native_name, locale_code, sort_order) VALUES
('vi', 'Vietnamese', 'Tiếng Việt', 'vi-VN', 1),
('en', 'English', 'English', 'en-US', 2),
('zh-CN', 'Chinese', '中文', 'zh-CN', 3),
('ja', 'Japanese', '日本語', 'ja-JP', 4),
('ko', 'Korean', '한국어', 'ko-KR', 5)
ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    native_name = EXCLUDED.native_name,
    locale_code = EXCLUDED.locale_code,
    sort_order = EXCLUDED.sort_order,
    updated_at = NOW();

INSERT INTO categories (slug, name, icon_url) VALUES
('seafood', 'Hải sản', NULL),
('grilled', 'Đồ nướng', NULL),
('noodles', 'Món nước', NULL),
('snacks', 'Ăn vặt', NULL),
('desserts', 'Tráng miệng', NULL),
('rice', 'Cơm', NULL),
('dumplings', 'Há cảo', NULL),
('specialties', 'Đặc sản', NULL)
ON CONFLICT (slug) DO UPDATE SET
    name = EXCLUDED.name,
    updated_at = NOW();

DELETE FROM categories WHERE slug LIKE 'cat-%';

TRUNCATE TABLE stalls RESTART IDENTITY CASCADE;

CREATE TEMP TABLE import_stalls (name VARCHAR(200) NOT NULL, category_slug VARCHAR(50) NOT NULL, latitude DOUBLE PRECISION NOT NULL, longitude DOUBLE PRECISION NOT NULL, image_url TEXT, opening_hours VARCHAR(255), rating_avg NUMERIC(2,1) NOT NULL, reviews_count INTEGER NOT NULL, address TEXT, specialty_1 TEXT, specialty_2 TEXT, specialty_3 TEXT, poi_radius_m DOUBLE PRECISION NOT NULL DEFAULT 30, script_vi TEXT NOT NULL);

INSERT INTO import_stalls (name, category_slug, latitude, longitude, image_url, opening_hours, rating_avg, reviews_count, address, specialty_1, specialty_2, specialty_3, poi_radius_m, script_vi) VALUES
('Chè Hoa Cô Lan', 'desserts', 10.754671, 106.667296, 'che-hoa-co-lan.png', '09:00-22:00', 4.8, 124, '622 Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'chè hột gà trà', 'chè đậu đỏ', 'chè mè đen', 28, 'Nằm tại 622 Nguyễn Trãi, Chè Hoa Cô Lan là điểm đến lý tưởng cho ai mê chè Hoa Quận 5. Quán nổi tiếng với chè hột gà trà, đậu đỏ ngọt thanh, chuẩn vị. Không gian bình dân, mộc mạc tại đây chắc chắn sẽ khiến bạn hài lòng!'),
('Hủ Tiếu - Hủ Mỳ', 'noodles', 10.754630, 106.667263, 'hu-tieu-hu-my.png', '06:30-11:30', 4.7, 108, '012 Lô A, C/C Xóm Cải, An Đông, Hồ Chí Minh, Việt Nam', 'hủ tiếu', 'hủ mì', 'sủi cảo', 28, 'Tọa lạc trong khu Xóm Cải, quán Hủ Tiếu - Hủ Mỳ hấp dẫn thực khách với tô mì vịt quay trứ danh. Sợi mì dai ngon, vịt quay đậm đà, nước dùng thanh nhẹ. Đây là lựa chọn tuyệt vời cho bữa sáng hoặc trưa của bạn!'),
('Quán Cơm Phong Bình', 'rice', 10.754666, 106.667300, 'quan-com-phong-binh.png', '10:30-14:00', 4.5, 67, 'Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'cơm sườn', 'cơm thịt kho', 'canh cải', 26, 'Tọa lạc trong khu Xóm Cải, Quán Cơm Phong Bình là quán cơm bình dân được yêu thích với các món mặn quen thuộc. Quán gây ấn tượng bởi hương vị ổn định, khẩu phần đầy đặn và không khí gần gũi, đúng chất bữa cơm nhà.'),
('Hoà Ký', 'noodles', 10.754628, 106.667215, 'hoa-ky.png', '06:30-13:30', 4.8, 136, 'QM38+RRV, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'mì vịt quay', 'mì xá xíu', 'hoành thánh', 30, 'Ẩn mình trong khu chung cư cũ trên đường Nguyễn Trãi, Hoà Ký là quán mì người Hoa quen thuộc của nhiều thực khách sành ăn. Quán nổi bật với món mì vịt quay đậm đà, sợi mì dai ngon, nước dùng trong nhưng giàu hương vị.'),
('Minh Phát Hủ Tíu Mì Phường 8 Quận 5', 'noodles', 10.754615, 106.667104, 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png', '06:30-12:00', 4.6, 73, 'An Đông, Hồ Chí Minh, Việt Nam', 'hủ tiếu nước', 'mì khô', 'hoành thánh', 30, 'Nằm trong khu vực Quận 5 sầm uất, Minh Phát Hủ Tíu Mì là quán ăn quen thuộc của những ai yêu thích món nước kiểu Hoa. Quán nổi bật với nước dùng ngọt thanh, topping đầy đặn và cách phục vụ nhanh nhẹn.'),
('Hủ tiếu mì Bà Cao', 'noodles', 10.754606, 106.666961, 'hu-tieu-mi-ba-cao.png', '06:00-12:30', 4.7, 96, 'Chung cư Nguyễn Trãi lô A, 004 Lô A, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'hủ tiếu mì', 'xá xíu', 'nước dùng xương', 30, 'Nằm ở lô A chung cư Nguyễn Trãi, hủ tiếu mì Bà Cao là quán ăn sáng nổi tiếng với hương vị truyền thống. Tô hủ tiếu ở đây hấp dẫn nhờ nước dùng trong veo, topping xá xíu đậm vị và sợi mì vừa dai vừa thơm.'),
('Chả Cuốn Cá Trích "Tranh"', 'specialties', 10.754873, 106.667255, 'cha-cuon-ca-trich-tranh.png', '11:00-20:00', 4.6, 58, 'Lô C chung cư, cầu thang/013 Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'chả cuốn cá trích', 'rau sống', 'nước chấm đậm đà', 24, 'Tại khu Xóm Cải nhộn nhịp, Chả Cuốn Cá Trích "Tranh" là một địa chỉ đặc sắc với món cá trích cuốn độc đáo. Mỗi phần ăn được cuốn khéo léo, dậy mùi thơm đặc trưng, ăn kèm rau sống tươi và nước chấm đậm đà.'),
('Há Cảo Phánh', 'dumplings', 10.754861, 106.667172, 'ha-cao-phanh.png', '14:00-20:30', 4.6, 82, 'Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'há cảo', 'xíu mại', 'bánh xếp', 24, 'Nằm trong khu người Hoa Quận 5, Há Cảo Phánh là quán nhỏ nổi tiếng với những xửng há cảo nóng hổi thơm ngon. Vỏ mỏng, nhân đậm vị, món ăn được phục vụ nhanh và giữ trọn nét ẩm thực truyền thống.'),
('Bánh Canh 013', 'noodles', 10.754996, 106.667272, 'banh-canh-013.png', '06:30-13:00', 4.5, 64, 'Chung cư, Lô C/013 Xóm Cải, Phường 9, An Đông, Hồ Chí Minh, Việt Nam', 'bánh canh sườn sụn', 'nui', 'bún gạo', 26, 'Ẩn mình trong khu chung cư Xóm Cải, Bánh Canh 013 là quán ăn sáng quen thuộc của người dân địa phương. Tô bánh canh nóng hổi với nước dùng đậm đà, sợi bánh mềm dai và topping đầy đặn khiến ai thử cũng dễ nhớ.'),
('Cơm Tấm - Bảo Nhi', 'rice', 10.755039, 106.667484, 'com-tam-bao-nhi.png', '06:00-10:30', 4.7, 91, 'Lô C chung cư, Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'cơm tấm sườn', 'bì chả', 'trứng ốp la', 28, 'Tọa lạc trên đường Nguyễn Trãi, Cơm Tấm Bảo Nhi là điểm đến quen thuộc cho bữa sáng đậm chất Sài Gòn. Quán nổi bật với miếng sườn nướng thơm lừng, cơm tơi mềm và phần ăn đầy đặn, đậm đà.'),
('Bánh cuốn Phú Thành', 'snacks', 10.755199, 106.667475, 'banh-cuon-phu-thanh.png', '06:00-11:00', 4.6, 77, '42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'bánh cuốn', 'nem chả', 'hành phi', 24, 'Nằm trên đường Mạc Thiên Tích, Bánh cuốn Phú Thành là quán nhỏ được nhiều người tìm đến vào buổi sáng. Lớp bánh mỏng mịn, nhân vừa ăn, ăn kèm chả lụa và nước mắm pha hài hòa khiến món ăn thêm cuốn hút.'),
('Hủ tiếu xào 020 lô C', 'noodles', 10.755281, 106.667491, 'hu-tieu-xao-020-lo-c.png', '16:00-22:00', 4.6, 69, 'Chung cư Nguyễn Trãi lô A, lô C/42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'hủ tiếu xào', 'bún Singapore', 'cơm chiên', 24, 'Tại khu lô C Nguyễn Trãi, quán Hủ Tiếu Xào 020 là điểm hẹn chiều tối của nhiều tín đồ món xào kiểu Hoa. Sợi hủ tiếu được xào săn, thơm lửa, kết hợp cùng rau và thịt tạo nên hương vị hấp dẫn, khó quên.'),
('Khổ Qua Cà Ớt Híng Ky', 'specialties', 10.754876, 106.667537, 'kho-qua-ca-ot-hing-ky.png', '15:00-21:00', 4.7, 84, 'Chung cư Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'khổ qua dồn chả cá', 'cà tím dồn', 'nước lèo sa tế', 24, 'Nằm trong khu Xóm Cải đậm màu sắc người Hoa, Híng Ky gây ấn tượng với món khổ qua cà ớt mang hương vị lạ miệng, đậm đà. Món ăn được chế biến cầu kỳ, vừa giữ được vị thanh tự nhiên vừa có chiều sâu hương vị.'),
('Hủ Tiếu Mì Hồ Ký', 'noodles', 10.754536, 106.667561, 'hu-tieu-mi-ho-ky.png', '06:00-11:30', 4.5, 62, 'Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'hủ tiếu nước', 'mì trụng', 'xá xíu', 28, 'Tọa lạc trong khu Xóm Cải, Hủ Tiếu Mì Hồ Ký là quán quen thuộc của người dân yêu thích món nước kiểu Hoa. Quán có phần nước dùng thanh, topping đầy đặn và phong vị mộc mạc, dễ ăn.'),
('Quán Ăn Phú Ký', 'noodles', 10.754517, 106.667761, 'quan-an-phu-ky.png', '06:00-12:00', 4.6, 71, '598/6 Nguyễn Trãi, An Đông, Hồ Chí Minh, Việt Nam', 'hủ tiếu gia đình', 'xương hầm', 'mì trứng', 28, 'Nằm tại 598/6 Nguyễn Trãi, Quán Ăn Phú Ký là địa chỉ bình dân nhưng được nhiều người yêu thích nhờ hương vị ổn định. Quán phục vụ các món mì và hủ tiếu với phần nước dùng ninh xương đậm vị, thích hợp cho bữa sáng hoặc trưa.'),
('Mì Khô Xá Xíu', 'noodles', 10.754559, 106.667771, 'mi-kho-xa-xiu.png', '06:00-12:00', 4.7, 89, 'Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam', 'mì khô xá xíu', 'sủi cảo', 'hoành thánh', 28, 'Nằm trong khu Nguyễn Trãi sôi động, Mì Khô Xá Xíu là quán ăn hấp dẫn với món mì khô trộn đậm vị. Sợi mì dai, xá xíu thơm ngọt, ăn cùng nước lèo nóng và topping đầy đặn tạo nên trải nghiệm rất tròn vị.');

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
                WHEN 'che-hoa-co-lan.png' THEN 'Nằm tại 622 Nguyễn Trãi, Chè Hoa Cô Lan là điểm đến lý tưởng cho ai mê chè Hoa Quận 5. Quán nổi tiếng với chè hột gà trà, đậu đỏ ngọt thanh, chuẩn vị. Không gian bình dân, mộc mạc tại đây chắc chắn sẽ khiến bạn hài lòng!'
                WHEN 'hu-tieu-hu-my.png' THEN 'Tọa lạc trong khu Xóm Cải, quán Hủ Tiếu - Hủ Mỳ hấp dẫn thực khách với tô mì vịt quay trứ danh. Sợi mì dai ngon, vịt quay đậm đà, nước dùng thanh nhẹ. Đây là lựa chọn tuyệt vời cho bữa sáng hoặc trưa của bạn!'
                WHEN 'quan-com-phong-binh.png' THEN 'Tọa lạc trong khu Xóm Cải, Quán Cơm Phong Bình là quán cơm bình dân được yêu thích với các món mặn quen thuộc. Quán gây ấn tượng bởi hương vị ổn định, khẩu phần đầy đặn và không khí gần gũi, đúng chất bữa cơm nhà.'
                WHEN 'hoa-ky.png' THEN 'Ẩn mình trong khu chung cư cũ trên đường Nguyễn Trãi, Hoà Ký là quán mì người Hoa quen thuộc của nhiều thực khách sành ăn. Quán nổi bật với món mì vịt quay đậm đà, sợi mì dai ngon, nước dùng trong nhưng giàu hương vị.'
                WHEN 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png' THEN 'Nằm trong khu vực Quận 5 sầm uất, Minh Phát Hủ Tíu Mì là quán ăn quen thuộc của những ai yêu thích món nước kiểu Hoa. Quán nổi bật với nước dùng ngọt thanh, topping đầy đặn và cách phục vụ nhanh nhẹn.'
                WHEN 'hu-tieu-mi-ba-cao.png' THEN 'Nằm ở lô A chung cư Nguyễn Trãi, hủ tiếu mì Bà Cao là quán ăn sáng nổi tiếng với hương vị truyền thống. Tô hủ tiếu ở đây hấp dẫn nhờ nước dùng trong veo, topping xá xíu đậm vị và sợi mì vừa dai vừa thơm.'
                WHEN 'cha-cuon-ca-trich-tranh.png' THEN 'Tại khu Xóm Cải nhộn nhịp, Chả Cuốn Cá Trích "Tranh" là một địa chỉ đặc sắc với món cá trích cuốn độc đáo. Mỗi phần ăn được cuốn khéo léo, dậy mùi thơm đặc trưng, ăn kèm rau sống tươi và nước chấm đậm đà.'
                WHEN 'ha-cao-phanh.png' THEN 'Nằm trong khu người Hoa Quận 5, Há Cảo Phánh là quán nhỏ nổi tiếng với những xửng há cảo nóng hổi thơm ngon. Vỏ mỏng, nhân đậm vị, món ăn được phục vụ nhanh và giữ trọn nét ẩm thực truyền thống.'
                WHEN 'banh-canh-013.png' THEN 'Ẩn mình trong khu chung cư Xóm Cải, Bánh Canh 013 là quán ăn sáng quen thuộc của người dân địa phương. Tô bánh canh nóng hổi với nước dùng đậm đà, sợi bánh mềm dai và topping đầy đặn khiến ai thử cũng dễ nhớ.'
                WHEN 'com-tam-bao-nhi.png' THEN 'Tọa lạc trên đường Nguyễn Trãi, Cơm Tấm Bảo Nhi là điểm đến quen thuộc cho bữa sáng đậm chất Sài Gòn. Quán nổi bật với miếng sườn nướng thơm lừng, cơm tơi mềm và phần ăn đầy đặn, đậm đà.'
                WHEN 'banh-cuon-phu-thanh.png' THEN 'Nằm trên đường Mạc Thiên Tích, Bánh cuốn Phú Thành là quán nhỏ được nhiều người tìm đến vào buổi sáng. Lớp bánh mỏng mịn, nhân vừa ăn, ăn kèm chả lụa và nước mắm pha hài hòa khiến món ăn thêm cuốn hút.'
                WHEN 'hu-tieu-xao-020-lo-c.png' THEN 'Tại khu lô C Nguyễn Trãi, quán Hủ Tiếu Xào 020 là điểm hẹn chiều tối của nhiều tín đồ món xào kiểu Hoa. Sợi hủ tiếu được xào săn, thơm lửa, kết hợp cùng rau và thịt tạo nên hương vị hấp dẫn, khó quên.'
                WHEN 'kho-qua-ca-ot-hing-ky.png' THEN 'Nằm trong khu Xóm Cải đậm màu sắc người Hoa, Híng Ky gây ấn tượng với món khổ qua cà ớt mang hương vị lạ miệng, đậm đà. Món ăn được chế biến cầu kỳ, vừa giữ được vị thanh tự nhiên vừa có chiều sâu hương vị.'
                WHEN 'hu-tieu-mi-ho-ky.png' THEN 'Tọa lạc trong khu Xóm Cải, Hủ Tiếu Mì Hồ Ký là quán quen thuộc của người dân yêu thích món nước kiểu Hoa. Quán có phần nước dùng thanh, topping đầy đặn và phong vị mộc mạc, dễ ăn.'
                WHEN 'quan-an-phu-ky.png' THEN 'Nằm tại 598/6 Nguyễn Trãi, Quán Ăn Phú Ký là địa chỉ bình dân nhưng được nhiều người yêu thích nhờ hương vị ổn định. Quán phục vụ các món mì và hủ tiếu với phần nước dùng ninh xương đậm vị, thích hợp cho bữa sáng hoặc trưa.'
                WHEN 'mi-kho-xa-xiu.png' THEN 'Nằm trong khu Nguyễn Trãi sôi động, Mì Khô Xá Xíu là quán ăn hấp dẫn với món mì khô trộn đậm vị. Sợi mì dai, xá xíu thơm ngọt, ăn cùng nước lèo nóng và topping đầy đặn tạo nên trải nghiệm rất tròn vị.'
                ELSE src.script_vi
            END
        WHEN 'en' THEN
            CASE src.image_url
                WHEN 'che-hoa-co-lan.png' THEN 'Chè Hoa Cô Lan is a local food stop at 622 Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include chè hột gà trà, chè đậu đỏ, and chè mè đen.'
                WHEN 'hu-tieu-hu-my.png' THEN 'Hủ Tiếu - Hủ Mỳ is a local food stop at 012 Lô A, C/C Xóm Cải, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include hủ tiếu, hủ mì, and sủi cảo.'
                WHEN 'quan-com-phong-binh.png' THEN 'Quán Cơm Phong Bình is a local food stop at Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include cơm sườn, cơm thịt kho, and canh cải.'
                WHEN 'hoa-ky.png' THEN 'Hoà Ký is a local food stop at QM38+RRV, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include mì vịt quay, mì xá xíu, and hoành thánh.'
                WHEN 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png' THEN 'Minh Phát Hủ Tíu Mì Phường 8 Quận 5 is a local food stop at An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include hủ tiếu nước, mì khô, and hoành thánh.'
                WHEN 'hu-tieu-mi-ba-cao.png' THEN 'Hủ tiếu mì Bà Cao is a local food stop at Chung cư Nguyễn Trãi lô A, 004 Lô A, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include hủ tiếu mì, xá xíu, and nước dùng xương.'
                WHEN 'cha-cuon-ca-trich-tranh.png' THEN 'Chả Cuốn Cá Trích "Tranh" is a local food stop at Lô C chung cư, cầu thang/013 Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include chả cuốn cá trích, rau sống, and nước chấm đậm đà.'
                WHEN 'ha-cao-phanh.png' THEN 'Há Cảo Phánh is a local food stop at Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include há cảo, xíu mại, and bánh xếp.'
                WHEN 'banh-canh-013.png' THEN 'Bánh Canh 013 is a local food stop at Chung cư, Lô C/013 Xóm Cải, Phường 9, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include bánh canh sườn sụn, nui, and bún gạo.'
                WHEN 'com-tam-bao-nhi.png' THEN 'Cơm Tấm - Bảo Nhi is a local food stop at Lô C chung cư, Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include cơm tấm sườn, bì chả, and trứng ốp la.'
                WHEN 'banh-cuon-phu-thanh.png' THEN 'Bánh cuốn Phú Thành is a local food stop at 42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include bánh cuốn, nem chả, and hành phi.'
                WHEN 'hu-tieu-xao-020-lo-c.png' THEN 'Hủ tiếu xào 020 lô C is a local food stop at Chung cư Nguyễn Trãi lô A, lô C/42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include hủ tiếu xào, bún Singapore, and cơm chiên.'
                WHEN 'kho-qua-ca-ot-hing-ky.png' THEN 'Khổ Qua Cà Ớt Híng Ky is a local food stop at Chung cư Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include khổ qua dồn chả cá, cà tím dồn, and nước lèo sa tế.'
                WHEN 'hu-tieu-mi-ho-ky.png' THEN 'Hủ Tiếu Mì Hồ Ký is a local food stop at Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include hủ tiếu nước, mì trụng, and xá xíu.'
                WHEN 'quan-an-phu-ky.png' THEN 'Quán Ăn Phú Ký is a local food stop at 598/6 Nguyễn Trãi, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include hủ tiếu gia đình, xương hầm, and mì trứng.'
                WHEN 'mi-kho-xa-xiu.png' THEN 'Mì Khô Xá Xíu is a local food stop at Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam. Signature dishes include mì khô xá xíu, sủi cảo, and hoành thánh.'
                ELSE src.script_vi
            END
        WHEN 'zh-CN' THEN
            CASE src.image_url
                WHEN 'che-hoa-co-lan.png' THEN 'Chè Hoa Cô Lan 位于622 Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括chè hột gà trà、chè đậu đỏ和chè mè đen。'
                WHEN 'hu-tieu-hu-my.png' THEN 'Hủ Tiếu - Hủ Mỳ 位于012 Lô A, C/C Xóm Cải, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括hủ tiếu、hủ mì和sủi cảo。'
                WHEN 'quan-com-phong-binh.png' THEN 'Quán Cơm Phong Bình 位于Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括cơm sườn、cơm thịt kho和canh cải。'
                WHEN 'hoa-ky.png' THEN 'Hoà Ký 位于QM38+RRV, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括mì vịt quay、mì xá xíu和hoành thánh。'
                WHEN 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png' THEN 'Minh Phát Hủ Tíu Mì Phường 8 Quận 5 位于An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括hủ tiếu nước、mì khô和hoành thánh。'
                WHEN 'hu-tieu-mi-ba-cao.png' THEN 'Hủ tiếu mì Bà Cao 位于Chung cư Nguyễn Trãi lô A, 004 Lô A, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括hủ tiếu mì、xá xíu和nước dùng xương。'
                WHEN 'cha-cuon-ca-trich-tranh.png' THEN 'Chả Cuốn Cá Trích "Tranh" 位于Lô C chung cư, cầu thang/013 Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括chả cuốn cá trích、rau sống和nước chấm đậm đà。'
                WHEN 'ha-cao-phanh.png' THEN 'Há Cảo Phánh 位于Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括há cảo、xíu mại和bánh xếp。'
                WHEN 'banh-canh-013.png' THEN 'Bánh Canh 013 位于Chung cư, Lô C/013 Xóm Cải, Phường 9, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括bánh canh sườn sụn、nui和bún gạo。'
                WHEN 'com-tam-bao-nhi.png' THEN 'Cơm Tấm - Bảo Nhi 位于Lô C chung cư, Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括cơm tấm sườn、bì chả和trứng ốp la。'
                WHEN 'banh-cuon-phu-thanh.png' THEN 'Bánh cuốn Phú Thành 位于42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括bánh cuốn、nem chả和hành phi。'
                WHEN 'hu-tieu-xao-020-lo-c.png' THEN 'Hủ tiếu xào 020 lô C 位于Chung cư Nguyễn Trãi lô A, lô C/42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括hủ tiếu xào、bún Singapore和cơm chiên。'
                WHEN 'kho-qua-ca-ot-hing-ky.png' THEN 'Khổ Qua Cà Ớt Híng Ky 位于Chung cư Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括khổ qua dồn chả cá、cà tím dồn和nước lèo sa tế。'
                WHEN 'hu-tieu-mi-ho-ky.png' THEN 'Hủ Tiếu Mì Hồ Ký 位于Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括hủ tiếu nước、mì trụng和xá xíu。'
                WHEN 'quan-an-phu-ky.png' THEN 'Quán Ăn Phú Ký 位于598/6 Nguyễn Trãi, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括hủ tiếu gia đình、xương hầm和mì trứng。'
                WHEN 'mi-kho-xa-xiu.png' THEN 'Mì Khô Xá Xíu 位于Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam，招牌菜包括mì khô xá xíu、sủi cảo和hoành thánh。'
                ELSE src.script_vi
            END
        WHEN 'ja' THEN
            CASE src.image_url
                WHEN 'che-hoa-co-lan.png' THEN 'Chè Hoa Cô Lan は 622 Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは chè hột gà trà、chè đậu đỏ、chè mè đen です。'
                WHEN 'hu-tieu-hu-my.png' THEN 'Hủ Tiếu - Hủ Mỳ は 012 Lô A, C/C Xóm Cải, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは hủ tiếu、hủ mì、sủi cảo です。'
                WHEN 'quan-com-phong-binh.png' THEN 'Quán Cơm Phong Bình は Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは cơm sườn、cơm thịt kho、canh cải です。'
                WHEN 'hoa-ky.png' THEN 'Hoà Ký は QM38+RRV, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは mì vịt quay、mì xá xíu、hoành thánh です。'
                WHEN 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png' THEN 'Minh Phát Hủ Tíu Mì Phường 8 Quận 5 は An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは hủ tiếu nước、mì khô、hoành thánh です。'
                WHEN 'hu-tieu-mi-ba-cao.png' THEN 'Hủ tiếu mì Bà Cao は Chung cư Nguyễn Trãi lô A, 004 Lô A, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは hủ tiếu mì、xá xíu、nước dùng xương です。'
                WHEN 'cha-cuon-ca-trich-tranh.png' THEN 'Chả Cuốn Cá Trích "Tranh" は Lô C chung cư, cầu thang/013 Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは chả cuốn cá trích、rau sống、nước chấm đậm đà です。'
                WHEN 'ha-cao-phanh.png' THEN 'Há Cảo Phánh は Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは há cảo、xíu mại、bánh xếp です。'
                WHEN 'banh-canh-013.png' THEN 'Bánh Canh 013 は Chung cư, Lô C/013 Xóm Cải, Phường 9, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは bánh canh sườn sụn、nui、bún gạo です。'
                WHEN 'com-tam-bao-nhi.png' THEN 'Cơm Tấm - Bảo Nhi は Lô C chung cư, Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは cơm tấm sườn、bì chả、trứng ốp la です。'
                WHEN 'banh-cuon-phu-thanh.png' THEN 'Bánh cuốn Phú Thành は 42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは bánh cuốn、nem chả、hành phi です。'
                WHEN 'hu-tieu-xao-020-lo-c.png' THEN 'Hủ tiếu xào 020 lô C は Chung cư Nguyễn Trãi lô A, lô C/42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは hủ tiếu xào、bún Singapore、cơm chiên です。'
                WHEN 'kho-qua-ca-ot-hing-ky.png' THEN 'Khổ Qua Cà Ớt Híng Ky は Chung cư Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは khổ qua dồn chả cá、cà tím dồn、nước lèo sa tế です。'
                WHEN 'hu-tieu-mi-ho-ky.png' THEN 'Hủ Tiếu Mì Hồ Ký は Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは hủ tiếu nước、mì trụng、xá xíu です。'
                WHEN 'quan-an-phu-ky.png' THEN 'Quán Ăn Phú Ký は 598/6 Nguyễn Trãi, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは hủ tiếu gia đình、xương hầm、mì trứng です。'
                WHEN 'mi-kho-xa-xiu.png' THEN 'Mì Khô Xá Xíu は Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam にある人気店です。おすすめは mì khô xá xíu、sủi cảo、hoành thánh です。'
                ELSE src.script_vi
            END
        WHEN 'ko' THEN
            CASE src.image_url
                WHEN 'che-hoa-co-lan.png' THEN 'Chè Hoa Cô Lan 는 622 Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 chè hột gà trà, chè đậu đỏ, chè mè đen 입니다.'
                WHEN 'hu-tieu-hu-my.png' THEN 'Hủ Tiếu - Hủ Mỳ 는 012 Lô A, C/C Xóm Cải, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 hủ tiếu, hủ mì, sủi cảo 입니다.'
                WHEN 'quan-com-phong-binh.png' THEN 'Quán Cơm Phong Bình 는 Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 cơm sườn, cơm thịt kho, canh cải 입니다.'
                WHEN 'hoa-ky.png' THEN 'Hoà Ký 는 QM38+RRV, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 mì vịt quay, mì xá xíu, hoành thánh 입니다.'
                WHEN 'minh-phat-hu-tiu-mi-phuong-8-quan-5.png' THEN 'Minh Phát Hủ Tíu Mì Phường 8 Quận 5 는 An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 hủ tiếu nước, mì khô, hoành thánh 입니다.'
                WHEN 'hu-tieu-mi-ba-cao.png' THEN 'Hủ tiếu mì Bà Cao 는 Chung cư Nguyễn Trãi lô A, 004 Lô A, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 hủ tiếu mì, xá xíu, nước dùng xương 입니다.'
                WHEN 'cha-cuon-ca-trich-tranh.png' THEN 'Chả Cuốn Cá Trích "Tranh" 는 Lô C chung cư, cầu thang/013 Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 chả cuốn cá trích, rau sống, nước chấm đậm đà 입니다.'
                WHEN 'ha-cao-phanh.png' THEN 'Há Cảo Phánh 는 Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 há cảo, xíu mại, bánh xếp 입니다.'
                WHEN 'banh-canh-013.png' THEN 'Bánh Canh 013 는 Chung cư, Lô C/013 Xóm Cải, Phường 9, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 bánh canh sườn sụn, nui, bún gạo 입니다.'
                WHEN 'com-tam-bao-nhi.png' THEN 'Cơm Tấm - Bảo Nhi 는 Lô C chung cư, Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 cơm tấm sườn, bì chả, trứng ốp la 입니다.'
                WHEN 'banh-cuon-phu-thanh.png' THEN 'Bánh cuốn Phú Thành 는 42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 bánh cuốn, nem chả, hành phi 입니다.'
                WHEN 'hu-tieu-xao-020-lo-c.png' THEN 'Hủ tiếu xào 020 lô C 는 Chung cư Nguyễn Trãi lô A, lô C/42 Mạc Thiên Tích, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 hủ tiếu xào, bún Singapore, cơm chiên 입니다.'
                WHEN 'kho-qua-ca-ot-hing-ky.png' THEN 'Khổ Qua Cà Ớt Híng Ky 는 Chung cư Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 khổ qua dồn chả cá, cà tím dồn, nước lèo sa tế 입니다.'
                WHEN 'hu-tieu-mi-ho-ky.png' THEN 'Hủ Tiếu Mì Hồ Ký 는 Khu Xóm Cải, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 hủ tiếu nước, mì trụng, xá xíu 입니다.'
                WHEN 'quan-an-phu-ky.png' THEN 'Quán Ăn Phú Ký 는 598/6 Nguyễn Trãi, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 hủ tiếu gia đình, xương hầm, mì trứng 입니다.'
                WHEN 'mi-kho-xa-xiu.png' THEN 'Mì Khô Xá Xíu 는 Nguyễn Trãi, Phường 7, An Đông, Hồ Chí Minh, Việt Nam 에 있는 현지 맛집입니다. 대표 메뉴는 mì khô xá xíu, sủi cảo, hoành thánh 입니다.'
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
