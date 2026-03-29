BEGIN;

UPDATE users SET full_name = 'Quản trị hệ thống' WHERE username = 'admin';
UPDATE users SET full_name = 'Chủ quán Ốc Vĩnh Khánh' WHERE username = 'chuoc';
UPDATE users SET full_name = 'Chủ quán Bánh Tráng Nướng' WHERE username = 'chubanhtrang';
UPDATE users SET full_name = 'Chủ quán Phở Gà Chú Tư' WHERE username = 'chupho';
UPDATE users SET full_name = 'Chủ quán Kem Dừa' WHERE username = 'chukem';

UPDATE stalls SET name = 'Ốc Vĩnh Khánh' WHERE created_by_user_id = (SELECT id FROM users WHERE username = 'chuoc');
UPDATE stalls SET name = 'Bánh Tráng Nướng Cô Út' WHERE created_by_user_id = (SELECT id FROM users WHERE username = 'chubanhtrang');
UPDATE stalls SET name = 'Phở Gà Chú Tư' WHERE created_by_user_id = (SELECT id FROM users WHERE username = 'chupho');
UPDATE stalls SET name = 'Kem Dừa Cầu Đá' WHERE created_by_user_id = (SELECT id FROM users WHERE username = 'chukem');

UPDATE stall_translations
SET title = 'Ốc Vĩnh Khánh',
    script_text = 'Chào mừng bạn đến với Ốc Vĩnh Khánh, điểm hẹn hải sản đường phố nổi tiếng tại Quận 4.',
    updated_at = NOW()
WHERE stall_id = (SELECT id FROM stalls WHERE created_by_user_id = (SELECT id FROM users WHERE username = 'chuoc') LIMIT 1)
  AND language_id = (SELECT id FROM languages WHERE code = 'vi');

UPDATE stall_translations
SET title = 'Bánh Tráng Nướng Cô Út',
    script_text = 'Bánh tráng nướng giòn thơm với nhiều loại topping, rất phù hợp cho buổi tối đi dạo.',
    updated_at = NOW()
WHERE stall_id = (SELECT id FROM stalls WHERE created_by_user_id = (SELECT id FROM users WHERE username = 'chubanhtrang') LIMIT 1)
  AND language_id = (SELECT id FROM languages WHERE code = 'vi');

UPDATE stall_translations
SET title = 'Phở Gà Chú Tư',
    script_text = 'Một quán phở gà buổi sáng với nước dùng thanh, thịt gà mềm và khu vực ngồi gọn gàng.',
    updated_at = NOW()
WHERE stall_id = (SELECT id FROM stalls WHERE created_by_user_id = (SELECT id FROM users WHERE username = 'chupho') LIMIT 1)
  AND language_id = (SELECT id FROM languages WHERE code = 'vi');

UPDATE stall_translations
SET title = 'Kem Dừa Cầu Đá',
    script_text = 'Món kem dừa mát lạnh với topping dừa nạo và đậu phộng rang, phù hợp để tráng miệng.',
    updated_at = NOW()
WHERE stall_id = (SELECT id FROM stalls WHERE created_by_user_id = (SELECT id FROM users WHERE username = 'chukem') LIMIT 1)
  AND language_id = (SELECT id FROM languages WHERE code = 'vi');

UPDATE stall_update_requests
SET name = 'Ốc Vĩnh Khánh Chi Nhánh Tối',
    script_vi = 'Quán vừa bổ sung menu hải sản nướng và cập nhật lại khung giờ phục vụ buổi tối.'
WHERE submitted_by_user_id = (SELECT id FROM users WHERE username = 'chuoc')
  AND status = 'pending';

COMMIT;
