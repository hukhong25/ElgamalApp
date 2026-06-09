using System;
using System.IO;
using System.IO.Compression; // Thêm để bóc tách cấu trúc file mã nguồn Word (.docx)
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Xml.Linq;       // Thêm để xử lý đọc thẻ XML văn bản của Word
using Microsoft.Win32;

namespace ElgamalApp
{
    public partial class MainWindow : Window
    {
        private readonly ElgamalService _elgamalService;

        private BigInteger currentR;
        private BigInteger currentS;
        private BigInteger hiddenHashToSign = -1;

        // Biến lưu trạng thái lúc ký để đối chiếu nguyên nhân lỗi khi xác minh
        private string originalSignedText = "";
        private BigInteger originalSignedP = 0;
        private BigInteger originalSignedG = 0;
        private BigInteger originalSignedY = 0;

        public MainWindow()
        {
            InitializeComponent();
            _elgamalService = new ElgamalService();
        }

        // =====================================================================
        // ===================== HÀM TIỆN ÍCH / TRỢ GIÚP =======================
        // =====================================================================

        private void SetKeyInputState(bool isEnabled)
        {
            txtP.IsEnabled = isEnabled;
            txtG.IsEnabled = isEnabled;
            txtX.IsEnabled = isEnabled;
        }

        // Hàm băm chuỗi văn bản hiển thị trên TextBox thành BigInteger dương
        private BigInteger GetStringHash(string text)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] stringBytes = Encoding.UTF8.GetBytes(text);
                byte[] hashBytes = sha256.ComputeHash(stringBytes);

                byte[] positiveHashBytes = new byte[hashBytes.Length + 1];
                Array.Copy(hashBytes.Reverse().ToArray(), positiveHashBytes, hashBytes.Length);

