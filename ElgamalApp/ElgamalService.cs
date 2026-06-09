using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace ElgamalApp
{
    public class ElgamalService
    {
        private readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        // 1. Hàm tạo bộ khóa (Trả về một Tuple chứa p, g, x, y)
        public (BigInteger p, BigInteger g, BigInteger x, BigInteger y) GenerateKeys(int bits = 256)
        {
            BigInteger p = GenerateProbablePrime(bits);
            BigInteger g = GenerateRandomBigInteger(2, p - 2);
            BigInteger x = GenerateRandomBigInteger(2, p - 2);
            BigInteger y = BigInteger.ModPow(g, x, p);

            return (p, g, x, y);
        }

        // 2. Hàm ký số (Trả về Tuple chứa r, s)
        public (BigInteger r, BigInteger s) SignData(BigInteger m, BigInteger p, BigInteger g, BigInteger x)
        {
            BigInteger pMinus1 = p - 1;
            BigInteger k;

            // Chọn k ngẫu nhiên (1 < k < p-1) và UCLN(k, p-1) = 1
            do
            {
                k = GenerateRandomBigInteger(2, pMinus1 - 1);
            } while (BigInteger.GreatestCommonDivisor(k, pMinus1) != 1);

            // r = g^k mod p
            BigInteger r = BigInteger.ModPow(g, k, p);

            // s = [k^-1 * (H(M) - x*r)] mod (p-1)
            BigInteger kInverse = ModInverse(k, pMinus1);
            BigInteger s = Mod(kInverse * (m - x * r), pMinus1);

            return (r, s);
        }

        // 3. Hàm xác thực chữ ký (Trả về true/false)
        public bool VerifySignature(BigInteger m, BigInteger p, BigInteger g, BigInteger y, BigInteger r, BigInteger s)
        {
            // Kiểm tra điều kiện cơ bản của chữ ký: 0 < r < p
            if (r <= 0 || r >= p) return false;

            // u = g^H(M) mod p
            BigInteger u = BigInteger.ModPow(g, m, p);

            // v = (y^r * r^s) mod p
            BigInteger v1 = BigInteger.ModPow(y, r, p);
            BigInteger v2 = BigInteger.ModPow(r, s, p);
            BigInteger v = (v1 * v2) % p;

            return u == v;
        }

        // 4. Hàm băm file thành BigInteger
        public BigInteger GetFileHash(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                byte[] hashBytes = sha256.ComputeHash(fileBytes);

                // Đảo ngược mảng byte và thêm 0x00 vào cuối để đảm bảo số dương
                byte[] positiveHashBytes = new byte[hashBytes.Length + 1];
                Array.Copy(hashBytes.Reverse().ToArray(), positiveHashBytes, hashBytes.Length);

                return new BigInteger(positiveHashBytes);
            }
        }

        // ================= CÁC HÀM TIỆN ÍCH TOÁN HỌC =================

        private BigInteger Mod(BigInteger a, BigInteger m)
        {
            BigInteger result = a % m;
            return result < 0 ? result + m : result;
        }

        private BigInteger ModInverse(BigInteger a, BigInteger m)
        {
            BigInteger m0 = m, t, q;
            BigInteger x0 = 0, x1 = 1;

            if (m == 1) return 0;
            a = Mod(a, m);

            while (a > 1)
            {
                q = a / m;
                t = m;
                m = a % m;
                a = t;
                t = x0;
                x0 = x1 - q * x0;
                x1 = t;
            }

            if (x1 < 0) x1 += m0;
            return x1;
        }

        private BigInteger GenerateRandomBigInteger(BigInteger min, BigInteger max)
        {
            byte[] bytes = max.ToByteArray();
            BigInteger result;
            do
            {
                rng.GetBytes(bytes);
                bytes[bytes.Length - 1] &= (byte)0x7F;
                result = new BigInteger(bytes);
            } while (result < min || result >= max);
            return result;
        }

        private BigInteger GenerateProbablePrime(int bits)
        {
            byte[] bytes = new byte[bits / 8];
            BigInteger p;
            do
            {
                rng.GetBytes(bytes);
                bytes[bytes.Length - 1] &= (byte)0x7F;
                p = new BigInteger(bytes);
                p |= BigInteger.One;
                p |= (BigInteger.One << (bits - 2));
            } while (!IsProbablePrime(p, 10));

            return p;
        }

        public bool IsProbablePrime(BigInteger source, int certainty)
        {
            if (source == 2 || source == 3) return true;
            if (source < 2 || source % 2 == 0) return false;

            BigInteger d = source - 1;
            int s = 0;

            while (d % 2 == 0)
            {
                d /= 2;
                s += 1;
            }

            for (int i = 0; i < certainty; i++)
            {
                BigInteger a = GenerateRandomBigInteger(2, source - 2);
                BigInteger x = BigInteger.ModPow(a, d, source);

                if (x == 1 || x == source - 1) continue;

                for (int r = 1; r < s; r++)
                {
                    x = BigInteger.ModPow(x, 2, source);
                    if (x == 1) return false;
                    if (x == source - 1) break;
                }
                if (x != source - 1) return false;
            }
            return true;
        }
    }
}