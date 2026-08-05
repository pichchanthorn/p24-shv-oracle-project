using System.Security.Cryptography;
using OtpNet;
using QRCoder;

namespace backend.Services
{
    public class TwoFactorService
    {
        private const int SecretSizeBytes = 20;
        private const string Issuer = "P24AuthenticationApi";

        public string GenerateSecret()
        {
            var key = KeyGeneration.GenerateRandomKey(SecretSizeBytes);
            return Base32Encoding.ToString(key);
        }

        public string BuildOtpAuthUri(string secret, string username)
        {
            var encodedIssuer = Uri.EscapeDataString(Issuer);
            var encodedUsername = Uri.EscapeDataString(username);
            return $"otpauth://totp/{encodedIssuer}:{encodedUsername}" +
                   $"?secret={secret}&issuer={encodedIssuer}&digits=6&period=30&algorithm=SHA1";
        }

        public string GenerateQrCodeDataUrl(string otpAuthUri)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var bytes = qrCode.GetGraphic(20);
            var base64 = Convert.ToBase64String(bytes);
            return $"data:image/png;base64,{base64}";
        }

        public bool VerifyCode(string secret, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            {
                return false;
            }

            try
            {
                var keyBytes = Base32Encoding.ToBytes(secret);
                var totp = new Totp(keyBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
                return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
            }
            catch
            {
                return false;
            }
        }
    }
}