                return new BigInteger(positiveHashBytes);
            }
        }

        // Hàm giải mã cấu trúc nén XML để trích xuất chữ thuần túy từ file Word (.docx)
        private string ExtractTextFromDocx(string filePath)
        {
            try
            {
                // 1. Thử đọc theo cấu trúc file Word chuẩn (Zip Archive chứa XML)
                using (ZipArchive zip = ZipFile.OpenRead(filePath))
                {
                    var entry = zip.GetEntry("word/document.xml");
                    if (entry == null) return "Không tìm thấy nội dung văn bản hợp lệ trong file Word.";

                    using (Stream stream = entry.Open())
                    {
                        XDocument doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

                        // Lấy toàn bộ các thẻ đoạn văn (w:p) để giữ nguyên cấu trúc xuống dòng
                        var paragraphs = doc.Descendants(w + "p");
                        StringBuilder sb = new StringBuilder();

                        foreach (var p in paragraphs)
                        {
                            var texts = p.Descendants(w + "t").Select(t => t.Value);
                            string pText = string.Concat(texts);
                            if (!string.IsNullOrEmpty(pText))
                            {
                                sb.AppendLine(pText);
                            }
                        }
                        return sb.ToString().TrimEnd();
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    return File.ReadAllText(filePath);
                }
                catch (Exception ex)
                {
                    return $"Lỗi đọc cấu trúc file Word: {ex.Message}";
                }
            }
        }

        // Hàm trích xuất chữ thuần túy từ tệp tin PDF bằng thư viện PdfPig
        private string ExtractTextFromPdf(string filePath)
        {
            try
            {
                // 1. Thử đọc bằng thư viện PdfPig trước (Áp dụng cho file PDF chuẩn từ bên ngoài)
                using (var pdf = UglyToad.PdfPig.PdfDocument.Open(filePath))
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var page in pdf.GetPages())
                    {
                        sb.AppendLine(page.Text);
                    }
                    return sb.ToString().TrimEnd();
                }
            }
            catch (Exception)
            {
                try
                {
                    return File.ReadAllText(filePath);
                }
                catch (Exception ex)
                {
                    return $"Lỗi đọc cấu trúc file PDF: {ex.Message}";
                }
            }
        }

        // =====================================================================
        // ========================= CỘT 1: TẠO KHÓA ===========================
        // =====================================================================

        private void BtnGenerateKeys_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var keys = _elgamalService.GenerateKeys(512);
                txtP.Text = keys.p.ToString();
                txtG.Text = keys.g.ToString();
                txtX.Text = keys.x.ToString();
                txtY.Text = keys.y.ToString();
                SetKeyInputState(false);

                MessageBox.Show("Tạo khóa ngẫu nhiên 512-bit thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCalculateY_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtP.Text) || string.IsNullOrEmpty(txtG.Text) || string.IsNullOrEmpty(txtX.Text))
                {
                    MessageBox.Show("Vui lòng điền đủ p, g, x để tính khóa công khai y!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!BigInteger.TryParse(txtP.Text, out BigInteger p) ||
                    !BigInteger.TryParse(txtG.Text, out BigInteger g) ||
                    !BigInteger.TryParse(txtX.Text, out BigInteger x))
                {
                    MessageBox.Show("Các thông số p, g, x phải là số nguyên dương hợp lệ!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!_elgamalService.IsProbablePrime(p, 10))
                {
                    MessageBox.Show("Số p phải là một SỐ NGUYÊN TỐ!", "Lỗi thuật toán", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (g <= 1 || g >= p)
                {
                    MessageBox.Show($"Phần tử g phải thỏa mãn điều kiện: 1 < g < p (tức là từ 2 đến {p - 1})!", "Lỗi thuật toán", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (x <= 1 || x >= p - 1)
                {
                    MessageBox.Show($"Khóa bí mật x phải thỏa mãn điều kiện: 1 < x < {p - 1} (tức là từ 2 đến {p - 2})!", "Lỗi thuật toán", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                BigInteger y = BigInteger.ModPow(g, x, p);
                txtY.Text = y.ToString();
                SetKeyInputState(false);
                MessageBox.Show("Thông số hợp lệ! Tính toán khóa công khai Y thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Đã xảy ra lỗi hệ thống: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveKey_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtP.Text) || string.IsNullOrEmpty(txtG.Text) || string.IsNullOrEmpty(txtY.Text))
            {
                MessageBox.Show("Hệ thống chưa có thông tin khóa để lưu! Vui lòng bấm tạo hoặc tính khóa trước.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|Word Documents (*.doc;*.docx)|*.doc;*.docx|PDF Files (*.pdf)|*.pdf",
                Title = "Lưu Khóa Công Khai"
            };
            if (sfd.ShowDialog() == true)
            {
                string keyData = $"p={txtP.Text}\ng={txtG.Text}\ny={txtY.Text}";
                File.WriteAllText(sfd.FileName, keyData);
                MessageBox.Show("Đã xuất file khóa công khai thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnForwardKey_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtP.Text) || string.IsNullOrEmpty(txtG.Text) || string.IsNullOrEmpty(txtY.Text))
            {
                MessageBox.Show("Chưa có đủ thông tin Khóa công khai để chuyển tiếp!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            txtVerifyP.Text = txtP.Text;
            txtVerifyG.Text = txtG.Text;
            txtVerifyY.Text = txtY.Text;

            txtKeyPathVerify.Text = "Khóa được chuyển tiếp trực tiếp từ hệ thống";
            MessageBox.Show("Đã chuyển tiếp Khóa công khai sang phần Xác Minh!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            txtP.Clear();
            txtG.Clear();
            txtX.Clear();
            txtY.Clear();

            SetKeyInputState(true);

            txtFilePathSign.Clear();
            txtSignResult.Clear();
            txtInputTextSign.Clear();

            txtFilePathVerify.Clear();
            txtSigPathVerify.Clear();
            txtVerifySignData.Clear();
            txtInputTextVerify.Clear();

            txtKeyPathVerify.Clear();
            txtVerifyP.Clear();
            txtVerifyG.Clear();
            txtVerifyY.Clear();

            currentR = 0;
            currentS = 0;

            hiddenHashToSign = -1;

            originalSignedText = "";
            originalSignedP = 0;
            originalSignedG = 0;
            originalSignedY = 0;
        }

        // =====================================================================
        // ======================== CỘT 2: KÝ VĂN BẢN ==========================
        // =====================================================================

        private void BtnOpenFileToSign_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Supported Files (*.txt;*.doc;*.docx;*.pdf)|*.txt;*.doc;*.docx;*.pdf|All files (*.*)|*.*",
                Title = "Chọn file để ký số"
            };
            if (ofd.ShowDialog() == true)
            {
                txtFilePathSign.Text = ofd.FileName;
                string ext = Path.GetExtension(ofd.FileName).ToLower();

                // Bóc tách nội dung thô hiển thị trực tiếp lên giao diện tùy theo đuôi file
                if (ext == ".docx" || ext == ".doc")
                {
                    txtInputTextSign.Text = ExtractTextFromDocx(ofd.FileName);
                }
                else if (ext == ".pdf")
                {
                    txtInputTextSign.Text = ExtractTextFromPdf(ofd.FileName);
                }
                else
                {
                    txtInputTextSign.Text = File.ReadAllText(ofd.FileName);
                }

                // Luôn cập nhật mã băm đồng bộ từ chuỗi chữ thực tế đang hiển thị trên TextBox
                hiddenHashToSign = GetStringHash(txtInputTextSign.Text);
            }
        }

        private void BtnSaveFileSign_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtInputTextSign.Text))
            {
                MessageBox.Show("Không có nội dung để lưu!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|Word Documents (*.doc;*.docx)|*.doc;*.docx|PDF Files (*.pdf)|*.pdf",
                Title = "Lưu file văn bản"
            };
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, txtInputTextSign.Text);
                hiddenHashToSign = GetStringHash(txtInputTextSign.Text);

                MessageBox.Show("Lưu file văn bản thành công!\n(Hệ thống đã cập nhật mã băm mới theo nội dung hiện tại)", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSignData_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtInputTextSign.Text) || string.IsNullOrEmpty(txtP.Text) || string.IsNullOrEmpty(txtG.Text) || string.IsNullOrEmpty(txtX.Text))
            {
                MessageBox.Show("Vui lòng nạp/nhập văn bản và điền đủ thông số khóa hệ thống (p, g, x)!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Trường hợp người dùng tự gõ tay hoàn toàn từ đầu mà chưa bấm lưu file
            if (hiddenHashToSign == -1)
            {
                hiddenHashToSign = GetStringHash(txtInputTextSign.Text);
            }

            try
            {
                BigInteger p = BigInteger.Parse(txtP.Text);
                BigInteger g = BigInteger.Parse(txtG.Text);
                BigInteger x = BigInteger.Parse(txtX.Text);

                // Ký trực tiếp trên chuỗi chữ hiển thị ở TextBox
                BigInteger m = GetStringHash(txtInputTextSign.Text);

                var signature = _elgamalService.SignData(m, p, g, x);
                currentR = signature.r;
                currentS = signature.s;

                txtSignResult.Text = $"r: {currentR}\ns: {currentS}";

                // Lưu lại vết trạng thái để đối chiếu chính xác nguyên nhân lỗi khi xác minh
                originalSignedText = txtInputTextSign.Text;
                originalSignedP = p;
                originalSignedG = g;
                originalSignedY = BigInteger.ModPow(g, x, p);

                MessageBox.Show("Ký văn bản thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi ký số: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnForwardData_Click(object sender, RoutedEventArgs e)
        {

            txtInputTextVerify.Text = txtInputTextSign.Text; // Đẩy toàn bộ văn bản chữ thuần túy sang
            txtVerifySignData.Text = txtSignResult.Text;     // Đẩy nội dung cặp chữ ký (r, s)

            MessageBox.Show("Đã chuyển tiếp dữ liệu sang cột Xác minh!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnSaveSignature_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtSignResult.Text))
            {
                MessageBox.Show("Chưa có chữ ký để lưu!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|Word Documents (*.doc;*.docx)|*.doc;*.docx|PDF Files (*.pdf)|*.pdf",
                Title = "Lưu file chữ ký"
            };
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, txtSignResult.Text);
                MessageBox.Show("Lưu file chữ ký thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // =====================================================================
        // ======================= CỘT 3: XÁC MINH CHỮ KÝ ======================
        // =====================================================================

        private void BtnOpenFileToVerify_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Supported Files (*.txt;*.doc;*.docx;*.pdf)|*.txt;*.doc;*.docx;*.pdf|All files (*.*)|*.*",
                Title = "Chọn file để kiểm tra"
            };
            if (ofd.ShowDialog() == true)
            {
                txtFilePathVerify.Text = ofd.FileName;
                string ext = Path.GetExtension(ofd.FileName).ToLower();

                // Đọc sạch chữ tương tự như bên cột ký
                if (ext == ".docx" || ext == ".doc")
                {
                    txtInputTextVerify.Text = ExtractTextFromDocx(ofd.FileName);
                }
                else if (ext == ".pdf")
                {
                    txtInputTextVerify.Text = ExtractTextFromPdf(ofd.FileName);
                }
                else
                {
                    txtInputTextVerify.Text = File.ReadAllText(ofd.FileName);
                }
            }
        }

        private void BtnLoadKey_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Supported Files (*.txt;*.doc;*.docx;*.pdf)|*.txt;*.doc;*.docx;*.pdf|All files (*.*)|*.*",
                Title = "Chọn file khóa công khai"
            };
            if (ofd.ShowDialog() == true)
            {
                txtKeyPathVerify.Text = ofd.FileName;
                try
                {
                    string[] lines = File.ReadAllLines(ofd.FileName);
                    if (lines.Length >= 3)
                    {
                        txtVerifyP.Text = lines[0].Replace("p=", "").Trim();
                        txtVerifyG.Text = lines[1].Replace("g=", "").Trim();
                        txtVerifyY.Text = lines[2].Replace("y=", "").Trim();
                    }
                }
                catch
                {
                    MessageBox.Show("Đã xảy ra lỗi hoặc file khóa không đúng cấu trúc!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnLoadSignature_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Supported Files (*.txt;*.doc;*.docx;*.pdf)|*.txt;*.doc;*.docx;*.pdf|All files (*.*)|*.*",
                Title = "Chọn file chữ ký"
            };
            if (ofd.ShowDialog() == true)
            {
                txtSigPathVerify.Text = ofd.FileName;
                txtVerifySignData.Text = File.ReadAllText(ofd.FileName);
            }
        }

        private void BtnVerify_Click(object sender, RoutedEventArgs e)
        {
            // =====================================================================
            // 1. BẮT LỖI CHI TIẾT ĐỂ TRỐNG TỪNG Ô DỮ LIỆU ĐẦU VÀO
            // =====================================================================
            if (string.IsNullOrEmpty(txtInputTextVerify.Text))
            {
                MessageBox.Show("Vui lòng nhập nội dung văn bản hoặc nhấn 'Mở file' để nạp nội dung cần kiểm tra!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(txtVerifyP.Text))
            {
                MessageBox.Show("Tham số khóa công khai 'p' không được để trống!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(txtVerifyG.Text))
            {
                MessageBox.Show("Tham số phần tử nguyên thủy 'g' không được để trống!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(txtVerifyY.Text))
            {
                MessageBox.Show("Tham số khóa công khai 'y' không được để trống!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(txtVerifySignData.Text))
            {
                MessageBox.Show("Nội dung chữ ký số cần kiểm tra (r, s) không được để trống!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // =====================================================================
                // 2. BẮT LỖI ĐỊNH DẠNG SỐ NGUYÊN (TRÁNH CRASH KHI GÕ SAI KÝ TỰ)
                // =====================================================================
                if (!BigInteger.TryParse(txtVerifyP.Text, out BigInteger vp))
                {
                    MessageBox.Show("Tham số khóa p phải là một dãy số nguyên dương hợp lệ!", "Lỗi định dạng số", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!BigInteger.TryParse(txtVerifyG.Text, out BigInteger vg))
                {
                    MessageBox.Show("Tham số phần tử g phải là một dãy số nguyên dương hợp lệ!", "Lỗi định dạng số", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!BigInteger.TryParse(txtVerifyY.Text, out BigInteger vy))
                {
                    MessageBox.Show("Tham số khóa công khai y phải là một dãy số nguyên dương hợp lệ!", "Lỗi định dạng số", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // =====================================================================
                // 3. TÁCH VÀ ÉP KIỂU AN TOÀN CHO CẶP CHỮ KÝ (R, S)
                // =====================================================================
                BigInteger r = 0, s = 0;
                bool parseSigSuccess = false;
                string sigText = txtVerifySignData.Text.Trim();

                // Thử cắt theo định dạng xuống dòng (r: ... \n s: ...)
                string[] lines = sigText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length >= 2)
                {
                    string rPart = lines[0].Replace("r:", "").Trim();
                    string sPart = lines[1].Replace("s:", "").Trim();
                    if (BigInteger.TryParse(rPart, out r) && BigInteger.TryParse(sPart, out s))
                    {
                        parseSigSuccess = true;
                    }
                }

                // Thử cắt theo định dạng dấu gạch đứng (r|s) đề phòng đọc từ file thô thế hệ cũ
                if (!parseSigSuccess)
                {
                    string[] parts = sigText.Split('|');
                    if (parts.Length >= 2)
                    {
                        if (BigInteger.TryParse(parts[0].Trim(), out r) && BigInteger.TryParse(parts[1].Trim(), out s))
                        {
                            parseSigSuccess = true;
                        }
                    }
                }

                if (!parseSigSuccess)
                {
                    MessageBox.Show("Cấu trúc nội dung cặp chữ ký số (r, s) không đúng mẫu định dạng!\nĐịnh dạng chuẩn yêu cầu dạng 2 dòng:\nr: [Số lớn]\ns: [Số lớn]", "Lỗi cấu trúc chữ ký", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }


                BigInteger m = GetStringHash(txtInputTextVerify.Text); 
                bool isValid = _elgamalService.VerifySignature(m, vp, vg, vy, r, s);

                if (isValid)
                {
                    MessageBox.Show("CHỮ KÝ HỢP LỆ!\nVăn bản hoàn toàn nguyên vẹn và được xác thực thành công.", "Kết quả thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // BỔ SUNG PHẦN CÒN THIẾU: Kiểm tra xem có dữ liệu gốc của phiên chạy hiện tại hay không
                    if (originalSignedP != 0)
                    {
                        bool isTextChanged = (txtInputTextVerify.Text != originalSignedText);
                        bool isKeyChanged = (vp != originalSignedP || vg != originalSignedG || vy != originalSignedY);

                        if (isTextChanged && isKeyChanged)
                        {
                            MessageBox.Show("CHỮ KÝ KHÔNG HỢP LỆ!\nNguyên nhân: Cả nội dung văn bản kiểm tra và bộ khóa công khai đều đã bị chỉnh sửa so với lúc ký!", "Kết quả thất bại", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (isTextChanged)
                        {
                            MessageBox.Show("CHỮ KÝ KHÔNG HỢP LỆ!\nNguyên nhân: Văn bản đã bị sửa đổi nội dung hoặc sai lệch tệp tin so với bản gốc lúc ký!", "Kết quả thất bại", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else if (isKeyChanged)
                        {
                            MessageBox.Show("CHỮ KÝ KHÔNG HỢP LỆ!\nNguyên nhân: Khóa công khai dùng để xác thực không khớp với bộ khóa đã dùng để ký văn bản này!", "Kết quả thất bại", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else
                        {
                            MessageBox.Show("CHỮ KÝ KHÔNG HỢP LỆ!\nNguyên nhân: Các tham số chữ ký số (r, s) đã bị can thiệp, sai lệch toán học hoặc bị làm giả.", "Kết quả thất bại", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        // Trường hợp nạp file độc lập hoàn toàn từ bên ngoài (Không có vết phiên gốc để đối chiếu chữ)
                        MessageBox.Show("CHỮ KÝ KHÔNG HỢP LỆ!\nThông tin văn bản, cặp khóa công khai hoặc tệp chữ ký số đính kèm không trùng khớp toán học với nhau.", "Kết quả thất bại", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Đã xảy ra lỗi bất thường trong quá trình xử lý dữ liệu. Chi tiết lỗi: " + ex.Message, "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}