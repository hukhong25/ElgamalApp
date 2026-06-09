# Phần mềm Chữ ký số ElGamal
Ứng dụng Desktop chạy trên hệ điều hành Windows dùng để mô phỏng và thực hiện trọn vẹn quy trình tạo khóa, ký số và xác minh chữ ký số dựa trên hệ mật mã bất đối xứng **ElGamal**. Nền tảng được phát triển bằng ngôn ngữ **C#** kết hợp giao diện đồ họa **WPF** tối ưu hiệu năng trên framework **.NET 8.0**.

---
## 📌 Các Tính Năng Cốt Lõi

Ứng dụng được tổ chức trực quan thành 3 cột chức năng độc lập theo đúng luồng xử lý thực tế của một hệ thống mã hóa bảo mật:

### 1. Quản Lý & Khởi Tạo Hệ Thống Khóa (Cột 1)

* **Sinh khóa tự động:** Tự động tính toán bộ khóa ngẫu nhiên đạt độ an toàn cao với kích thước **512-bit**. Số nguyên tố lớn p được kiểm định qua thuật toán xác suất Miller-Rabin.
* **Tạo khóa thủ công:** Cho phép người dùng tự cấu hình các tham số p, g, $ trực tiếp từ bàn phím. Hệ thống tự động kiểm tra tính nguyên tố của p và ràng buộc điều kiện toán học trước khi tính toán khóa công khai $y$.
* **Đóng gói lưu trữ:** Xuất bộ khóa công khai an toàn ra các định dạng tệp tin đa dạng như tệp văn bản thô (`.txt`), tài liệu Word (`.doc;*.docx`) hoặc tài liệu (`.pdf`).
* **Đẩy dữ liệu nội bộ:** Hỗ trợ cơ chế chuyển tiếp nhanh thông số khóa sang phân vùng xác minh mà không cần nạp lại file thủ công.

### 2. Ký Số Văn Bản & Thông Điệp (Cột 2)

* **Đa dạng đầu vào:** Người dùng có thể chọn nạp file tài liệu từ máy tính hoặc trực tiếp gõ/chỉnh sửa nội dung văn bản ngay trên màn hình.
* **Bộ trích xuất nội dung nâng cao:** * Tự động bóc tách lõi dữ liệu cấu trúc `document.xml` đối với các file Microsoft Word (`.docx`) để lấy chữ thuần túy.
* Tích hợp công cụ phân tích luồng trang của tệp tin PDF để trích xuất chữ thô trực quan.
* Tự động nhận diện cơ chế Fallback (đọc thô dạng Plain-Text) nếu tệp tin nạp vào là định dạng giả lập do hệ thống xuất ra để test luồng.


* **Mã hóa an toàn:** Sử dụng hàm băm bảo mật **SHA-256** để cô đọng nội dung văn bản thành một mã băm cố định duy nhất trước khi đưa vào công thức ký số.
* **Xuất bản chữ ký:** Lưu cặp chữ ký số $(r, s)$ đồng bộ ra file để đính kèm gửi đi.

### 3. Xác Minh Chữ Ký Số (Cột 3)

* **Xác thực toán học:** Áp dụng biểu thức đồng dư để kiểm tra tính toàn vẹn của văn bản nhận được.
* **Kiểm định biên chuyên sâu:** Bắt lỗi chặt chẽ các điều kiện toán học bắt buộc của hệ mật ElGamal ngay trước khi thực hiện lũy thừa nhằm tiết kiệm tài nguyên hệ thống và chặn đứng các chữ ký giả mạo cố ý.
* **Cô lập lý do lỗi:** Khi chữ ký không hợp lệ, hệ thống tự động so khớp vết với phiên làm việc hiện tại để chỉ ra chính xác nguyên nhân:
* Do nội dung văn bản đã bị can thiệp, chỉnh sửa.
* Do người gửi dùng sai bộ khóa công khai để xác thực.
* Do cặp thông số chữ ký số (r, s) bị làm giả hoặc sai cấu trúc.

## 📂 Cấu Trúc Thành Phần Mã Nguồn Chính

```text
├── ElgamalApp/
│   ├── App.xaml             # Định nghĩa Styles, Templates đồ họa phẳng cho Button/TextBox
│   ├── App.xaml.cs          # Logic khởi tạo ứng dụng nền
│   ├── AssemblyInfo.cs      # Khai báo thuộc tính cấu hình Theme của WPF
│   ├── ElgamalApp.csproj    # Tệp tin cấu hình SDK .NET 8.0 và liên kết thư viện phụ thuộc
│   ├── ElgamalService.cs    # Lớp dịch vụ chứa lõi giải thuật BigInteger (Miller-Rabin, ModInverse, Verify...)
│   ├── MainWindow.xaml      # Thiết kế giao diện XAML phân chia 3 cột có ScrollViewer tự động
│   └── MainWindow.xaml.cs   # Trình điều khiển bắt lỗi nhập liệu trống, xử lý sự kiện click nút bấm
└── ElgamalApp.slnx          # File giải pháp quản lý cấu trúc Solution của Microsoft Visual Studio
