
# 🏗️ Project Name: Food Tour Audio Guide
*Hệ thống quản lý hướng dẫn viên du lịch đa ngôn ngữ*

## 🚀 Quick Start (Hướng dẫn khởi chạy)

Để chạy dự án ở chế độ phát triển (development mode), hãy sử dụng lệnh sau trong terminal:

```bash
# Cài đặt các thư viện (nếu chưa có)
npm install

# Khởi chạy dự án
cmd /c npm run dev
```
---

## 🛠️ Stack Công nghệ (Tech Stack)

Dự án được xây dựng trên nền tảng các công nghệ hiện đại, đảm bảo hiệu suất cao và khả năng mở rộng:

| Công nghệ | Vai trò & Đặc điểm nổi bật |
| :--- | :--- |
| **React v19** | Thư viện UI cốt lõi. Sử dụng kiến trúc **Component** và các **React Hooks** (như `useState`) để quản lý trạng thái mượt mà (ví dụ: chuyển đổi Dashboard/Login). |
| **TypeScript** | Hệ thống định kiểu tĩnh (Static Typing) giúp mã nguồn an toàn, dễ bảo trì và hạn chế tối đa lỗi runtime. |
| **Vite** | Công cụ build thế hệ mới, tối ưu tốc độ biên dịch (HMR) cực nhanh so với Webpack truyền thống. |
| **Tailwind CSS v4** | Framework CSS utility-first. Triển khai các hiệu ứng hiện đại như **Glassmorphism** (`backdrop-blur`) và hệ màu thương hiệu (Coral: `#FF7F50`). |
| **Lucide React** | Thư viện icon mã nguồn mở dạng SVG, cung cấp các biểu tượng sắc nét như `Mic`, `ShoppingBasket`, `Store`. |

---

## 🎨 Thiết kế & Giao diện (UI/UX)

### 🖋️ Hệ thống Font chữ
Ứng dụng kết hợp hài hòa giữa các bộ font từ **Google Fonts**:
* **Poppins & Inter**: Font không chân (Sans-serif) hiện đại, sạch sẽ, dùng cho nội dung văn bản chính để tối ưu khả năng đọc.
* **Playfair Display**: Font có chân (Serif) sang trọng, dùng cho tiêu đề logo và lời chào mừng để tạo điểm nhấn nghệ thuật.

### 🌟 Đặc điểm nổi bật
* **Giao diện Dashboard**: Tối ưu trải nghiệm người dùng với bố cục chia đôi màn hình khoa học.
* **Responsive Design**: Tương thích tốt trên nhiều kích thước màn hình nhờ sức mạnh của Tailwind CSS.
* **Hiệu ứng thị giác**: Sử dụng phong cách thiết kế kính mờ (Glassmorphism) giúp giao diện trông cao cấp và có chiều sâu.

---

## 📂 Cấu trúc thư mục (Sơ lược)
```text
├── src/                # Chứa toàn bộ mã nguồn ứng dụng
│   ├── App.tsx         # Component gốc, điều hướng và bố cục chính
│   ├── main.tsx        # Điểm đầu vào (entry point) của ứng dụng
│   └── index.css       # File cấu hình CSS toàn cục & Tailwind directives
├── .env.example        # Tệp mẫu cấu hình biến môi trường
├── .gitignore          # Danh sách các tệp/thư mục Git bỏ qua
├── index.html          # File HTML gốc (Template)
├── metadata.json       # Chứa thông tin mô tả ứng dụng
├── package.json        # Quản lý các thư viện (dependencies) và scripts
├── tsconfig.json       # Cấu hình TypeScript cho dự án
├── vite.config.ts      # Cấu hình công cụ build Vite
└── README.md           # Tài liệu hướng dẫn dự án
```

---

